using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectBlink
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        string prefabPath = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        sb.AppendLine("=== prefab ===");
        if (go == null) sb.AppendLine("  MISSING");
        else
        {
            var animator = go.GetComponentInChildren<Animator>(true);
            sb.AppendLine("  animator=" + (animator != null) +
                          (animator != null ? " avatar=" + (animator.avatar != null) +
                                              " human=" + (animator.avatar != null && animator.avatar.isHuman) +
                                              " valid=" + (animator.avatar != null && animator.avatar.isValid) +
                                              " ctrl=" + (animator.runtimeAnimatorController != null) : ""));

            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    var mats = new List<string>();
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null) mats.Add(m.name + ":" + m.shader.name);
                    sb.AppendLine("  renderer=" + r.name + " type=" + r.GetType().Name + " mats=" + string.Join("|", mats));
                }
            }
            sb.AppendLine("  bounds size=" + b.size.ToString("F2") + " center=" + b.center.ToString("F2"));
            sb.AppendLine("  colliders=" + go.GetComponentsInChildren<Collider>(true).Length);
            sb.AppendLine("  transforms=" + go.GetComponentsInChildren<Transform>(true).Length);

            var names = new List<string>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (names.Count < 30) names.Add(t.name);
            }
            sb.AppendLine("  first transforms: " + string.Join(" ", names));
        }

        sb.AppendLine("=== human FBX ===");
        DumpModel("Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Meshes_Humans/HumanMale_Character.fbx");

        sb.AppendLine("=== animation clips ===");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Blink/Art/Animations" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) { sb.AppendLine("  " + p + " importer NULL"); continue; }

            var clips = new List<string>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                var c = a as AnimationClip;
                if (c == null || c.name.StartsWith("__preview__")) continue;
                clips.Add(c.name + "(" + c.length.ToString("F2") + "s)");
            }
            sb.AppendLine("  " + System.IO.Path.GetFileName(p) + " type=" + mi.animationType +
                          " importAnim=" + mi.importAnimation + " clipCount=" + clips.Count +
                          " clips=" + string.Join(",", clips));
        }

        sb.AppendLine("=== materials shaders ===");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Materials_Humans" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) continue;
            sb.AppendLine("  " + m.name + " shader=" + (m.shader != null ? m.shader.name : "NULL") +
                          " tex=" + (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null ? m.GetTexture("_MainTex").name :
                                     (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "none")));
        }

        Debug.Log(sb.ToString());
    }

    static void DumpModel(string path)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.Log("  " + path + " importer NULL"); return; }
        Debug.Log("  " + System.IO.Path.GetFileName(path) + " type=" + mi.animationType +
                  " importAnim=" + mi.importAnimation +
                  " defaultClips=" + (mi.defaultClipAnimations != null ? mi.defaultClipAnimations.Length : 0));
    }
}
