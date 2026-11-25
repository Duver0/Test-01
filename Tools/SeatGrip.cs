using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;

public static class SeatGrip
{
    const float ReachLimit = 1.15f;
    static readonly Vector3 WeaponPos = new Vector3(0.135f, -0.10f, 0.38f);
    const float WeaponScale = 0.50f;

    public static void Execute()
    {
        var log = new StringBuilder("[Grip] ");

        var active = EditorSceneManager.GetActiveScene();
        PatchScene(active, log, true);

        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));

        foreach (var p in paths)
        {
            if (p == active.path) continue;
            var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Additive);
            try
            {
                if (PatchScene(scene, log, false))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, p);
                    log.Append(" saved ").Append(p);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        Debug.Log(log.ToString());
    }

    static bool PatchScene(Scene scene, StringBuilder log, bool saveActive)
    {
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "Player") continue;

            var cam = root.GetComponentInChildren<Camera>(true);
            var weapon = cam != null ? cam.transform.Find("Weapon") : null;
            if (weapon != null)
            {
                weapon.localPosition = WeaponPos;
                weapon.localScale = Vector3.one * WeaponScale;
                changed = true;
                log.Append(scene.name).Append(" weapon=").Append(weapon.localPosition.ToString("F2"))
                   .Append(" scale=").Append(weapon.localScale.x.ToString("F2")).Append(' ');
            }

            var arms = root.GetComponentInChildren<FirstPersonArms>(true);
            if (arms != null)
            {
                arms.reachLimit = ReachLimit;
                if (weapon != null)
                {
                    arms.grip = weapon.Find("Grip");
                    arms.foregrip = weapon.Find("Foregrip");
                }
                changed = true;
                log.Append("arms reach=").Append(arms.reachLimit.ToString("F2"))
                   .Append(" grip=").Append(arms.grip != null ? "ok" : "NULL")
                   .Append(" foregrip=").Append(arms.foregrip != null ? "ok" : "NULL").Append(' ');
            }
        }

        if (changed && saveActive)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);
            log.Append("saved ").Append(scene.path).Append(' ');
        }

        return changed;
    }
}
