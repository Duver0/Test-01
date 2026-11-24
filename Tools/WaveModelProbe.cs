using System.Text;
using UnityEngine;

/// <summary>Play-mode check: are the wave bodies wearing the imported characters?</summary>
public class WaveModelProbe
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        var units = Object.FindObjectsByType<Slotgun.EnemyUnit>(FindObjectsSortMode.None);
        var zombies = Object.FindObjectsByType<Slotgun.ZombieEnemyUnit>(FindObjectsSortMode.None);

        sb.AppendLine($"[WMODEL] units={units.Length} zombies={zombies.Length}");

        foreach (var u in units) Describe(sb, u.transform, u.role.ToString());
        foreach (var z in zombies) Describe(sb, z.transform, "Zombie");

        Debug.Log(sb.ToString());
    }

    static void Describe(StringBuilder sb, Transform root, string label)
    {
        var cap = root.GetComponent<CapsuleCollider>();
        sb.Append($"  {label} at {root.position:F1} visual=");

        Transform visual = root.childCount > 0 ? root.GetChild(0) : null;
        if (visual == null) { sb.AppendLine("<none>"); return; }

        sb.AppendLine($"{visual.name} scale={visual.localScale.x:F2} capR={(cap != null ? cap.radius : 0f):F2}");

        foreach (var r in visual.GetComponentsInChildren<Renderer>())
        {
            string shader = r.sharedMaterial != null && r.sharedMaterial.shader != null
                ? r.sharedMaterial.shader.name : "<none>";
            sb.AppendLine($"    renderer {r.name} shader={shader} boundsSize={r.bounds.size:F2}");
        }
    }
}
