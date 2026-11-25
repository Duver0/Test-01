using UnityEngine;
using Slotgun;

/// <summary>
/// Directed check of the now-invisible weak point: a shot on it must still pay double damage and
/// still hand the body the speed surge, even though there is no mesh left to aim at.
/// </summary>
public static class WeakProbe
{
    public static string Execute()
    {
        var units = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        if (units.Length == 0) return "no EnemyUnit in the scene";

        var unit = units[0];
        var point = unit.GetComponentInChildren<BossWeakPoint>(true);
        if (point == null) return "no BossWeakPoint under " + unit.name;

        var renderers = point.GetComponentsInChildren<Renderer>(true).Length;
        var col = point.GetComponent<SphereCollider>();

        float before = unit.Current;
        float speedBefore = unit.SpeedMultiplier;

        point.RangedHit(10f, unit.transform.position, unit.transform.position + Vector3.forward);

        float after = unit.Current;

        return "unit=" + unit.name +
               " max=" + unit.MaxHealth.ToString("0") +
               " renderers under weak point=" + renderers +
               " collider=" + (col != null ? (col.isTrigger ? "TRIGGER" : "solid") : "MISSING") +
               "\nhealth " + before.ToString("0.0") + " -> " + after.ToString("0.0") +
               "  delta=" + (before - after).ToString("0.0") + " (a 10-damage shot must take 20)" +
               "\nweak surge: speed " + speedBefore.ToString("0.00") + " -> " + unit.SpeedMultiplier.ToString("0.00") +
               "  surgeActive=" + unit.WeakSurgeActive +
               "  pointActive=" + point.Active;
    }
}
