using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectBones
{
    static readonly string[] HeroPrefabs =
    {
        "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab",
        "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab",
        "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab",
        "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Male_Armors_Skeleton_Modular.prefab",
    };

    static readonly string[] EnvFolders =
    {
        "Assets/BrokenVector/LowPolyTreePack/Prefabs",
        "Assets/Snowy_Low_Poly_Trees",
        "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs",
    };

    public static void Execute()
    {
        var sb = new StringBuilder();

        foreach (var path in HeroPrefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("=== " + path + " ===");
            if (go == null) { sb.AppendLine("  MISSING PREFAB"); continue; }

            var animator = go.GetComponentInChildren<Animator>(true);
            sb.AppendLine("  animator=" + (animator != null) +
                          (animator != null ? " avatar=" + (animator.avatar != null) + " human=" + (animator.avatar != null && animator.avatar.isHuman) + " ctrl=" + (animator.runtimeAnimatorController != null) : ""));

            var names = new List<string>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (names.Count < 400) names.Add(t.name);
            }
            sb.AppendLine("  transforms=" + names.Count);
            sb.AppendLine("  names: " + string.Join(" ", names));

            string[] wanted = { "thigh", "shin", "calf", "leg", "upper_arm", "upperarm", "arm", "forearm", "hand", "spine", "hips", "pelvis" };
            sb.AppendLine("  bone hits:");
            foreach (var w in wanted)
            {
                var hits = new List<string>();
                foreach (var n in names) if (n.ToLowerInvariant().Contains(w) && hits.Count < 8) hits.Add(n);
                if (hits.Count > 0) sb.AppendLine("     " + w + " -> " + string.Join(",", hits));
            }
        }

        sb.AppendLine("=== Environment prefabs ===");
        var shaders = new Dictionary<string, int>();
        int total = 0;
        foreach (var folder in EnvFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder) && !AssetDatabase.LoadAssetAtPath<GameObject>(folder)) { sb.AppendLine("  MISSING " + folder); continue; }
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            sb.AppendLine("  " + folder + " -> " + guids.Length);
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                total++;
                int colliders = go.GetComponentsInChildren<Collider>(true).Length;
                int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
                var b = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string key = m.shader != null ? m.shader.name : "NULL";
                        shaders[key] = shaders.TryGetValue(key, out var c) ? c + 1 : 1;
                    }
                }
                if (total <= 12)
                    sb.AppendLine("     " + p.Substring(p.LastIndexOf('/') + 1) + "  size=" + b.size.ToString("F2") + " colliders=" + colliders + " renderers=" + renderers);
            }
        }
        sb.AppendLine("  env prefabs total=" + total);
        sb.AppendLine("  shaders used by env materials:");
        foreach (var kv in shaders) sb.AppendLine("     " + kv.Key + " x" + kv.Value);

        Debug.Log(sb.ToString());
    }
}
