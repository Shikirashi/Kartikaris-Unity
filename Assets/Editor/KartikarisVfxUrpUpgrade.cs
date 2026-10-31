using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class KartikarisVfxUrpUpgrade
{
    private const string WarFxUrpShaderName = "Kartikaris/URP/WarFX Universal";
    private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

    private static readonly string[] Roots =
    {
        "Assets/Mirza",
        "Assets/Mirza Beig",
        "Assets/JMO Assets"
    };

    [MenuItem("Kartikaris/VFX/Upgrade Imported VFX Materials To URP")]
    public static void Run()
    {
        Shader warFxShader = Shader.Find(WarFxUrpShaderName);
        Shader urpLitShader = Shader.Find(UrpLitShaderName);
        if (warFxShader == null || urpLitShader == null)
            throw new InvalidOperationException("Required Kartikaris WarFX or URP Lit shader is unavailable.");

        int standardConverted = 0;
        int effectConverted = 0;
        int alreadyUrp = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", Roots);
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    skipped++;
                    continue;
                }

                string oldShaderName = material.shader.name;
                if (oldShaderName == WarFxUrpShaderName || IsUrpShader(oldShaderName))
                {
                    alreadyUrp++;
                    continue;
                }

                if (oldShaderName == "Standard")
                {
                    ConvertStandardMaterial(material, urpLitShader);
                    standardConverted++;
                }
                else if (TryGetEffectSettings(oldShaderName, out EffectSettings settings))
                {
                    ConvertEffectMaterial(material, warFxShader, settings);
                    effectConverted++;
                }
                else
                {
                    Debug.LogWarning($"VFX_URP_SKIPPED|{path}|{oldShaderName}");
                    skipped++;
                    continue;
                }

                EditorUtility.SetDirty(material);
                Debug.Log($"VFX_URP_CONVERTED|{path}|{oldShaderName}|{material.shader.name}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"VFX_URP_UPGRADE_COMPLETE|standard={standardConverted}|effects={effectConverted}|" +
            $"alreadyUrp={alreadyUrp}|skipped={skipped}");
    }

    private static bool IsUrpShader(string shaderName)
    {
        return shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
            shaderName == "Mirza Beig/Distortion Shockwave Particle";
    }

    private static void ConvertStandardMaterial(Material material, Shader urpLitShader)
    {
        Texture baseMap = GetTexture(material, "_MainTex");
        Vector2 textureScale = material.HasProperty("_MainTex")
            ? material.GetTextureScale("_MainTex")
            : Vector2.one;
        Vector2 textureOffset = material.HasProperty("_MainTex")
            ? material.GetTextureOffset("_MainTex")
            : Vector2.zero;
        Color baseColor = GetColor(material, "_Color", Color.white);
        float metallic = GetFloat(material, "_Metallic", 0f);
        float smoothness = GetFloat(material, "_Glossiness", 0.5f);
        Texture normalMap = GetTexture(material, "_BumpMap");
        float normalScale = GetFloat(material, "_BumpScale", 1f);
        Texture emissionMap = GetTexture(material, "_EmissionMap");
        Color emissionColor = GetColor(material, "_EmissionColor", Color.black);

        material.shader = urpLitShader;
        material.SetTexture("_BaseMap", baseMap);
        material.SetTextureScale("_BaseMap", textureScale);
        material.SetTextureOffset("_BaseMap", textureOffset);
        material.SetColor("_BaseColor", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetTexture("_BumpMap", normalMap);
        material.SetFloat("_BumpScale", normalScale);
        material.SetTexture("_EmissionMap", emissionMap);
        material.SetColor("_EmissionColor", emissionColor);

        if (normalMap != null)
            material.EnableKeyword("_NORMALMAP");
        if (emissionMap != null || emissionColor.maxColorComponent > 0f)
            material.EnableKeyword("_EMISSION");
    }

    private static void ConvertEffectMaterial(
        Material material,
        Shader warFxShader,
        EffectSettings settings)
    {
        Texture mainTexture = GetTexture(material, "_MainTex") ?? GetTexture(material, "_BaseMap");
        string sourceTextureProperty = material.HasProperty("_MainTex") ? "_MainTex" : "_BaseMap";
        Vector2 textureScale = material.HasProperty(sourceTextureProperty)
            ? material.GetTextureScale(sourceTextureProperty)
            : Vector2.one;
        Vector2 textureOffset = material.HasProperty(sourceTextureProperty)
            ? material.GetTextureOffset(sourceTextureProperty)
            : Vector2.zero;
        Color tint = material.HasProperty("_TintColor")
            ? material.GetColor("_TintColor")
            : GetColor(material, "_BaseColor", GetColor(material, "_Color", Color.white));
        float inverseFade = GetFloat(material, "_InvFade", 1f);
        float scrollSpeed = GetFloat(material, "_ScrollSpeed", 0f);
        float shininess = GetFloat(material, "_Shininess", 0.078125f);
        Color specularColor = GetColor(material, "_SpecColor", new Color(0.5f, 0.5f, 0.5f, 0f));

        material.shader = warFxShader;
        material.SetTexture("_MainTex", mainTexture);
        material.SetTextureScale("_MainTex", textureScale);
        material.SetTextureOffset("_MainTex", textureOffset);
        material.SetColor("_TintColor", tint);
        material.SetFloat("_InvFade", inverseFade);
        material.SetFloat("_ScrollSpeed", scrollSpeed);
        material.SetFloat("_Shininess", shininess);
        material.SetColor("_SpecColor", specularColor);
        material.SetFloat("_EffectMode", settings.Mode);
        material.SetFloat("_UseSoftParticles", settings.UseSoftParticles ? 1f : 0f);
        material.SetFloat("_SrcBlend", (float)settings.SourceBlend);
        material.SetFloat("_DstBlend", (float)settings.DestinationBlend);
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static bool TryGetEffectSettings(string shaderName, out EffectSettings settings)
    {
        switch (shaderName)
        {
            case "Particles/Standard Unlit":
            case "Legacy Shaders/Particles/Alpha Blended":
            case "WFX/Alpha Blended (No Soft Particles)":
                settings = new EffectSettings(0, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
                return true;
            case "WFX/Additive Alpha8":
                settings = new EffectSettings(1, BlendMode.One, BlendMode.One, true);
                return true;
            case "WFX/Additive (Soft) Alpha8":
                settings = new EffectSettings(2, BlendMode.OneMinusDstColor, BlendMode.One, true);
                return true;
            case "WFX/Multiply Alpha8":
                settings = new EffectSettings(3, BlendMode.Zero, BlendMode.SrcColor, true);
                return true;
            case "WFX/Scroll/Additive":
                settings = new EffectSettings(4, BlendMode.One, BlendMode.One, true);
                return true;
            case "WFX/Scroll/Alpha Blended":
                settings = new EffectSettings(5, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, true);
                return true;
            case "WFX/Multiply Soft Tint":
                settings = new EffectSettings(6, BlendMode.DstColor, BlendMode.SrcColor, false);
                return true;
            case "WFX/Scroll/Multiply Soft Tint":
                settings = new EffectSettings(7, BlendMode.DstColor, BlendMode.SrcColor, false);
                return true;
            case "WFX/Scroll/Smoke":
                settings = new EffectSettings(8, BlendMode.DstColor, BlendMode.SrcAlpha, false);
                return true;
            case "WFX/Transparent Diffuse":
                settings = new EffectSettings(9, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
                return true;
            case "WFX/Transparent Specular":
                settings = new EffectSettings(10, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false);
                return true;
            case "Legacy Shaders/Particles/Additive":
                settings = new EffectSettings(11, BlendMode.One, BlendMode.One, false);
                return true;
            case "Legacy Shaders/Particles/Additive (Soft)":
                settings = new EffectSettings(11, BlendMode.OneMinusDstColor, BlendMode.One, true);
                return true;
            default:
                settings = default;
                return false;
        }
    }

    private static Texture GetTexture(Material material, string propertyName)
    {
        return material.HasProperty(propertyName) ? material.GetTexture(propertyName) : null;
    }

    private static Color GetColor(Material material, string propertyName, Color fallback)
    {
        return material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
    }

    private static float GetFloat(Material material, string propertyName, float fallback)
    {
        return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
    }

    private readonly struct EffectSettings
    {
        public int Mode { get; }
        public BlendMode SourceBlend { get; }
        public BlendMode DestinationBlend { get; }
        public bool UseSoftParticles { get; }

        public EffectSettings(
            int mode,
            BlendMode sourceBlend,
            BlendMode destinationBlend,
            bool useSoftParticles)
        {
            Mode = mode;
            SourceBlend = sourceBlend;
            DestinationBlend = destinationBlend;
            UseSoftParticles = useSoftParticles;
        }
    }
}
