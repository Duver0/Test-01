using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Slotgun;

/// <summary>
/// Why did the sprint stay on after shift was released? Reads the Sprint action and the controller's
/// own flag side by side, on press and on release.
/// </summary>
public static class SprintReleaseTest
{
    static double _startedAt;
    static double _stageAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static PlayerController _player;
    static PlayerInput _input;
    static Keyboard _kb;
    static FieldInfo _sprintField;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stageAt = _startedAt;
        _stage = 0;
        _done = false;
        _fails = 0;
        _player = null;
        _input = null;
        _kb = null;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[SPRINT] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[SPRINT] " + s);

    static void Check(bool ok, string what)
    {
        if (ok) Note("ok   - " + what);
        else { _fails++; Note("FAIL - " + what); }
    }

    static void Finish()
    {
        if (_done) return;
        _done = true;
        EditorApplication.update -= Tick;
        if (_kb != null)
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_kb, new KeyboardState(new Key[0]));
        Debug.Log("[SPRINT] DONE - " + _fails + " FAIL");
    }

    static void Send(Key[] keys)
    {
        if (_kb != null)
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
    }

    static bool SprintFlag()
    {
        if (_sprintField == null || _player == null) return false;
        object raw = _sprintField.GetValue(_player);
        return raw is bool b && b;
    }

    static void Dump(string label)
    {
        InputAction action = null;
        if (_input != null && _input.actions != null) action = _input.actions.FindAction("Sprint");

        string actionValue = "missing";
        bool actionEnabled = false;
        if (action != null)
        {
            actionValue = action.IsPressed() ? "pressed" : "released";
            actionEnabled = action.enabled;
        }

        string mapName = "none";
        if (_input != null && _input.currentActionMap != null) mapName = _input.currentActionMap.name;

        Note(label + ": action=" + actionValue + " enabled=" + actionEnabled +
             " controllerSprint=" + SprintFlag() + " map=" + mapName);
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 20)
        {
            Note("FAIL - timeout at stage " + _stage);
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
                _input = _player.GetComponent<PlayerInput>();
                _kb = Keyboard.current;
                _sprintField = typeof(PlayerController).GetField("_sprint", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_input == null || _kb == null || _sprintField == null) return;

                Dump("at rest");
                Send(new[] { Key.W, Key.LeftShift });
                _stageAt = Now;
                _stage = 1;
                return;
            }

            case 1:
            {
                if (Now - _stageAt < 0.5) return;
                Dump("holding shift");
                Check(SprintFlag(), "shift down asks for sprint");

                Send(new[] { Key.W });
                _stageAt = Now;
                _stage = 2;
                return;
            }

            case 2:
            {
                if (Now - _stageAt < 0.5) return;
                Dump("after release");
                Check(!SprintFlag(), "releasing shift stops the sprint");

                Send(new Key[0]);
                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
