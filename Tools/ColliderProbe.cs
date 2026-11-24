using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>Who owns the 0 m hit in front of the player's eye? Lists the player's colliders and the
/// first thing a shot raycast runs into.</summary>
public static class ColliderProbe
{
    public static void Execute()
    {
        void L(string k, string v) => Debug.Log($"[COL] {k} = {v}");

        var d = GameDirector.Instance;
        if (d == null || d.player == null) { L("player", "missing"); return; }

        Vector3 eye = d.player.Eye.position;
        L("eye", $"{eye}  forward={d.player.Eye.forward}");

        var cols = d.player.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            L("collider", $"{c.transform.name}: {c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} " +
                          $"layer={LayerMask.LayerToName(c.gameObject.layer)} bounds={c.bounds} " +
                          $"eyeInside={c.bounds.Contains(eye)}");
        }

        if (Physics.Raycast(eye, d.player.Eye.forward, out RaycastHit fwd, 40f, ~0, QueryTriggerInteraction.Ignore))
            L("forwardHit", $"'{fwd.collider.name}' at {fwd.distance:0.00} m trigger={fwd.collider.isTrigger}");
        else L("forwardHit", "nothing");

        var boss = d.CurrentBoss as BossController;
        if (boss == null) { L("boss", "missing"); return; }

        var zone = boss.GetComponentInChildren<BossWeakPoint>();
        if (zone == null) { L("zone", "missing"); return; }

        Vector3 center = zone.GetComponent<Collider>().bounds.center;
        Vector3 dir = (center - eye).normalized;
        L("toZone", $"center={center} dist={Vector3.Distance(eye, center):0.0}");

        var hits = Physics.RaycastAll(eye, dir, 60f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length && i < 8; i++)
            L($"rayZone[{i}]", $"'{hits[i].collider.name}' at {hits[i].distance:0.00} m " +
                               $"trigger={hits[i].collider.isTrigger} " +
                               $"zone={(hits[i].collider.GetComponentInParent<IRangedHitZone>() != null)} " +
                               $"damageable={(hits[i].collider.GetComponentInParent<IDamageable>() != null)}");
    }
}
