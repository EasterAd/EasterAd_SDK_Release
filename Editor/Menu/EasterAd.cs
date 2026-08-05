using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ETA;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once RedundantUsingDirective
using System.IO;

namespace ETA_Editor.Menu
{
    public class EasterAd : EditorWindow
    {
        private bool _easterAdEnabled;
        private string _tempGameId = "";
        private string _tempSdkKey = "";
        private bool _tempLogEnable;

        private string _currentGameId = "";
        private string _currentSdkKey = "";

        private Vector2 _scrollPosition = Vector2.zero;
        private readonly Dictionary<int, string> _pendingItemIds = new Dictionary<int, string>();

        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/EasterAd")]
        private static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EasterAd window = (EasterAd)GetWindow(typeof(EasterAd));
            window.titleContent = new GUIContent("EasterAd");
            // window.minSize = new Vector2(820,350);
            window.Show();
        }

        void OnEnable()
        {
            string filename = "ETA_Config.txt";
            string filepath = Path.Combine(Application.streamingAssetsPath, filename);
            if (File.Exists(filepath) == false) { return; }

            string[] config = File.ReadAllLines(filepath);
            _easterAdEnabled = Boolean.Parse(config[0]);
            _tempGameId = config[1];
            _tempSdkKey = config[2];
            _tempLogEnable = Boolean.Parse(config[3]);

            _currentGameId = _tempGameId;
            _currentSdkKey = _tempSdkKey;
        }

        private void OnGUI()
        {
            if (IsUniversalRenderPipelineActive())
            {
                EnsureUrpRendererFeatureInstalled();
            }
            else
            {
                DrawRenderPipelineSupportNotice();
                EditorGUILayout.Space();
            }

            // Migration status check and notification
            bool hasLegacyAssets = EasterAdMigrationHelper.HasLegacyAssets();
            bool hasUnifiedShader = EasterAdMigrationHelper.HasUnifiedShader();

            if (hasLegacyAssets)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("⚠ Migration Available", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Legacy assets detected in 'Assets/EasterAd/'.\n" +
                    "The new system (v1.2.0+) uses assets directly from the package.\n\n" +
                    "All assets are now managed under:\n" +
                    "Packages/EasterAd SDK/Runtime/",
                    MessageType.Warning
                );

                EditorGUILayout.Space();

                // Migration buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("1. Migrate Prefab References", GUILayout.Height(30)))
                {
                    EasterAdMigrationHelper.MigratePrefabReferences();
                }

                GUI.enabled = !hasLegacyAssets || hasUnifiedShader;
                if (GUILayout.Button("2. Clean Up Legacy Assets", GUILayout.Height(30)))
                {
                    EasterAdMigrationHelper.CleanupLegacyAssets();
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Status: " + EasterAdMigrationHelper.GetMigrationStatus(), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }
            else if (hasUnifiedShader)
            {
                // Show migration success message only once
                if (!EditorPrefs.GetBool("EasterAd_UnifiedShaderNoticeShown", false))
                {
                    EditorGUILayout.HelpBox(
                        "✅ Using new unified shader system.",
                        MessageType.Info
                    );

                    // Mark as shown
                    EditorPrefs.SetBool("EasterAd_UnifiedShaderNoticeShown", true);
                }
            }

            GUILayout.Label("Base Settings", EditorStyles.boldLabel);
            _easterAdEnabled = EditorGUILayout.BeginToggleGroup("Enable EasterAd SDK", _easterAdEnabled);

            EditorGUILayout.BeginHorizontal();
            _tempGameId = EditorGUILayout.TextField("Game ID", _tempGameId);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField(_currentGameId);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _tempSdkKey = EditorGUILayout.TextField("SDK Key", _tempSdkKey);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField(_currentSdkKey);
            EditorGUILayout.EndHorizontal();

            _tempLogEnable = EditorGUILayout.Toggle("Enable Log", _tempLogEnable);

            if (GUILayout.Button("Save"))
            {
                SaveSettings();
            }

            if (EasterAdSdk.OnceInitialized)
            {
                if (GUILayout.Button("Re-Initialize SDK"))
                {
                    EasterAdSdk.Instance.ReInitialize();
                }
            }

            EditorGUILayout.Space();


            EditorGUILayout.LabelField("List of Items", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Focus", GUILayout.Width(50));
            EditorGUILayout.LabelField("Item ID", GUILayout.Width(170));
            EditorGUILayout.LabelField("", GUILayout.Width(60));
            EditorGUILayout.LabelField("Location", GUILayout.Width(150));
            EditorGUILayout.LabelField("Status", GUILayout.Width(100));
            EditorGUILayout.LabelField("Impression", GUILayout.Width(80));
            EditorGUILayout.LabelField("Load On Start", GUILayout.Width(100));
            EditorGUILayout.LabelField("Interactable", GUILayout.Width(80));
            EditorGUILayout.LabelField("Interaction", GUILayout.Width(100));
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField("Refresh", GUILayout.Width(70));
            EditorGUILayout.LabelField("Remove", GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();

            int index = 1;
            foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.InstanceID))
            {
                var itemClient = item.Client;
                int itemInstanceId = item.GetInstanceID();
                if (!_pendingItemIds.TryGetValue(itemInstanceId, out string pendingItemId))
                {
                    pendingItemId = item.adUnitId ?? string.Empty;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(index.ToString(), GUILayout.Width(50)))
                {
                    FocusItem(item);
                }

                string editedItemId = EditorGUILayout.TextField(pendingItemId, GUILayout.Width(170));
                _pendingItemIds[itemInstanceId] = editedItemId;
                bool itemIdChanged = !String.Equals(editedItemId, item.adUnitId, StringComparison.Ordinal);
                using (new EditorGUI.DisabledScope(!itemIdChanged))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    {
                        ApplyItemId(item, editedItemId);
                    }
                }

                EditorGUILayout.LabelField(item.transform.position.ToString(), GUILayout.Width(150));
                if (Application.isPlaying && itemClient != null)
                {
                    EditorGUILayout.LabelField(itemClient.GetStatus().ToString(), GUILayout.Width(100));
                }
                else if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Not initialized", GUILayout.Width(100));
                }
                else
                {
                    EditorGUILayout.LabelField("Editor Mode", GUILayout.Width(100));
                }
                item.allowImpression = GUILayout.Toggle(item.allowImpression, "", GUILayout.Width(80));
                item.loadOnStart = GUILayout.Toggle(item.loadOnStart, "", GUILayout.Width(100));
                item.interactable = GUILayout.Toggle(item.interactable, "", GUILayout.Width(80));
                if (GUILayout.Button("Start", GUILayout.Width(50)))
                {
                    if (Application.isPlaying)
                    {
                        string interactionUrl = item.StartInteraction();
                        if (!String.IsNullOrEmpty(interactionUrl))
                        {
                            Application.OpenURL(interactionUrl);
                        }
                    }
                }
                if (GUILayout.Button("End", GUILayout.Width(50)))
                {
                    if (Application.isPlaying)
                    {
                        item.EndInteraction();
                    }
                }
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                item.enableRefresh = GUILayout.Toggle(item.enableRefresh, "", GUILayout.Width(70));

                if (GUILayout.Button("Remove", GUILayout.Width(75)))
                {
                    _pendingItemIds.Remove(itemInstanceId);
                    DestroyImmediate(item.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                index++;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndToggleGroup();

            // Prefab section
            GUILayout.Label("Ad Prefab", EditorStyles.boldLabel);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.easterad.easterad/Runtime/Prefabs/PlaneItem.prefab"
            );

            if (prefab != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Plane Item Prefab", prefab, typeof(GameObject), false);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Drag this prefab into your scene to place an ad.",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox("Prefab not found. Package may need reinstallation.", MessageType.Error);
            }

            // if (!Application.isPlaying)
            // {
            //     EtaSdk.DestroyCall();
            // }
        }

        // private void ImportPackage(string packageName)
        // {
        //     if (!string.IsNullOrEmpty(packageName))
        //     {
        //         AssetDatabase.ImportPackage(packageName, true);
        //     }
        //     else
        //     {
        //         Debug.Log(packageName + " is not found.");
        //     }
        // }

        private void SaveSettings()
        {
            if (_tempGameId == "")
            {
                try
                {
                    EasterAdSdk.DestroyCall();
                }
                finally
                {
                    Debug.LogError("Game ID is required");
                }
            }
            else
            {
                StringBuilder config = new StringBuilder();
                config.AppendLine(_easterAdEnabled.ToString());
                config.AppendLine(_tempGameId);
                config.AppendLine(_tempSdkKey);
                config.AppendLine(_tempLogEnable.ToString());
                AssetDatabase.Refresh();

                if (Directory.Exists(Application.streamingAssetsPath) == false)
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                }

                string filepath = Path.Combine(Application.streamingAssetsPath, "ETA_Config.txt");
                File.WriteAllText(filepath, config.ToString());
                AssetDatabase.Refresh();

                _currentGameId = _tempGameId;
                _currentSdkKey = _tempSdkKey;
            }
        }

