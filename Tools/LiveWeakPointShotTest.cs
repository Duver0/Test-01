using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// The end-to-end proof: aim the player's camera at the weak point of the boss the director actually
/// spawned and pull the real weapon trigger. No synthetic RangedHit calls — this fires Weapon.TryFire,
/// which is the same entry point the mouse uses, so it covers the eye-raycast, the IRangedHitZone
/// routing in FirePellet, the doubled damage and the speed surge in one shot. A second shot is then
/// aimed low on the same body to prove normal damage still applies anywhere but the circle.
/// </summary>
public static class LiveWeakPointShotTest
{
    static double _startedAt;
    static double _stageAt;
    static int _stage;
    static bool _done;
    static int _fails;
    static int _aimTries;

    static BossController _boss;
    static BossWeakPoint _zone;
    static Weapon _weapon;
    static Transform _eye;
    static Vector3 _center;
    static float _damage;
    static float _before;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stageAt = _startedAt;
        _stage = 0;
        _done = false;
        _fails = 0;
        _aimTries = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[LIVESHOT] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[LIVESHOT] " + s);

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
        Debug.Log($"[LIVESHOT] DONE · {_fails} FAIL");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 40)
        {
            Note($"FAIL · timeout at stage {_stage} after {_aimTries} aim tries");
            Finish();
            return;
        }

        var director = GameDirector.Instance;
        if (director == null || director.player == null) return;

        switch (_stage)
        {
            case 0:
            {
                _boss = director.CurrentBoss as BossController;
                _weapon = director.weapon;
                if (_boss == null || _weapon == null || !_weapon.isActiveAndEnabled) return;

                _zone = _boss.GetComponentInChildren<BossWeakPoint>();
                if (_zone == null)
                {
                    Check(false, "the live boss carries a weak point");
                    Finish();
                    return;
                }

                var col = _zone.GetComponent<Collider>();
                if (col == null)
                {
                    Check(false, "the live weak point has a collider");
                    Finish();
                    return;
                }

                _eye = director.player.Eye;
                _center = col.bounds.center;

                // The boss turns to face the player, so the circle has to come into view on its own.
                if (Physics.Raycast(_eye.position, (_center - _eye.position).normalized, out RaycastHit los,
                                    200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (los.collider.GetComponentInParent<IRangedHitZone>() != _zone)
                    {
                        if (++_aimTries > 600)
                        {
                            Check(false, $"the circle is exposed to the player's eye (blocked by '{los.collider.name}')");
                            Finish();
                        }
                        return;
                    }
                }

                Check(true, $"the circle is exposed to the player's eye at {Vector3.Distance(_eye.position, _center):0.0} m");

                _weapon.RefillAmmo();
                _damage = _weapon.Stats.damage;
                _before = _boss.Current;

                _eye.LookAt(_center);
                if (!_weapon.TryFire()) return;

                _stage = 1;
                return;
            }

            case 1:
            {
                float lost = _before - _boss.Current;
                Check(Mathf.Abs(lost - 2f * _damage) < 0.6f,
                      $"a live trigger pull on the circle takes 2x ({lost:0.0} from a {_damage:0.0} shot)");
                Check(_boss.WeakSurgeActive && _boss.SpeedMultiplier >= 2.49f,
                      $"the same shot surges the boss to x{_boss.SpeedMultiplier:0.00}");

                _stageAt = Now;
                _stage = 2;
                return;
            }

            case 2:
            {
                if (Now - _stageAt < 0.4) return;   // clear the pistol's fire-rate lockout

                _before = _boss.Current;
                _eye.LookAt(_boss.transform.position + Vector3.up * 0.4f);
                if (!_weapon.TryFire()) return;

                _stage = 3;
                return;
            }

            case 3:
            {
                float lost = _before - _boss.Current;
                Check(Mathf.Abs(lost - _damage) < 0.6f,
                      $"a shot on the body still takes normal damage ({lost:0.0})");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
