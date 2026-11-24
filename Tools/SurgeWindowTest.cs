using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// The surge is specified as 2.5x for 5 seconds. BossVarietyTest only proves it switches on, so this
/// samples a probe boss (no target, so nothing moves) across the window: still surged just before five
/// seconds, back to its normal speed just after.
/// </summary>
public static class SurgeWindowTest
{
    static double _startedAt;
    static double _hitAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static BossController _probe;
    static float _base;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stage = 0;
        _done = false;
        _fails = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[SURGE] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[SURGE] " + s);

    static void Check(bool ok, string what)
    {
        if (ok) Note("ok   · " + what);
        else { _fails++; Note("FAIL · " + what); }
    }

    static void Finish()
    {
        if (_done) return;
        _done = true;
        EditorApplication.update -= Tick;
        Debug.Log($"[SURGE] DONE · {_fails} FAIL");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 40)
        {
            Note($"FAIL · timeout at stage {_stage}");
            Finish();
            return;
        }

        if (GameDirector.Instance == null) return;

        switch (_stage)
        {
            case 0:
            {
                var cfg = new BossConfig
                {
                    round = 1,
                    family = BossFamily.Single,
                    displayName = "SONDA DE OLEAJE",
                    body = BossBody.SlotGolem,
                    movement = BossMovement.Chase,
                    primaryAttack = BossAttack.AimedVolley,
                    hasSecondary = false,
                    secondaryAttack = BossAttack.AimedVolley,
                    ability = BossAbility.None,
                    modifier = BossModifier.None,
                    budgetCost = 0,
                    maxHealth = 5000f,
                    damageScale = 1f,
                    moveSpeed = 3f,
                    attackCooldown = 3f,
                    visualScale = 1f,
                    primaryColor = Palette.CharacterSecondary,
                    secondaryColor = Palette.Accent
                };

                var go = new GameObject("SurgeProbe");
                go.transform.position = new Vector3(200f, 40f, 0f);
                go.AddComponent<CapsuleCollider>();

                _probe = go.AddComponent<BossController>();
                _probe.Initialize(cfg, null, Vector3.zero, 400f);   // no target: it never moves

                _stage = 1;
                return;
            }

            case 1:
            {
                if (_probe == null) { Check(false, "probe boss alive"); Finish(); return; }

                var zone = _probe.GetComponentInChildren<BossWeakPoint>();
                if (zone == null) { Check(false, "probe has a weak point"); Finish(); return; }

                _base = _probe.SpeedMultiplier;
                Vector3 point = _probe.transform.position + Vector3.forward;
                zone.RangedHit(10f, point, point + Vector3.forward);

                _hitAt = Now;
                Note($"base speed x{_base:0.00}, surge on at x{_probe.SpeedMultiplier:0.00}");
                _stage = 2;
                return;
            }

            case 2:
            {
                double elapsed = Now - _hitAt;
                if (elapsed < 4.6) return;

                Check(_probe.WeakSurgeActive && _probe.SpeedMultiplier >= 2.49f,
                      $"still surging at 4.6 s (x{_probe.SpeedMultiplier:0.00})");
                _stage = 3;
                return;
            }

            case 3:
            {
                double elapsed = Now - _hitAt;
                if (elapsed < 5.4) return;

                Check(!_probe.WeakSurgeActive, "the surge has expired at 5.4 s");
                Check(Mathf.Abs(_probe.SpeedMultiplier - _base) < 0.05f,
                      $"speed returns to x{_base:0.00} (now x{_probe.SpeedMultiplier:0.00})");

                Object.Destroy(_probe.gameObject);
                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
