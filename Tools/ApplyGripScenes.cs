using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slotgun;

/// <summary>
/// Applies the corrected viewmodel placement to every scene that owns a player rig, not just the
/// open one. Slotgun_Arena still held the old oversized pistol and its two misplaced grip empties,
/// so playing that scene alone would have brought the broken hold back.
/// </summary>
public static class ApplyGripScenes
{
    static readonly Vector3 WeaponPosition = new Vector3(0.115f, -0.115f, 0.34f);
    const float WeaponScale = 0.30f;
    static readonly Vector3 GripFromHandle = new Vector3(0f, 0.10f, -0.06f);
    static readonly Vector3 ForegripFromHandle = new Vector3(-0.05f, -0.09f, -0.02f);

    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Slotgun_Core.unity",
        "Assets/Scenes/Slotgun_Arena.unity",
    };

    public static void Execute()
    {
        var log = new StringBuilder("[ApplyGripScenes]\n");

        var active = EditorSceneManager.GetActiveScene();
        log.Append(Apply(active.GetRootGameObjects(), active.name)).Append("\n");

        foreach (var path in ScenePaths)
        {
            if (active.path == path) continue;
            if (!System.IO.File.Exists(path)) continue;

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                log.Append(Apply(scene.GetRootGameObjects(), scene.name)).Append("\n");
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        EditorSceneManager.MarkSceneDirty(active);
        if (!string.IsNullOrEmpty(active.path)) EditorSceneManager.SaveScene(active, active.path);

        Debug.Log(log.ToString());
    }

    static string Apply(GameObject[] roots, string sceneName)
    {
        GameObject player = null;
        foreach (var root in roots)
            if (root.name == "Player") { player = root; break; }

        if (player == null) return sceneName + ": no Player";

        var body = player.transform.Find("Body");
        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null || body == null) return sceneName + ": no weapon rig";

        weapon.localPosition = WeaponPosition;
        weapon.localRotation = Quaternion.identity;
        weapon.localScale = Vector3.one * WeaponScale;

        // The handle is the magazine mesh; the empty that used to be placed off the M1911's own
        // origin sat ~5 cm above and to the right of it, so the hands closed beside the gun.
        Vector3 handle = Vector3.zero;
        foreach (var mr in weapon.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name != "Magazin") continue;
            handle = weapon.InverseTransformPoint(mr.bounds.center);
            break;
        }

        var grip = weapon.Find("Grip");
        var foregrip = weapon.Find("Foregrip");
        if (grip != null) grip.localPosition = handle + GripFromHandle;
        if (foregrip != null) foregrip.localPosition = handle + ForegripFromHandle;

        var arms = body.GetComponent<FirstPersonArms>();
        if (arms != null)
        {
            arms.grip = grip;
            arms.foregrip = foregrip;
            arms.gripBack = 0.03f;
            arms.gripDown = 0.012f;
            arms.foregripBack = 0.03f;
            arms.foregripDown = 0.016f;
        }

        return sceneName + ": weapon=" + WeaponPosition.ToString("F3") + " scale=" + WeaponScale
             + " grip=" + (grip != null ? grip.localPosition.ToString("F3") : "NULL");
    }
}
