#if EASTERAD_ADSEG_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if EASTERAD_USE_RENDER_GRAPH
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
#endif
using System;

namespace EasterAd
{
#if EASTERAD_USE_RENDER_GRAPH
    public class AdSegmentationScriptableRenderPass : ScriptableRenderPass
    {
        private Material material;
        private ComputeShader pixelCounterCS;
        private int kernelIndex;
        private ComputeBuffer pixelCountBuffer;
        private readonly Action<uint[]> onPixelCountsReadback;
        private RTHandle segmentationRTHandle;
        private RTHandle segmentationDepthHandle;

        // Phase 4: 버퍼 클리어용 배열 (static으로 재사용)
        private static readonly uint[] _zeroBuffer = new uint[256];

        public AdSegmentationScriptableRenderPass(
            Material material,
            ComputeShader pixelCounterCS,
            ComputeBuffer pixelCountBuffer,
            Action<uint[]> onPixelCountsReadback)
        {
            this.material = material;
            this.pixelCounterCS = pixelCounterCS;
            this.pixelCountBuffer = pixelCountBuffer;
            this.onPixelCountsReadback = onPixelCountsReadback;

            // Compute Shader 커널 인덱스 가져오기
            if (pixelCounterCS != null)
            {
                this.kernelIndex = pixelCounterCS.FindKernel("EasterAd_CountPixels");
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // ===== Pass 1: Segmentation Rendering (RasterPass) =====
            TextureHandle segmentationTexture;

            using (var builder = renderGraph.AddRasterRenderPass<RasterPassData>(
                "AdSegmentationPass", out var passData))
            {
                // FrameData 가져오기
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                // RendererList 생성 (AdSegmentationObject가 표시한 광고 Renderer만 대상)
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var filteringSettings = new FilteringSettings(
                    RenderQueueRange.all,
                    -1,
                    AdSegmentationObject.SegmentationRenderingLayerMask);
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    new ShaderTagId("UniversalForward"),
                    renderingData, cameraData, lightData, sortingCriteria);

                // Material Override로 AdSegmentation Shader 강제 적용
                drawSettings.overrideMaterial = material;
                drawSettings.overrideMaterialPassIndex = 0;

                var rendererListParams = new RendererListParams(
                    renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

                // Color RenderTexture 생성 (256x256 ARGB32)
                var colorDesc = new RenderTextureDescriptor(256, 256, RenderTextureFormat.ARGB32, 0);
                colorDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                colorDesc.depthStencilFormat = GraphicsFormat.None;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref segmentationRTHandle, colorDesc,
                    FilterMode.Point, TextureWrapMode.Clamp,
                    name: "_AdSegmentationTexture");

                // Depth RenderTexture 생성 (256x256, 동일한 크기)
                var depthDesc = new RenderTextureDescriptor(256, 256, RenderTextureFormat.Depth, 24);
                depthDesc.graphicsFormat = GraphicsFormat.None;
                depthDesc.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref segmentationDepthHandle, depthDesc,
                    FilterMode.Point, TextureWrapMode.Clamp,
                    name: "_AdSegmentationDepth");

                segmentationTexture = renderGraph.ImportTexture(segmentationRTHandle);
                TextureHandle depthTexture = renderGraph.ImportTexture(segmentationDepthHandle);

                builder.SetRenderAttachment(segmentationTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.Write);
                builder.UseRendererList(passData.rendererListHandle);
                builder.AllowPassCulling(false);

                // 렌더링 함수 설정
                builder.SetRenderFunc((RasterPassData data, RasterGraphContext context) =>
                {
                    // RenderTarget 클리어 (검은색 배경)
                    context.cmd.ClearRenderTarget(true, true, Color.black);

                    // 렌더링 실행 (ID를 RenderTexture에 저장)
                    context.cmd.DrawRendererList(data.rendererListHandle);
                });
            }

            // ===== Pass 2: Pixel Counting (UnsafePass) =====
            if (pixelCounterCS != null && pixelCountBuffer != null)
            {
                // ComputeCommandBuffer does not expose ordered async readback. Keep dispatch and
                // readback in one unsafe pass so the native request executes after the dispatch.
                using (var builder = renderGraph.AddUnsafePass<ComputePassData>(
                    "PixelCountingPass", out var passData))
                {
                    passData.computeShader = pixelCounterCS;
                    passData.kernelIndex = kernelIndex;
                    passData.pixelCountBuffer = pixelCountBuffer;
                    passData.segmentationTexture = segmentationTexture;
                    passData.onPixelCountsReadback = onPixelCountsReadback;

                    // Compute Shader가 RenderTexture 읽기
                    builder.UseTexture(segmentationTexture, AccessFlags.Read);

                    builder.AllowPassCulling(false);

                    // Compute Shader 실행
                    builder.SetRenderFunc((ComputePassData data, UnsafeGraphContext context) =>
                    {
                        // 버퍼 클리어 (0으로 초기화)
                        context.cmd.SetBufferData(data.pixelCountBuffer, _zeroBuffer);

                        // Compute Shader 파라미터 바인딩
                        context.cmd.SetComputeTextureParam(
                            data.computeShader, data.kernelIndex,
                            "_SegmentationTexture", data.segmentationTexture);

                        context.cmd.SetComputeBufferParam(
                            data.computeShader, data.kernelIndex,
                            "_PixelCountBuffer", data.pixelCountBuffer);

                        // Dispatch Compute Shader
                        // 256×256 텍스처 / 8×8 thread groups = 32×32 dispatches
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, 32, 32, 1);
                        CommandBuffer nativeCommandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Action<uint[]> readbackCallback = data.onPixelCountsReadback;
                        nativeCommandBuffer.RequestAsyncReadback(data.pixelCountBuffer, request =>
                        {
                            if (request.hasError || readbackCallback == null)
                            {
                                return;
                            }

                            NativeArray<uint> source = request.GetData<uint>();
                            uint[] counts = new uint[source.Length];
                            source.CopyTo(counts);
                            readbackCallback.Invoke(counts);
                        });
                    });
                }
            }
        }

        public void Dispose()
        {
            segmentationRTHandle?.Release();
            segmentationDepthHandle?.Release();
        }

        private class RasterPassData
        {
            public RendererListHandle rendererListHandle;
        }

        private class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public ComputeBuffer pixelCountBuffer;
            public TextureHandle segmentationTexture;
            public Action<uint[]> onPixelCountsReadback;
        }
    }
#else
    public class AdSegmentationScriptableRenderPass : ScriptableRenderPass
    {
        public AdSegmentationScriptableRenderPass(
            Material material,
            ComputeShader pixelCounterCS,
            ComputeBuffer pixelCountBuffer,
            Action<uint[]> onPixelCountsReadback)
        {
        }

        public void Dispose()
        {
        }
    }
#endif
}
#endif
