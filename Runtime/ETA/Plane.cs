using ETA_Implementation;
using ETA_Implementation.Library;
using UnityEngine;

namespace ETA
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
            FunctionScheduler.FuncCall(ref _client, "Load");
        }
        
        public override string StartInteraction()
        {
            FunctionScheduler.FuncCall(ref _client, "StartInteraction", out string interactionUrl);
            return interactionUrl;
        }
        
        public override void EndInteraction()
        {
            FunctionScheduler.FuncCall(ref _client, "EndInteraction");
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
            return new PlaneClient(new ETA_Dependencies.Unity.GameObject(clientObject), adUnitId);
        }
    }
}