using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;

public static class WaveVerify
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        var spawner = Object.FindAnyObjectByType<WaveSpawner>();
        if (spawner == null)
        {
            sb.AppendLine("WaveSpawner: NOT FOUND");
        }
        else
        {
            sb.AppendLine("WaveSpawner Total=" + spawner.Total + " Alive=" + spawner.Alive);
        }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            sb.AppendLine("PlayerController: NOT FOUND");
            return sb.ToString();
        }

        var animator = player.GetComponentInChildren<Animator>(true);
        float playerHeight = animator != null ? ModelHeight.Measure(animator.transform) : 0f;
        sb.AppendLine("player body height = " + playerHeight.ToString("0.00") + " m");

        // sprint vs walk, straight out of the private rule
        var sprint = typeof(PlayerController).GetMethod("SprintSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        float sprintSpeed = sprint != null ? (float)sprint.Invoke(player, null) : -1f;
        sb.AppendLine("walk=" + player.walkSpeed.ToString("0.0") + "  sprint(effective)=" +
                      sprintSpeed.ToString("0.0") + "  ratio=" + (player.walkSpeed > 0f ? (sprintSpeed / player.walkSpeed).ToString("0.00") : "n/a"));

        // enemy heights
        int units = 0, zombies = 0;
        float minH = float.MaxValue, maxH = 0f;
        foreach (var u in Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None))
        {
            var v = u.transform.Find("Visual");
            float h = v != null ? ModelHeight.Measure(v) : 0f;
            if (h <= 0f) continue;
            units++;
            minH = Mathf.Min(minH, h);
            maxH = Mathf.Max(maxH, h);
        }
        foreach (var z in Object.FindObjectsByType<ZombieEnemyUnit>(FindObjectsSortMode.None))
        {
            var v = z.transform.Find("ZombieVisual");
            float h = v != null ? ModelHeight.Measure(v) : 0f;
            if (h <= 0f) continue;
            zombies++;
            minH = Mathf.Min(minH, h);
            maxH = Mathf.Max(maxH, h);
        }
        sb.AppendLine("enemies measured: units=" + units + " zombies=" + zombies);
        sb.AppendLine("enemy height range = " + (units + zombies > 0 ? minH.ToString("0.00") + " .. " + maxH.ToString("0.00") : "none") + " m");

        // weak points
        int weak = 0;
        foreach (var w in Object.FindObjectsByType<BossWeakPoint>(FindObjectsSortMode.None)) weak++;
        sb.AppendLine("weak points mounted = " + weak);

        // tree colliders in the loaded scenes
        int trees = 0, solid = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (!sc.isLoaded) continue;
            foreach (var root in sc.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Tree_")) continue;
                    trees++;
                    if (t.GetComponent<Collider>() != null) solid++;
                }
            }
        }
        sb.AppendLine("tree instances=" + trees + " with a collider=" + solid);

        return sb.ToString();
    }
}
