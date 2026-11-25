using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Runtime probe for the wave/weak-point round: which packs the wave actually spawned, whether the
/// weak points are invisible but still hittable, and whether the shared controller offers the
/// attack triggers and is playing Attack / Tackle right now.
/// </summary>
public static class EnemyProbe
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        var spawner = Object.FindAnyObjectByType<WaveSpawner>();
        sb.Append("wave total=" + (spawner != null ? spawner.Total.ToString() : "?") +
                  " alive=" + (spawner != null ? spawner.Alive.ToString() : "?"));

        var player = Object.FindAnyObjectByType<PlayerController>();
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null)
            sb.Append("  player=" + health.Current.ToString("0.0") + "/" + health.maxHealth.ToString("0"));

        var units = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        var zombies = Object.FindObjectsByType<ZombieEnemyUnit>(FindObjectsSortMode.None);

        var packs = new Dictionary<string, int>();
        int meleeOnly = 0, withRig = 0, attacking = 0, tackling = 0;

        for (int i = 0; i < units.Length; i++)
        {
            if (units[i].meleeOnly) meleeOnly++;

            var anim = units[i].GetComponentInChildren<Animator>(true);
            if (anim == null || anim.runtimeAnimatorController == null) continue;

            withRig++;
            Track(packs, anim);

            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Attack")) attacking++;
        }

        for (int i = 0; i < zombies.Length; i++)
        {
            var anim = zombies[i].GetComponentInChildren<Animator>(true);
            if (anim == null || anim.runtimeAnimatorController == null) continue;

            withRig++;
            Track(packs, anim);

            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Attack")) attacking++;
            if (state.IsName("Tackle")) tackling++;
        }

        sb.Append("\nunits=" + units.Length + " zombies=" + zombies.Length +
                  " meleeOnly=" + meleeOnly + " withRig=" + withRig);
        sb.Append("\nmodel packs=" + packs.Count + " → ");
        foreach (var kv in packs) sb.Append(kv.Key + "×" + kv.Value + "  ");

        sb.Append("\nplaying now: Attack=" + attacking + " Tackle=" + tackling);

        int weak = 0, visible = 0, solid = 0;
        foreach (var w in Object.FindObjectsByType<BossWeakPoint>(FindObjectsSortMode.None))
        {
            weak++;
            if (w.GetComponentsInChildren<Renderer>(true).Length > 0) visible++;

            var col = w.GetComponent<SphereCollider>();
            if (col != null && !col.isTrigger) solid++;
        }

        sb.Append("\nweak points=" + weak + " with a renderer=" + visible + " with a solid collider=" + solid);

        Animator any = null;
        if (units.Length > 0) any = units[0].GetComponentInChildren<Animator>(true);
        else if (zombies.Length > 0) any = zombies[0].GetComponentInChildren<Animator>(true);

        if (any != null && any.runtimeAnimatorController != null)
        {
            bool hasAttack = false, hasTackle = false;
            var ps = any.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == "Attack") hasAttack = true;
                if (ps[i].name == "Tackle") hasTackle = true;
            }
            sb.Append("\ncontroller=" + any.runtimeAnimatorController.name +
                      " Attack=" + hasAttack + " Tackle=" + hasTackle);
        }

        return sb.ToString();
    }

    static void Track(Dictionary<string, int> packs, Animator anim)
    {
        string key = anim.avatar != null ? anim.avatar.name : "no-avatar";

        int n;
        if (packs.TryGetValue(key, out n)) packs[key] = n + 1;
        else packs[key] = 1;
    }
}
