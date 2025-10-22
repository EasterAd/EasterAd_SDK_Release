using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;
using System;
using ETA; // For AdSegmentationRendererFeature type

namespace ETA_Editor.Menu
{
    /// <summary>
    /// URP Renderer Feature 자동 설치/제거 관리
    /// </summary>
    public static class AdSegmentationFeatureManager
    {
        private const string FeatureName = "AdSegmentationRendererFeature";

        /// <summary>
        /// 현재 활성 URP 렌더러에 AdSegmentationRendererFeature가 설치되어 있는지 확인
        /// </summary>
        public static bool IsFeatureInstalled()
        {
            var rendererData = GetCurrentRendererData();
            if (rendererData == null) return false;

            // 타입으로 직접 비교
            return rendererData.rendererFeatures.Any(f => f != null && f is AdSegmentationRendererFeature);
        }

        /// <summary>
        /// AdSegmentationRendererFeature 설치
        /// </summary>
        public static bool InstallFeature()
        {
            var rendererData = GetCurrentRendererData();
            if (rendererData == null)
            {
                Debug.LogWarning("[EasterAd] No URP Renderer Data found. Please ensure URP is properly set up.");
                return false;
            }

            // 이미 설치되어 있으면 스킵
            if (IsFeatureInstalled())
            {
                Debug.Log("[EasterAd] Feature already installed.");
                return true;
            }

            // RendererFeature 생성 - 직접 타입 사용
            AdSegmentationRendererFeature feature = null;
            try
            {
                feature = ScriptableObject.CreateInstance<AdSegmentationRendererFeature>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EasterAd] Failed to create AdSegmentationRendererFeature: {e.Message}");
                return false;
            }
            feature.name = FeatureName;

            // RendererData에 추가
            Undo.RecordObject(rendererData, "Add EasterAd Feature");

            // SerializedObject를 통해 안전하게 추가
            var serializedObject = new SerializedObject(rendererData);
            var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");

            featuresProperty.arraySize++;
            var newFeatureProperty = featuresProperty.GetArrayElementAtIndex(featuresProperty.arraySize - 1);
            newFeatureProperty.objectReferenceValue = feature;

            serializedObject.ApplyModifiedProperties();

            // Feature를 RendererData의 서브애셋으로 추가
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // RendererData만 저장 (중요: SaveAssets() 대신 특정 애셋만 저장)
            EditorUtility.SetDirty(rendererData);
            string rendererPath = AssetDatabase.GetAssetPath(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);

            Debug.Log($"[EasterAd] Feature installed successfully to {AssetDatabase.GetAssetPath(rendererData)}");
            Debug.Log($"[EasterAd] Feature type: {feature.GetType().FullName}");
            return true;
        }

        /// <summary>
        /// AdSegmentationRendererFeature 제거
        /// </summary>
        public static bool UninstallFeature()
        {
            var rendererData = GetCurrentRendererData();
            if (rendererData == null) return false;

            // 타입으로 직접 찾기
            var featureToRemove = rendererData.rendererFeatures
                .FirstOrDefault(f => f != null && f is AdSegmentationRendererFeature);

            if (featureToRemove == null)
            {
                Debug.Log("[EasterAd] Feature not found, nothing to uninstall.");
                return true;
            }

            Undo.RecordObject(rendererData, "Remove EasterAd Feature");

            // SerializedObject를 통해 제거
            var serializedObject = new SerializedObject(rendererData);
            var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");

            for (int i = featuresProperty.arraySize - 1; i >= 0; i--)
            {
                var element = featuresProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == featureToRemove)
                {
                    // 첫 번째 호출: 참조를 null로 설정
                    featuresProperty.DeleteArrayElementAtIndex(i);
                    // 두 번째 호출: 배열에서 항목 제거
                    featuresProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            serializedObject.ApplyModifiedProperties();

            // Asset 제거 (먼저 Object 삭제, 그 다음 Asset 제거)
            UnityEngine.Object.DestroyImmediate(featureToRemove, true);

            // RendererData만 저장 (중요: SaveAssets() 대신 특정 애셋만 저장)
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);

            return true;
        }

        /// <summary>
        /// 현재 활성 URP Renderer Data 가져오기
        /// </summary>
        private static UniversalRendererData GetCurrentRendererData()
        {
            // Graphics Settings에서 현재 Render Pipeline Asset 가져오기
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogWarning("[EasterAd] URP is not active. Please switch to Universal Render Pipeline.");
                return null;
            }

            // Reflection으로 Renderer Data 접근
            var serializedObject = new SerializedObject(pipeline);
            var rendererDataListProperty = serializedObject.FindProperty("m_RendererDataList");

            if (rendererDataListProperty == null || rendererDataListProperty.arraySize == 0)
            {
                Debug.LogWarning("[EasterAd] No renderer data found in URP asset.");
                return null;
            }

            // 첫 번째 (기본) 렌더러 사용
            var rendererDataProperty = rendererDataListProperty.GetArrayElementAtIndex(0);
            var rendererData = rendererDataProperty.objectReferenceValue as UniversalRendererData;

            return rendererData;
        }

        /// <summary>
        /// SDK 활성화 상태에 맞춰 Feature 동기화
        /// </summary>
        public static void SyncFeatureWithSdkState(bool sdkEnabled)
        {
            bool isInstalled = IsFeatureInstalled();

            if (sdkEnabled && !isInstalled)
            {
                Debug.Log("[EasterAd] SDK enabled - Installing Renderer Feature...");
                InstallFeature();
            }
            else if (!sdkEnabled && isInstalled)
            {
                Debug.Log("[EasterAd] SDK disabled - Uninstalling Renderer Feature...");
                UninstallFeature();
            }
        }
    }
}
