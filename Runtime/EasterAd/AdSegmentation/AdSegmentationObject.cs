#nullable enable annotations
using UnityEngine;
using InstanceManager = EasterAd_Dependencies.Unity.InstanceManager;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko">광고 오브젝트를 GPU 기반 가시성 측정용 segmentation pass에 등록합니다.</para>
    /// <para xml:lang="en">Registers an ad object for GPU-based visibility measurement in the segmentation pass.</para>
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class AdSegmentationObject : MonoBehaviour
    {
        /// <summary>
        /// <para xml:lang="ko">segmentation pass가 광고 Renderer만 필터링하기 위해 사용하는 rendering layer mask입니다.</para>
        /// <para xml:lang="en">Rendering layer mask used by the segmentation pass to filter ad renderers only.</para>
        /// </summary>
        public const uint SegmentationRenderingLayerMask = 1u << 31;

        /// <summary>
        /// <para xml:lang="ko">이 광고 오브젝트에 할당된 segmentation ID입니다. 0은 등록 실패를 의미합니다.</para>
        /// <para xml:lang="en">Segmentation ID assigned to this ad object. Zero means registration failed.</para>
        /// </summary>
        public int SegmentationId { get; private set; }
        private Item? _item;
        private Renderer? _renderer;
        private MaterialPropertyBlock? _propBlock;
        private uint _previousRenderingLayerMask;
        private bool _hasPreviousRenderingLayerMask;

        void Start()
        {
            // Item 컴포넌트 가져오기
            _item = GetComponent<Item>();
            if (_item == null)
            {
                InstanceManager.DebugLogger.LogWarning($"AdSegmentationObject on {gameObject.name}: Item component not found");
                enabled = false;
                return;
            }

            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
            {
                InstanceManager.DebugLogger.LogWarning($"AdSegmentationObject on {gameObject.name}: Renderer component not found");
                enabled = false;
                return;
            }

            // Phase 5: GameObject의 Instance ID 사용 (ItemClient.GetUnityInstanceId()와 일치)
            int itemInstanceId = gameObject.GetInstanceID();
            SegmentationId = InstanceManager.AdSegmentationManager.RegisterAd(itemInstanceId);

            if (SegmentationId == 0)
            {
                InstanceManager.DebugLogger.LogWarning($"AdSegmentationObject on {gameObject.name}: Failed to register (ID pool exhausted?)");
                enabled = false;
                return;
            }

            // MaterialPropertyBlock 설정
            _propBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetInt("_adSegmentationId", SegmentationId);
            _renderer.SetPropertyBlock(_propBlock);
            _previousRenderingLayerMask = _renderer.renderingLayerMask;
            _hasPreviousRenderingLayerMask = true;
            _renderer.renderingLayerMask |= SegmentationRenderingLayerMask;
        }

        void OnDestroy()
        {
            if (_renderer != null && _hasPreviousRenderingLayerMask)
            {
                _renderer.renderingLayerMask = _previousRenderingLayerMask;
            }

            // ID 반납 (Manager null 체크)
            if (SegmentationId > 0 && InstanceManager.AdSegmentationManager != null)
            {
                InstanceManager.AdSegmentationManager.UnregisterAd(SegmentationId);
            }
        }

        // 디버그용 (Editor에서 ID 확인)
        void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && SegmentationId > 0)
            {
                // Inspector에서 ID 표시 (ReadOnly)
                InstanceManager.DebugLogger.Log($"AdSegmentationObject on {gameObject.name}: Current ID = {SegmentationId}");
            }
#endif
        }
    }
}
