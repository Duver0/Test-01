using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// The shot matrix, on the boss the director really spawned and through the real trigger (Weapon.TryFire,
/// the same entry point the mouse uses — no synthetic RangedHit calls):
///   · from 10 m, the circled plate takes exactly double and switches the speed surge on
///   · the same trigger, aimed low on the same body, takes plain damage
/// The player is topped up every tick, because a boss left alone at 10 m kills them and a dead player
/// has the weapon disabled, which would stall the run rather than test anything.
/// </summary>
public static class ShotMatrixTest
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
    static float _damage;
    static float _before;
    static float _standoff;
    static string _leg = "";

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
        Debug.Log("[SHOTMATRIX] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[SHOTMATRIX] " + s);

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
        Debug.Log($"[SHOTMATRIX] DONE · {_fails} FAIL");
    }

    static void Deepen()
    {
        var director = GameDirector.Instance;
        if (director != null && director.playerHealth != null) director.playerHealth.Heal(50f);
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 60)
        {
            Note($"FAIL · timeout at stage {_stage} ({_leg})");
            Finish();
            return;
        }

        var director = GameDirector.Instance;
        if (director == null || director.player == null) return;

        Deepen();

        _boss = director.CurrentBoss as BossController;
        if (_boss == null) return;

        switch (_stage)
        {
            case 0:
            {
                _weapon = director.weapon;
                if (_weapon == null) return;

                _zone = _boss.GetComponentInChildren<BossWeakPoint>();
                if (_zone == null || _zone.GetComponent<Collider>() == null)
                {
                    Check(false, "the live boss carries a collidered weak point");
                    Finish();
                    return;
                }

                // Stand inside the pistol's reach, on the side the player already occupies.
                _standoff = Mathf.Min(10f, _weapon.Stats.range * 0.5f);
                Vector3 away = director.player.transform.position - _boss.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                away.Normalize();

                Vector3 stand = _boss.transform.position + away * _standoff;
                stand.y = director.player.transform.position.y;
                director.player.Teleport(stand, Quaternion.LookRotation(-away).eulerAngles.y);

                _stageAt = Now;
                _stage = 1;
                return;
            }

            case 1:
            {
                if (Now - _stageAt < 0.15) return;          // let the teleport settle

                _eye = director.player.Eye;
                float distance = Vector3.Distance(_eye.position, _boss.transform.position);
                if (distance < _standoff * 0.6f) return;

                Vector3 center = _zone.GetComponent<Collider>().bounds.center;
                if (!Preview(center, out string what))
                {
                    if (++_aimTries > 300)
                    {
                        Check(false, $"the circle is exposed from {distance:0.0} m (first hit: {what})");
                        Finish();
                    }
                    return;
                }

                Note($"circle exposed from {distance:0.0} m at {what}");
                _leg = "circle";
                _damage = _weapon.Stats.damage;
                _before = _boss.Current;
                _weapon.RefillAmmo();

                _eye.LookAt(center);
                if (!_weapon.TryFire()) return;

                _stage = 2;
                return;
            }

            case 2:
            {
                float lost = _before - _boss.Current;
                Check(Mathf.Abs(lost - 2f * _damage) < 0.6f,
                      $"a live trigger pull on the circle takes 2x ({lost:0.0} from a {_damage:0.0} shot)");
                Check(_boss.WeakSurgeActive && _boss.SpeedMultiplier >= 2.49f,
                      $"that shot surges the boss to x{_boss.SpeedMultiplier:0.00} for 5 s");

                _stageAt = Now;
                _stage = 3;
                return;
            }

            case 3:
            {
                if (Now - _stageAt < 0.45) return;          // clear the fire-rate lockout

                // Above the plate (which sits near y 1.55 on the visual) but still on the same capsule.
                var capsule = _boss.GetComponent<Collider>();
                float top = capsule != null ? capsule.bounds.max.y : _boss.transform.position.y + 2.4f;
                Vector3 high = _boss.transform.position + Vector3.up * Mathf.Min(2.6f, top - 0.4f);
                if (!Preview(high, out string what)) { Note($"nothing under the high aim point ({what})"); return; }

                Note($"body exposed at {what}");
                _leg = "body";
                _damage = _weapon.Stats.damage;
                _before = _boss.Current;

                _eye.LookAt(high);
                if (!_weapon.TryFire()) return;

                _stage = 4;
                return;
            }

            case 4:
            {
                float lost = _before - _boss.Current;
                Check(Mathf.Abs(lost - _damage) < 0.6f,
                      $"the same trigger aimed at the body takes plain damage ({lost:0.0})");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }

    /// <summary>Raycast the way FirePellet now does — nearest hit that is not the shooter — and describe it.</summary>
    static bool Preview(Vector3 worldPoint, out string what)
    {
        what = "nothing";
        Vector3 origin = _eye.position;
        Vector3 dir = (worldPoint - origin).normalized;

        var all = Physics.RaycastAll(origin, dir, _weapon.Stats.range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));

        Collider first = null;
        float nearest = float.MaxValue;
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i].collider;
            if (c == null) continue;
            if (GameDirector.Instance != null && GameDirector.Instance.player != null &&
                c.transform.IsChildOf(GameDirector.Instance.player.transform)) continue;
            if (all[i].distance >= nearest) continue;
            nearest = all[i].distance;
            first = c;
        }

        if (first == null) return false;

        var zone = first.GetComponentInParent<IRangedHitZone>();
        var body = first.GetComponentInParent<IDamageable>();

        what = $"'{first.name}' at {nearest:0.0} m" +
               (zone != null ? " [weak point]" : body != null ? " [damageable body]" : " [neither]");

        bool onZone = zone == _zone;
        bool onBody = zone == null && body != null;
        bool aimingAtZone = Vector3.Distance(worldPoint, _zone.GetComponent<Collider>().bounds.center) < 0.5f;

        return aimingAtZone ? onZone : onBody;
    }
}
