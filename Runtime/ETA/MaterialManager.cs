// ReSharper disable once RedundantNullableDirective
#nullable enable
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace ETA
{
    /**
 * <summary>
 * <para xml:lang="en">Manager class allocating default material on plane prefab.</para>
 * <para xml:lang="ko">plane prefab에 기본 머터리얼을 할당하기 위한 매니저 클래스 입니다.</para>
 * </summary>
 */
    [ExecuteInEditMode]
    public class MaterialManager : MonoBehaviour
    {
        /// <summary>
        /// <para xml:lang="ko">프리팹에 적용할 기본 머터리얼입니다.</para>
        /// <para xml:lang="en">The default material applied to the prefab.</para>
        /// </summary>
        public Material? defaultMaterial;
        /// <summary>
        /// <para xml:lang="ko">기본 셰이더입니다. 설정 시 기본 머터리얼의 셰이더를 이 값으로 교체합니다.</para>
        /// <para xml:lang="en">The default shader. When set, it overrides the shader of the default material.</para>
        /// </summary>
        public Shader? defaultShader;
        private Renderer? planeRenderer;
        void Awake()
        {
            planeRenderer = GetComponent<Renderer>();
            ApplyMaterial();
        }

        void Update()
        {
            if (planeRenderer != null && planeRenderer.sharedMaterial == null)
            {
                planeRenderer = GetComponent<Renderer>();
                ApplyMaterial();
            }

            SetConstantData();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplyMaterial();
            SetConstantData();
        }
#endif

        void ApplyMaterial()
        {
            if (planeRenderer == null)
            {
                planeRenderer = GetComponent<Renderer>();
                if (planeRenderer == null)
                {
                    Debug.LogError("Plane Renderer is not found on ad prefab.");
                    return;
                }
            }

            Material material;

            if (defaultMaterial != null)
            {
                material = new Material(defaultMaterial)
                {
                    shader = defaultShader ? defaultShader : null
                };
            }
            else
            {
                
                // ReSharper disable once ShaderLabShaderReferenceNotResolved
                Shader shader = Shader.Find("EasterAd/UnifiedShader");

                if (shader != null)
                {
                    material = new Material(shader);
                }
                else
                {
                    // Fallback to legacy shader
                    // ReSharper disable ShaderLabShaderReferenceNotResolved
                    shader = Shader.Find("EasterAd/DefaultShader");
                    if (shader != null)
                    {
                        material = new Material(shader);

                        // Show warning once per session
                        #if UNITY_EDITOR
                        if (!UnityEditor.EditorPrefs.GetBool("EasterAd_LegacyWarningShown_1.2.0", false))
                        {
                            Debug.LogWarning(
                                "[EasterAd] Using legacy shader. Please migrate to the new unified system:\n" +
                                "1. Open Window > EasterAd\n" +
                                "2. Click 'Migrate to New System' button\n" +
                                "This will update all prefab references and clean up old assets."
                            );
                            UnityEditor.EditorPrefs.SetBool("EasterAd_LegacyWarningShown_1.2.0", true);
                        }
                        #endif
                    }
                    else
                    {
                        Debug.LogError("[EasterAd] No shader found. Package may be corrupted.");
                        return;
                    }
                }
            }

            if (material == null)
            {
                Debug.LogError("Material creation fail.");
                return;
            }

            planeRenderer.sharedMaterial = material;
        }

        private void SetConstantData()
        {
            Vector3 data = new Vector3(
                gameObject.transform.lossyScale.x,
                gameObject.transform.lossyScale.z,
                1.0f
            );

            Plane plane = GetComponent<Plane>();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (plane != null && plane.Client != null) // 경고 무시하고 plane.Client != null 검사해야 함
            {
                ETA_Implementation.ItemStatus status = plane.Client.GetStatus();
                bool hideLogo = status >= ETA_Implementation.ItemStatus.Loaded;
                data.z = hideLogo ? 0.0f : 1.0f;
            }

            // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
            planeRenderer!.sharedMaterial.SetVector("_ConstantData", data);
        }
    }
}
