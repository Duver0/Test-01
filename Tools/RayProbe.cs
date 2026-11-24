using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>Compares Physics.Raycast (what FirePellet uses) with RaycastAll from the player's eye, so the
/// harness can tell whether its own preview is lying about the player's CharacterController.</summary>
public static class RayProbe
{
    public static void Execute()
    {
        void L(string k, string v) => Debug.Log($"[RAY] {k} = {v}");

        var d = GameDirector.Instance;
        if (d == null || d.player == null) { L("player", "missing"); return; }

        L("phase", d.Phase.ToString());
        if (d.playerHealth != null) L("health", $"{d.playerHealth.Current:0.0}/{d.playerHealth.maxHealth:0.0}");

        var boss = d.CurrentBoss as BossController;
        if (boss == null) { L("boss", "missing"); return; }

        var zone = boss.GetComponentInChildren<BossWeakPoint>();
        if (zone == null) { L("zone", "missing"); return; }

        Transform eye = d.player.Eye;
        L("eye", $"{eye.position} insideController={d.player.GetComponent<CharacterController>().bounds.Contains(eye.position)}");
        L("distance", $"{Vector3.Distance(eye.position, boss.transform.position):0.0} m");

        Vector3 center = zone.GetComponent<Collider>().bounds.center;
        Vector3 high = boss.transform.position + Vector3.up * 2.6f;

        Single(eye, center, "circle");
        Single(eye, high, "highBody");

        var all = Physics.RaycastAll(eye.position, (center - eye.position).normalized, 60f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < all.Length && i < 5; i++)
            L($"all[{i}]", $"'{all[i].collider.name}' at {all[i].distance:0.00} m");
    }

    static void Single(Transform eye, Vector3 point, string label)
    {
        Vector3 dir = (point - eye.position).normalized;
        if (Physics.Raycast(eye.position, dir, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            Debug.Log($"[RAY] {label}: first hit '{hit.collider.name}' at {hit.distance:0.00} m " +
                      $"zone={(hit.collider.GetComponentInParent<IRangedHitZone>() != null)} " +
                      $"damageable={(hit.collider.GetComponentInParent<IDamageable>() != null)}");
        else
            Debug.Log($"[RAY] {label}: nothing");
    }
}
