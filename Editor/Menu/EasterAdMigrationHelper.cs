using System.Collections.Generic;
using ETA;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ETA_Editor.Menu
{
    /// <summary>
    /// Helper class for migrating from legacy EasterAd assets to the new unified system
    /// </summary>
    public static class EasterAdMigrationHelper
    {
        // Path mappings for migration
        private const string OldPrefabPath = "Assets/EasterAd/Plane Item.prefab";
        private const string NewPrefabPath = "Packages/com.easterad.easterad/Runtime/Prefabs/PlaneItem.prefab";
        private const string LegacyAssetFolder = "Assets/EasterAd";

        /// <summary>
        /// Check if legacy assets exist in the project
        /// </summary>
        public static bool HasLegacyAssets()
        {
            return AssetDatabase.IsValidFolder(LegacyAssetFolder);
        }

        /// <summary>
        /// Check if the unified shader is available
        /// </summary>
        public static bool HasUnifiedShader()
        {
            // ReSharper disable once ShaderLabShaderReferenceNotResolved
            return Shader.Find("EasterAd/UnifiedShader") != null;
        }

        /// <summary>
        /// Check if legacy shaders are still present
        /// </summary>
        public static bool HasLegacyShaders()
        {
            // ReSharper disable once ShaderLabShaderReferenceNotResolved
            return Shader.Find("EasterAd/DefaultShader") != null;
        }

        /// <summary>
        /// Migrate all prefab references in the current scene from old to new system
        /// </summary>
        public static void MigratePrefabReferences()
        {
            GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewPrefabPath);
            if (newPrefab == null)
            {
                Debug.LogError($"[EasterAd] New prefab not found at: {NewPrefabPath}");
                return;
            }

            // Find all Item components in the scene
            int migratedCount = 0;
            int checkedCount = 0;
#if UNITY_6000_0_OR_NEWER
            Item[] allItems = Object.FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
#else
            Item[] allItems = Object.FindObjectsOfType<Item>();
#endif

            if (allItems.Length == 0)
            {
                Debug.Log("[EasterAd] No Item components found in the current scene.");
                return;
            }

            List<GameObject> itemsToMigrate = new List<GameObject>();

            foreach (Item item in allItems)
            {
                checkedCount++;

                // Check if this is an instance of the old prefab
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                if (sourcePrefab != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);

                    if (sourcePath == OldPrefabPath)
                    {
                        itemsToMigrate.Add(item.gameObject);
                    }
                }
            }

            if (itemsToMigrate.Count == 0)
            {
                Debug.Log($"[EasterAd] Checked {checkedCount} Item(s). No legacy prefab instances found to migrate.");
                return;
            }

            // Perform migration
            foreach (GameObject oldItem in itemsToMigrate)
            {
                Item itemComponent = oldItem.GetComponent<Item>();
                if (itemComponent == null) continue;

                // Store current configuration
                string adUnitId = itemComponent.adUnitId;
                bool allowImpression = itemComponent.allowImpression;
                bool loadOnStart = itemComponent.loadOnStart;
                Vector3 localPosition = oldItem.transform.localPosition;
                Quaternion localRotation = oldItem.transform.localRotation;
                Vector3 localScale = oldItem.transform.localScale;
                Transform parent = oldItem.transform.parent;
                string name = oldItem.name;
                int siblingIndex = oldItem.transform.GetSiblingIndex();

                // Instantiate new prefab
                GameObject newInstance = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, parent);
                newInstance.name = name;
                newInstance.transform.localPosition = localPosition;
                newInstance.transform.localRotation = localRotation;
                newInstance.transform.localScale = localScale;
                newInstance.transform.SetSiblingIndex(siblingIndex);

                // Restore configuration
                Item newItem = newInstance.GetComponent<Item>();
                if (newItem != null)
                {
                    newItem.adUnitId = adUnitId;
                    newItem.allowImpression = allowImpression;
                    newItem.loadOnStart = loadOnStart;

                    // Record property modifications for prefab override
                    PrefabUtility.RecordPrefabInstancePropertyModifications(newItem);
                }

                // Destroy old instance
                Object.DestroyImmediate(oldItem);

                migratedCount++;
            }

            if (migratedCount > 0)
            {
                Debug.Log($"[EasterAd] Successfully migrated {migratedCount} prefab instance(s) to the new system.");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    SceneManager.GetActiveScene()
                );

                // Prompt to save the scene
                if (EditorUtility.DisplayDialog(
                    "Migration Complete",
                    $"Successfully migrated {migratedCount} prefab instance(s).\n\nWould you like to save the scene now?",
                    "Save Scene",
                    "Save Later"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                }
            }
        }

        /// <summary>
        /// Clean up legacy assets from the project
        /// </summary>
        public static void CleanupLegacyAssets()
        {
            if (!HasLegacyAssets())
            {
                Debug.Log("[EasterAd] No legacy assets found to clean up.");
                return;
            }

            // Check if there are still references to old assets
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            List<string> referencingAssets = new List<string>();

            foreach (string assetPath in allAssetPaths)
            {
                if (assetPath.StartsWith(LegacyAssetFolder))
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (string dependency in dependencies)
                {
                    if (dependency.StartsWith(LegacyAssetFolder))
                    {
                        referencingAssets.Add(assetPath);
                        break;
                    }
                }
            }

            string message = $"This will delete the folder '{LegacyAssetFolder}' containing old assets.\n\n";

            if (referencingAssets.Count > 0)
            {
                message += $"⚠ Warning: Found {referencingAssets.Count} asset(s) still referencing legacy assets.\n";
                message += "You should migrate all references first.\n\n";
            }

            message += "This action cannot be undone. Continue?";

            if (EditorUtility.DisplayDialog(
                "Clean Up Legacy Assets",
                message,
                "Delete",
                "Cancel"))
            {
                AssetDatabase.DeleteAsset(LegacyAssetFolder);
                AssetDatabase.Refresh();
                Debug.Log("[EasterAd] Legacy assets cleaned up successfully.");

                // Clear the warning flag so it shows again if needed
                EditorPrefs.DeleteKey("EasterAd_LegacyWarningShown_1.2.0");
            }
        }

        /// <summary>
        /// Get a summary of the current migration status
        /// </summary>
        public static string GetMigrationStatus()
        {
            bool hasLegacy = HasLegacyAssets();
            bool hasUnified = HasUnifiedShader();
            bool hasLegacyShaders = HasLegacyShaders();

            if (!hasLegacy && hasUnified && !hasLegacyShaders)
            {
                return "✅ Fully migrated to unified system";
            }
            else if (hasUnified && hasLegacy)
            {
                return "⚠ Unified system installed, legacy assets can be cleaned up";
            }
            else if (!hasUnified && hasLegacyShaders)
            {
                return "⚠ Using legacy system, migration recommended";
            }
            else
            {
                return "❌ No EasterAd assets detected";
            }
        }
    }
}