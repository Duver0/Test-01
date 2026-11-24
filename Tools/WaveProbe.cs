using System.Text;
using UnityEngine;

public class WaveProbe
{
    public static void Report()
    {
        var director = Object.FindAnyObjectByType<Slotgun.GameDirector>();
        var sb = new StringBuilder();

        sb.Append("[WAVE] phase=" + (director != null ? director.Phase.ToString() : "<no director>"));
        if (director != null)
        {
            sb.Append($" round={director.Round} waveTotal={director.WaveTotal} waveRemaining={director.WaveRemaining}");
            sb.Append($" boss={(director.CurrentBoss != null && director.CurrentBoss.Config != null ? director.CurrentBoss.Config.displayName : "<none>")}");
        }

        var units = Object.FindObjectsByType<Slotgun.EnemyUnit>(FindObjectsSortMode.None);
        var zombies = Object.FindObjectsByType<Slotgun.ZombieEnemyUnit>(FindObjectsSortMode.None);
        sb.Append($" enemies={units.Length} zombies={zombies.Length}");

        var health = Object.FindAnyObjectByType<Slotgun.PlayerHealth>();
        if (health != null) sb.Append($" hp={health.Current:0}/{health.maxHealth}");

        foreach (var u in units)
            sb.Append($"\n  unit {u.name} role={u.role} alive={u.IsAlive} hp={u.Current:0}");
        foreach (var z in zombies)
            sb.Append($"\n  zombie {z.name} alive={z.IsAlive} pos={z.transform.position}");

        Debug.Log(sb.ToString());
    }

    public static void Kill()
    {
        int killed = 0;

        foreach (var u in Object.FindObjectsByType<Slotgun.EnemyUnit>(FindObjectsSortMode.None))
        {
            if (!u.IsAlive) continue;
            u.TakeDamage(9999f, u.transform.position, u.transform.position + Vector3.forward);
            killed++;
        }

        foreach (var z in Object.FindObjectsByType<Slotgun.ZombieEnemyUnit>(FindObjectsSortMode.None))
        {
            if (!z.IsAlive) continue;
            z.TakeDamage(9999f, z.transform.position, z.transform.position + Vector3.forward);
            killed++;
        }

        Debug.Log($"[WAVE] killed={killed}");
    }
}
