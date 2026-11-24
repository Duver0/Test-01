using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// Finish what LiveWeakPointShotTest could not: the boss had walked into the player's face (0.8 m), so
/// the body shot started inside its capsule and Unity reported no hit at all. This backs the player off
/// to 14 m, waits for the boss to face them, then pulls the real trigger aimed low on the same body to
/// confirm only the circled plate doubles the damage.
/// </summary>
public static class BodyShotTest
{
    static double _startedAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static BossController _boss;
    static Weapon _weapon;
    static Transform _eye;
    static float _before;
    static float _damage;
    static Vector3 _stand;
    static float _standoff;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stage = 0;
        _done = false;
        _fails = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[BODYSHOT] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[BODYSHOT] " + s);

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
        Debug.Log($"[BODYSHOT] DONE · {_fails} FAIL");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 30)
        {
            Note($"FAIL · timeout at stage {_stage}");
            Finish();
            return;
        }

        var director = GameDirector.Instance;
        if (director == null || director.player == null) return;

        _boss = director.CurrentBoss as BossController;
        if (_boss == null) return;

        switch (_stage)
        {
            case 0:
            {
                _weapon = director.weapon;
                if (_weapon == null) return;

                if (_weapon.Stats.range < 3f)
                {
                    Check(false, $"the pistol has usable range ({_weapon.Stats.range:0.0} m)");
                    Finish();
                    return;
                }

                Vector3 away = director.player.transform.position - _boss.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                away.Normalize();

                // FirePellet raycasts no further than Stats.range, so the standoff has to sit inside it.
                _standoff = Mathf.Clamp(_weapon.Stats.range * 0.6f, 3f, 14f);
                _stand = _boss.transform.position + away * _standoff;
                _stand.y = director.player.transform.position.y;

                director.player.Teleport(_stand, Quaternion.LookRotation(-away).eulerAngles.y);

                _stage = 1;
                return;
            }

            case 1:
            {
                if (!_weapon.isActiveAndEnabled) return;

                _eye = director.player.Eye;
                float distance = Vector3.Distance(_eye.position, _boss.transform.position);
                if (distance > _standoff * 1.25f) return;   // the teleport has not landed yet

                Vector3 aim = _boss.transform.position + Vector3.up * 0.5f;
                _damage = _weapon.Stats.damage;
                _before = _boss.Current;
                _weapon.RefillAmmo();

                _eye.LookAt(aim);
                if (!_weapon.TryFire()) return;

                Note($"fired from {distance:0.0} m (range {_weapon.Stats.range:0.0} m) at a point 0.5 m up the body");
                _stage = 2;
                return;
            }

            case 2:
            {
                float lost = _before - _boss.Current;
                Check(Mathf.Abs(lost - _damage) < 0.6f,
                      $"a shot on the body from range takes normal damage ({lost:0.0} from a {_damage:0.0} shot)");
                Check(!(_boss.WeakSurgeActive && lost > _damage + 0.6f),
                      "the body shot does not double and does not surge");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
