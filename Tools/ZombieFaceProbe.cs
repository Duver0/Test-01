using UnityEditor;
using UnityEngine;

/// <summary>Frames the two zombie pack characters head-on so their facing can be checked.</summary>
public class ZombieFaceProbe
{
    const string SuitPath = "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
    const string PatientPath = "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab";

    public static void Execute()
    {
        var old = GameObject.Find("ZombieFaceProbe");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("ZombieFaceProbe");

        var a = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SuitPath));
        a.transform.SetParent(root.transform, false);
        a.transform.localPosition = new Vector3(-0.9f, 0f, 0f);
        a.transform.localRotation = Quaternion.identity;
        a.transform.localScale = Vector3.one * 0.85f;

        var b = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PatientPath));
        b.transform.SetParent(root.transform, false);
        b.transform.localPosition = new Vector3(0.9f, 0f, 0f);
        b.transform.localRotation = Quaternion.identity;
        b.transform.localScale = Vector3.one * 0.85f;

        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.LookAt(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 180f, 0f), 2.6f);
            sv.Repaint();
        }

        Debug.Log("[ZFACE] probe built");
    }

    public static void Cleanup()
    {
        var root = GameObject.Find("ZombieFaceProbe");
        if (root != null) Object.DestroyImmediate(root);
        Debug.Log("[ZFACE] probe removed");
    }
}
