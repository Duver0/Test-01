using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// Focused runtime check for the ammo economy and the three weapon slots:
///   · 15 rounds in the magazine, 3 spare magazines, 4 magazines total
///   · firing drains a magazine, reloading spends a spare, running dry hands you the fists
///   · slot 3 with no weapon selected resolves to the fists, slot 2 goes back to the pistol
///   · fists actually damage the boss
///   · each third of boss health drops ammo packs and turns on the retaliation burst
/// </summary>
public static class WeaponAmmoTest
{
    static double _startedAt;
    static double _stepAt;
    static double _lastDrainLog;
    static int _stage;
    static bool _done;

    static int _dropsBefore = -1;
    static int _dropsMid = -1;
    static float _bossHealthBeforePunch;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stepAt = _startedAt;
        _lastDrainLog = _startedAt;
        _stage = 0;
        _done = false;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[WPNTEST] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;

    static void Note(string s) => Debug.Log("[WPNTEST] " + s);

    static void Finish()
    {
        if (_done) return;
        _done = true;
        EditorApplication.update -= Tick;
        Debug.Log("[WPNTEST] DONE");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 90)
        {
            Note($"FAIL: timeout at stage {_stage}");
            Finish();
            return;
        }

        var d = GameDirector.Instance;
        if (d == null || d.loadout == null || d.weapon == null)
        {
            if (Now - _startedAt > 25) { Note("FAIL: director/loadout never appeared"); Finish(); }
            return;
        }

        if (d.Phase == RunPhase.Dead)
        {
            Note($"FAIL: player died at stage {_stage}");
            Finish();
            return;
        }

        if (d.Phase != RunPhase.Fight || d.CurrentBoss == null) return;

        // keep the tester alive: the boss is allowed to shoot while we exercise the weapons
        if (d.playerHealth != null && d.playerHealth.Current < 70f) d.playerHealth.Heal(200f);

