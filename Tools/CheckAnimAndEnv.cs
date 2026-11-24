using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CheckAnimAndEnv
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== AnimationClip assets ===");
        var clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
        sb.AppendLine("count=" + clipGuids.Length);
        foreach (var g in clipGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            sb.AppendLine("  " + p + "  clip=" + (c != null ? c.name + " len=" + c.length.ToString("0.00") : "NULL"));
        }

        sb.AppendLine("=== guid 10806ce02e9c5404185224520c5fa640 ===");
        sb.AppendLine("  path='" + AssetDatabase.GUIDToAssetPath("10806ce02e9c5404185224520c5fa640") + "'");

        sb.AppendLine("=== Character FBX importers ===");
        string[] fbxs =
        {
            "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Models/Characters_Zombie_SuitMan_1.fbx",
            "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Models/Characters_Zombie_Pacient_04.fbx",
            "Assets/Polytope Studio/Lowpoly_Characters/Modular_Characters/Skeleton/Male/PT_Skeleton_Male_Modular.fbx",
            "Assets/Polytope Studio/Lowpoly_Characters/Sources/Animations/PT_Peasant_Pose_02.fbx",
        };
        foreach (var f in fbxs)
        {
            var mi = AssetImporter.GetAtPath(f) as ModelImporter;
            if (mi == null) { sb.AppendLine("  " + f + " => importer NULL"); continue; }
            sb.AppendLine("  " + f);
            sb.AppendLine("     importAnimation=" + mi.importAnimation + " animationType=" + mi.animationType +
                          " clips=" + (mi.clipAnimations != null ? mi.clipAnimations.Length : 0) +
                          " defaultClips=" + (mi.defaultClipAnimations != null ? mi.defaultClipAnimations.Length : 0));
            if (mi.defaultClipAnimations != null)
                foreach (var dc in mi.defaultClipAnimations) sb.AppendLine("       clip: " + dc.name + " [" + dc.firstFrame + ".." + dc.lastFrame + "]");
        }

        sb.AppendLine("=== All FBX/model assets under Assets (for animation sweep) ===");
        var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
        sb.AppendLine("models=" + modelGuids.Length);
        int animModels = 0;
        foreach (var g in modelGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.ToLower().EndsWith(".fbx") && !p.ToLower().EndsWith(".dae") && !p.ToLower().EndsWith(".glb")) continue;
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) continue;
            var clips = AssetDatabase.LoadAllAssetsAtPath(p);
            var names = new List<string>();
            foreach (var a in clips) if (a is AnimationClip && !((AnimationClip)a).name.StartsWith("__preview__")) names.Add(a.name);
            if (names.Count > 0) { animModels++; sb.AppendLine("  ANIM " + p + " -> " + string.Join(",", names)); }
        }
        sb.AppendLine("  animatedModels=" + animModels);

        sb.AppendLine("=== Environment prefabs ===");
        string[] envRoots =
        {
            "Assets/BrokenVector/LowPolyTreePack/Prefabs",
            "Assets/Snowy_Low_Poly_Trees",
            "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs",
        };
        foreach (var root in envRoots)
        {
            if (!AssetDatabase.IsValidFolder(root)) { sb.AppendLine("  MISSING FOLDER " + root); continue; }
            var gs = AssetDatabase.FindAssets("t:Prefab", new[] { root });
            sb.AppendLine("  " + root + " prefabs=" + gs.Length);
        }

        sb.AppendLine("=== Build settings scenes ===");
        foreach (var s in EditorBuildSettings.scenes)
            sb.AppendLine("  " + (s.enabled ? "[x] " : "[ ] ") + s.path);

        Debug.Log(sb.ToString());
    }
}
