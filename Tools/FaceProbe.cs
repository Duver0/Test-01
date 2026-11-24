using UnityEditor;
using UnityEngine;

/// <summary>Lines the four candidate enemy models up facing local +Z so their fronts can be inspected.</summary>
public class FaceProbe
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
        var old = GameObject.Find("FaceProbe");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("FaceProbe");

        for (int i = 0; i < Paths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[i]);
            if (prefab == null) { Debug.Log("[FACE] missing " + Paths[i]); continue; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Model" + i;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(i * 2.5f - 3.75f, 0f, 0f);
            go.transform.localRotation = Quaternion.identity;
        }

        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            // Camera on +Z looking back down -Z: whatever we see is the model's front if it faces +Z.
            sv.LookAt(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 180f, 0f), 5.5f);
            sv.Repaint();
        }

        Debug.Log("[FACE] probe built: 0=zombie suit, 1=zombie patient, 2=skeleton, 3=armoured skeleton");
    }

    public static void Cleanup()
    {
        var root = GameObject.Find("FaceProbe");
        if (root != null) Object.DestroyImmediate(root);
        Debug.Log("[FACE] probe removed");
    }
}
