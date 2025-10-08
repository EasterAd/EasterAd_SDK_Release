using System;
using System.Text;
using ETA;
using UnityEditor;
using UnityEngine;

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

        private bool _customInfoEnable;
        private DeviceType _customDeviceType;
        private RuntimePlatform _customPlatform;
        private SystemLanguage _customLanguage;

        private string _currentGameId = "";
        private string _currentSdkKey = "";

        private DeviceType _currentcustomDeviceType;
        private RuntimePlatform _currentcustomPlatform;
        private SystemLanguage _currentcustomLanguage;
        
        private Vector2 _scrollPosition=Vector2.zero;
    
        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/EasterAd")]
        private static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EasterAd window = (EasterAd)GetWindow(typeof(EasterAd));
            window.titleContent = new GUIContent("EasterAd");
            window.minSize = new Vector2(820,350);
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
            GUILayout.Label ("Base Settings", EditorStyles.boldLabel);
            _easterAdEnabled = EditorGUILayout.BeginToggleGroup ("Enable EasterAd SDK", _easterAdEnabled);

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

            _customInfoEnable = EditorGUILayout.BeginToggleGroup ("Custom Info", _customInfoEnable);

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

            EditorGUILayout.EndToggleGroup ();

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
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index", GUILayout.Width(50));
            EditorGUILayout.LabelField("Item ID", GUILayout.Width(200));
            EditorGUILayout.LabelField("Location",GUILayout.Width(150));
            EditorGUILayout.LabelField("Status",GUILayout.Width(100));
            EditorGUILayout.LabelField("Impression",GUILayout.Width(100));
            EditorGUILayout.LabelField("Load On Start",GUILayout.Width(100));
            EditorGUILayout.LabelField("Remove", GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(820), GUILayout.Height(200));
            int index = 1;
            foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.InstanceID))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(item.adUnitId, GUILayout.Width(200));
                EditorGUILayout.LabelField(item.transform.position.ToString(),GUILayout.Width(150));
                //Playmode 일때 상태 표시
                if(Application.isPlaying)
                {
                    EditorGUILayout.LabelField(item.Client.GetStatus().ToString(),GUILayout.Width(100));
                    item.Client.AllowImpression = item.allowImpression;
                }
                else
                {
                    EditorGUILayout.LabelField("Editor Mode",GUILayout.Width(100));
                }
                item.allowImpression = GUILayout.Toggle(item.allowImpression, "", GUILayout.Width(100));
                item.loadOnStart = GUILayout.Toggle(item.loadOnStart, "", GUILayout.Width(100));
                
                if (GUILayout.Button("Remove", GUILayout.Width(75)))
                {
                    DestroyImmediate(item.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                index++;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndToggleGroup ();

            GUILayout.Label("Import Ad Assets", EditorStyles.boldLabel);
            if (GUILayout.Button("Import Built-in Render Pipeline Assets"))
            {
                ImportPackage("Packages/com.easterad.easterad/Packages/EasterAd_BuiltIn.unitypackage");
            }
            if (GUILayout.Button("Import Universal Render Pipeline (URP) Assets"))
            {
                ImportPackage("Packages/com.easterad.easterad/Packages/EasterAd_URP.unitypackage");
            }
            if (GUILayout.Button("Import High Definition Render Pipeline (HDRP) Assets"))
            {
                ImportPackage("Packages/com.easterad.easterad/Packages/EasterAd_HDRP.unitypackage");
            }

            // if (!Application.isPlaying)
            // {
            //     EtaSdk.DestroyCall();
            // }
        }

        private void ImportPackage(string packageName)
        {
            if (!string.IsNullOrEmpty(packageName))
            {
                AssetDatabase.ImportPackage(packageName, true);
            }
            else
            {
                Debug.Log(packageName + " is not found.");
            }
        }

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
    }
}
