using UnityEngine;
using UnityEngine.UI;

public static class ProbeSlotgun
{
    public static string Execute()
    {
        var d = Slotgun.GameDirector.Instance;
        if (d == null) return "[PROBE] GameDirector.Instance is NULL";

        var hud = Object.FindAnyObjectByType<Slotgun.HUD>();
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var weapon = d.weapon;
        var bossCount = Object.FindObjectsByType<Slotgun.BossController>(FindObjectsSortMode.None).Length;
        var textCount = canvas != null ? canvas.GetComponentsInChildren<Text>(true).Length : 0;
        var buttonCount = canvas != null ? canvas.GetComponentsInChildren<Button>(true).Length : 0;

        string stats = weapon != null
            ? $"dmg={weapon.Stats.damage:0.0} rate={weapon.Stats.fireRate:0.0} mag={weapon.Stats.magazine} recoil={weapon.Stats.recoil:0.00} acc={weapon.Stats.accuracy:0.00} range={weapon.Stats.range:0} ammo={weapon.Ammo}"
            : "NO WEAPON";

        string boss = d.CurrentBossConfig != null
            ? $"{d.CurrentBossConfig.displayName} | {d.CurrentBossConfig.Describe()} | hp={d.CurrentBossConfig.maxHealth:0} cost={d.CurrentBossConfig.budgetCost}"
            : "NO CONFIG";

        return "[PROBE] " + string.Join("\n",
            $"phase={d.Phase} round={d.Round}/{d.totalRounds} money={d.Money}",
            $"bossConfig={boss}",
            $"bossesAlive={bossCount} bossSpawn={(d.bossSpawn != null ? d.bossSpawn.position.ToString() : "null")}",
            $"weapon={stats} grants={weapon?.Grants.Count}",
            $"hud={(hud != null)} canvas={(canvas != null)} texts={textCount} buttons={buttonCount}",
            $"playerEnabled={(d.player != null && d.player.enabled)} weaponEnabled={(weapon != null && weapon.enabled)}",
            $"cam={(d.player != null && d.player.cam != null ? d.player.cam.transform.position.ToString() : "null")}");
    }
}
