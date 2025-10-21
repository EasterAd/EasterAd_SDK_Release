using UnityEngine;
using InstanceManager = ETA_Dependencies.Unity.InstanceManager;

namespace ETA
{
    [RequireComponent(typeof(Renderer))]
    public class AdSegmentationObject : MonoBehaviour
    {
        public int SegmentationId { get; private set; }
        private Item _item;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;

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
        }

        void OnDestroy()
        {
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
