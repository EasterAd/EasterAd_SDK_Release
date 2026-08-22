#nullable enable annotations
using System;
using System.Collections.Generic;
using System.IO;
using EasterAd_Dependencies.Unity;
using UnityEngine;
using EasterAd_Implementation;
using GameObject = UnityEngine.GameObject;
#pragma warning disable CS1591 // 공개된 형식 또는 멤버에 대한 XML 주석이 없습니다.

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko"><c>Item</c> 클래스를 통해 각 광고 오브젝트들을 제어할 수 있습니다.</para>
    /// <para xml:lang="en">You can control each ad object through the <c>Item</c> class.</para>
    /// </summary>
    public abstract class Item : MonoBehaviour
    {
        protected ItemClient _client = null!;
        public ItemClient Client => _client;

        private enum ItemInitializationState
        {
            Uninitialized,
            WaitingForSdk,
            Initialized,
            Rejected
        }

        public string adUnitId = null!; //must be set in Unity Editor

        /// <summary>
        /// <para xml:lang="ko">Awake에서 자동으로 초기화할지 여부를 결정합니다. false로 설정하면 수동으로 초기화해야 합니다.</para>
        /// <para xml:lang="en">Determines whether to automatically initialize in Awake. If set to false, manual initialization is required.</para>
        /// </summary>
        public bool autoInitialize = true;

        public bool allowImpression = true;
        public bool loadOnStart = true;
        public bool interactable;
        public bool enableRefresh = true;
        public float refreshTime = 10.0f;
        public bool hideDuringCapture = true;

        private bool _startRefresh;
        private float _refreshWaited;
        private bool _isInitialized = false;
        private bool _loadAfterInitialize;
        private bool _inGameRenderingSuppressed;
        private ItemInitializationState _initializationState = ItemInitializationState.Uninitialized;


        internal void Awake()
        {
            if (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.WebGLPlayer)
            {
                SuppressInGameRendering();
            }

            if (EasterAdSdk.OnceInitialized == false)
            {
                _initializationState = ItemInitializationState.WaitingForSdk;
                EasterAdSdk.RegisterPendingItem(this);
                EnableSDK();
                return;
            }

            TryAutoInitialize("Awake");
        }

        internal void InitializeAfterSdkReady()
        {
            if (_isInitialized || _initializationState == ItemInitializationState.Rejected) { return; }
            TryAutoInitialize("SDK initialization");
        }

        private void TryAutoInitialize(string source)
        {
            if (!autoInitialize)
            {
                _initializationState = ItemInitializationState.Uninitialized;
                return;
            }

            if (string.IsNullOrEmpty(adUnitId))
            {
                InstanceManager.DebugLogger.LogWarning("adUnitId is empty. Cannot auto-initialize Item from " + source + ".");
                _initializationState = ItemInitializationState.Uninitialized;
                return;
            }

            InitializeItemClient();
        }

        /// <summary>
        /// <para xml:lang="ko">ItemClient를 초기화합니다.</para>
        /// <para xml:lang="en">Initializes the ItemClient.</para>
        /// </summary>
        private bool InitializeItemClient()
        {
            if (!EasterAdSdk.OnceInitialized)
            {
                InstanceManager.DebugLogger.LogWarning("EasterAdSdk is not initialized yet. Postponing Item initialization.");
                _initializationState = ItemInitializationState.WaitingForSdk;
                EasterAdSdk.RegisterPendingItem(this);
                EnableSDK();
                return false;
            }

            if (string.IsNullOrEmpty(adUnitId))
            {
                InstanceManager.DebugLogger.LogWarning("adUnitId is empty. Cannot initialize Item.");
                _initializationState = ItemInitializationState.Uninitialized;
                return false;
            }

            ItemClient? existingClient = EasterAdSdk.Instance.GetItemClient(adUnitId);
            if (existingClient != null && !ReferenceEquals(existingClient, _client))
            {
                InstanceManager.DebugLogger.LogWarning("Item already exists for adUnitId. Rejecting duplicate Item initialization: " + adUnitId);
                _client = null!;
                _isInitialized = false;
                _initializationState = ItemInitializationState.Rejected;
                return false;
            }

            _client = GetClient(gameObject, adUnitId);
            _client.AllowImpression = allowImpression;
            _client.Interactable = interactable;
            EasterAdSdk.Instance.AddItemClient(adUnitId, ref _client);
            _isInitialized = true;
            _initializationState = ItemInitializationState.Initialized;
            InstanceManager.DebugLogger.Log("Item added: " + adUnitId);

            if (ShouldSuppressInGameRendering())
            {
                SuppressInGameRendering();
            }

            if (_loadAfterInitialize && loadOnStart)
            {
                _loadAfterInitialize = false;
                Load();
            }

            return true;
        }

        private void Start()
        {
            if (!loadOnStart) { return; }

            if (IsInitialized)
            {
                Load();
            }
            else
            {
                _loadAfterInitialize = true;
            }
        }

        private void Update()
        {
            // client가 없으면 Update 스킵
            if (_client == null) return;

            if (ShouldSuppressInGameRendering())
            {
                SuppressInGameRendering();
                return;
            }

            _client.AllowImpression = allowImpression;
            _client.Interactable = interactable;

            if (!enableRefresh) { return; }

            if (_startRefresh)
            {
                _refreshWaited += Time.unscaledDeltaTime;
                if (_refreshWaited >= refreshTime)
                {
                    _startRefresh = false;
                    _refreshWaited = 0.0f;
                    Load();
                }

                if (Client.GetStatus() != ItemStatus.Impressed && Client.GetStatus() != ItemStatus.Interacted)
                {
                    _startRefresh = false;
                    _refreshWaited = 0.0f;
                }
            }
            else if (Client.GetStatus() == ItemStatus.Impressed || Client.GetStatus() == ItemStatus.Interacted)
            {
                _startRefresh = true;
                _refreshWaited = 0.0f;
            }
            else if (Client.GetStatus() == ItemStatus.Impressing)
            {
                _startRefresh = false;
                _refreshWaited = 0.0f;
            }
        }

        private bool ShouldSuppressInGameRendering()
        {
            if (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return true;
            }

            return EasterAdSdk.TryGetActiveInstance(out EasterAdSdk sdk) &&
                   (sdk.UsesExternalMobileAds || !sdk.SupportsAdsOnCurrentPlatform);
        }

        private void SuppressInGameRendering()
        {
            if (_inGameRenderingSuppressed) { return; }

            SetRenderingVisible(false);
            _inGameRenderingSuppressed = true;
        }

        private void OnDestroy()
        {
            EasterAdSdk.UnregisterPendingItem(this);
            try
            {
                if (_isInitialized &&
                    _client != null &&
                    EasterAdSdk.TryGetActiveInstance(out EasterAdSdk sdk))
                {
                    sdk.DestroyItemClient(_client);
                }
            }
            catch
            {
                // ignored
            }
        }


        /// <summary>
        /// <para xml:lang="ko">런타임에 adUnitId를 설정하고 Item을 수동으로 초기화합니다.</para>
        /// <para xml:lang="en">Sets the adUnitId at runtime and manually initializes the Item.</para>
        /// </summary>
        /// <param name="newAdUnitId">
        /// <para xml:lang="ko">설정할 광고 단위 ID입니다.</para>
        /// <para xml:lang="en">The ad unit ID to set.</para>
        /// </param>
        public void InitializeWithAdUnitId(string newAdUnitId)
        {
            if (_isInitialized && _client != null)
            {
                // 기존 client 정리
                try
                {
                    EasterAdSdk.Instance.DestroyItemClient(_client);
                    InstanceManager.DebugLogger.Log($"Removed existing Item client: {adUnitId}");
                }
                catch (Exception ex)
                {
                    InstanceManager.DebugLogger.LogWarning($"Failed to remove existing client: {ex.Message}");
                }
            }

            adUnitId = newAdUnitId;
            _isInitialized = false;
            _initializationState = ItemInitializationState.Uninitialized;
            InitializeItemClient();
        }

        /// <summary>
        /// <para xml:lang="ko">현재 설정된 adUnitId로 Item을 수동으로 초기화합니다.</para>
        /// <para xml:lang="en">Manually initializes the Item with the currently set adUnitId.</para>
        /// </summary>
        public void Initialize()
        {
            if (string.IsNullOrEmpty(adUnitId))
            {
                InstanceManager.DebugLogger.LogWarning("adUnitId is not set. Call InitializeWithAdUnitId() or set adUnitId first.");
                return;
            }

            if (_isInitialized && _client != null)
            {
                InstanceManager.DebugLogger.LogWarning($"Item {adUnitId} is already initialized.");
                return;
            }

            InitializeItemClient();
        }

        /// <summary>
        /// <para xml:lang="ko">Item이 초기화되었는지 확인합니다.</para>
        /// <para xml:lang="en">Checks if the Item is initialized.</para>
        /// </summary>
        public bool IsInitialized => _isInitialized && _client != null;

        protected bool TryGetInitializedClient(string operationName, out ItemClient itemClient)
        {
            if (IsInitialized)
            {
                itemClient = _client;
                return true;
            }

            string state = _initializationState.ToString();
            InstanceManager.DebugLogger.LogWarning($"Cannot {operationName} before Item initialization. AdUnitId: {adUnitId}, State: {state}");
            itemClient = null!;
            return false;
        }

        /// <summary>
        /// <para xml:lang="ko">스크린샷/공유 캡처 등을 위해 광고 렌더링을 표시하거나 숨깁니다.</para>
        /// <para xml:lang="en">Shows or hides ad rendering for screenshot or sharing capture flows.</para>
        /// </summary>
        public void SetRenderingVisible(bool visible)
        {
            if (visible && ShouldSuppressInGameRendering())
            {
                InstanceManager.DebugLogger.LogWarning(
                    "In-game ad rendering cannot be enabled when presentation is externally owned or the runtime platform is unsupported.");
                return;
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }

            foreach (Behaviour behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                string typeName = behaviour.GetType().FullName ?? "";
                if (typeName == "UnityEngine.UI.RawImage" || typeName == "UnityEngine.UI.Image")
                {
                    behaviour.enabled = visible;
                }
            }
        }

        internal ItemRenderingState CaptureRenderingState()
        {
            return new ItemRenderingState(this);
        }

        /// <summary>
        /// <para xml:lang="ko">Android/iOS에서는 등록된 외부 provider에 load-and-show를 위임하고, 지원되는 비-WebGL 비모바일 플랫폼에서는 EasterAd 광고를 게임 안에 로드합니다. Unity WebGL에서는 fail-closed로 비활성화합니다.</para>
        /// <para xml:lang="en">Delegates load-and-show to the registered external provider on Android/iOS, loads the EasterAd creative in-game on supported non-WebGL non-mobile platforms, and fails closed on Unity WebGL.</para>
        /// </summary>
        public abstract void Load();

        // /// <summary>
        // /// <para xml:lang="ko">텍스처를 일반 텍스처로 변경합니다.</para>
        // /// <para xml:lang="en">Change texture to general texture.</para>
        // /// </summary>
        // /// <remarks>
        // /// <para xml:lang="ko">메인 스레드 외부에서 이 메서드를 호출하면 오류가 발생합니다.</para>
        // /// <para xml:lang="en">Call this method outside of the main thread will cause an error.</para>
        // /// </remarks>
        // public void UnShow(object sender, EventArgs e)
        // {
        //     // Client.UnShow();
        // }
        //
        // /// <summary>
        // /// <para xml:lang="ko"><c>GameObject</c>의 렌더링을 중지합니다.</para>
        // /// <para xml:lang="en">Stop rendering the <c>GameObject</c>.</para>
        // /// </summary>
        // /// <remarks>
        // /// <para xml:lang="ko">메인 스레드 외부에서 이 메서드를 호출하면 오류가 발생합니다.</para>
        // /// <para xml:lang="en">Call this method outside of the main thread will cause an error.</para>
        // /// </remarks>
        // public void Hide(object sender, EventArgs e)
        // {
        //     Client.Hide();
        // }
        //
        // /// <summary>
        // /// <para xml:lang="ko"><c>GameObject</c>를 파괴합니다.</para>
        // /// <para xml:lang="en">Destroy the <c>GameObject</c>.</para>
        // /// </summary>
        // /// <remarks>
        // /// <para xml:lang="ko">메인 스레드 외부에서 이 메서드를 호출하면 오류가 발생합니다.</para>
        // /// <para xml:lang="en">Call this method outside of the main thread will cause an error.</para>
        // /// </remarks>
        // public void Destroy()
        // {
        //     Client.Destroy();
        // }

        public abstract string StartInteraction();
        public abstract void EndInteraction();

        /// <summary>
        /// <para xml:lang="ko">상속된 클래스에서 구현해야 합니다.</para>
        /// <para xml:lang="en">Must be implemented in the inherited class.</para>
        /// </summary>
        protected abstract ItemClient GetClient(GameObject clientObject, string adUnitId);

        internal sealed class ItemRenderingState
        {
            private readonly Renderer[] _renderers;
            private readonly bool[] _rendererStates;
            private readonly Behaviour[] _uiBehaviours;
            private readonly bool[] _uiStates;

            internal ItemRenderingState(Item item)
            {
                _renderers = item.GetComponentsInChildren<Renderer>(true);
                _rendererStates = new bool[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    _rendererStates[i] = _renderers[i].enabled;
                    _renderers[i].enabled = false;
                }

                List<Behaviour> uiBehaviours = new List<Behaviour>();
                foreach (Behaviour behaviour in item.GetComponentsInChildren<Behaviour>(true))
                {
                    string typeName = behaviour.GetType().FullName ?? "";
                    if (typeName == "UnityEngine.UI.RawImage" || typeName == "UnityEngine.UI.Image")
                    {
                        uiBehaviours.Add(behaviour);
                    }
                }

                _uiBehaviours = uiBehaviours.ToArray();
                _uiStates = new bool[_uiBehaviours.Length];
                for (int i = 0; i < _uiBehaviours.Length; i++)
                {
                    _uiStates[i] = _uiBehaviours[i].enabled;
                    _uiBehaviours[i].enabled = false;
                }
            }

            internal void Restore()
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].enabled = _rendererStates[i];
                }

                for (int i = 0; i < _uiBehaviours.Length; i++)
                {
                    if (_uiBehaviours[i] != null) _uiBehaviours[i].enabled = _uiStates[i];
                }
            }
        }


        private void EnableSDK()
        {
            if (!EasterAdConfigFiles.TryReadConfig(out string[] config, out _)) { return; }
            if (config.Length == 0 || !bool.TryParse(config[0], out bool easterAdEnabled))
            {
                InstanceManager.DebugLogger.LogWarning("EasterAd config is missing the enable flag.");
                return;
            }

            if (easterAdEnabled)
            {
                EasterAdSdk.CreateEasterAdSdk();
            }
        }
    }
}
