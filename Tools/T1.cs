using System.Text;
using UnityEngine;

public static class T1
{
    static Slotgun.GameDirector D => Slotgun.GameDirector.Instance;

    static int ProjCount() => Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None).Length;

    public static string Execute()
    {
        var d = D;
        var sb = new StringBuilder();
        sb.AppendLine($"proj={ProjCount()}");
        sb.AppendLine($"phase={d.Phase} round={d.Round} money={d.Money} grants={d.weapon.Grants.Count}");
        sb.AppendLine($"stats {d.weapon.Stats.damage:0.0} {d.weapon.Stats.magazine}");
        sb.AppendLine($"hp {d.playerHealth.Current:0}/{d.playerHealth.maxHealth}");
        sb.AppendLine($"bossCost={d.CurrentBossConfig.budgetCost} sec={d.CurrentBossConfig.hasSecondary}");
        return sb.ToString();
    }
}
