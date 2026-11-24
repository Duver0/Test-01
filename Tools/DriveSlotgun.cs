using System.Text;
using UnityEngine;

public static class DriveSlotgun
{
    static Slotgun.GameDirector D => Slotgun.GameDirector.Instance;

    static string State(string tag)
    {
        var d = D;
        if (d == null) return $"[{tag}] NO DIRECTOR";
        var boss = d.CurrentBoss;
        string bossInfo = boss != null
            ? $"boss={boss.DisplayName} hp={boss.Current:0}/{boss.MaxHealth:0} phase={boss.Phase} pos={boss.transform.position}"
            : "boss=none";
        string proj = Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None).Length.ToString();
        return $"[{tag}] phase={d.Phase} round={d.Round} money={d.Money} {bossInfo} projectiles={proj} " +
               $"grants={d.weapon?.Grants.Count} dmg={d.weapon?.Stats.damage:0.0} " +
               $"playerHp={d.playerHealth?.Current:0}";
    }

    public static string Spin()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            var r = D.machine.TrySpin(Slotgun.RiskTier.Risky);
            sb.AppendLine($"[spin{i}] valid={r.valid} success={r.success} msg={r.message}");
        }
        var direct = D.machine.TryDirectPurchase(Slotgun.WeaponStat.Damage);
        sb.AppendLine($"[direct] valid={direct.valid} msg={direct.message}");
        sb.AppendLine(State("afterSpin"));
        sb.AppendLine($"stats dmg={D.weapon.Stats.damage:0.0} rate={D.weapon.Stats.fireRate:0.0} mag={D.weapon.Stats.magazine} recoil={D.weapon.Stats.recoil:0.00} acc={D.weapon.Stats.accuracy:0.00} range={D.weapon.Stats.range:0}");
        return sb.ToString();
    }

    public static string Fight()
    {
        D.BeginFight();
        return State("fight");
    }

    public static string Probe() => State("probe");

    public static string DamageBoss()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "[damage] no boss";
        // knock it to ~55% so phase 2 should trigger on the next Update
        float amount = boss.Current - boss.MaxHealth * 0.55f;
        boss.TakeDamage(amount, boss.transform.position, Vector3.zero);
        return State("damage55");
    }

    public static string DamageBossDeep()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "[damage] no boss";
        float amount = boss.Current - boss.MaxHealth * 0.20f;
        boss.TakeDamage(amount, boss.transform.position, Vector3.zero);
        return State("damage20");
    }

    public static string KillBoss()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "[kill] no boss";
        boss.TakeDamage(boss.MaxHealth * 2f, boss.transform.position, Vector3.zero);
        return State("bossKilled");
    }

    public static string KillPlayer()
    {
        var ph = D.playerHealth;
        if (ph == null) return "[killplayer] no health";
        ph.TakeDamage(9999f, Vector3.zero, Vector3.zero);
        return State("playerKilled");
    }
}
