using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectEnvMats
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        string[] folders =
        {
            "Assets/BrokenVector/LowPolyTreePack/Materials",
            "Assets/Snowy_Low_Poly_Trees/Materials",
            "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Materials",
        };

        foreach (var f in folders)
        {
            sb.AppendLine("=== " + f + " ===");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { f }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                var baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                var col = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta);
                sb.AppendLine("  " + m.name + " shader='" + (m.shader != null ? m.shader.name : "NULL") +
                              "' tex=" + (tex != null ? tex.name : (baseMap != null ? baseMap.name : "none")) +
                              " color=" + col.ToString("F2") +
                              " renderQueue=" + m.renderQueue +
                              " props=" + string.Join(",", GetProps(m)));
            }
        }

        sb.AppendLine("=== vertex colours on env meshes ===");
        string[] prefabs =
        {
            "Assets/BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 01.prefab",
            "Assets/Snowy_Low_Poly_Trees/Pine_Snowy1.prefab",
            "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Tree_1_1.prefab",
            "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Grass_1_1.prefab",
        };
        foreach (var p in prefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) { sb.AppendLine("  MISSING " + p); continue; }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                sb.AppendLine("  " + p.Substring(p.LastIndexOf('/') + 1) + " mesh=" + mesh.name +
                              " verts=" + mesh.vertexCount + " colors=" + mesh.colors.Length +
                              " uv=" + mesh.uv.Length + " submeshes=" + mesh.subMeshCount);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine("     renderer=" + r.name + " mats=" + r.sharedMaterials.Length +
                              " boundsSize=" + r.bounds.size.ToString("F2") + " boundsCenter=" + r.bounds.center.ToString("F2"));
        }

        Debug.Log(sb.ToString());
    }

    static List<string> GetProps(Material m)
    {
        var list = new List<string>();
        var shader = m.shader;
        if (shader == null) return list;
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count && list.Count < 14; i++)
        {
            var name = shader.GetPropertyName(i);
            if (name.StartsWith("_") && (name.Contains("Color") || name.Contains("Tex") || name.Contains("Map") || name.Contains("Cutoff") || name.Contains("Alpha")))
                list.Add(name);
        }
        return list;
    }
}
