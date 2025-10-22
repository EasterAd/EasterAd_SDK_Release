using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using InstanceManager = ETA_Dependencies.Unity.InstanceManager;

namespace ETA
{
    public class AdSegmentationRendererFeature : ScriptableRendererFeature
    {
        private AdSegmentationScriptableRenderPass renderPass;
        private ComputeBuffer _pixelCountBuffer;
        private const int BufferSize = 256;

        public override void Create()
        {
            // Shader 로드
            Shader shader = Shader.Find("EasterAd/AdSegmentation");
            if (shader == null)
            {
                InstanceManager.DebugLogger.LogWarning("Shader 'EasterAd/AdSegmentation' not found!");
                return;
            }

            // Material 생성
            Material material = CoreUtils.CreateEngineMaterial(shader);
            if (material == null)
            {
                InstanceManager.DebugLogger.LogWarning("Failed to create material from shader");
                return;
            }

            // Phase 4: Compute Shader 로드
            ComputeShader pixelCounterCS = Resources.Load<ComputeShader>("Shaders/AdSegmentationPixelCounter");
            if (pixelCounterCS == null)
            {
                InstanceManager.DebugLogger.LogWarning("ComputeShader 'Shaders/AdSegmentationPixelCounter' not found in Resources!");
                return;
            }

            // ComputeBuffer 생성 (렌더링 계층에서 관리)
            _pixelCountBuffer?.Dispose();
            _pixelCountBuffer = new ComputeBuffer(BufferSize, sizeof(uint), ComputeBufferType.Default);

            // RenderPass 생성 (Material + ComputeShader + Buffer 전달)
            renderPass = new AdSegmentationScriptableRenderPass(material, pixelCounterCS, _pixelCountBuffer);
            renderPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

            // RendererFeature 등록
            var adSegManager = InstanceManager.AdSegmentationManager;
            adSegManager.SetRendererFeature(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null || _pixelCountBuffer == null)
                return;

            // VR Note: This implementation processes only the target camera configured in EasterAdSDK.
            // In VR mode, this will naturally process once per frame regardless of stereo rendering mode.
            // This approach avoids buffer conflicts and URP version compatibility issues with XRPass.eyeIndex.
            // Future enhancement: Add conditional VR-specific handling if needed.

            // Get global camera from EasterAdSdk
            Camera targetCam = GetGlobalTargetCamera();

            if (targetCam != null && renderingData.cameraData.camera == targetCam)
            {
                renderer.EnqueuePass(renderPass);

                // AdSegmentationManager에 픽셀 카운트 업데이트 요청
                var adSegManager = InstanceManager.AdSegmentationManager;
                adSegManager.UpdatePixelCounts(_pixelCountBuffer);
            }
        }

        /// <summary>
        /// Gets the global target camera from EasterAdSdk.
        /// Priority: EasterAdSdk.targetCamera > Camera.main
        /// </summary>
        private Camera GetGlobalTargetCamera()
        {
            // Try to get the EasterAdSdk instance safely
            try
            {
                // Use global camera from EasterAdSdk (synchronized with CameraManager through Update())
                if (EasterAdSdk.Instance != null && EasterAdSdk.Instance.targetCamera != null)
                {
                    return EasterAdSdk.Instance.targetCamera;
                }
            }
            catch (Exception)
            {
                // SDK is not enabled, fallback to Camera.main
            }

            // Fallback: Camera.main
            return Camera.main;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pixelCountBuffer?.Dispose();
                _pixelCountBuffer = null;
            }

            renderPass?.Dispose();
            base.Dispose(disposing);
        }
    }
}