        switch (_stage)
        {
            case 0:
                Note($"start: mag {d.weapon.Ammo}/{d.weapon.Stats.magazine} · spare {d.weapon.SpareMags} · " +
                     $"reserve rounds {d.weapon.ReserveRounds} · slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}'");

                if (d.weapon.Ammo != 15) Note($"FAIL: magazine is {d.weapon.Ammo}, expected 15");
                if (d.weapon.SpareMags != 3) Note($"FAIL: spares are {d.weapon.SpareMags}, expected 3");

                // face away from the boss so the drain does not kill it
                d.player.Teleport(new Vector3(0f, 0.2f, -8f), 180f);
                _stepAt = Now;
                _stage = 1;
                break;

            case 1:
                // drain all four magazines; TryFire is rate limited by the weapon itself
                d.weapon.TryFire();

                if (Now - _lastDrainLog > 1.5)
                {
                    _lastDrainLog = Now;
                    Note($"draining: mag {d.weapon.Ammo}/{d.weapon.Stats.magazine} · spare {d.weapon.SpareMags} · " +
                         $"reloading {d.weapon.IsReloading} · slot {d.loadout.CurrentSlot}");
                }

                if (!d.weapon.HasAnyAmmo)
                {
                    Note($"dry: mag {d.weapon.Ammo} · spare {d.weapon.SpareMags} · hasAmmo {d.weapon.HasAnyAmmo}");
                    _stage = 2;
                    _stepAt = Now;
                }
                break;

            case 2:
                // pulling the trigger with nothing left must hand over the fists
                d.loadout.SetTrigger(true);
                d.loadout.SetTrigger(false);

                Note($"after dry trigger: slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}' melee {d.loadout.Current.IsMelee}");
                if (d.loadout.CurrentSlot != 1 || !d.loadout.Current.IsMelee)
                    Note("FAIL: dry trigger did not fall back to the fists");

                _stage = 3;
                _stepAt = Now;
                break;

            case 3:
                // slot 3 is empty: it must resolve to the fists, then 2 goes back to the pistol
                d.loadout.SelectSlot(3);
                Note($"press 3 -> slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}' melee {d.loadout.Current.IsMelee}");
                if (d.loadout.CurrentSlot != 1 || !d.loadout.Current.IsMelee)
                    Note("FAIL: slot 3 with no weapon did not resolve to the fists");

                d.loadout.SelectSlot(2);
                Note($"press 2 -> slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}' melee {d.loadout.Current.IsMelee}");
                if (d.loadout.CurrentSlot != 2 || d.loadout.Current.IsMelee)
                    Note("FAIL: slot 2 did not return the sidearm");

                d.loadout.SelectSlot(1);
                Note($"press 1 -> slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}' melee {d.loadout.Current.IsMelee}");
                if (d.loadout.CurrentSlot != 1 || !d.loadout.Current.IsMelee)
                    Note("FAIL: slot 1 did not select the fists");

                // give ammo back through the same path the boss drops use
                bool added = d.weapon.AddSpareMags(2);
                Note($"pickup path: +2 spares -> spare {d.weapon.SpareMags} (accepted {added})");

                _stepAt = Now;
                _stage = 4;
                break;

            case 4:
            {
                var boss = d.CurrentBoss;
                Vector3 toPlayer = (d.player.transform.position - boss.transform.position);
                toPlayer.y = 0f;
                if (toPlayer.magnitude > 2.4f)
                {
                    Vector3 spot = boss.transform.position + toPlayer.normalized * 1.9f;
                    d.player.Teleport(new Vector3(spot.x, 0.2f, spot.z),
                                      Quaternion.LookRotation(boss.transform.position - spot).eulerAngles.y);
                }

                if (_bossHealthBeforePunch <= 0f)
                {
                    _bossHealthBeforePunch = boss.Current;
                    Note($"punch test: boss at {boss.Current:0.0}/{boss.MaxHealth:0.0}");
                }

                bool hit = d.loadout.fists.Punch();
                if (hit || boss.Current < _bossHealthBeforePunch - 0.01f)
                {
                    Note($"punch landed: boss {_bossHealthBeforePunch:0.0} -> {boss.Current:0.0}");
                    _dropsBefore = Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None).Length;
                    Note($"ammo packs on the floor before the threshold: {_dropsBefore}");
                    _stepAt = Now;
                    _stage = 5;
                }
                else if (Now - _stepAt > 6)
                {
                    Note("FAIL: fists never damaged the boss (or never landed)");
                    Finish();
                }
                break;
            }

            case 5:
            {
                var boss = d.CurrentBoss;
                // walk the boss down to just past the two-third mark
                float target = boss.MaxHealth * 0.60f;
                float damage = boss.Current - target;
                if (damage > 0f) boss.TakeDamage(damage, boss.transform.position, d.player.transform.position);

                int packs = Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None).Length;
                Note($"crossed 2/3: boss {boss.Current:0.0}/{boss.MaxHealth:0.0} · packs {packs} · InBurst {boss.InBurst}");
                if (packs <= 0) Note("FAIL: no ammo dropped at the two-thirds threshold");
                if (!boss.InBurst) Note("FAIL: no retaliation burst at the two-thirds threshold");
                _dropsMid = packs;

                _stepAt = Now;
                _stage = 6;
                break;
            }

            case 6:
            {
                var boss = d.CurrentBoss;
                float target = boss.MaxHealth * 0.28f;
                float damage = boss.Current - target;
                if (damage > 0f) boss.TakeDamage(damage, boss.transform.position, d.player.transform.position);

                int packs = Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None).Length;
                Note($"crossed 1/3: boss {boss.Current:0.0}/{boss.MaxHealth:0.0} · packs {packs} · InBurst {boss.InBurst}");
                if (packs <= _dropsMid) Note("FAIL: no new ammo at the one-third threshold");

                Note("all checks executed");
                Finish();
                break;
            }
        }
    }
}
