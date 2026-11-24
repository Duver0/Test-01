using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Builds and caches the game's only material family. Everything is a toon material driven by
    /// Palette; no default Lit material is ever left on a renderer.
    /// </summary>
    public static class StylizedMaterials
    {
        static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        static Shader _toon;

        public static Shader ToonShader
        {
            get
            {
                if (_toon == null) _toon = Shader.Find("Slotgun/Toon");
                if (_toon == null) _toon = Shader.Find("Universal Render Pipeline/Lit");
                return _toon;
            }
        }

        public static Material Solid(Color color, bool emissive = false)
        {
            int key = color.GetHashCode() * 31 + (emissive ? 7 : 0);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = new Material(ToonShader)
            {
                name = "Toon_" + ColorUtility.ToHtmlStringRGB(color) + (emissive ? "_E" : "")
            };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", Color.Lerp(color, Palette.Shadow, 0.62f));
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", emissive ? color : Color.Lerp(color, Color.white, 0.35f));
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", emissive ? 1.3f : 2.6f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissive ? (Vector4)(color * 1.4f) : Vector4.zero);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            Cache[key] = mat;
            return mat;
        }

        public static void Apply(GameObject go, Color color, bool emissive = false)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = Solid(color, emissive);
        }
    }
}
