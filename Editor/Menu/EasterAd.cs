using System;
using System.Text;
using ETA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once RedundantUsingDirective
using System.IO;

namespace ETA_Editor.Menu
{
    public class EasterAd : EditorWindow
    {
        private enum RenderPipelineType
        {
            BuiltIn,
            URP,
            HDRP,
            Unknown
        }

        private bool _easterAdEnabled;
        private string _tempGameId = "";
        private string _tempSdkKey = "";
        private bool _tempLogEnable;

        private bool _customInfoEnable;
        private DeviceType _customDeviceType;
        private RuntimePlatform _customPlatform;
        private SystemLanguage _customLanguage;

        private string _currentGameId = "";
        private string _currentSdkKey = "";

        private DeviceType _currentcustomDeviceType;
        private RuntimePlatform _currentcustomPlatform;
        private SystemLanguage _currentcustomLanguage;

        private Vector2 _scrollPosition = Vector2.zero;

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

            _customInfoEnable = Boolean.Parse(config[4]);
            if (_customInfoEnable)
            {
                _customDeviceType = (DeviceType)Enum.Parse(typeof(DeviceType), config[5]);
                _customPlatform = (RuntimePlatform)Enum.Parse(typeof(RuntimePlatform), config[6]);
                _customLanguage = (SystemLanguage)Enum.Parse(typeof(SystemLanguage), config[7]);
            }

            _currentGameId = _tempGameId;
            _currentSdkKey = _tempSdkKey;
            _currentcustomDeviceType = _customDeviceType;
            _currentcustomPlatform = _customPlatform;
            _currentcustomLanguage = _customLanguage;
        }

