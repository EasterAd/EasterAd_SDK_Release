#nullable enable
using System;
using EasterAd_Implementation;
using EasterAd_Implementation.Library;
using UnityEngine;
using GameObject = UnityEngine.GameObject;

namespace ETA
{
    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA</c> 네임스페이스 사용자를 위한 한시적 호환 래퍼입니다. 새 코드에서는 <c>EasterAd.EasterAdSdk</c>를 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility wrapper for users of the previous <c>ETA</c> namespace. Use <c>EasterAd.EasterAdSdk</c> in new code.</para>
    /// </summary>
    [Obsolete("Use EasterAd.EasterAdSdk. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public class EasterAdSdk : EasterAd.EasterAdSdk
    {
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.Item</c> 참조를 위한 한시적 호환 기본 클래스입니다. 새 코드에서는 <c>EasterAd.Item</c>을 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility base class for previous <c>ETA.Item</c> references. Use <c>EasterAd.Item</c> in new code.</para>
    /// </summary>
    [Obsolete("Use EasterAd.Item. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public abstract class Item : EasterAd.Item
    {
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.Plane</c> 참조를 위한 한시적 호환 컴포넌트입니다. 새 코드에서는 <c>EasterAd.Plane</c>을 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility component for previous <c>ETA.Plane</c> references. Use <c>EasterAd.Plane</c> in new code.</para>
    /// </summary>
    [Obsolete("Use EasterAd.Plane. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public class Plane : Item
    {
        /// <inheritdoc />
        public override void Load()
        {
            if (!TryGetInitializedClient("Load", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "Load");
        }

        /// <inheritdoc />
        public override string StartInteraction()
        {
            if (!TryGetInitializedClient("StartInteraction", out ItemClient itemClient)) { return ""; }
            FunctionScheduler.FuncCall(ref itemClient, "StartInteraction", out string interactionUrl);
            return interactionUrl;
        }

        /// <inheritdoc />
        public override void EndInteraction()
        {
            if (!TryGetInitializedClient("EndInteraction", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "EndInteraction");
        }

        /// <inheritdoc />
        protected override ItemClient GetClient(GameObject clientObject, string adUnitId)
        {
            return new PlaneClient(new EasterAd_Dependencies.Unity.GameObject(clientObject), adUnitId);
        }
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.CanvasItem</c> 참조를 위한 한시적 호환 컴포넌트입니다. 새 코드에서는 <c>EasterAd.CanvasItem</c>을 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility component for previous <c>ETA.CanvasItem</c> references. Use <c>EasterAd.CanvasItem</c> in new code.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [Obsolete("Use EasterAd.CanvasItem. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public class CanvasItem : Item
    {
        /// <inheritdoc />
        public override void Load()
        {
            if (!TryGetInitializedClient("Load", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "Load");
        }

        /// <inheritdoc />
        public override string StartInteraction()
        {
            if (!TryGetInitializedClient("StartInteraction", out ItemClient itemClient)) { return ""; }
            FunctionScheduler.FuncCall(ref itemClient, "StartInteraction", out string interactionUrl);
            return interactionUrl;
        }

        /// <inheritdoc />
        public override void EndInteraction()
        {
            if (!TryGetInitializedClient("EndInteraction", out ItemClient itemClient)) { return; }
            FunctionScheduler.FuncCall(ref itemClient, "EndInteraction");
        }

        /// <inheritdoc />
        protected override ItemClient GetClient(GameObject clientObject, string adUnitId)
        {
            return new CanvasItemClient(new EasterAd_Dependencies.Unity.GameObject(clientObject), adUnitId);
        }
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.MaterialManager</c> 참조를 위한 한시적 호환 컴포넌트입니다. 새 코드에서는 <c>EasterAd.MaterialManager</c>를 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility component for previous <c>ETA.MaterialManager</c> references. Use <c>EasterAd.MaterialManager</c> in new code.</para>
    /// </summary>
    [Obsolete("Use EasterAd.MaterialManager. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public class MaterialManager : EasterAd.MaterialManager
    {
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.AdSegmentationObject</c> 참조를 위한 한시적 호환 컴포넌트입니다. 새 코드에서는 <c>EasterAd.AdSegmentationObject</c>를 사용하세요.</para>
    /// <para xml:lang="en">Temporary compatibility component for previous <c>ETA.AdSegmentationObject</c> references. Use <c>EasterAd.AdSegmentationObject</c> in new code.</para>
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [Obsolete("Use EasterAd.AdSegmentationObject. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public class AdSegmentationObject : EasterAd.AdSegmentationObject
    {
    }

    /// <summary>
    /// <para xml:lang="ko">이전 <c>ETA.EasterAdInteraction</c> 호출을 새 API로 전달하는 한시적 호환 헬퍼입니다.</para>
    /// <para xml:lang="en">Temporary compatibility helper that forwards previous <c>ETA.EasterAdInteraction</c> calls to the new API.</para>
    /// </summary>
    [Obsolete("Use EasterAd.EasterAdInteraction. The ETA namespace is kept only as a temporary source-compatibility bridge.", false)]
    public static class EasterAdInteraction
    {
        /// <summary>
        /// <para xml:lang="ko">앱이 자체 확인 모달이나 보호자 고지 UX를 표시할 수 있도록 호출되는 이벤트입니다.</para>
        /// <para xml:lang="en">Event invoked so the app can show its own confirmation modal or guardian notice UX.</para>
        /// </summary>
        public static event Action<string>? ExternalNavigationRequested
        {
            add => EasterAd.EasterAdInteraction.ExternalNavigationRequested += value;
            remove => EasterAd.EasterAdInteraction.ExternalNavigationRequested -= value;
        }

        /// <summary>
        /// <para xml:lang="ko">이벤트 구독자가 없을 때 <c>Application.OpenURL</c>로 바로 여는지 여부입니다.</para>
        /// <para xml:lang="en">Whether to fall back to <c>Application.OpenURL</c> when no event subscriber is registered.</para>
        /// </summary>
        public static bool OpenUrlWhenUnhandled
        {
            get => EasterAd.EasterAdInteraction.OpenUrlWhenUnhandled;
            set => EasterAd.EasterAdInteraction.OpenUrlWhenUnhandled = value;
        }

        /// <summary>
        /// <para xml:lang="ko">외부 이동 URL 처리를 요청합니다.</para>
        /// <para xml:lang="en">Requests handling for an external navigation URL.</para>
        /// </summary>
        public static bool RequestOpen(string url)
        {
            return EasterAd.EasterAdInteraction.RequestOpen(url);
        }

        /// <summary>
        /// <para xml:lang="ko">광고 상호작용을 시작한 뒤 반환된 URL 처리를 요청합니다.</para>
        /// <para xml:lang="en">Starts ad interaction and requests handling for the returned URL.</para>
        /// </summary>
        public static bool StartAndRequestOpen(EasterAd.Item item)
        {
            return EasterAd.EasterAdInteraction.StartAndRequestOpen(item);
        }
    }
}
