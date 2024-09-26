#nullable enable
using System.Text;
using System.IO;
using ETA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace ETA_Editor.Menu
{
    public class EasterAd : EditorWindow
    { 

        private bool _easterAdEnabled;
        private string _tempGameId = "";
        private string _tempSdkKey = "";
        private bool _tempLogEnable;
    
        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/EasterAd")]
        private static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow.GetWindow(typeof(EasterAd));
        }

        private void OnGUI()
        {
            GUILayout.Label ("Base Settings", EditorStyles.boldLabel);
            _easterAdEnabled = EditorGUILayout.BeginToggleGroup ("Enable EasterAd SDK", _easterAdEnabled);
            _tempGameId = EditorGUILayout.TextField("Game ID", _tempGameId);
            _tempSdkKey = EditorGUILayout.TextField("SDK Key", _tempSdkKey);
            _tempLogEnable = EditorGUILayout.Toggle("Enable Log", _tempLogEnable);
            if (GUILayout.Button("Save"))
            {
                if (_tempGameId == "")
                {
                    try
                    {
                        EtaSdk.DestroyCall();
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
                }
            }
            
            EditorGUILayout.Space();
            
            
            //list
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("List of Items", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index", GUILayout.Width(50));
            EditorGUILayout.LabelField("Item ID", GUILayout.Width(200));
            EditorGUILayout.LabelField("Location",GUILayout.Width(150));
            EditorGUILayout.LabelField("Status",GUILayout.Width(150));
            EditorGUILayout.LabelField("Impression",GUILayout.Width(150));
            EditorGUILayout.LabelField("Remove", GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();
            int index = 1;
            foreach (Item item in FindObjectsOfType<Item>())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(item.adUnitId, GUILayout.Width(200));
                EditorGUILayout.LabelField(item.transform.position.ToString(),GUILayout.Width(150));
                //Playmode 일때 상태 표시
                if(Application.isPlaying)
                {
                    EditorGUILayout.LabelField(item.Client.GetStatus().ToString(),GUILayout.Width(150));
                    item.Client.AllowImpression = item.allowImpression;
                }
                else
                {
                    EditorGUILayout.LabelField("Editor Mode",GUILayout.Width(150));
                }
                item.allowImpression = GUILayout.Toggle(item.allowImpression, "", GUILayout.Width(150));
                
                if (GUILayout.Button("Remove", GUILayout.Width(75)))
                {
                    DestroyImmediate(item.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                index++;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndToggleGroup ();

            // if (!Application.isPlaying)
            // {
            //     EtaSdk.DestroyCall();
            // }
        }
    }
}
