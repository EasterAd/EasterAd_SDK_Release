#nullable enable annotations
using System;
using UnityEngine;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko">광고 인터랙션 URL을 앱의 외부 이동 고지 UX와 연결하기 위한 헬퍼입니다.</para>
    /// <para xml:lang="en">Helper for routing ad interaction URLs through the app's external-navigation notice UX.</para>
    /// </summary>
    public static class EasterAdInteraction
    {
        /// <summary>
        /// <para xml:lang="ko">앱이 자체 확인 모달이나 보호자 고지 UX를 표시할 수 있도록 호출되는 이벤트입니다.</para>
        /// <para xml:lang="en">Event invoked so the app can show its own confirmation modal or guardian notice UX.</para>
        /// </summary>
        public static event Action<string>? ExternalNavigationRequested;

        /// <summary>
        /// <para xml:lang="ko">이벤트 구독자가 없을 때 <c>Application.OpenURL</c>로 바로 여는지 여부입니다.</para>
        /// <para xml:lang="en">Whether to fall back to <c>Application.OpenURL</c> when no event subscriber is registered.</para>
        /// </summary>
        public static bool OpenUrlWhenUnhandled { get; set; } = true;

        /// <summary>
        /// <para xml:lang="ko">외부 이동 URL 처리를 요청합니다. 구독자가 없으면 설정에 따라 <c>Application.OpenURL</c>을 호출합니다.</para>
        /// <para xml:lang="en">Requests handling for an external navigation URL. If no subscriber exists, it may call <c>Application.OpenURL</c>.</para>
        /// </summary>
        /// <param name="url">
        /// <para xml:lang="ko">열거나 앱 UX로 전달할 URL입니다.</para>
        /// <para xml:lang="en">The URL to open or pass to app-owned UX.</para>
        /// </param>
        /// <returns>
        /// <para xml:lang="ko">요청이 처리되었으면 true입니다.</para>
        /// <para xml:lang="en">True when the request was handled.</para>
        /// </returns>
        public static bool RequestOpen(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            if (ExternalNavigationRequested != null)
            {
                ExternalNavigationRequested.Invoke(url);
                return true;
            }

            if (!OpenUrlWhenUnhandled) return false;

            Application.OpenURL(url);
            return true;
        }

        /// <summary>
        /// <para xml:lang="ko">광고 상호작용을 시작한 뒤 반환된 URL 처리를 요청합니다.</para>
        /// <para xml:lang="en">Starts ad interaction and requests handling for the returned URL.</para>
        /// </summary>
        /// <param name="item">
        /// <para xml:lang="ko">상호작용을 시작할 광고 아이템입니다.</para>
        /// <para xml:lang="en">The ad item that starts interaction.</para>
        /// </param>
        /// <returns>
        /// <para xml:lang="ko">상호작용 URL 처리가 요청되었으면 true입니다.</para>
        /// <para xml:lang="en">True when handling was requested for the interaction URL.</para>
        /// </returns>
        public static bool StartAndRequestOpen(Item item)
        {
            if (item == null) return false;
            string url = item.StartInteraction();
            return RequestOpen(url);
        }
    }
}
