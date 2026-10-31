using UnityEngine;
using UnityEngine.Rendering;

namespace Kartikaris.Cockpit
{
    /// <summary>
    /// Creates one renderer-only inverted-hull proxy and reuses a shared material.
    /// Focus changes only toggle the proxy; no per-frame materials are allocated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisCockpitOutline : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField, Min(0f)] private float outlineThickness = 0.0003f;

        private GameObject proxyObject;
        private Renderer proxyRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool outlined;

        public Renderer TargetRenderer => targetRenderer;
        public bool IsOutlined => outlined;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
            BuildProxy();
            SetOutlined(false);
        }

        private void OnDestroy()
        {
            if (proxyObject != null)
                Destroy(proxyObject);
        }

        private void OnValidate()
        {
            outlineThickness = Mathf.Max(0f, outlineThickness);
            ApplyProperties();
        }

        public void SetOutlined(bool value)
        {
            outlined = value;
            if (proxyRenderer == null && outlined)
                BuildProxy();
            if (proxyRenderer != null)
                proxyRenderer.enabled = outlined;
        }

        private void BuildProxy()
        {
            if (proxyObject != null || targetRenderer == null || outlineMaterial == null)
                return;

            proxyObject = new GameObject("Outline Proxy");
            proxyObject.layer = gameObject.layer;
            proxyObject.transform.SetParent(targetRenderer.transform, false);

            if (targetRenderer is SkinnedMeshRenderer sourceSkinned)
            {
                SkinnedMeshRenderer targetSkinned =
                    proxyObject.AddComponent<SkinnedMeshRenderer>();
                targetSkinned.sharedMesh = sourceSkinned.sharedMesh;
                targetSkinned.bones = sourceSkinned.bones;
                targetSkinned.rootBone = sourceSkinned.rootBone;
                targetSkinned.localBounds = sourceSkinned.localBounds;
                targetSkinned.quality = sourceSkinned.quality;
                targetSkinned.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
                proxyRenderer = targetSkinned;
            }
            else if (targetRenderer is MeshRenderer)
            {
                MeshFilter sourceFilter = targetRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    Destroy(proxyObject);
                    proxyObject = null;
                    return;
                }

                MeshFilter targetFilter = proxyObject.AddComponent<MeshFilter>();
                targetFilter.sharedMesh = sourceFilter.sharedMesh;
                proxyRenderer = proxyObject.AddComponent<MeshRenderer>();
            }

            if (proxyRenderer == null)
            {
                Destroy(proxyObject);
                proxyObject = null;
                return;
            }

            int materialCount = 1;
            if (targetRenderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                materialCount = Mathf.Max(1, skinned.sharedMesh.subMeshCount);
            else
            {
                MeshFilter filter = targetRenderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    materialCount = Mathf.Max(1, filter.sharedMesh.subMeshCount);
            }

            Material[] shared = new Material[materialCount];
            for (int index = 0; index < shared.Length; index++)
                shared[index] = outlineMaterial;
            proxyRenderer.sharedMaterials = shared;
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.lightProbeUsage = LightProbeUsage.Off;
            proxyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            proxyRenderer.renderingLayerMask = targetRenderer.renderingLayerMask;
            ApplyProperties();
            proxyRenderer.enabled = outlined;
        }

        private void ApplyProperties()
        {
            if (proxyRenderer == null)
                return;
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            proxyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(OutlineColorId, outlineColor);
            propertyBlock.SetFloat(OutlineThicknessId, outlineThickness);
            proxyRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
