using System.Text;
using UnityEditor;
using UnityEngine;

public class InspectEnemyModels
{
    static readonly string[] Paths =
    {
        "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab",
        "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab",
        "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab",
        "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Male_Armors_Skeleton_Modular.prefab",
    };

    public static void Execute()
    {
        foreach (var path in Paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.Log("[ENEMYMODEL] missing " + path); continue; }

            var root = Object.Instantiate(prefab);
            root.transform.position = Vector3.zero;

            var sb = new StringBuilder();
            sb.AppendLine($"[ENEMYMODEL] {System.IO.Path.GetFileName(path)} bounds={BoundsOf(root.transform)}");

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Vector3 localCenter = root.transform.InverseTransformPoint(r.bounds.center);
                string mats = "";
                foreach (var m in r.sharedMaterials)
                    mats += (m != null ? m.name + "(" + (m.shader != null ? m.shader.name : "?") + ")" : "<null>") + " ";

                sb.AppendLine($"  renderer {PathOf(r.transform, root.transform)} localCenter={localCenter:F3} size={r.bounds.size:F3} mats={mats}");
            }

            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                sb.AppendLine($"  collider {PathOf(c.transform, root.transform)} type={c.GetType().Name}");

            Debug.Log(sb.ToString());
            Object.DestroyImmediate(root);
        }
    }

    static string BoundsOf(Transform root)
    {
        bool has = false;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return has ? $"min={b.min:F2} max={b.max:F2} size={b.size:F2}" : "<none>";
    }

    static string PathOf(Transform t, Transform root)
    {
        if (t == root) return root.name;
        return PathOf(t.parent, root) + "/" + t.name;
    }
}
