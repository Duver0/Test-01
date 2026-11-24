using System.Text;
using UnityEditor;
using UnityEngine;

public static class TreeMats
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/BrokenVector/LowPolyTreePack/Materials" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            sb.AppendLine("  mat '" + m.name + "' shader=" + m.shader.name +
                          " tex=" + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "none"));
        }

        sb.AppendLine("[TreeMats] prefab -> material");
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BrokenVector/LowPolyTreePack/Prefabs" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var r = go != null ? go.GetComponentInChildren<Renderer>(true) : null;
            sb.AppendLine("  " + go.name + " -> " + (r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "none"));
        }
        Debug.Log(sb.ToString());
    }
}
