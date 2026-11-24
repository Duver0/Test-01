using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slotgun.EditorTools;

public static class RunFirstPersonRig2
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject player = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Player") { player = root; break; }

        if (player == null) { Debug.LogError("[FP2] no Player root"); return; }

        string log = FirstPersonRig.Install(player);
        Debug.Log(log);

        var weapon = player.transform.Find("Camera/Weapon") ?? player.transform.Find("Body/Camera/Weapon");
        if (weapon == null)
        {
            var cam = player.GetComponentInChildren<Camera>(true);
            if (cam != null) weapon = cam.transform.Find("Weapon");
        }

        if (weapon != null)
        {
            Debug.Log($"[FP2] weapon localPos={weapon.localPosition.ToString("F3")} scale={weapon.localScale.x:F3}");
            var grip = weapon.Find("Grip");
            var fore = weapon.Find("Foregrip");
            Debug.Log($"[FP2] grip local={(grip != null ? grip.localPosition.ToString("F3") : "NULL")} world={(grip != null ? grip.position.ToString("F3") : "NULL")}");
            Debug.Log($"[FP2] foregrip local={(fore != null ? fore.localPosition.ToString("F3") : "NULL")}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);
        Debug.Log($"[FP2] saved={scene.path}");
    }
}
