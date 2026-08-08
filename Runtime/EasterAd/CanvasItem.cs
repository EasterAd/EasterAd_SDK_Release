using EasterAd_Implementation;
using UnityEngine;
using GameObject = UnityEngine.GameObject;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko"><c>CanvasItem</c> 클래스는 Canvas/RectTransform 기반 UI 광고 오브젝트를 제어합니다.</para>
    /// <para xml:lang="en">The <c>CanvasItem</c> class controls Canvas/RectTransform based UI ad objects.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
        public class CanvasItem : Item
        {
        /// <summary>
        /// <para xml:lang="ko">서버에서 UI 광고를 로드하고 대상 UI 컴포넌트에 적용합니다.</para>
        /// <para xml:lang="en">Loads a UI ad from the server and applies it to the target UI component.</para>
        /// </summary>
        public override void Load()
        {
            if (!TryGetInitializedClient("Load", out ItemClient itemClient)) { return; }
            EasterAd_Implementation.Library.FunctionScheduler.FuncCall(ref itemClient, "Load");
        }

        /// <summary>
        /// <para xml:lang="ko">광고 상호작용을 시작하고 외부 이동 URL을 반환합니다.</para>
        /// <para xml:lang="en">Starts ad interaction and returns the external navigation URL.</para>
        /// </summary>
        /// <returns>
        /// <para xml:lang="ko">상호작용 URL입니다. 사용할 수 없으면 빈 문자열입니다.</para>
        /// <para xml:lang="en">The interaction URL, or an empty string when unavailable.</para>
        /// </returns>
        public override string StartInteraction()
        {
            if (!TryGetInitializedClient("StartInteraction", out ItemClient itemClient)) { return ""; }
            EasterAd_Implementation.Library.FunctionScheduler.FuncCall(ref itemClient, "StartInteraction", out string interactionUrl);
            return interactionUrl;
        }

        /// <summary>
        /// <para xml:lang="ko">진행 중인 광고 상호작용을 종료합니다.</para>
        /// <para xml:lang="en">Ends the current ad interaction.</para>
        /// </summary>
        public override void EndInteraction()
        {
            if (!TryGetInitializedClient("EndInteraction", out ItemClient itemClient)) { return; }
            EasterAd_Implementation.Library.FunctionScheduler.FuncCall(ref itemClient, "EndInteraction");
        }

        /// <inheritdoc />
        protected override ItemClient GetClient(GameObject clientObject, string adUnitId)
        {
            return new CanvasItemClient(new EasterAd_Dependencies.Unity.GameObject(clientObject), adUnitId);
        }
    }
}
