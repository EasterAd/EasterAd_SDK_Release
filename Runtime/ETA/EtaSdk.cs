// ReSharper disable once RedundantNullableDirective
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using ETA_Implementation;
using ETA_Implementation.Impression;
using System;
using System.IO;
using InstanceManager = ETA_Dependencies.Unity.InstanceManager;
using GameObject = UnityEngine.GameObject;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace ETA
{
    /// <summary>
    /// <para xml:lang="ko">SDK의 메인 코어입니다. 모든 SDK의 정보는 이 클래스에 의해 제어됩니다.</para>
    /// <para xml:lang="en">Main core of the SDK. All the SDK information will be controlled by this class.</para>
    /// </summary>
    /// <remarks>
    /// <para xml:lang="ko">EasterAd SDK의 정상적인 작동을 위해서는 Unity Scene 내에 <c>EtaSdk</c> 스크립트를 컴포넌트로 가지는 오브젝트가 단일하게 존재해야 합니다.</para>
    /// <para xml:lang="en">For the proper operation of the EasterAd SDK, there must be a single object in the Unity Scene that has the <c>EtaSdk</c> script as a component.</para>
    /// </remarks>
    public class EtaSdk : MonoBehaviour
    {
        private static readonly ImpressionCtrl ImpressionCtrl = new ImpressionCtrl();
        private EtaSdkClient? _etaSdkClient;
        private static EtaSdk? _instance;
        internal static bool OnceInitialized { get; private set; }

        public Camera? targetCamera;
        internal string gameId = "";
        internal string sdkKey = "";
        internal bool logEnable;
        
        internal static readonly Queue<Item> ItemAwakeQueue = new Queue<Item>();
        
        /// <summary>
        /// <para xml:lang="ko"><c>EtaSdk</c>의 인스턴스를 가져옵니다. 인스턴스가 없으면 새로 생성합니다.</para>
        /// <para xml:lang="en">Gets the instance of <c>EtaSdk</c>. If no instance exists, a new one is created.</para>
        /// </summary>
        public static EtaSdk Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EtaSdk>();
                    if (_instance == null)
                    {
                        throw new Exception("EtaSdk is not Enabled, Please Remove EasterAd Script or Enable EtaSdk at Window -> EasterAd");
                    }
                }
                
                _instance._etaSdkClient ??= EtaSdkClient.CreateClient(_instance);
                
                return _instance;
            }
        }
        
        public static void CreateEtaSdk()
        {
            if(_instance == null)
            {
                _instance = FindObjectOfType<EtaSdk>();
                if (_instance == null)
                {
                    _instance = new GameObject("EtaSdk").AddComponent<EtaSdk>();
                    _instance._etaSdkClient ??= EtaSdkClient.CreateClient(_instance);
                }
            }
        }

        
        public static void DestroyCall()
        {
            if(_instance == null) _instance = FindObjectOfType<EtaSdk>();
            if (_instance != null && _instance.gameObject != null)
            {
                DestroyImmediate(_instance.gameObject);
            }
        }

        public void SetCamera(Camera userCamera)
        {
            InstanceManager.CameraManager.SetMainCamera(new ETA_Dependencies.Unity.GameObject(userCamera.gameObject).Camera);
        }

        private EtaSdk()
        {
            string filename = "ETA_Config.txt";
            string filepath = Path.Combine(Application.streamingAssetsPath, filename);
            if (File.Exists(filepath) == false) { return; }
            
            string[] config = File.ReadAllLines(filepath);
            gameId = config[1];
            sdkKey = config[2];
            logEnable = bool.Parse(config[3]);
        }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Initialize();
            DontDestroyOnLoad(gameObject);

            while (ItemAwakeQueue.Count > 0)
            {
                ItemAwakeQueue.Dequeue().Awake();
            }
        }

        void Update()
        {
            _etaSdkClient!.LogEnable = logEnable;
            ImpressionCtrl.ImpressionRoutine();
#if UNITY_EDITOR
            InstanceManager.UI.UpdateCurrentGameDisplay();
#endif

            if (targetCamera != null) return;

            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                SetCamera(targetCamera);
            }
        }
        
        private void OnDestroy()
        {
            OnApplicationQuit();
        }

        private void OnApplicationQuit()
        {
            // make null all the static variables
            
            OnceInitialized = false;
            _etaSdkClient?.OnApplicationQuit();
            _etaSdkClient = null;
            _instance = null;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!logEnable) { return; }
            InstanceManager.UI.DrawDebugGizmos();
        }

        void OnGUI()
        {
            if (!logEnable) { return; }
            InstanceManager.UI.DrawDebugGUI();
        }
