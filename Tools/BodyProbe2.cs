using System.Text;
using UnityEngine;
using UnityEditor;

public static class BodyProbe2
{
    const string Path = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";

    public static void Execute()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (go == null) { Debug.Log("[BodyProbe2] prefab not found"); return; }

        var sb = new StringBuilder();

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.GetComponentInChildren<Animator>(true);
        if (anim == null) sb.AppendLine("[BodyProbe2] NO Animator");
        else
        {
            sb.AppendLine("[BodyProbe2] animator on '" + anim.gameObject.name + "' avatar=" +
                          (anim.avatar != null ? anim.avatar.name : "null") +
                          " isHuman=" + (anim.avatar != null && anim.avatar.isHuman) +
                          " valid=" + (anim.avatar != null && anim.avatar.isValid) +
                          " ctrl=" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null") +
                          " rootMotion=" + anim.applyRootMotion);
        }

        // union of all renderer bounds, expressed in the prefab root's local space
        var root = go.transform;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var w = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var l = root.InverseTransformPoint(w);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
                any = true;
            }
        }

        if (any)
        {
            sb.AppendLine("[BodyProbe2] local bounds min=" + min + " max=" + max + " size=" + (max - min));
        }

        // per-part local bounds, to see where the arms actually point
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var b = r.bounds;
            Vector3 mn = root.InverseTransformPoint(b.min);
            Vector3 mx = root.InverseTransformPoint(b.max);
            sb.AppendLine("  " + r.name + " min=" + mn.ToString("F2") + " max=" + mx.ToString("F2"));
        }

        var matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Blink" });
        sb.AppendLine("[BodyProbe2] Blink materials=" + matGuids.Length);
        foreach (var g in matGuids)
            sb.AppendLine("   " + AssetDatabase.GUIDToAssetPath(g));

        Debug.Log(sb.ToString());
    }
}
