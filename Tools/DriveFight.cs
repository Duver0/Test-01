using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class DriveFight
{
    static Slotgun.GameDirector D => Slotgun.GameDirector.Instance;

    static int ProjCount() => Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None).Length;

    static void ClearProjectiles()
    {
        foreach (var p in Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None))
            if (p != null) Object.Destroy(p.gameObject);
    }

    public static string State()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR (play mode running?)";
        var boss = d.CurrentBoss;
        string b = boss != null
            ? $"boss={boss.DisplayName} hp={boss.Current:0}/{boss.MaxHealth:0} frac={boss.HealthFraction:0.00} phase={boss.Phase} pos={boss.transform.position}\n" +
              $"cfg={boss.config.body}/{boss.config.movement}/{boss.config.primaryAttack}+{boss.config.secondaryAttack}/{boss.config.ability}/{boss.config.modifier} cost={boss.config.budgetCost}"
            : "boss=none";
        string cfg2 = d.CurrentBossConfig != null
            ? $"nextCfg={d.CurrentBossConfig.body}/{d.CurrentBossConfig.movement}/{d.CurrentBossConfig.primaryAttack}/{d.CurrentBossConfig.modifier}"
            : "nextCfg=none";
        return $"phase={d.Phase} round={d.Round}/{d.totalRounds} money={d.Money} reward={d.LastReward}\n{b}\n{cfg2}\n" +
               $"projectiles={ProjCount()} playerHp={d.playerHealth.Current:0}/{d.playerHealth.maxHealth} " +
               $"dmg={d.weapon.Stats.damage:0.0} rate={d.weapon.Stats.fireRate:0.0} mag={d.weapon.Stats.magazine} " +
               $"recoil={d.weapon.Stats.recoil:0.00} acc={d.weapon.Stats.accuracy:0.00} range={d.weapon.Stats.range:0} grants={d.weapon.Grants.Count}";
    }

    public static string Attacks()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        var boss = d.CurrentBoss;
        if (boss == null) return "no boss (call Fight first)";

        var mi = typeof(Slotgun.BossController).GetMethod("ExecuteAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi == null) return "ExecuteAttack not found";

        var sb = new StringBuilder();
        var quiet = new[] { Slotgun.BossAttack.RadialBurst, Slotgun.BossAttack.AimedVolley,
                            Slotgun.BossAttack.GroundSlam, Slotgun.BossAttack.HomingOrb,
                            Slotgun.BossAttack.StompRing };

        foreach (var a in quiet)
        {
            ClearProjectiles();
            int before = ProjCount();
            var en = (IEnumerator)mi.Invoke(boss, new object[] { a });
            boss.StartCoroutine(en);
            int after = ProjCount();
            sb.AppendLine($"{a,-14} spawned={after - before,3}  playerHp={d.playerHealth.Current:0}");
            ClearProjectiles();
        }

        // SpinSpray is a 1.7s sustained spray: sample the first burst, then stop it
        // and restart the natural loop so the fight continues unaffected.
        ClearProjectiles();
        var spray = (IEnumerator)mi.Invoke(boss, new object[] { Slotgun.BossAttack.SpinSpray });
        boss.StartCoroutine(spray);
        sb.AppendLine($"SpinSpray      firstBurst={ProjCount()}  (sustained 1.7s @ 0.085s, ~20 shots)");
        ClearProjectiles();
        boss.StopAllCoroutines();

        var loop = typeof(Slotgun.BossController).GetMethod("AttackLoop", BindingFlags.Instance | BindingFlags.NonPublic);
        if (loop != null) boss.StartCoroutine((IEnumerator)loop.Invoke(boss, null));
        sb.AppendLine("attack loop restarted");

        ClearProjectiles();
        d.playerHealth.ResetHealth();
        sb.AppendLine($"playerHp after={d.playerHealth.Current:0}");
        return sb.ToString();
    }

    public static string To55()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "no boss";
        boss.TakeDamage(boss.Current - boss.MaxHealth * 0.55f, boss.transform.position, Vector3.zero);
        return State();
    }

    public static string To20()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "no boss";
        boss.TakeDamage(boss.Current - boss.MaxHealth * 0.20f, boss.transform.position, Vector3.zero);
        return State();
    }

    public static string Kill()
    {
        var boss = D.CurrentBoss;
        if (boss == null) return "no boss";
        boss.TakeDamage(boss.MaxHealth * 2f, boss.transform.position, Vector3.zero);
        return State();
    }

    public static string Fight()
    {
        D.BeginFight();
        return State();
    }

    public static string Face()
    {
        var d = D;
        var boss = d.CurrentBoss;
        if (boss == null || d.player == null) return "no boss/player";
        var pc = d.player;
        Vector3 from = pc.transform.position;
        Vector3 to = boss.transform.position;
        Vector3 dir = to - from;
        dir.y = 0f;
        float yaw = Quaternion.LookRotation(dir.normalized).eulerAngles.y;
        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        var yawField = typeof(Slotgun.PlayerController).GetField("_yaw", bf);
        var pitchField = typeof(Slotgun.PlayerController).GetField("_aimPitch", bf);
        if (yawField != null) yawField.SetValue(pc, yaw);
        if (pitchField != null) pitchField.SetValue(pc, -4f);
        return $"facing boss yaw={yaw:0} dist={dir.magnitude:0.0}";
    }

    public static string LoopTest()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        var sb = new StringBuilder();

        if (d.Phase != Slotgun.RunPhase.Fight) d.BeginFight();
        sb.AppendLine($"started: phase={d.Phase} boss={(d.CurrentBoss != null ? d.CurrentBoss.DisplayName : "none")} money={d.Money}");

        for (int guard = 0; guard < 6 && d.Phase != Slotgun.RunPhase.RunComplete; guard++)
        {
            if (d.CurrentBoss == null) d.BeginFight();
            var boss = d.CurrentBoss;
            if (boss == null) { sb.AppendLine("!! no boss to kill"); break; }

            sb.AppendLine($"kill round={d.Round} {boss.DisplayName} hp={boss.Current:0} cfg={boss.config.body}/{boss.config.primaryAttack}/{boss.config.modifier}");
            boss.TakeDamage(boss.MaxHealth * 2f, boss.transform.position, Vector3.zero);
            sb.AppendLine($"   -> phase={d.Phase} round={d.Round} money={d.Money} reward={d.LastReward} " +
                          $"next={(d.CurrentBossConfig != null ? d.CurrentBossConfig.body.ToString() : "-")} " +
                          $"playerHp={d.playerHealth.Current:0}");
        }

        sb.AppendLine($"final: phase={d.Phase} round={d.Round}/{d.totalRounds} money={d.Money}");
        return sb.ToString();
    }

    public static string Restart()
    {
        D.Restart();
        return "restart requested";
    }

    public static string KillPlayer()
    {
        var d = D;
        d.playerHealth.ResetHealth();
        d.playerHealth.TakeDamage(9999f, Vector3.zero, Vector3.zero);
        return State();
    }

    public static string Heal()
    {
        D.playerHealth.ResetHealth();
        return State();
    }
}
