using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slotgun;

public static class LookAudit
{
    static readonly string[] MaterialFolders =
    {
        "Assets/BrokenVector/LowPolyTreePack/Materials",
        "Assets/Snowy_Low_Poly_Trees/Materials",
        "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Materials",
        "Assets/Polytope Studio/Lowpoly_Props/Sources/Materials",
        "Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials",
        "Assets/Polytope Studio/Lowpoly_Village/Sources/Materials",
        "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Materials_Humans"
    };

    public static void Execute()
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Look] ---- imported material shaders ----");
        foreach (var folder in MaterialFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) { sb.AppendLine("  (missing) " + folder); continue; }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                sb.AppendLine("  " + mat.name + "  ->  " + (mat.shader != null ? mat.shader.name : "NULL") +
                              (mat.HasProperty("_BaseColor") ? "   base=" + ColorUtility.ToHtmlStringRGB(mat.GetColor("_BaseColor")) : ""));
            }
        }

        sb.AppendLine("[Look] ---- scene lighting (open scene) ----");
        sb.AppendLine("  scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        sb.AppendLine("  skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null") +
                      " ambientMode=" + RenderSettings.ambientMode +
                      " ambient=" + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight));
        sb.AppendLine("  fog=" + RenderSettings.fog + " mode=" + RenderSettings.fogMode +
                      " color=" + ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor) +
                      " density=" + RenderSettings.fogDensity);

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            sb.AppendLine("  light " + light.name + " type=" + light.type + " color=" + ColorUtility.ToHtmlStringRGB(light.color) +
                          " intensity=" + light.intensity + " shadows=" + light.shadows +
                          " range=" + light.range.ToString("F1"));

        sb.AppendLine("[Look] ---- sector catalog ----");
        var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>("Assets/Data/SectorCatalog.asset");
        if (catalog == null) sb.AppendLine("  no catalog");
        else
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                var d = catalog.Get(i);
                if (d == null) continue;
                sb.AppendLine("  [" + i + "] " + d.displayName + " scene=" + d.sceneName +
                              " R=" + d.arenaRadius + " H=" + d.wallHeight + " pattern=" + d.pattern +
                              " floor=" + ColorUtility.ToHtmlStringRGB(d.floorTint) +
                              " wall=" + ColorUtility.ToHtmlStringRGB(d.wallTint) +
                              " accent=" + ColorUtility.ToHtmlStringRGB(d.accent) +
                              " fog=" + d.fogDensity);
            }
        }

        Debug.Log(sb.ToString());
    }
}