#endif

        /// <summary>
        /// <para xml:lang="ko">SDK를 초기화합니다.</para>
        /// <para xml:lang="en">Initializes the SDK.</para>
        /// </summary>
        private void Initialize()
        {
            if (OnceInitialized)
            {
                Debug.Log("EtaSdk is already initialized");
                return;
            }
            _etaSdkClient!.Initialize(gameId, logEnable, sdkKey);
            if (targetCamera!=null) { SetCamera(targetCamera); }
            _etaSdkClient.AxesNames = GetAxesNames();
            OnceInitialized = true;
        }

        /// <summary>
        /// <para xml:lang="ko"><c>ItemClient</c>를 가져옵니다.</para>
        /// <para xml:lang="en">Gets an <c>ItemClient</c>.</para>
        /// </summary>
        /// <param name="adUnitId">
        /// <para xml:lang="ko">가져올 <c>ItemClient</c>의 광고 단위 ID입니다.</para>
        /// <para xml:lang="en">The ad unit ID of the <c>ItemClient</c> to get.</para>
        /// </param>
        /// <returns>
        /// <para xml:lang="ko">가져온 <c>ItemClient</c>입니다.</para>
        /// <para xml:lang="en">The retrieved <c>ItemClient</c>.</para>
        /// </returns>
#nullable enable
        public ItemClient? GetItemClient(string adUnitId)
        {
            return _etaSdkClient!.GetItemClient(adUnitId);
        }
#nullable disable


        /// <summary>
        /// <para xml:lang="ko"><c>ItemClient</c> 목록을 가져옵니다.</para>
        /// <para xml:lang="en">Gets the list of <c>ItemClient</c>s.</para>
        /// </summary>
        /// <returns>
        /// <para xml:lang="ko"><c>ItemClient</c> 목록입니다.</para>
        /// <para xml:lang="en">The list of <c>ItemClient</c>s.</para>
        /// </returns>
        public List<string> GetItemClientList()
        {
            return _etaSdkClient!.GetItemClientList();
        }

        /// <summary>
        /// <para xml:lang="ko"><c>ItemClient</c> dictionary를 가져옵니다.</para>
        /// <para xml:lang="en">Gets the dictionary of <c>ItemClient</c>s.</para>
        /// </summary>
        /// <returns>
        /// <para xml:lang="ko"><c>ItemClient</c> dictionary입니다.</para>
        /// <para xml:lang="en">The dictionary of <c>ItemClient</c>s.</para>
        /// </returns>
        public Dictionary<string, ItemClient> GetItemClient()
        {
            return _etaSdkClient!.GetItemClient();
        }
        
        public void AddItemClient(string key, ref ItemClient itemClient)
        {
            _etaSdkClient!.AddItemClient(key, ref itemClient);
        }
        
        public void UpdateItemClient(string key, ref ItemClient itemClient)
        {
            _etaSdkClient!.UpdateItemClient(key, ref itemClient);
        }
        
        public void RemoveItemClient(string key)
        {
            _etaSdkClient!.RemoveItemClient(key);
        }

        private List<string> GetAxesNames()
        {
            List<string> axesNames = new List<string>();
#if UNITY_EDITOR
            UnityEngine.Object inputManager = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/InputManager.asset");
            if (inputManager == null) { return axesNames; }
            
            SerializedObject obj = new SerializedObject(inputManager);
            SerializedProperty axisArray = obj.FindProperty("m_Axes");

            for (int i = 0; i < axisArray.arraySize; i++)
            {
                SerializedProperty axis = axisArray.GetArrayElementAtIndex(i);
                string name = axis.FindPropertyRelative("m_Name").stringValue;
                if(string.IsNullOrEmpty(name) == false) { axesNames.Add(name); }
            }
            return axesNames;
#else
            string filepath = Path.Combine(Application.streamingAssetsPath, "ETA_Axes.txt");
            if (File.Exists(filepath) == false) { return axesNames; }
            string inputAxesText = File.ReadAllText(filepath);

            string[] axesNamesArr = inputAxesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            axesNames.AddRange(axesNamesArr);
            return axesNames;
#endif
        }
    }
}
