using System.Collections.Generic;
using ETA;
using UnityEngine;
using UnityEditor;

namespace ETA_Editor.Menu
{
    /// <summary>
    /// Helper class for migrating from legacy EasterAd assets to the new unified system
    /// </summary>
    public static class EasterAdMigrationHelper
    {
        // Path mappings for migration
        private const string OLD_PREFAB_PATH = "Assets/EasterAd/Plane Item.prefab";
        private const string NEW_PREFAB_PATH = "Packages/com.easterad.easterad/Runtime/Prefabs/PlaneItem.prefab";
        private const string LEGACY_ASSET_FOLDER = "Assets/EasterAd";

        /// <summary>
        /// Check if legacy assets exist in the project
        /// </summary>
        public static bool HasLegacyAssets()
        {
            return AssetDatabase.IsValidFolder(LEGACY_ASSET_FOLDER);
        }

        /// <summary>
        /// Check if the unified shader is available
        /// </summary>
        public static bool HasUnifiedShader()
        {
            return Shader.Find("EasterAd/UnifiedShader") != null;
        }

        /// <summary>
        /// Check if legacy shaders are still present
        /// </summary>
        public static bool HasLegacyShaders()
        {
            return Shader.Find("EasterAd/DefaultShader") != null;
        }

        /// <summary>
        /// Migrate all prefab references in the current scene from old to new system
        /// </summary>
        public static void MigratePrefabReferences()
        {
            GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NEW_PREFAB_PATH);
            if (newPrefab == null)
            {
                Debug.LogError($"[EasterAd] New prefab not found at: {NEW_PREFAB_PATH}");
                return;
            }

            // Find all Item components in the scene
            int migratedCount = 0;
            int checkedCount = 0;
            Item[] allItems = GameObject.FindObjectsOfType<Item>();

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

                    if (sourcePath == OLD_PREFAB_PATH)
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
                Vector3 position = oldItem.transform.position;
                Quaternion rotation = oldItem.transform.rotation;
                Vector3 scale = oldItem.transform.localScale;
                Transform parent = oldItem.transform.parent;
                string name = oldItem.name;
                int siblingIndex = oldItem.transform.GetSiblingIndex();

                // Instantiate new prefab
                GameObject newInstance = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, parent);
                newInstance.name = name;
                newInstance.transform.position = position;
                newInstance.transform.rotation = rotation;
                newInstance.transform.localScale = scale;
                newInstance.transform.SetSiblingIndex(siblingIndex);

                // Restore configuration
                Item newItem = newInstance.GetComponent<Item>();
                if (newItem != null)
                {
                    newItem.adUnitId = adUnitId;
                    newItem.allowImpression = allowImpression;
                    newItem.loadOnStart = loadOnStart;
                }

                // Mark as dirty for saving
                EditorUtility.SetDirty(newInstance);

                // Destroy old instance
                GameObject.DestroyImmediate(oldItem);

                migratedCount++;
            }

            if (migratedCount > 0)
            {
                Debug.Log($"[EasterAd] Successfully migrated {migratedCount} prefab instance(s) to the new system.");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
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
                if (assetPath.StartsWith(LEGACY_ASSET_FOLDER))
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (string dependency in dependencies)
                {
                    if (dependency.StartsWith(LEGACY_ASSET_FOLDER))
                    {
                        referencingAssets.Add(assetPath);
                        break;
                    }
                }
            }

            string message = $"This will delete the folder '{LEGACY_ASSET_FOLDER}' containing old assets.\n\n";

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
                AssetDatabase.DeleteAsset(LEGACY_ASSET_FOLDER);
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