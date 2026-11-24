using UnityEditor;
using UnityEngine;

/// <summary>Play-mode check for the zombie packs: drops one of each in front of the player, standing still.</summary>
public class ZombieModelProbe
{
    const string SuitPath = "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
    const string PatientPath = "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab";

    public static void Execute()
    {
        var player = Object.FindFirstObjectByType<Slotgun.PlayerController>();
        if (player == null) { Debug.LogError("[ZPROBE] no player"); return; }

        Transform t = player.transform;
        Vector3 forward = t.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 basePos = t.position + forward * 9f;

        Spawn("ZombieSuit", SuitPath, basePos - side * 1.8f, player);
        Spawn("ZombiePatient", PatientPath, basePos + side * 1.8f, player);

        Debug.Log("[ZPROBE] spawned near " + basePos.ToString("F1"));
    }

    static void Spawn(string name, string path, Vector3 position, Slotgun.PlayerController player)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogError("[ZPROBE] missing " + path); return; }

        var go = new GameObject(name);
        go.transform.position = position;

        var zombie = go.AddComponent<Slotgun.ZombieEnemyUnit>();
        zombie.Initialize(position, player, prefab);
        zombie.moveSpeed = 0f;

        Debug.Log("[ZPROBE] " + name + " child0=" + (go.transform.childCount > 0 ? go.transform.GetChild(0).name : "none"));
    }
}
