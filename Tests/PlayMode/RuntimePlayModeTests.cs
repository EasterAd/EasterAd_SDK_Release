using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EasterAd_Dependencies;
using EasterAd_Implementation;
using EasterAd_Implementation.Library;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AdSegmentationManager = EasterAd_Dependencies.Unity.AdSegmentationManager;
using DependencyGameObject = EasterAd_Dependencies.Unity.GameObject;
using InstanceManager = EasterAd_Dependencies.Unity.InstanceManager;
using Object = UnityEngine.Object;
using RuntimeUI = EasterAd_Dependencies.Unity.UI;

namespace EasterAd.Tests.PlayMode
{
    public sealed class RuntimePlayModeTests
    {
        private Material assignedMaterial;
        private Camera testCamera;
        private RenderTexture testRenderTexture;
        private Texture2D sourceTexture;
        private Texture2D readbackTexture;
        private UnityEngine.GameObject unityObject;
        private UnityEngine.GameObject cameraObject;
        private string registeredClientKey;
        private int registeredSegmentationId;
        private FieldInfo componentManagerField;
        private IComponentManager originalComponentManager;
        private IDisposable impressionLoggingOverride;
        private ICamera originalMainCamera;
        private bool originalLogEnable;
        private List<string> originalDebugLogs;
        private List<RuntimeUI.DebugMesh> originalDebugMeshes;
        private UnityEngine.GameObject preExistingClientObject;
        private string preExistingClientKey;
        private ItemClient originalPreExistingClient;

        private sealed class MockImageComponentManager : IComponentManager
        {
            private readonly Renderer renderer;
            private readonly Texture2D texture;

            internal MockImageComponentManager(Renderer renderer, Texture2D texture)
            {
                this.renderer = renderer;
                this.texture = texture;
            }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Action<Dictionary<string, object>> callback)
            {
                renderer.material.mainTexture = texture;
                callback(new Dictionary<string, object>
                {
                    { "_id", "mock-fill" },
                    { "url", "mock://ad-image" },
                    { "mime", "image/png" },
                    { "width", 2 },
                    { "height", 2 },
                    { "interactionUrl", string.Empty }
                });
            }
        }

        [SetUp]
        public void SetUp()
        {
            originalMainCamera = InstanceManager.CameraManager.GetMainCamera();
            originalLogEnable = InstanceManager.DebugLogger.LogEnable;
            originalDebugLogs = new List<string>(InstanceManager.DebugLogger.DebugLogs);
            originalDebugMeshes = new List<RuntimeUI.DebugMesh>(RuntimeUI.DebugMeshes);
        }

        [UnityTest]
        public IEnumerator PackagedMaterialManagerRecreatesMaterialClearedDuringPlayerLoop()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var renderer = unityObject.GetComponent<Renderer>();
            renderer.sharedMaterial = null;
            unityObject.AddComponent<EasterAd.MaterialManager>();
            assignedMaterial = renderer.sharedMaterial;

            yield return null;

            Assert.That(assignedMaterial, Is.Not.Null,
                "The packaged MaterialManager must assign its initial SDK material.");

            Object.Destroy(assignedMaterial);
            renderer.sharedMaterial = null;
            yield return null;

