using System.Text;
using UnityEditor;
using UnityEngine;
using Slotgun;

public static class CatalogDiag
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        var cat = AssetDatabase.LoadAssetAtPath<SectorCatalog>("Assets/Data/SectorCatalog.asset");
        if (cat == null) { Debug.LogError("[CatalogDiag] catalog missing"); return; }

        sb.AppendLine("[CatalogDiag] count=" + cat.Count);

        for (int i = 0; i < cat.Count; i++)
        {
            var def = cat.Get(i);
            if (def == null) { sb.AppendLine("  [" + i + "] NULL definition"); continue; }

            string scenePath = "Assets/Scenes/" + def.sceneName + ".unity";
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;

            sb.AppendLine("  [" + i + "] name='" + def.sceneName + "' display='" + def.displayName +
                          "' radius=" + def.arenaRadius + " pattern=" + def.pattern +
                          " sceneExists=" + exists + " path=" + scenePath);
        }

        var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        sb.AppendLine("[CatalogDiag] scene assets=" + guids.Length);
        foreach (var g in guids) sb.AppendLine("   " + AssetDatabase.GUIDToAssetPath(g));

        Debug.Log(sb.ToString());
    }
}
