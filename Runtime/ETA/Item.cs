using System.IO;
using ETA_Dependencies.Unity;
using UnityEngine;
using ETA_Implementation;
using GameObject = UnityEngine.GameObject;
#pragma warning disable CS1591 // 공개된 형식 또는 멤버에 대한 XML 주석이 없습니다.

namespace ETA
{
    /// <summary>
    /// <para xml:lang="ko"><c>Item</c> 클래스를 통해 각 광고 오브젝트들을 제어할 수 있습니다.</para>
    /// <para xml:lang="en">You can control each ad object through the <c>Item</c> class.</para>
    /// </summary>
    public abstract class Item : MonoBehaviour
    {
        protected ItemClient _client = null!;
        public ItemClient Client => _client;
        
        public string adUnitId = null!; //must be set in Unity Editor
        public bool allowImpression = true;
        public bool loadOnStart = true;
        public float refreshTime = 10.0f;
        
        private bool _isImpressed;
        private float _afterImpressedTime;
        

        internal void Awake() // todo change Destroy process
        {
            if(EtaSdk.OnceInitialized == false)
            {
                EtaSdk.ItemAwakeQueue.Enqueue(this);
                EnableSDK();
                return;
            }
            
            if (EtaSdk.Instance.GetItemClient(adUnitId) != null)
            {
                InstanceManager.DebugLogger.Log("Item already exist: " + adUnitId);
            }

            _client = GetClient(gameObject, adUnitId);
            EtaSdk.Instance.AddItemClient(adUnitId, in _client);
            InstanceManager.DebugLogger.Log("Item added: " + adUnitId);
        }

        private void Start()
        {
            if (loadOnStart) { Load(); }
        }
        
        private void Update()
        {
            if(_isImpressed)
            {
                _afterImpressedTime += Time.unscaledDeltaTime;
                if (_afterImpressedTime >= refreshTime && _client.GetStatus() == ItemStatus.Impressed)
                {
                    _isImpressed = false;
                    _afterImpressedTime = 0.0f;
                    Load();
                }
            }
            else if (_client.GetStatus() == ItemStatus.Impressed)
            {
                _isImpressed = true;
                _afterImpressedTime = 0.0f;
            }
            else if (_client.GetStatus() == ItemStatus.Impressing)
            {
                _isImpressed = false;
                _afterImpressedTime = 0.0f;
            }
        }

        private void OnDestroy()
        {
            try{
                EtaSdk.Instance.RemoveItemClient(adUnitId);
            }
            catch
            {
                // ignored
            }
        }


        /// <summary>
        /// <para xml:lang="ko">서버에서 광고를 로드하고 표시합니다.</para>
        /// <para xml:lang="en">Load Ad from server and show if.</para>
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

        /// <summary>
        /// <para xml:lang="ko">상속된 클래스에서 구현해야 합니다.</para>
        /// <para xml:lang="en">Must be implemented in the inherited class.</para>
        /// </summary>
        protected abstract ItemClient GetClient(GameObject clientObject, string adUnitId);

        
        private void EnableSDK()
        {
            string filename = "ETA_Config.txt";
            string filepath = Path.Combine(Application.streamingAssetsPath, filename);
            if (File.Exists(filepath) == false) { return; }
            
            string[] config = File.ReadAllLines(filepath);
            bool easterAdEnabled = bool.Parse(config[0]);
            
            if (easterAdEnabled)
            {
                EtaSdk.CreateEtaSdk();
            }
        }
    }
}
