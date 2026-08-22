using EasterAd_Implementation;
using EasterAd_Implementation.Library;
using UnityEngine;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko"><c>Plane</c> 클래스는 <see cref="Item"/> 클래스를 상속받아 평면 광고 오브젝트를 제어합니다.</para>
    /// <para xml:lang="en">The <c>Plane</c> class inherits from the <see cref="Item"/> class to control plane ad objects.</para>
    /// </summary>
    public class Plane : Item
    {
        
#pragma warning disable CS1591 // 공개된 형식 또는 멤버에 대한 XML 주석이 없습니다.
        public override void Load()
#pragma warning restore CS1591 // 공개된 형식 또는 멤버에 대한 XML 주석이 없습니다.
        {
            if (!TryGetInitializedClient("Load", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "Load");
        }
        
        /// <summary>
        /// <para xml:lang="ko">지원되는 게임 내 광고와의 상호작용을 시작하고 URL을 반환합니다. Android/iOS 또는 Unity WebGL에서는 빈 문자열을 반환합니다.</para>
        /// <para xml:lang="en">Starts interaction with a supported in-game ad and returns its URL. It returns an empty string on Android/iOS and Unity WebGL.</para>
        /// </summary>
        /// <returns>
        /// <para xml:lang="ko">상호작용 URL 문자열입니다.</para>
        /// <para xml:lang="en">The interaction URL string.</para>
        /// </returns>
        public override string StartInteraction()
        {
            if (!TryGetInitializedClient("StartInteraction", out ItemClient itemClient)) { return ""; }
            FunctionScheduler.FuncCall(ref itemClient, "StartInteraction", out string interactionUrl);
            return interactionUrl;
        }
        
        /// <summary>
        /// <para xml:lang="ko">지원되는 게임 내 광고와의 상호작용을 종료합니다. Android/iOS와 Unity WebGL에서는 아무 작업도 하지 않습니다.</para>
        /// <para xml:lang="en">Ends interaction with a supported in-game ad. This is a no-op on Android/iOS and Unity WebGL.</para>
        /// </summary>
        public override void EndInteraction()
        {
            if (!TryGetInitializedClient("EndInteraction", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "EndInteraction");
        }

        /// <summary>  
        /// <para xml:lang="ko"><c>Plane</c>의 생성자입니다.</para>  
        /// <para xml:lang="en">Constructor for <c>Plane</c>.</para>  
        /// </summary>  
        /// <param name="clientObject">  
        /// <para xml:lang="ko">광고 오브젝트를 나타내는 <c>GameObject"</c>입니다.</para>
        /// <para xml:lang="en">The <c>GameObject</c> representing the ad object.</para>
        /// </param>  
        /// <param name="adUnitId">  
        /// <para xml:lang="ko">광고 단위 ID입니다.</para>  
        /// <para xml:lang="en">The ad unit ID.</para>  
        /// </param>  
        /// <returns>  
        /// <para xml:lang="ko">생성된 <c>PlaneClient</c> 객체입니다.</para>  
        /// <para xml:lang="en">The created <c>PlaneClient</c> object.</para>  
        /// </returns>  
        // ReSharper disable once ParameterHidesMember
        protected override ItemClient GetClient(GameObject clientObject, string adUnitId)
        {
            return new PlaneClient(new EasterAd_Dependencies.Unity.GameObject(clientObject), adUnitId);
        }
    }
}