        private static void DrawRenderPipelineSupportNotice()
        {
            EditorGUILayout.HelpBox(
                "EasterAd SDK supports the Universal Render Pipeline (URP) only.\n" +
                "Built-in Render Pipeline and HDRP are not supported.",
                MessageType.Info
            );
        }

        private static bool IsUniversalRenderPipelineActive()
        {
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;
            return currentPipeline != null && currentPipeline.GetType().Name.Contains("Universal");
        }

        private void ApplyItemId(Item item, string itemId)
        {
            string normalizedItemId = itemId.Trim();
            if (string.IsNullOrEmpty(normalizedItemId))
            {
                Debug.LogWarning("[EasterAd] Item ID cannot be empty.");
                return;
            }

            bool isDuplicate = FindObjectsByType<Item>(FindObjectsSortMode.None)
                .Any(candidate => candidate != item && String.Equals(candidate.adUnitId, normalizedItemId, StringComparison.Ordinal));
            if (isDuplicate)
            {
                Debug.LogWarning($"[EasterAd] Item ID '{normalizedItemId}' is already in use.");
                return;
            }

            if (Application.isPlaying)
            {
                item.InitializeWithAdUnitId(normalizedItemId);
            }
            else
            {
                Undo.RecordObject(item, "Change EasterAd Item ID");
                item.adUnitId = normalizedItemId;
                EditorUtility.SetDirty(item);
                EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
            }

            _pendingItemIds[item.GetInstanceID()] = normalizedItemId;
        }

        private static void FocusItem(Item item)
        {
            Selection.activeGameObject = item.gameObject;
            EditorGUIUtility.PingObject(item.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static void EnsureUrpRendererFeatureInstalled()
        {
            Type featureManagerType = Type.GetType(
                "ETA_Editor.Menu.AdSegmentationFeatureManager, ETA.Editor.URP");

            if (featureManagerType == null)
            {
                return;
            }

            var isInstalledMethod = featureManagerType.GetMethod(
                "IsFeatureInstalled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var installMethod = featureManagerType.GetMethod(
                "InstallFeature",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (isInstalledMethod == null || installMethod == null)
            {
                return;
            }

            bool isInstalled = (bool)isInstalledMethod.Invoke(null, null);
            if (!isInstalled)
            {
                installMethod.Invoke(null, null);
            }
        }
    }
}
