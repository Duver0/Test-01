using System.Text;
using UnityEngine;
using UnityEditor;

public static class BodyProbe
{
    const string Path = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";

    public static void Execute()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (go == null) { Debug.Log("[BodyProbe] prefab not found"); return; }

        var sb = new StringBuilder();
        sb.AppendLine("[BodyProbe] root scale=" + go.transform.localScale + " pos=" + go.transform.localPosition);

        int n = 0;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            n++;
            string mats = "";
            foreach (var m in r.sharedMaterials) mats += (m != null ? m.name + "(" + m.shader.name + ")" : "null") + "|";
            sb.AppendLine("  " + t.name + "  enabled=" + r.enabled + "  type=" + r.GetType().Name + "  mats=" + mats);
        }
        sb.AppendLine("[BodyProbe] renderers=" + n);

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            var b = smr.bounds;
            sb.AppendLine("[BodyProbe] bounds size=" + b.size + " center=" + b.center);
        }

        // armour roots: any transform whose name starts with a known set prefix
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            string nm = t.name;
            if (nm.StartsWith("Starter") || nm.StartsWith("PlateSet1") || nm.StartsWith("PlateSet"))
                sb.AppendLine("[BodyProbe] set-node " + t.name + " active=" + t.gameObject.activeSelf);
        }

        Debug.Log(sb.ToString());
    }
}
