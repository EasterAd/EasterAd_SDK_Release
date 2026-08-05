using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;

namespace EasterAd.Tests.EditMode
{
    public sealed class RuntimeEditModeTests
    {
        [TestCase("ETA_Implementation.dll")]
        [TestCase("ETA_Dependencies.dll")]
        public void PackageContainsNonEmptyRuntimeAssembly(string fileName)
        {
            string assemblyPath = Path.Combine(ResolvedPackagePath(), "Runtime", fileName);

            Assert.That(File.Exists(assemblyPath), Is.True,
                $"The prepared package must contain Runtime/{fileName}.");
            Assert.That(new FileInfo(assemblyPath).Length, Is.GreaterThan(0),
                $"The prepared package runtime assembly {fileName} must not be empty.");
        }

        [Test]
        public void PackagedMaterialManagerAssignsSdkShaderMaterial()
        {
            GameObject unityObject = null;
            Renderer renderer = null;
            Material assignedMaterial = null;

            try
            {
                unityObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                renderer = unityObject.GetComponent<Renderer>();
                renderer.sharedMaterial = null;

                unityObject.AddComponent<ETA.MaterialManager>();
                assignedMaterial = renderer.sharedMaterial;

                Assert.That(assignedMaterial, Is.Not.Null,
                    "The packaged MaterialManager must assign a material to an ad renderer.");
                Assert.That(assignedMaterial.shader, Is.Not.Null,
                    "The assigned ad material must use an available SDK shader.");
                Assert.That(assignedMaterial.shader.name, Is.EqualTo("EasterAd/UnifiedShader"),
                    "The packaged MaterialManager must use the current unified EasterAd shader.");
            }
            finally
            {
                Material materialToDestroy = assignedMaterial != null
                    ? assignedMaterial
                    : renderer != null ? renderer.sharedMaterial : null;
                if (materialToDestroy != null)
                {
                    UnityEngine.Object.DestroyImmediate(materialToDestroy);
                }

                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
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
                LogAssert.Expect(LogType.Exception,
                    new Regex("MissingComponentException: There is no 'Renderer' attached"));

                unityObject.AddComponent<ETA.MaterialManager>();
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
                ETA.Plane plane = unityObject.AddComponent<ETA.Plane>();
                MethodInfo startMethod = typeof(ETA.Item).GetMethod(
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
