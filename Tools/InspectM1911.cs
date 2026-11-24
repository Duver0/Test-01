using System.Text;
using UnityEditor;
using UnityEngine;

public class InspectM1911
{
    const string PrefabPath = "Assets/Low Poly Weapons VOL.1/Prefabs/M1911.prefab";

    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.Log("[M1911] prefab not found"); return; }

        var root = Object.Instantiate(prefab);
        var sb = new StringBuilder();
        sb.AppendLine("[M1911] root bounds " + BoundsOf(root.transform, root.transform));

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string extra = "";
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var m = mf.sharedMesh;
                extra = $" mesh={m.name} verts={m.vertexCount} meshBounds={m.bounds}";
                extra += GripProbe(m);
            }
            sb.AppendLine($"  path={PathOf(t, root.transform)} lp={t.localPosition} lr={t.localEulerAngles} ls={t.localScale}{extra}");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Low Poly Weapons VOL.1/Gun_MAT.mat");
        sb.AppendLine("[M1911] material shader = " + (mat != null && mat.shader != null ? mat.shader.name : "<none>"));

        Debug.Log(sb.ToString());
        Object.DestroyImmediate(root);
    }

    // Which end of the model hangs lowest: that end is the grip (rear), the other one is the muzzle.
    static string GripProbe(Mesh m)
    {
        var v = m.vertices;
        if (v.Length == 0) return "";

        float backMinY = float.MaxValue, frontMinY = float.MaxValue;
        int back = 0, front = 0;
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i].z < 0f) { back++; if (v[i].y < backMinY) backMinY = v[i].y; }
            else { front++; if (v[i].y < frontMinY) frontMinY = v[i].y; }
        }
        return $"  backVerts(z<0)={back} backMinY={backMinY:0.000}  frontVerts(z>0)={front} frontMinY={frontMinY:0.000}";
    }

    static Bounds BoundsOf(Transform root, Transform t)
    {
        bool has = false;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return has ? b : new Bounds(t.position, Vector3.zero);
    }

    static string PathOf(Transform t, Transform root)
    {
        if (t == root) return root.name;
        return PathOf(t.parent, root) + "/" + t.name;
    }
}
