using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko">스크린샷 또는 공유 이미지 캡처 동안 광고 렌더링을 숨겼다가 원복하는 범위 객체입니다.</para>
    /// <para xml:lang="en">Scope object that hides ad rendering during screenshot or share-image capture and restores it afterwards.</para>
    /// </summary>
    public sealed class EasterAdCaptureScope : IDisposable
    {
        private readonly List<Item.ItemRenderingState> _states = new List<Item.ItemRenderingState>();
        private bool _disposed;

        internal EasterAdCaptureScope(bool onlyItemsOptedIn)
        {
            foreach (Item item in UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.InstanceID))
            {
                if (onlyItemsOptedIn && !item.hideDuringCapture) continue;
                _states.Add(item.CaptureRenderingState());
            }
        }

        /// <summary>
        /// <para xml:lang="ko">숨겨둔 광고 렌더링 상태를 원복합니다.</para>
        /// <para xml:lang="en">Restores the ad rendering states hidden by this scope.</para>
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                _states[i].Restore();
            }
            _disposed = true;
        }
    }
}
