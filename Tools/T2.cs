using System.Collections;
using System.Reflection;
using UnityEngine;

public static class T2
{
    public static string Execute()
    {
        var d = Slotgun.GameDirector.Instance;
        var boss = d.CurrentBoss;

        foreach (var p in Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None))
            if (p != null) Object.Destroy(p.gameObject);

        var mi = typeof(Slotgun.BossController).GetMethod("ExecuteAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi == null) return "no method";
        var en = (IEnumerator)mi.Invoke(boss, new object[] { Slotgun.BossAttack.RadialBurst });
        boss.StartCoroutine(en);
        int n = Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None).Length;
        return "spawned=" + n;
    }
}
