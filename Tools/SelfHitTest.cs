using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// Regression check for the self-hit fix: the eye sits inside the player's own CharacterController, so a
/// downward shot used to resolve against the shooter and damage them. Three shots aimed at the floor in
/// front of the player must cost the player nothing. The player is parked away from the boss first, so
/// nothing else can take the health in the same window.
/// </summary>
public static class SelfHitTest
{
    static double _startedAt;
    static double _stageAt;
    static int _stage;
    static bool _done;
    static int _fails;
    static int _shots;

    static PlayerHealth _health;
    static Weapon _weapon;
    static Transform _eye;
    static float _before;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stageAt = _startedAt;
        _stage = 0;
        _done = false;
        _fails = 0;
        _shots = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[SELFHIT] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[SELFHIT] " + s);

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
        Debug.Log($"[SELFHIT] DONE · {_fails} FAIL");
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

        switch (_stage)
        {
            case 0:
            {
                _weapon = director.weapon;
                _health = director.playerHealth;
                if (_weapon == null || _health == null) return;
                if (!(director.CurrentBoss is Component bossBody)) return;

                // Park the player far from the boss so its melee cannot pollute the measurement.
                Vector3 pos = director.player.transform.position + Vector3.up * 0.2f;
                Vector3 away = pos - bossBody.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                away.Normalize();

                Vector3 stand = bossBody.transform.position + away * 30f;
                stand.y = director.player.transform.position.y;
                director.player.Teleport(stand, Quaternion.LookRotation(-away).eulerAngles.y);

                _stageAt = Now;
                _stage = 1;
                return;
            }

            case 1:
            {
                if (Now - _stageAt < 0.2) return;

                _health.Heal(_health.maxHealth);
                _weapon.RefillAmmo();
                _eye = director.player.Eye;
                _before = _health.Current;
                _stageAt = Now;
                _stage = 2;
                return;
            }

            case 2:
            {
                // Straight down and slightly forward: the ray leaves the eye inside the controller.
                _eye.LookAt(_eye.position + _eye.forward * 2f + Vector3.down * 6f);
                if (_weapon.TryFire())
                {
                    _shots++;
                    Note($"shot {_shots} fired downward");
                }

                if (_shots >= 3)
                {
                    _stage = 3;
                    return;
                }

                _stageAt = Now;
                _stage = 4;
                return;
            }

            case 4:
            {
                if (Now - _stageAt < 0.35) return;   // fire-rate lockout between shots
                _stage = 2;
                return;
            }

            case 3:
            {
                float lost = _before - _health.Current;
                Check(lost <= 0.01f, $"firing at your own feet costs the shooter nothing ({lost:0.0} lost)");
                Check(_health.Current > 0f, $"the player survives the volley ({_health.Current:0.0} HP)");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
