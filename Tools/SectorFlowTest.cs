using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;

public static class SectorFlowTest
{
    static double _stepAt;
    static double _lastNoteAt;
    static int _lastRound;
    static bool _prepActionsDone;
    static bool _finished;

    public static void Execute()
    {
        _stepAt = EditorApplication.timeSinceStartup;
        _lastNoteAt = _stepAt;
        _lastRound = 0;
        _prepActionsDone = false;
        _finished = false;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[SLOTTEST] harness armed");
    }

    static double Now => EditorApplication.timeSinceStartup;

    static void Note(string s) => Debug.Log("[SLOTTEST] " + s);

    static string Scenes()
    {
        var names = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) names.Add(SceneManager.GetSceneAt(i).name);
        return string.Join(" + ", names);
    }

    static DoorPortal Door(DoorWay way)
    {
        foreach (var door in Object.FindObjectsByType<DoorPortal>(FindObjectsSortMode.None))
            if (door != null && door.way == way) return door;
        return null;
    }

    static void Finish()
    {
        if (_finished) return;
        _finished = true;
        EditorApplication.update -= Tick;
        Debug.Log("[SLOTTEST] DONE");
    }

    static void Tick()
    {
        if (_finished) return;

        var d = GameDirector.Instance;
        if (d == null)
        {
            if (Now - _stepAt > 25) { Note("FAIL: GameDirector never appeared"); Finish(); }
            return;
        }

        if (Now - _stepAt > 40)
        {
            Note($"FAIL: stuck in {d.Phase} round {d.Round}");
            Finish();
            return;
        }

        switch (d.Phase)
        {
            case RunPhase.Loading:
                if (Now - _lastNoteAt > 3)
                {
                    _lastNoteAt = Now;
                    Note($"loading '{d.LoadLabel}' {d.LoadProgress:P0} | scenes: {Scenes()}");
                }
                break;

            case RunPhase.Fight:
                _prepActionsDone = false;
                if (d.CurrentBoss != null)
                {
                    if (_lastRound != d.Round)
                    {
                        _lastRound = d.Round;
                        Note($"FIGHT sector {d.Round} '{d.SectorName}' vs {d.CurrentBossConfig.displayName} | scenes: {Scenes()}");
                        _stepAt = Now;
                    }
                    else if (Now - _stepAt > 1.0)
                    {
                        d.CurrentBoss.TakeDamage(999999f, d.CurrentBoss.transform.position, Vector3.zero);
                        _stepAt = Now;
                    }
                }
                else
                {
                    var door = Door(DoorWay.ArenaExit);
                    if (door == null) { Note("FAIL: arena has no exit door"); Finish(); return; }

                    if (!door.IsOpen) { Note("FAIL: door stayed shut after the boss died"); Finish(); return; }

                    if (Now - _lastNoteAt > 2.5)
                    {
                        _lastNoteAt = Now;
                        Note($"boss down · door open · scenes: {Scenes()}");
                    }

                    if (Now - _stepAt > 0.4)
                    {
                        _stepAt = Now;
                        Vector3 p = door.transform.position;
                        d.player.Teleport(new Vector3(p.x, 0.2f, p.z - 2.5f), 0f);
                    }
                }
                break;

            case RunPhase.Prep:
                if (!_prepActionsDone)
                {
                    _prepActionsDone = true;
                    Note($"PREP after sector {d.Round} | money ${d.Money} | scenes: {Scenes()}");

                    d.playerHealth.TakeDamage(45f, Vector3.zero, Vector3.zero);
                    float before = d.playerHealth.Current;
                    d.OpenBetting();
                    Note($"  · heal buy -> '{d.TryBuyHeal()}' hp {before:0} -> {d.playerHealth.Current:0}");
                    d.weapon.TryFire(); d.weapon.TryFire(); d.weapon.TryFire();
                    Note($"  · ammo buy -> '{d.TryBuyAmmo()}' ammo {d.weapon.Ammo}/{d.weapon.Stats.magazine}");
                    var spin = d.machine != null ? d.machine.TrySpin(RiskTier.Risky) : default;
                    Note($"  · spin -> '{spin.message}' | money ${d.Money}");
                    d.CloseBetting();
                    _stepAt = Now;
                }

                var exit = Door(DoorWay.PrepExit);
                if (exit == null) { Note("FAIL: vestibule has no exit door"); Finish(); return; }

                if (Now - _lastNoteAt > 3)
                {
                    _lastNoteAt = Now;
                    Note($"vestibule · exit open: {exit.IsOpen} | scenes: {Scenes()}");
                }

                if (Now - _stepAt > 0.5)
                {
                    _stepAt = Now;
                    Vector3 p = exit.transform.position;
                    d.player.Teleport(new Vector3(p.x, 0.2f, p.z - 2.5f), 0f);
                }
                break;

            case RunPhase.RunComplete:
                Note($"RUN COMPLETE after {d.Round} sectors | money ${d.Money} | scenes: {Scenes()}");
                Finish();
                break;

            case RunPhase.Dead:
                Note($"FAIL: player died in sector {d.Round}");
                Finish();
                break;
        }
    }
}
