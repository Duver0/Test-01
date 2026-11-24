using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Slotgun;

/// <summary>
/// Checks the three feel changes against the real input pipeline:
///   · holding shift asks for the sprint speed (keys are queued on the real keyboard device, so the
///     PlayerInput -> OnSprint -> Move() chain is the one under test, not a reflection shortcut)
///   · a jump is high and slow: ~2.6 m of apex over more than a second of airtime
///   · health never creeps back up on its own
/// </summary>
public static class PlayerFeelTest
{
    static double _startedAt;
    static double _stageAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static PlayerController _player;
    static PlayerHealth _health;
    static CharacterController _cc;
    static Keyboard _kb;

    static float _sprintSpeed;
    static float _walkSpeed;
    static bool _sprintFlag;

    static float _groundY;
    static float _peakY;
    static double _jumpAt;
    static float _airTime;

    static float _hurtTo;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stageAt = _startedAt;
        _stage = 0;
        _done = false;
        _fails = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[FEEL] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[FEEL] " + s);

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
        Release();
        Debug.Log($"[FEEL] DONE · {_fails} FAIL");
    }

    static void Keys(params Key[] keys)
    {
        if (_kb == null) return;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
    }

    static void Release() => Keys();

    static float HorizontalSpeed()
    {
        Vector3 v = _cc.velocity;
        v.y = 0f;
        return v.magnitude;
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 45)
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
                _player = director.player;
                _health = director.playerHealth;
                _cc = _player.GetComponent<CharacterController>();
                _kb = Keyboard.current;

                if (_health == null || _cc == null || _kb == null) return;
                if (!_cc.isGrounded) return;

                Note($"keyboard '{_kb.displayName}' source={_kb.deviceId}");

                // Open ground, facing away from the boss, so the speed run cannot hit a wall.
                Vector3 forward = Vector3.forward;
                if (director.CurrentBoss is Component boss)
                {
                    Vector3 away = director.arenaCenter - boss.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.01f) forward = away.normalized;
                }

                _player.Teleport(director.arenaCenter, Quaternion.LookRotation(forward).eulerAngles.y);
                _stageAt = Now;
                _stage = 7;
                return;
            }

            case 7:
            {
                if (Now - _stageAt < 0.35) return;
                _stageAt = Now;
                _stage = 1;
                return;
            }

            case 1:
            {
                // Walk forward, then forward + shift, and let the speed settle.
                Keys(Key.W, Key.LeftShift);
                if (Now - _stageAt < 0.9) return;

                _sprintFlag = (bool)typeof(PlayerController)
                    .GetField("_sprint", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(_player);
                _sprintSpeed = HorizontalSpeed();

                Check(_sprintFlag, "shift reaches the controller as a sprint request");
                Check(_sprintSpeed > _player.walkSpeed + 1f,
                      $"shift actually moves faster ({_sprintSpeed:0.0} m/s vs {_player.walkSpeed:0.0} walking)");

                _stageAt = Now;
                _stage = 2;
                return;
            }

            case 2:
            {
                Keys(Key.W);                                  // release shift, keep walking
                if (Now - _stageAt < 0.9) return;

                _walkSpeed = HorizontalSpeed();
                Check(_walkSpeed < _sprintSpeed - 1f,
                      $"letting shift go drops back to a walk ({_walkSpeed:0.0} m/s)");

                Release();
                _stageAt = Now;
                _stage = 3;
                return;
            }

            case 3:
            {
                if (Now - _stageAt < 0.5) return;             // come to a stop

                _groundY = _player.transform.position.y;
                _peakY = _groundY;
                _jumpAt = Now;
                _airTime = 0f;

                Keys(Key.Space);                              // one press, released next frame
                _stage = 4;
                return;
            }

            case 4:
            {
                Release();

                float y = _player.transform.position.y;
                if (y > _peakY) _peakY = y;

                double elapsed = Now - _jumpAt;
                if (_cc.isGrounded && elapsed > 0.25)
                {
                    _airTime = (float)elapsed;
                    float apex = _peakY - _groundY;

                    Check(apex > 2.0f, $"the jump is high ({apex:0.00} m of apex)");
                    Check(_airTime > 1.15f, $"the jump is slow ({_airTime:0.00} s in the air)");

                    _stageAt = Now;
                    _stage = 5;
                    return;
                }

                if (elapsed > 6)
                {
                    Check(false, $"the jump came back down (apex {_peakY - _groundY:0.00} m after {elapsed:0.0} s)");
                    _stageAt = Now;
                    _stage = 5;
                }
                return;
            }

            case 5:
            {
                if (Now - _stageAt < 0.4) return;

                _health.TakeDamage(30f, _player.transform.position, _player.transform.position + Vector3.forward);
                _hurtTo = _health.Current;
                Note($"hurt to {_hurtTo:0.0}, watching for regeneration");
                _stageAt = Now;
                _stage = 6;
                return;
            }

            case 6:
            {
                if (Now - _stageAt < 8.5) return;

                Check(_health.Current <= _hurtTo + 0.01f,
                      $"life does not regenerate after 8 s ({_hurtTo:0.0} -> {_health.Current:0.0})");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
