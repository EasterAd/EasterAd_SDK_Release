// ReSharper disable once RedundantNullableDirective
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ETA_Implementation;
using ETA_Implementation.Library;
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
    public class EasterAdSdk : MonoBehaviour
    {
        private EasterAdSdkClient? _easterAdSdkClient;
        private static EasterAdSdk? _instance;

        /// <summary>
        /// <para xml:lang="ko">SDK가 한 번이라도 초기화되었는지 여부를 나타냅니다.</para>
        /// <para xml:lang="en">Indicates whether the SDK has been initialized at least once.</para>
        /// </summary>
        public static bool OnceInitialized { get; private set; }

        private static string _configFilePath = Path.Combine(Application.streamingAssetsPath, "ETA_Config.txt");
        private Camera? _targetCamera;  // Internal camera storage

        /// <summary>
        /// <para xml:lang="ko">SDK가 사용할 대상 카메라입니다. 설정되지 않은 경우 자동으로 <c>Camera.main</c>을 시도합니다.</para>
        /// <para xml:lang="en">The target camera used by the SDK. If not set, it will try to use <c>Camera.main</c> automatically.</para>
        /// </summary>
        public Camera? targetCamera
        {
            get => _targetCamera;
            set
            {
                if (_targetCamera != value)
                {
                    _targetCamera = value;
                    if (value != null)
                    {
                        // Directly synchronize with CameraManager when camera is set
                        InstanceManager.CameraManager.SetMainCamera(new ETA_Dependencies.Unity.GameObject(value.gameObject).Camera);
                    }
                }
            }
        }
        internal string GameId = "";
        internal string sdkKey = "";
        internal bool logEnable;

        internal bool CustomInfoEnabled;
        internal int CustomDeviceType = -1;
        internal string CustomPlatform = "";
        internal string CustomLanguage = "";

        internal static readonly Queue<Item> ItemAwakeQueue = new Queue<Item>();

        /// <summary>
        /// <para xml:lang="ko"><c>EasterAdSdk</c>의 인스턴스를 가져옵니다. 인스턴스가 없으면 새로 생성합니다.</para>
        /// <para xml:lang="en">Gets the instance of <c>EasterAdSdk</c>. If no instance exists, a new one is created.</para>
        /// </summary>
        public static EasterAdSdk Instance {
            get {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<EasterAdSdk>();
                    if (_instance == null)
                    {
                        throw new Exception("EasterAdSdk is not Enabled, Please Remove EasterAd Script or Enable EasterAdSdk at Window -> EasterAd");
                    }
                }

                _instance._easterAdSdkClient ??= EasterAdSdkClient.CreateClient(_instance);

                return _instance;
            }
        }

        /// <summary>
        /// <para xml:lang="ko"><c>EasterAdSdk</c> 인스턴스를 생성합니다. 이미 인스턴스가 존재하면 아무 작업도 수행하지 않습니다.</para>
        /// <para xml:lang="en">Creates an instance of <c>EasterAdSdk</c>. If an instance already exists, no action is taken.</para>
        /// </summary>
        public static void CreateEtaSdk()
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EasterAdSdk>();
                if (_instance == null)
                {
                    _instance = new GameObject("EasterAdSdk").AddComponent<EasterAdSdk>();
                    _instance._easterAdSdkClient ??= EasterAdSdkClient.CreateClient(_instance);
                }
            }
        }


        /// <summary>
        /// <para xml:lang="ko">씬에서 <c>EtaSdk</c> 게임 오브젝트를 즉시 제거합니다.</para>
        /// <para xml:lang="en">Immediately destroys the <c>EtaSdk</c> game object from the scene.</para>
        /// </summary>
        public static void DestroyCall()
        {
            if (_instance == null) _instance = FindFirstObjectByType<EasterAdSdk>();
            if (_instance != null && _instance.gameObject != null)
            {
                DestroyImmediate(_instance.gameObject);
            }
        }

        /// <summary>
        /// <para xml:lang="ko">사용자 카메라를 설정합니다. 설정된 카메라는 광고 노출 계산에 사용됩니다.</para>
        /// <para xml:lang="en">Sets the user camera. The set camera is used for ad impression calculation.</para>
        /// </summary>
        /// <param name="userCamera">
        /// <para xml:lang="ko">SDK가 사용할 Unity 카메라 인스턴스.</para>
        /// <para xml:lang="en">The Unity camera instance to be used by the SDK.</para>
        /// </param>
        [Obsolete("Use targetCamera property instead. This method will be removed in a future version.")]
        public void SetCamera(Camera userCamera)
        {
            targetCamera = userCamera;
        }

        private void RefreshConfig()
        {
            if (!File.Exists(_configFilePath)) { return; }
            string[] config = File.ReadAllLines(_configFilePath);

            GameId = config[1];
            sdkKey = config[2];
            logEnable = bool.Parse(config[3]);
            if (bool.Parse(config[4]))
            {
                CustomInfoEnabled = true;
                CustomDeviceType = DeviceTypeCode((DeviceType)Enum.Parse(typeof(DeviceType), config[5]));
                CustomPlatform = PlatformCode((RuntimePlatform)Enum.Parse(typeof(RuntimePlatform), config[6]));
                CustomLanguage = LanguageCode((SystemLanguage)Enum.Parse(typeof(SystemLanguage), config[7]));
            }
            else
            {
                CustomInfoEnabled = false;
                CustomDeviceType = -1;
                CustomPlatform = "";
                CustomLanguage = "";
            }
        }

        private EasterAdSdk()
        {
            RefreshConfig();
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

        private float _time;

        void Update()
        {
            _easterAdSdkClient!.LogEnable = logEnable;
            _easterAdSdkClient.ImpressionRoutine();

#if UNITY_EDITOR
            InstanceManager.UI.UpdateCurrentGameDisplay();
#endif

            // Auto-detect Camera.main if targetCamera is not set
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _time += Time.deltaTime;
            if (_time > 5)
            {
                _time = 0;
                FunctionScheduler.FailFuncCall();
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
            _easterAdSdkClient?.OnApplicationQuit();
            _easterAdSdkClient = null;
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
            if (CustomInfoEnabled)
                _easterAdSdkClient!.Initialize(GameId, logEnable, sdkKey, CustomDeviceType, CustomPlatform, CustomLanguage);
            else
                _easterAdSdkClient!.Initialize(GameId, logEnable, sdkKey);
            if (_targetCamera != null)
            {
                // Force sync with CameraManager after initialization
                var cam = _targetCamera;
                _targetCamera = null;  // Force change detection
                targetCamera = cam;     // Trigger property setter
            }
            _easterAdSdkClient.AxesNames = GetAxesNames();
            OnceInitialized = true;
        }
        /// <summary>
        /// todo
        /// </summary>
        public void ReInitialize()
        {
            RefreshConfig();

            if (CustomInfoEnabled) _easterAdSdkClient!.ReInitialize(logEnable, CustomDeviceType, CustomPlatform, CustomLanguage);
            else _easterAdSdkClient!.ReInitialize(logEnable);
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
            return _easterAdSdkClient!.GetItemClient(adUnitId);
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
            return _easterAdSdkClient!.GetItemClientList();
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
            return _easterAdSdkClient!.GetItemClient();
        }

        /// <summary>
        /// <para xml:lang="ko">지정된 키로 <c>ItemClient</c>를 등록하거나 교체합니다.</para>
        /// <para xml:lang="en">Adds or replaces an <c>ItemClient</c> with the specified key.</para>
        /// </summary>
        /// <param name="key">
        /// <para xml:lang="ko"><c>ItemClient</c>를 식별할 키.</para>
        /// <para xml:lang="en">The key used to identify the <c>ItemClient</c>.</para>
        /// </param>
        /// <param name="itemClient">
        /// <para xml:lang="ko">등록할 <c>ItemClient</c> 참조.</para>
        /// <para xml:lang="en">Reference to the <c>ItemClient</c> to add.</para>
        /// </param>
        public void AddItemClient(string key, ref ItemClient itemClient)
        {
            _easterAdSdkClient!.AddItemClient(key, ref itemClient);
        }

        /// <summary>
        /// <para xml:lang="ko">지정된 키의 <c>ItemClient</c> 값을 갱신합니다.</para>
        /// <para xml:lang="en">Updates the <c>ItemClient</c> associated with the specified key.</para>
        /// </summary>
        /// <param name="key">
        /// <para xml:lang="ko">갱신할 대상의 키.</para>
        /// <para xml:lang="en">The key of the entry to update.</para>
        /// </param>
        /// <param name="itemClient">
        /// <para xml:lang="ko">새 <c>ItemClient</c> 참조.</para>
        /// <para xml:lang="en">The new <c>ItemClient</c> reference.</para>
        /// </param>
        public void UpdateItemClient(string key, ref ItemClient itemClient)
        {
            _easterAdSdkClient!.UpdateItemClient(key, ref itemClient);
        }

        /// <summary>
        /// <para xml:lang="ko">지정된 키의 <c>ItemClient</c>를 제거합니다.</para>
        /// <para xml:lang="en">Removes the <c>ItemClient</c> associated with the specified key.</para>
        /// </summary>
        /// <param name="key">
        /// <para xml:lang="ko">제거할 대상의 키.</para>
        /// <para xml:lang="en">The key of the entry to remove.</para>
        /// </param>
        public void RemoveItemClient(string key)
        {
            _easterAdSdkClient!.RemoveItemClient(key);
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

        private static string LanguageCode(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Afrikaans => "af",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Basque => "eu",
                SystemLanguage.Belarusian => "be",
                SystemLanguage.Bulgarian => "bg",
                SystemLanguage.Catalan => "ca",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Danish => "da",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.English => "en",
                SystemLanguage.Estonian => "et",
                SystemLanguage.Faroese => "fo",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hebrew => "he",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Icelandic => "is",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Italian => "it",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Latvian => "lv",
                SystemLanguage.Lithuanian => "lt",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Russian => "ru",
                SystemLanguage.SerboCroatian => "sh",
                SystemLanguage.Slovak => "sk",
                SystemLanguage.Slovenian => "sl",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Thai => "th",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Vietnamese => "vi",
                SystemLanguage.ChineseSimplified => "zh",
                SystemLanguage.ChineseTraditional => "zh",
                _ => "en"
            };
        }

        private static string PlatformCode(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.WindowsPlayer => "Windows",
                RuntimePlatform.WindowsEditor => "WindowsEditor",
                RuntimePlatform.LinuxEditor => "LinuxEditor",
                RuntimePlatform.OSXEditor => "OSXEditor",
                RuntimePlatform.Android => "Android",
                RuntimePlatform.IPhonePlayer => "iOS",
                _ => Application.platform.ToString()
            };
        }

        private static int DeviceTypeCode(DeviceType deviceType)
        {
            return deviceType switch
            {
                DeviceType.Desktop => 2,
                DeviceType.Handheld => 1,
                DeviceType.Console => 6,
                _ => 1
            };
        }

    }
}
