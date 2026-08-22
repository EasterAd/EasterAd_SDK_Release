using System.IO;
using System.Reflection;
using EasterAd_Dependencies.Common;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;

namespace EasterAd.Tests.EditMode
{
    public sealed class RuntimeEditModeTests
    {
        [TestCase("EasterAd_Implementation.dll")]
        [TestCase("EasterAd_Dependencies.dll")]
        public void PackageContainsNonEmptyRuntimeAssembly(string fileName)
        {
            string assemblyPath = Path.Combine(ResolvedPackagePath(), "Runtime", fileName);

            Assert.That(File.Exists(assemblyPath), Is.True,
                $"The prepared package must contain Runtime/{fileName}.");
            Assert.That(new FileInfo(assemblyPath).Length, Is.GreaterThan(0),
                $"The prepared package runtime assembly {fileName} must not be empty.");
        }

        [TestCase("https://cdn.example.com/ad.png", true)]
        [TestCase("http://cdn.example.com/ad.png", true)]
        [TestCase("/relative/ad.png", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("data:image/png;base64,AA==", false)]
        [TestCase("file:///tmp/ad.png", false)]
        [TestCase("https://user:password@cdn.example.com/ad.png", false)]
        [TestCase("https://cdn.example.com@evil.example/ad.png", false)]
        public void AdContentUrlPolicyAllowsOnlyAbsoluteHttpUrlsWithoutUserInfo(string value, bool expected)
        {
            Assert.That(HttpUrlPolicy.IsAllowed(value), Is.EqualTo(expected));
        }

        [Test]
        public void PackagedMaterialManagerAssignsSdkShaderMaterial()
        {
            GameObject unityObject = null;
            Renderer renderer = null;
            Material firstAssignedMaterial = null;
            Material replacementMaterial = null;
            Material hostMaterial = null;

            try
            {
                unityObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                renderer = unityObject.GetComponent<Renderer>();
                renderer.sharedMaterial = null;

                EasterAd.MaterialManager manager = unityObject.AddComponent<EasterAd.MaterialManager>();
                firstAssignedMaterial = renderer.sharedMaterial;

                Assert.That(firstAssignedMaterial, Is.Not.Null,
                    "The packaged MaterialManager must assign a material to an ad renderer.");
                Assert.That(firstAssignedMaterial.shader, Is.Not.Null,
                    "The assigned ad material must use an available SDK shader.");
                Assert.That(firstAssignedMaterial.shader.name, Is.EqualTo("EasterAd/UnifiedShader"),
                    "The packaged MaterialManager must use the current unified EasterAd shader.");

                hostMaterial = new Material(firstAssignedMaterial.shader);
                manager.defaultMaterial = hostMaterial;
                MethodInfo applyMaterial = typeof(EasterAd.MaterialManager).GetMethod(
                    "ApplyMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(applyMaterial, Is.Not.Null);
                applyMaterial.Invoke(manager, null);
                replacementMaterial = renderer.sharedMaterial;

                Assert.That(replacementMaterial, Is.Not.SameAs(hostMaterial),
                    "MaterialManager must clone, not take ownership of, a host material.");
                Assert.That(firstAssignedMaterial == null, Is.True,
                    "Replacing an SDK material in EditMode must destroy the previous native material immediately.");

                UnityEngine.Object.DestroyImmediate(unityObject);
                unityObject = null;
                Assert.That(replacementMaterial == null, Is.True,
                    "Destroying the ad target must release the SDK-owned material.");
                Assert.That(hostMaterial == null, Is.False,
                    "Destroying the ad target must not destroy the host-owned source material.");
            }
            finally
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
                }

                if (hostMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(hostMaterial);
                }
            }
        }

        [Test]
        public void PackagedMaterialManagerExposesMissingRendererFailureBoundary()
        {
            GameObject unityObject = null;

            try
            {
                unityObject = new GameObject("EasterAd renderer failure boundary");
                // AddComponent invokes both Awake and OnValidate in EditMode, so the same invalid
                // component is evaluated at two distinct lifecycle boundaries.
                for (int lifecycleCallback = 0; lifecycleCallback < 2; lifecycleCallback++)
                {
                    LogAssert.Expect(LogType.Error, "Plane Renderer is not found on ad prefab.");
                }
                unityObject.AddComponent<EasterAd.MaterialManager>();
            }
            finally
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
                }
            }
        }

        [Test]
        public void UninitializedPlaneIgnoresRuntimeActions()
        {
            GameObject unityObject = null;

            try
            {
                unityObject = new GameObject("EasterAd uninitialized plane");
                EasterAd.Plane plane = unityObject.AddComponent<EasterAd.Plane>();
                MethodInfo startMethod = typeof(EasterAd.Item).GetMethod(
                    "Start", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(startMethod, Is.Not.Null,
                    "Item must retain the runtime Start callback that handles automatic loading.");
                Assert.DoesNotThrow(() => startMethod.Invoke(plane, null),
                    "An Item with no initialized client must not load when its Start callback runs.");
                Assert.DoesNotThrow(plane.Load,
                    "Calling Load before SDK item initialization must not throw.");
                Assert.That(plane.StartInteraction(), Is.Empty,
                    "An uninitialized item must not report an interaction URL.");
                Assert.DoesNotThrow(plane.EndInteraction,
                    "Ending interaction before SDK item initialization must not throw.");
            }
            finally
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
                }
            }
        }

        private static string ResolvedPackagePath()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(RuntimeEditModeTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null, "The test assembly must belong to the EasterAd package.");
            return packageInfo.resolvedPath;
        }
    }
}
