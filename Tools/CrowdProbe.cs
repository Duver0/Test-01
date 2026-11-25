using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Measures the wave's actual spacing: the closest two living bodies, and how many pairs are
/// standing inside one another. Two capsules that overlap read as enemies walking through each other.
/// </summary>
public static class CrowdProbe
{
    public static void Execute()
    {
        var log = new StringBuilder("[CrowdProbe]\n");

        var units = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        var zombies = Object.FindObjectsByType<ZombieEnemyUnit>(FindObjectsSortMode.None);

        var points = new System.Collections.Generic.List<Vector3>();
        var names = new System.Collections.Generic.List<string>();

        foreach (var u in units)
            if (u != null && u.IsAlive) { points.Add(u.transform.position); names.Add(u.role.ToString()); }

        foreach (var z in zombies)
            if (z != null && z.IsAlive) { points.Add(z.transform.position); names.Add("Zombie"); }

        log.Append("alive bodies=").Append(points.Count).Append("\n");

        int overlapping = 0;      // capsules (r ~0.35) truly inside one another
        int tooClose = 0;         // closer than the separation spacing the bodies ask for
        float closest = float.MaxValue;
        string closestPair = "-";

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                Vector3 d = points[i] - points[j];
                d.y = 0f;
                float m = d.magnitude;

                if (m < closest) { closest = m; closestPair = names[i] + "/" + names[j]; }
                if (m < 0.70f) overlapping++;
                if (m < 1.05f) tooClose++;
            }
        }

        log.Append("closest pair=").Append(closest.ToString("F2")).Append(" m (").Append(closestPair).Append(")\n")
           .Append("pairs under 0.70 m (capsules intersecting)=").Append(overlapping).Append("\n")
           .Append("pairs under 1.05 m (inside their requested spacing)=").Append(tooClose).Append("\n");

        if (points.Count > 0)
        {
            Vector3 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            log.Append("spread=").Append((max - min).ToString("F1")).Append("\n");
        }

        Debug.Log(log.ToString());
    }
}