            assignedMaterial = renderer.sharedMaterial;
            Assert.That(assignedMaterial, Is.Not.Null,
                "The packaged MaterialManager must recover when another runtime component clears its material.");
            Assert.That(assignedMaterial.shader.name, Is.EqualTo("EasterAd/UnifiedShader"),
                "Material recovery must preserve the current EasterAd shader contract.");
        }

        [UnityTest]
        public IEnumerator AssignedAdTextureRendersExpectedCenterPixel()
        {
            Color32 expectedPixel = new Color32(17, 93, 211, 255);
            Func<Texture2D> acquireAdImage = () => CreateSolidTexture(expectedPixel);
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var renderer = unityObject.GetComponent<Renderer>();
            renderer.sharedMaterial = null;
            unityObject.AddComponent<EasterAd.MaterialManager>();
            assignedMaterial = renderer.sharedMaterial;

            sourceTexture = acquireAdImage();
            ReplaceComponentManager(new MockImageComponentManager(renderer, sourceTexture));
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out _);
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), "runtime-image-render");
            FunctionScheduler.FuncCall(ref client, "Load");
            assignedMaterial = renderer.sharedMaterial;

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded),
                "The mocked image response must complete the SDK ad-loading path.");
            Assert.That(assignedMaterial.mainTexture, Is.SameAs(sourceTexture),
                "The SDK loading path must assign the acquired ad image to the runtime material.");

            cameraObject = new UnityEngine.GameObject("EasterAd frame verification camera");
            testCamera = cameraObject.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.orthographic = true;
            testCamera.orthographicSize = 0.75f;
            testCamera.transform.position = new Vector3(0f, 0f, -2f);
            testRenderTexture = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
            testCamera.targetTexture = testRenderTexture;

            yield return null;

            assignedMaterial.SetVector("_ConstantData", new Vector3(1f, 1f, 0f));
            testCamera.Render();

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = testRenderTexture;
                readbackTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                readbackTexture.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                readbackTexture.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32 actualPixel = readbackTexture.GetPixel(16, 16);
            Assert.That(actualPixel, Is.EqualTo(expectedPixel),
                "The player-loop frame must preserve the assigned ad texture pixel at the rendered quad center.");
        }

        [UnityTest]
        public IEnumerator AdSegmentationManagerReadsExactPixelCountsFromGpuBuffer()
        {
            const uint expectedVisiblePixels = 4096;
            var manager = new AdSegmentationManager();
            var pixelCountBuffer = new ComputeBuffer(256, sizeof(uint));

            try
            {
                const int itemInstanceId = 314159;
                int segmentationId = manager.RegisterAd(itemInstanceId);
                var pixelCounts = new uint[256];
                pixelCounts[segmentationId] = expectedVisiblePixels;
                pixelCountBuffer.SetData(pixelCounts);

                typeof(AdSegmentationManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(field => field.FieldType == typeof(float))
                    .SetValue(manager, Time.time - 1f);

                yield return null;

                manager.UpdatePixelCounts(pixelCountBuffer);

                Assert.That(manager.GetPixelCount(segmentationId), Is.EqualTo(expectedVisiblePixels),
                    "The GPU readback boundary must preserve the exact segmentation pixel count.");
                Assert.That(manager.GetScreenAreaRatio(segmentationId), Is.EqualTo(0.0625f).Within(0.000001f),
                    "The viewability area ratio must be derived from the exact 256x256 segmentation frame.");
            }
            finally
            {
                pixelCountBuffer.Dispose();
                manager.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ViewConditionsProduceExpectedRuntimeLog()
        {
            registeredClientKey = "runtime-view-log";
            preExistingClientKey = "pre-existing-runtime-client";
            var capturedImpressionLogs = new List<Dictionary<string, object>>();
            var capturedResultLogs = new List<Dictionary<string, object>>();
            EasterAdSdkClient preExistingSdkClient = EasterAdSdkClient.CreateClient(this);
            preExistingClientObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient preExistingClient = new PlaneClient(
                new DependencyGameObject(preExistingClientObject), preExistingClientKey);
            SetItemStatus(preExistingClient, ItemStatus.Loaded);
            preExistingClient.AllowImpression = false;
            originalPreExistingClient = preExistingSdkClient.GetItemClient(preExistingClientKey);
            preExistingSdkClient.RemoveItemClient(preExistingClientKey);
            preExistingSdkClient.AddItemClient(preExistingClientKey, ref preExistingClient);

            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), registeredClientKey);
            SetItemStatus(client, ItemStatus.Loaded);

            InstanceManager.UI.AddDebugMesh(new DependencyGameObject(unityObject), new[] { 255, 0, 0, 128 });
            var expectedDebugMeshes = new List<RuntimeUI.DebugMesh>(RuntimeUI.DebugMeshes);

            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                value => capturedImpressionLogs.Add((Dictionary<string, object>)value),
                value => capturedResultLogs.Add((Dictionary<string, object>)value),
                DateTime.Now, out EasterAdSdkClient sdkClient);
            sdkClient.AddItemClient(registeredClientKey, ref client);

            cameraObject = new UnityEngine.GameObject("EasterAd view-condition camera");
            testCamera = cameraObject.AddComponent<Camera>();
            testCamera.orthographic = true;
            testCamera.orthographicSize = 6f;
            testCamera.transform.position = new Vector3(0f, 5f, 0f);
            testCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
            InstanceManager.CameraManager.SetMainCamera(new DependencyGameObject(cameraObject).Camera);

            var segmentationManager = (AdSegmentationManager)InstanceManager.AdSegmentationManager;
            registeredSegmentationId = segmentationManager.RegisterAd(unityObject.GetInstanceID());
            uint[] pixelCounts = (uint[])typeof(AdSegmentationManager)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(uint[]))
                .GetValue(segmentationManager);
            pixelCounts[registeredSegmentationId] = segmentationManager.GetTotalScreenPixels();

            InstanceManager.DebugLogger.LogEnable = true;
            InstanceManager.DebugLogger.DebugLogs.Clear();
            for (int evaluation = 0; evaluation < 5; evaluation++)
            {
                sdkClient.ImpressionRoutine();
                yield return new WaitForSecondsRealtime(0.21f);
            }

            client.AllowImpression = false;
            sdkClient.ImpressionRoutine();

            impressionLoggingOverride.Dispose();
            impressionLoggingOverride = null;
            RestoreDebugMeshes(expectedDebugMeshes);

            Assert.That(InstanceManager.DebugLogger.DebugLogs,
                Is.EqualTo(new[] { "Impression passed: " + registeredClientKey }),
                "An ad satisfying the runtime view conditions must reach the SDK impression logging boundary exactly once.");
            Assert.That(capturedImpressionLogs, Has.Count.EqualTo(6),
                "Each view-condition evaluation must use the injected logger instead of the process-wide HTTP log worker.");
            Assert.That(capturedResultLogs, Has.Count.EqualTo(1),
                "Ending a qualified view must invoke the injected impression-result logger exactly once.");
            Assert.That(capturedResultLogs[0]["adUnitId"], Is.EqualTo(registeredClientKey),
                "The emitted impression result must identify the viewed ad placement.");
            Assert.That(EasterAdSdkClient.CreateClient(this), Is.SameAs(preExistingSdkClient),
                "The logging override must restore the pre-existing SDK singleton object graph.");
            Assert.That(preExistingClient.GetStatus(), Is.EqualTo(ItemStatus.Loaded),
                "The isolated view test must not mutate a pre-existing loaded client.");
            Assert.That(RuntimeUI.DebugMeshes, Is.EqualTo(expectedDebugMeshes),
                "The isolated UI must preserve debug meshes that existed before the view test ran.");
        }

        [Test]
        public void PackagedDependencyWrapperTracksCameraComponentLifecycle()
        {
            unityObject = new UnityEngine.GameObject("EasterAd camera wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);

            Assert.That(wrapper.Camera, Is.Null,
                "A wrapped object without a Unity Camera must not expose a stale camera wrapper.");

            var unityCamera = unityObject.AddComponent<Camera>();
            Assert.That(wrapper.Camera, Is.Not.Null,
                "The wrapper must discover a Camera added after wrapper construction.");

            Object.DestroyImmediate(unityCamera);
            Assert.That(wrapper.Camera, Is.Null,
                "The wrapper must stop exposing a Camera after the Unity component is removed.");
        }

        [Test]
        public void PackagedDependencyTransformWritesThroughToUnityObject()
        {
            unityObject = new UnityEngine.GameObject("EasterAd transform wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);

            wrapper.Transform.X = 2.5f;
            wrapper.Transform.Y = -3.0f;
            wrapper.Transform.Z = 4.5f;
            wrapper.Transform.LocalScaleX = 1.5f;
            wrapper.Transform.LocalScaleY = 2.0f;
            wrapper.Transform.LocalScaleZ = 2.5f;

            Assert.That(unityObject.transform.position, Is.EqualTo(new Vector3(2.5f, -3.0f, 4.5f)),
                "Dependency transform position writes must update the wrapped Unity object.");
            Assert.That(unityObject.transform.localScale, Is.EqualTo(new Vector3(1.5f, 2.0f, 2.5f)),
                "Dependency transform scale writes must update the wrapped Unity object.");
        }
        [Test]
        public void NonInteractablePlaneClientRejectsInteractionWithoutStateTransition()
        {
            unityObject = new UnityEngine.GameObject("EasterAd interaction failure boundary");
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), "non-interactable-runtime-test");

            FunctionScheduler.FuncCall(ref client, "StartInteraction", out string interactionUrl);

            Assert.That(interactionUrl, Is.Empty,
                "A non-interactable placement must not expose an interaction URL.");
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Ready),
                "Rejected interaction must leave the ad item in its prior ready state.");
        }

        [UnityTest]
        public IEnumerator PackagedDependencyWrapperPreservesUnityObjectIdentityAcrossFrame()
        {
            unityObject = new UnityEngine.GameObject("EasterAd identity wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);
            int expectedInstanceId = unityObject.GetInstanceID();

            yield return null;

            Assert.That(wrapper.GetInstanceID, Is.EqualTo(expectedInstanceId),
                "The packaged dependency wrapper must preserve Unity object identity across a player-loop frame.");

            Object.Destroy(unityObject);
            yield return null;

            Assert.That(unityObject == null, Is.True,
                "The PlayMode player loop must process destruction of the wrapped Unity object.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (impressionLoggingOverride != null)
            {
                impressionLoggingOverride.Dispose();
            }

            if (!string.IsNullOrEmpty(preExistingClientKey))
            {
                EasterAdSdkClient sdkClient = EasterAdSdkClient.CreateClient(this);
                sdkClient.RemoveItemClient(preExistingClientKey);
                if (originalPreExistingClient != null)
                {
                    sdkClient.AddItemClient(preExistingClientKey, ref originalPreExistingClient);
                }
            }

            if (registeredSegmentationId > 0)
            {
                InstanceManager.AdSegmentationManager.UnregisterAd(registeredSegmentationId);
            }

            InstanceManager.CameraManager.SetMainCamera(originalMainCamera);
            InstanceManager.DebugLogger.LogEnable = originalLogEnable;
            if (originalDebugLogs != null)
            {
                InstanceManager.DebugLogger.DebugLogs.Clear();
                InstanceManager.DebugLogger.DebugLogs.AddRange(originalDebugLogs);
            }

            if (componentManagerField != null)
            {
                componentManagerField.SetValue(null, originalComponentManager);
            }

            if (originalDebugMeshes != null)
            {
                RestoreDebugMeshes(originalDebugMeshes);
            }

            if (assignedMaterial != null)
            {
                Object.Destroy(assignedMaterial);
            }

            if (unityObject != null)
            {
                Object.Destroy(unityObject);
            }

            if (cameraObject != null)
            {
                Object.Destroy(cameraObject);
            }

            if (preExistingClientObject != null)
            {
                Object.Destroy(preExistingClientObject);
            }

            if (sourceTexture != null)
            {
                Object.Destroy(sourceTexture);
            }

            if (readbackTexture != null)
            {
                Object.Destroy(readbackTexture);
            }

            if (testRenderTexture != null)
            {
                testRenderTexture.Release();
                Object.Destroy(testRenderTexture);
            }

            assignedMaterial = null;
            testCamera = null;
            testRenderTexture = null;
            sourceTexture = null;
            readbackTexture = null;
            unityObject = null;
            cameraObject = null;
            preExistingClientObject = null;
            registeredClientKey = null;
            preExistingClientKey = null;
            registeredSegmentationId = 0;
            componentManagerField = null;
            originalComponentManager = null;
            impressionLoggingOverride = null;
            originalMainCamera = null;
            originalLogEnable = false;
            originalDebugLogs = null;
            originalDebugMeshes = null;
            originalPreExistingClient = null;
            yield return null;
        }

        private void ReplaceComponentManager(IComponentManager replacement)
        {
            componentManagerField = typeof(InstanceManager)
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(IComponentManager));
            originalComponentManager = (IComponentManager)componentManagerField.GetValue(null);
            componentManagerField.SetValue(null, replacement);
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels32(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static void RestoreDebugMeshes(IEnumerable<RuntimeUI.DebugMesh> debugMeshes)
        {
            RuntimeUI.DebugMeshes.Clear();
            RuntimeUI.DebugMeshes.AddRange(debugMeshes);
        }

        private static void SetItemStatus(ItemClient client, ItemStatus status)
        {
            for (Type type = client.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(ItemStatus))
                    {
                        field.SetValue(client, status);
                    }
                }
            }
        }

    }
}
