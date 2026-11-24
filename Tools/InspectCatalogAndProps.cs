using System.Collections.Generic;
using System.Text;
using Slotgun;
using UnityEditor;
using UnityEngine;

public static class InspectCatalogAndProps
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== SectorCatalog ===");
        var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>("Assets/Data/SectorCatalog.asset");
        if (catalog == null) sb.AppendLine("  NULL");
        else
        {
            sb.AppendLine("  count=" + catalog.Count);
            for (int i = 0; i < catalog.Count; i++)
            {
                var d = catalog.sectors[i];
                if (d == null) { sb.AppendLine("   [" + i + "] NULL"); continue; }
                sb.AppendLine("   [" + i + "] name=" + d.displayName + " scene=" + d.sceneName +
                              " radius=" + d.arenaRadius + " wall=" + d.wallHeight + " pattern=" + d.pattern);
            }
        }

        sb.AppendLine("=== Polytope props / village / plants prefabs ===");
        string[] folders =
        {
            "Assets/Polytope Studio/Lowpoly_Props/Prefabs",
            "Assets/Polytope Studio/Lowpoly_Village/Prefabs",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs",
        };
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) { sb.AppendLine("  MISSING " + folder); continue; }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;

                int colliders = go.GetComponentsInChildren<Collider>(true).Length;
                var b = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                var mats = new List<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null) mats.Add(m.name + ":" + m.shader.name);
                }
                sb.AppendLine("  " + p.Substring(p.IndexOf("Prefabs")) + " size=" + b.size.ToString("F2") +
                              " center=" + b.center.ToString("F2") + " colliders=" + colliders +
                              " mats=" + string.Join("|", mats));
            }
        }

        Debug.Log(sb.ToString());
    }
}
