using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ShootSlotgun
{
    static Slotgun.GameDirector D => Slotgun.GameDirector.Instance;

    static void SetMouseButtons(ushort buttons)
    {
        var mouse = Mouse.current;
        if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState { buttons = buttons });
    }

    public static string Press()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        SetMouseButtons(1);
        var act = d.weapon != null && d.weapon.GetComponentInParent<PlayerInput>() != null
            ? d.weapon.GetComponentInParent<PlayerInput>().actions.FindAction("Player/Attack", false)
            : null;
        return $"press queued · weaponEnabled={d.weapon.enabled} ammoBefore={d.weapon.Ammo} " +
               $"attackAction={(act != null)} actionEnabled={(act != null && act.enabled)} " +
               $"bossHp={(d.CurrentBoss != null ? d.CurrentBoss.Current : 0f):0}";
    }

    public static string Read()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        var boss = d.CurrentBoss;
        string s = $"ammo={d.weapon.Ammo}/{d.weapon.Stats.magazine} reloading={d.weapon.IsReloading} " +
                   $"bossHp={(boss != null ? boss.Current : 0f):0} grants={d.weapon.Grants.Count}";
        SetMouseButtons(0);
        return s;
    }

    public static string ReloadKey()
    {
        SetMouseButtons(0);
        var kb = Keyboard.current;
        if (kb == null) return "no keyboard";
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.R));
        return $"R queued · ammo={D.weapon.Ammo} reloading={D.weapon.IsReloading}";
    }

    public static string ReleaseKeys()
    {
        var kb = Keyboard.current;
        if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState());
        SetMouseButtons(0);
        return $"released · ammo={D.weapon.Ammo} reloading={D.weapon.IsReloading}";
    }

    public static string MoveW()
    {
        var kb = Keyboard.current;
        if (kb == null) return "no keyboard";
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
        return "W down · pos=" + D.player.transform.position;
    }

    public static string Pos()
    {
        var kb = Keyboard.current;
        if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState());
        return "pos=" + D.player.transform.position + " yaw=" + D.player.transform.eulerAngles.y.ToString("0") +
               " ammo=" + D.weapon.Ammo;
    }

    public static string Diag2()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var f = typeof(Slotgun.Weapon).GetField("_triggerHeld", bf);
        string held = f == null ? "?" : f.GetValue(d.weapon).ToString();
        var pi = d.weapon.GetComponentInParent<PlayerInput>();
        var act = pi.actions.FindAction("Player/Attack", false);
        string info = act == null ? "null" : $"enabled={act.enabled} pressed={act.IsPressed()} value={act.ReadValue<float>()}";
        return $"held={held} map={(pi.currentActionMap != null ? pi.currentActionMap.name : "-")} attack={info} " +
               $"mouseLeft={(Mouse.current != null && Mouse.current.leftButton.isPressed)} " +
               $"ammo={d.weapon.Ammo} bossHp={(d.CurrentBoss != null ? d.CurrentBoss.Current : 0f):0}";
    }

    public static string Clear()
    {
        if (Mouse.current != null) InputSystem.ResetDevice(Mouse.current, true);
        if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current, true);
        var d = D;
        return $"reset devices · mousePressed={(Mouse.current != null && Mouse.current.leftButton.isPressed)} " +
               $"ammo={d.weapon.Ammo} reloading={d.weapon.IsReloading} bossHp={(d.CurrentBoss != null ? d.CurrentBoss.Current : 0f):0}";
    }

    public static string TestReload()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        var prop = typeof(Slotgun.Weapon).GetProperty("Ammo",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (prop == null) return "Ammo property not found";

        SetMouseButtons(0);
        prop.SetValue(d.weapon, 3);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.R));
        return $"ammo forced to {d.weapon.Ammo}/{d.weapon.Stats.magazine} · R queued · reloading={d.weapon.IsReloading}";
    }

    public static string Hold()
    {
        var d = D;
        if (d == null) return "NO DIRECTOR";
        SetMouseButtons(1);
        return $"holding · ammo={d.weapon.Ammo}";
    }
}
