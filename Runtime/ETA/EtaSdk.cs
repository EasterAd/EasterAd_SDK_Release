using System.Collections.Generic;
using UnityEngine;
using ETA_Implementation;
using ETA_Implementation.Impression;

#if UNITY_EDITOR
using UnityEditor;
#else
using System.IO;
using System;
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
        private static readonly EtaSdkClient EtaSdkClient = EtaSdkClient.Instance;
        private static EtaSdk _instance = null!;
        private bool _onceInitialized;

        public Camera targetCamera;

        [SerializeField]
        private string appId = null!;
        [SerializeField]
        private bool logEnable;
        
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
                        _instance = new GameObject("EtaSdk").AddComponent<EtaSdk>();
                    }
                }
                return _instance;
            }
        }

        public void SetCamera(Camera camera)
        {
            CameraManager.SetCamera(camera);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Initialize();
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            EtaSdkClient.Instance.LogEnable = logEnable;
            ImpressionCtrl.ImpressionRoutine();
#if UNITY_EDITOR
            DebugLogger.updateCurrentGameDisplay();
#endif
        }

        private void OnApplicationQuit()
        {
            EtaSdkClient.OnApplicationQuit();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!logEnable) { return; }
            DebugLogger.DrawDebugGizmos();
        }
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        void OnGUI()
        {
            if (!logEnable) { return; }
            DebugLogger.DrawDebugGUI();
        }
#endif

        /// <summary>
        /// <para xml:lang="ko">SDK를 초기화합니다.</para>
        /// <para xml:lang="en">Initializes the SDK.</para>
        /// </summary>
        private void Initialize()
        {
            if (_onceInitialized)
            {
                Debug.Log("EtaSdk is already initialized");
                return;
            }
            EtaSdkClient.Initialize(appId, logEnable);
            if (targetCamera) { SetCamera(targetCamera); }
            EtaSdkClient.AxesNames = GetAxesNames();
            _onceInitialized = true;
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
            return EtaSdkClient.GetItemClient(adUnitId);
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
            return EtaSdkClient.GetItemClientList();
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
            return EtaSdkClient.GetItemClients();
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
