// ReSharper disable once RedundantNullableDirective
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EasterAd_Implementation;
using EasterAd_Implementation.Diagnostics;
using EasterAd_Implementation.Library;
using EasterAd_Implementation.Privacy;
using InstanceManager = EasterAd_Dependencies.Unity.InstanceManager;
using GameObject = UnityEngine.GameObject;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko">SDK의 메인 코어입니다. 모든 SDK의 정보는 이 클래스에 의해 제어됩니다.</para>
    /// <para xml:lang="en">Main core of the SDK. All the SDK information will be controlled by this class.</para>
    /// </summary>
    /// <remarks>
    /// <para xml:lang="ko"><c>Window &gt; EasterAd</c>에서 SDK가 활성화되어 있으면 런타임에 자동으로 생성됩니다. 씬에 직접 배치하는 경우에는 단일 인스턴스만 유지하세요.</para>
    /// <para xml:lang="en">When the SDK is enabled in <c>Window &gt; EasterAd</c>, it is created automatically at runtime. If placed directly in a scene, keep only one instance.</para>
    /// </remarks>
    public class EasterAdSdk : MonoBehaviour
    {
        private EasterAdSdkClient? _easterAdSdkClient;
        private static EasterAdSdk? _instance;
        private bool _isDuplicateInstance;
        private bool _hasShutDown;

        /// <summary>
        /// <para xml:lang="ko">SDK가 한 번이라도 초기화되었는지 여부를 나타냅니다.</para>
        /// <para xml:lang="en">Indicates whether the SDK has been initialized at least once.</para>
        /// </summary>
        public static bool OnceInitialized { get; private set; }

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
                        InstanceManager.CameraManager.SetMainCamera(new EasterAd_Dependencies.Unity.GameObject(value.gameObject).Camera);
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

        private static readonly List<Item> PendingItems = new List<Item>();

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

        internal static bool TryGetActiveInstance(out EasterAdSdk sdk)
        {
            sdk = _instance!;
            return _instance != null && !_instance._hasShutDown && !_instance._isDuplicateInstance;
        }

        internal static void RegisterPendingItem(Item item)
        {
            if (item == null || PendingItems.Contains(item)) { return; }
            PendingItems.Add(item);
        }

        internal static void UnregisterPendingItem(Item item)
        {
            PendingItems.Remove(item);
        }

        private static void ProcessPendingItems()
        {
            if (!OnceInitialized || PendingItems.Count == 0) { return; }

            Item[] items = PendingItems.ToArray();
            PendingItems.Clear();
            foreach (Item item in items)
            {
                if (item == null) { continue; }
                item.InitializeAfterSdkReady();
            }
        }

        /// <summary>
        /// <para xml:lang="ko"><c>EasterAdSdk</c> 인스턴스를 생성합니다. 이미 인스턴스가 존재하면 아무 작업도 수행하지 않습니다.</para>
        /// <para xml:lang="en">Creates an instance of <c>EasterAdSdk</c>. If an instance already exists, no action is taken.</para>
        /// </summary>
        public static void CreateEasterAdSdk()
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EasterAdSdk>();
                if (_instance == null)
                {
                    GameObject runtimeObject = new GameObject("EasterAdSdk");
                    runtimeObject.hideFlags = HideFlags.HideInHierarchy;
                    _instance = runtimeObject.AddComponent<EasterAdSdk>();
                    _instance._easterAdSdkClient ??= EasterAdSdkClient.CreateClient(_instance);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapFromConfig()
        {
            if (!ShouldBootstrapFromConfig()) { return; }
            if (_instance != null || FindFirstObjectByType<EasterAdSdk>() != null) { return; }

            CreateEasterAdSdk();
        }

        private static bool ShouldBootstrapFromConfig()
        {
            if (!EasterAdConfigFiles.TryReadConfig(out string[] config, out _)) { return false; }
            return config.Length > 0 &&
                   bool.TryParse(config[0], out bool easterAdEnabled) &&
                   easterAdEnabled;
        }


        /// <summary>
        /// <para xml:lang="ko">씬에서 <c>EasterAdSdk</c> 게임 오브젝트를 즉시 제거합니다.</para>
        /// <para xml:lang="en">Immediately destroys the <c>EasterAdSdk</c> game object from the scene.</para>
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
            if (!EasterAdConfigFiles.TryReadConfig(out string[] config, out string configFilePath)) { return; }
            if (configFilePath == EasterAdConfigFiles.LegacyConfigPath)
            {
                Debug.LogWarning("EasterAd is using legacy ETA_Config.txt. Please migrate it to EasterAd_Config.txt.");
            }

            if (config.Length < 5)
            {
                Debug.LogWarning("EasterAd config is incomplete. SDK will use default empty settings.");
                return;
            }

            GameId = config[1];
            sdkKey = config[2];
            logEnable = bool.TryParse(config[3], out bool parsedLogEnable) && parsedLogEnable;
            if (bool.TryParse(config[4], out bool parsedCustomInfoEnabled) && parsedCustomInfoEnabled)
            {
                if (config.Length < 8)
                {
                    Debug.LogWarning("EasterAd custom config is incomplete. Custom device info is disabled.");
                    CustomInfoEnabled = false;
                    return;
                }

                CustomInfoEnabled = true;
                if (!Enum.TryParse(config[5], out DeviceType deviceType) ||
                    !Enum.TryParse(config[6], out RuntimePlatform platform) ||
                    !Enum.TryParse(config[7], out SystemLanguage language))
                {
                    Debug.LogWarning("EasterAd custom config has invalid enum values. Custom device info is disabled.");
                    CustomInfoEnabled = false;
                    return;
                }

                CustomDeviceType = DeviceTypeCode(deviceType);
                CustomPlatform = PlatformCode(platform);
                CustomLanguage = LanguageCode(language);
            }
            else
            {
                CustomInfoEnabled = false;
                CustomDeviceType = -1;
                CustomPlatform = "";
                CustomLanguage = "";
            }
        }

        /// <summary>
        /// <para xml:lang="ko">EasterAdSdk 인스턴스를 초기화하고 설정 파일을 읽습니다.</para>
        /// <para xml:lang="en">Initializes an EasterAdSdk instance and reads the configuration file.</para>
        /// </summary>
        protected EasterAdSdk()
        {
            RefreshConfig();
        }

        private void Awake()
        {
            EasterAdSdk? existingInstance = _instance ?? FindFirstObjectByType<EasterAdSdk>();
            if (existingInstance != null && existingInstance != this)
            {
                _isDuplicateInstance = true;
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _easterAdSdkClient ??= EasterAdSdkClient.CreateClient(this);

            Initialize();
            DontDestroyOnLoad(gameObject);

            ProcessPendingItems();
        }

        private float _time;

        void Update()
        {
            if (_isDuplicateInstance || _easterAdSdkClient == null) { return; }

            _easterAdSdkClient.LogEnable = logEnable;
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
            ShutdownSdk();
        }

        private void OnApplicationQuit()
        {
            ShutdownSdk();
        }

        private void ShutdownSdk()
        {
            if (_hasShutDown || _isDuplicateInstance || _instance != this)
            {
                return;
            }

            _hasShutDown = true;
            // make null all the static variables
            OnceInitialized = false;
            PendingItems.Clear();
            FunctionScheduler.ClearScheduledCalls();
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
                Debug.Log("EasterAdSdk is already initialized");
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
        /// <para xml:lang="ko">아동 대상 앱, 맞춤형 광고, 광고 요청 허용 여부 등 개인정보 신호를 설정합니다.</para>
        /// <para xml:lang="en">Configures privacy signals such as child-directed treatment, personalized ads, and ad request permission.</para>
        /// </summary>
        public void ConfigurePrivacy(EasterAdPrivacyOptions options)
        {
            _easterAdSdkClient!.ConfigurePrivacy(options);
        }

        /// <summary>
        /// <para xml:lang="ko">아동 대상 앱 여부를 간단히 설정합니다. 3세 이용가 또는 가족 대상 앱은 true 사용을 권장합니다.</para>
        /// <para xml:lang="en">Convenience method for child-directed treatment. Use true for family or young-audience apps.</para>
        /// </summary>
        public void SetChildDirected(bool childDirected)
        {
            _easterAdSdkClient!.ConfigurePrivacy(new EasterAdPrivacyOptions
            {
                ChildDirected = childDirected,
                PersonalizedAdsAllowed = !childDirected,
                AdRequestsAllowed = true
            });
        }

        /// <summary>
        /// <para xml:lang="ko">동의 상태와 맞춤형 광고 허용 여부를 설정합니다.</para>
        /// <para xml:lang="en">Configures consent state and whether personalized ads are allowed.</para>
        /// </summary>
        public void SetPrivacyConsent(bool adRequestsAllowed, bool personalizedAdsAllowed, string consentString = "", string privacyRegion = "", bool childDirected = false)
        {
            _easterAdSdkClient!.ConfigurePrivacy(new EasterAdPrivacyOptions
            {
                ChildDirected = childDirected,
                PersonalizedAdsAllowed = personalizedAdsAllowed,
                AdRequestsAllowed = adRequestsAllowed,
                ConsentString = consentString ?? "",
                PrivacyRegion = privacyRegion ?? ""
            });
        }

        /// <summary>
        /// <para xml:lang="ko">광고 카테고리 차단/허용 목록을 설정합니다. 서버 정책과 함께 적용됩니다.</para>
        /// <para xml:lang="en">Sets blocked and allowed ad categories. These are applied together with server-side policy.</para>
        /// </summary>
        public void SetAdCategoryPolicy(IEnumerable<string>? blockedCategories = null, IEnumerable<string>? allowedCategories = null)
        {
            _easterAdSdkClient!.SetAdCategoryPolicy(blockedCategories, allowedCategories);
        }

        /// <summary>
        /// <para xml:lang="ko">오프라인 모드를 설정합니다. 활성화하면 광고 네트워크 요청을 수행하지 않습니다.</para>
        /// <para xml:lang="en">Sets offline mode. When enabled, the SDK skips network ad requests.</para>
        /// </summary>
        public void SetOfflineMode(bool enabled)
        {
            _easterAdSdkClient!.SetOfflineMode(enabled);
        }

        /// <summary>
        /// <para xml:lang="ko">로컬 킬스위치입니다. false로 설정하면 광고 네트워크 요청을 수행하지 않습니다.</para>
        /// <para xml:lang="en">Local kill switch. When set to false, the SDK skips ad network requests.</para>
        /// </summary>
        public void SetAdRequestsEnabled(bool enabled)
        {
            _easterAdSdkClient!.SetAdRequestsEnabled(enabled);
        }

        /// <summary>
        /// <para xml:lang="ko">광고 요청 결과가 관측될 때 호출됩니다.</para>
        /// <para xml:lang="en">Raised when an ad request outcome is observed.</para>
        /// </summary>
        public event Action<AdRequestDiagnostics> AdRequestObserved
        {
            add => _easterAdSdkClient!.AdRequestObserved += value;
            remove => _easterAdSdkClient!.AdRequestObserved -= value;
        }

        /// <summary>
        /// <para xml:lang="ko">광고 없는 스크린샷/공유 이미지 캡처를 위한 범위를 시작합니다.</para>
        /// <para xml:lang="en">Begins a scope for screenshot or share-image capture without ad rendering.</para>
        /// </summary>
        public EasterAdCaptureScope BeginAdHiddenCapture(bool onlyItemsOptedIn = true)
        {
            return new EasterAdCaptureScope(onlyItemsOptedIn);
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
        /// <para xml:lang="ko">등록된 <c>ItemClient</c>의 읽기 전용 snapshot을 가져옵니다.</para>
        /// <para xml:lang="en">Gets a read-only snapshot of registered <c>ItemClient</c>s.</para>
        /// </summary>
        /// <returns>
        /// <para xml:lang="ko">광고 단위 ID와 <c>ItemClient</c> snapshot입니다.</para>
        /// <para xml:lang="en">A snapshot of ad unit IDs and <c>ItemClient</c>s.</para>
        /// </returns>
        public IReadOnlyDictionary<string, ItemClient> GetItemClient()
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
            string filepath = EasterAdConfigFiles.ResolveAxesPath();
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
