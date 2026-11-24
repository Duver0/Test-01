using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>One-shot snapshot of the live run, used to see why the shot harness stopped progressing.</summary>
public static class StateProbe
{
    public static void Execute()
    {
        void L(string k, string v) => Debug.Log($"[STATE] {k} = {v}");

        L("isPlaying", Application.isPlaying.ToString());
        L("isPaused", EditorApplication.isPaused.ToString());

        var d = GameDirector.Instance;
        if (d == null) { L("director", "null"); return; }
        L("director", "ok");
        L("phase", d.Phase.ToString());
        L("round", d.Round.ToString());
        L("money", d.Money.ToString());
        L("currentBoss", d.CurrentBoss == null ? "null" : d.CurrentBoss.GetType().Name);
        L("player", d.player == null ? "null" : d.player.name);
        L("loadout", d.loadout == null ? "null" : d.loadout.name);
        L("gameplayActive", d.DoorsArmed.ToString());

        if (d.playerHealth != null)
            L("playerHealth", $"{d.playerHealth.Current:0.0}/{d.playerHealth.maxHealth:0.0}");

        if (d.weapon != null)
            L("weapon", $"{d.weapon.name} enabled={d.weapon.enabled} active={d.weapon.isActiveAndEnabled} " +
                        $"ammo={d.weapon.Ammo} spare={d.weapon.SpareMags} " +
                        $"range={d.weapon.Stats.range:0.0} dmg={d.weapon.Stats.damage:0.0}");
        else L("weapon", "null");

        var boss = d.CurrentBoss as BossController;
        if (boss != null)
        {
            var col = boss.GetComponent<Collider>();
            L("bossCollider", col == null ? "none" :
                $"{(col is CapsuleCollider cap ? $"capsule r={cap.radius:0.00} h={cap.height:0.00} c={cap.center}" : col.GetType().Name)} " +
                $"enabled={col.enabled} bounds={col.bounds}");
            L("bossHealth", $"{boss.Current:0.0}/{boss.MaxHealth:0.0} phase={boss.Phase}");
            Vector3 wp = boss.transform.TransformPoint(new Vector3(0f, 1.55f, 1.16f));
            L("weakPointGuess", $"{wp} dist={Vector3.Distance(wp, d.player.Eye.position):0.0}");
        }
    }
}