        private void OnGUI()
        {
            // Render Pipeline detection and Feature setup UI
            DrawRenderPipelineSetupUI();

            EditorGUILayout.Space();

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
                        "✅ Using new unified shader system.\n" +
                        "All render pipelines are supported automatically.",
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

            _customInfoEnable = EditorGUILayout.BeginToggleGroup("Custom Info", _customInfoEnable);

            EditorGUILayout.BeginHorizontal();
            _customDeviceType = (DeviceType)EditorGUILayout.EnumPopup("Device Type", _customDeviceType);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField(_currentcustomDeviceType.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _customPlatform = (RuntimePlatform)EditorGUILayout.EnumPopup("Platform", _customPlatform);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField(_currentcustomPlatform.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _customLanguage = (SystemLanguage)EditorGUILayout.EnumPopup("Language", _customLanguage);
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField(_currentcustomLanguage.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndToggleGroup();

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
            EditorGUILayout.LabelField("Index", GUILayout.Width(50));
            EditorGUILayout.LabelField("Item ID", GUILayout.Width(200));
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
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(item.adUnitId, GUILayout.Width(200));
                EditorGUILayout.LabelField(item.transform.position.ToString(), GUILayout.Width(150));
                //Playmode 일때 상태 표시
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(item.Client.GetStatus().ToString(), GUILayout.Width(100));
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
                    "Drag this prefab into your scene to place an ad.\n" +
                    "The unified shader automatically supports all render pipelines.",
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
                config.AppendLine(_customInfoEnable.ToString());
                if (_customInfoEnable)
                {
                    config.AppendLine(_customDeviceType.ToString());
                    config.AppendLine(_customPlatform.ToString());
                    config.AppendLine(_customLanguage.ToString());
                }
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
                _currentcustomDeviceType = _customDeviceType;
                _currentcustomPlatform = _customPlatform;
                _currentcustomLanguage = _customLanguage;
            }
        }

        /// <summary>
        /// Render Pipeline 감지 및 Feature 설치 UI 표시
        /// </summary>
        private void DrawRenderPipelineSetupUI()
        {
            RenderPipelineType pipelineType = DetectRenderPipeline();

            // URP이고 Feature가 설치되어 있으면 UI 숨김
            if (pipelineType == RenderPipelineType.URP)
            {
                var featureManagerType = System.Type.GetType("ETA_Editor.Menu.AdSegmentationFeatureManager, ETA.Editor.URP");
                if (featureManagerType != null)
                {
                    var isInstalledMethod = featureManagerType.GetMethod("IsFeatureInstalled",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    bool isFeatureInstalled = (bool)isInstalledMethod.Invoke(null, null);

                    if (isFeatureInstalled)
                    {
                        // 설치 완료 → UI 표시 안 함
                        return;
                    }
                }
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Render Pipeline Setup", EditorStyles.boldLabel);

            switch (pipelineType)
            {
                case RenderPipelineType.URP:
                    DrawURPSetupUI();
                    break;

                case RenderPipelineType.HDRP:
                    EditorGUILayout.HelpBox(
                        "🚧 HDRP Support Coming Soon\n" +
                        "AdSegmentation feature for HDRP is under development.",
                        MessageType.Info
                    );
                    break;

                case RenderPipelineType.BuiltIn:
                    EditorGUILayout.HelpBox(
                        "🚧 Built-in Render Pipeline Support Coming Soon\n" +
                        "AdSegmentation feature for Built-in RP is under development.",
                        MessageType.Info
                    );
                    break;

                case RenderPipelineType.Unknown:
                    EditorGUILayout.HelpBox(
                        "⚠ Unknown Render Pipeline\n" +
                        "Could not detect the current render pipeline.",
                        MessageType.Warning
                    );
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// URP 설정 UI 표시
        /// </summary>
        private void DrawURPSetupUI()
        {
            // Reflection으로 AdSegmentationFeatureManager 찾기
            var featureManagerType = System.Type.GetType("ETA_Editor.Menu.AdSegmentationFeatureManager, ETA.Editor.URP");

            if (featureManagerType == null)
            {
                EditorGUILayout.HelpBox(
                    "✅ Universal Render Pipeline (URP) Detected\n\n" +
                    "⚠ URP Editor Assembly Not Found\n" +
                    "The ETA.Editor.URP assembly is not loaded. " +
                    "This is normal if URP package is not installed.",
                    MessageType.Warning
                );
                return;
            }

            // IsFeatureInstalled() 메서드 호출
            var isInstalledMethod = featureManagerType.GetMethod("IsFeatureInstalled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            bool isFeatureInstalled = (bool)isInstalledMethod.Invoke(null, null);

            if (!isFeatureInstalled)
            {
                // Feature가 설치되지 않음 → 자동 설치 시도
                var installMethod = featureManagerType.GetMethod("InstallFeature",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                bool success = (bool)installMethod.Invoke(null, null);

                if (!success)
                {
                    // 자동 설치 실패 → 수동 안내 표시
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.HelpBox(
                        "⚠ AdSegmentation Renderer Feature Auto-Installation Failed\n\n" +
                        "The feature could not be installed automatically.\n" +
                        "Please check the Console for error messages and try manual installation.",
                        MessageType.Error
                    );

                    if (GUILayout.Button("Retry Installation", GUILayout.Height(30)))
                    {
                        bool retrySuccess = (bool)installMethod.Invoke(null, null);
                        if (retrySuccess)
                        {
                            EditorUtility.DisplayDialog(
                                "Installation Complete",
                                "AdSegmentation Renderer Feature has been installed successfully!",
                                "OK"
                            );
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                "Installation Failed",
                                "Failed to install AdSegmentation Renderer Feature.\n" +
                                "Please check the Console for error messages.",
                                "OK"
                            );
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                // 자동 설치 성공 → UI 표시 안 함 (다음 프레임에 사라짐)
            }
            // Feature 설치됨 → UI 표시 안 함
        }

        /// <summary>
        /// 현재 활성화된 Render Pipeline 감지
        /// </summary>
        private RenderPipelineType DetectRenderPipeline()
        {
            var currentPipeline = GraphicsSettings.currentRenderPipeline;

            if (currentPipeline == null)
            {
                return RenderPipelineType.BuiltIn;
            }

            string pipelineTypeName = currentPipeline.GetType().Name;

            if (pipelineTypeName.Contains("Universal"))
            {
                return RenderPipelineType.URP;
            }
            else if (pipelineTypeName.Contains("HDRenderPipeline") || pipelineTypeName.Contains("HDRP"))
            {
                return RenderPipelineType.HDRP;
            }
            else
            {
                return RenderPipelineType.Unknown;
            }
        }
    }
}
