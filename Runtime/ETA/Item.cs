using UnityEngine;

using ETA_Implementation;

namespace ETA
{
    /// <summary>
    /// <para xml:lang="ko"><c>Item</c> 클래스를 통해 각 광고 오브젝트들을 제어할 수 있습니다.</para>
    /// <para xml:lang="en">You can control each ad object through the <c>Item</c> class.</para>
    /// </summary>
    public abstract class Item : MonoBehaviour
    {
        private ItemClient _client = null!;
        [SerializeField] 
        private string adUnitId = null!; //must be set in Unity Editor

        void Awake() // todo change Destroy process
        {
            if (EtaSdkClient.Instance.GetItemClient(adUnitId) != null)
            {
                DebugLogger.Log("Item already exist: " + adUnitId);
            }

            _client = GetClient(gameObject, adUnitId);
            EtaSdkClient.Instance.AddItemClient(adUnitId, in _client);
        }

        private void OnDestroy()
        {
            EtaSdkClient.Instance.RemoveItemClient(adUnitId);
        }
        

        // /// <summary>
        // /// <para xml:lang="ko">서버에서 광고를 로드합니다.</para>
        // /// <para xml:lang="en">Load Ad from server.</para>
        // /// </summary>
        // /// <remarks>
        // /// <para xml:lang="ko"><see cref="Show(object, EventArgs)"/>를 호출하기 전에 호출해야 하며, 직접 호출하지 마십시오.</para>
        // /// <para xml:lang="en">Should be called before <see cref="Show(object, EventArgs)"/>, and do not call directly.</para>
        // /// </remarks>
        // public void Load(object sender, EventArgs e)
        // {
        //     Task.Run(() => { Client.Load();}); // Async processing
        // }
        //
        // /// <summary>
        // /// <para xml:lang="ko">GameObject에 광고를 렌더링합니다. 이 메서드를 <see cref="Load"/> 전에 호출하면 먼저 <see cref="Load"/> 메서드가 호출됩니다.</para>
        // /// <para xml:lang="en">Render Ad on the GameObject. When call this method before <see cref="Load"/>, it will be called <see cref="Load"/> method first.</para>
        // /// </summary>
        // /// <remarks>
        // /// <para xml:lang="ko">메인 스레드 외부에서 이 메서드를 호출하면 오류가 발생합니다.</para>
        // /// <para xml:lang="en">Call this method outside of the main thread will cause an error.</para>
        // /// </remarks>
        // public void Show(object sender, EventArgs e)
        // {
        //     if (Client.Status != ItemStatus.Loaded) // if not loaded, Load first
        //     {
        //         Load(this, EventArgs.Empty);
        //         if (Client.Status != ItemStatus.Loaded) return;
        //     }
        //     Client.Show(); // Do not call this method outside of the main thread!!
        // }
        //
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

    }
}
