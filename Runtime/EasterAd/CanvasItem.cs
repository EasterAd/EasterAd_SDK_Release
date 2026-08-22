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
        /// <para xml:lang="ko">Android/iOS에서는 외부 provider에 load-and-show를 위임하고, 지원되는 비-WebGL 비모바일 플랫폼에서는 UI 광고를 게임 안에 로드합니다. Unity WebGL에서는 fail-closed로 비활성화합니다.</para>
        /// <para xml:lang="en">Delegates load-and-show to the external provider on Android/iOS, loads the UI ad in-game on supported non-WebGL non-mobile platforms, and fails closed on Unity WebGL.</para>
        /// </summary>
        public override void Load()
        {
            if (!TryGetInitializedClient("Load", out ItemClient itemClient)) { return; }
            EasterAd_Implementation.Library.FunctionScheduler.FuncCall(ref itemClient, "Load");
        }

        /// <summary>
        /// <para xml:lang="ko">지원되는 게임 내 광고의 상호작용을 시작하고 외부 이동 URL을 반환합니다. Android/iOS 또는 Unity WebGL에서는 빈 문자열을 반환합니다.</para>
        /// <para xml:lang="en">Starts interaction for a supported in-game ad and returns its navigation URL. It returns an empty string on Android/iOS and Unity WebGL.</para>
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
        /// <para xml:lang="ko">진행 중인 지원 게임 내 광고 상호작용을 종료합니다. Android/iOS와 Unity WebGL에서는 아무 작업도 하지 않습니다.</para>
        /// <para xml:lang="en">Ends the current supported in-game ad interaction. This is a no-op on Android/iOS and Unity WebGL.</para>
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
