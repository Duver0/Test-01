using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ShootDiag
{
    public static string Execute()
    {
        var d = Slotgun.GameDirector.Instance;
        if (d == null) return "NO DIRECTOR";
        var sb = new StringBuilder();

        var mouse = Mouse.current;
        sb.AppendLine($"mouse={(mouse != null)} keyboard={(Keyboard.current != null)}");
        var pi = d.weapon.GetComponentInParent<PlayerInput>();
        sb.AppendLine($"playerInput={(pi != null)} scheme={(pi != null ? pi.currentControlScheme : "-")} " +
                      $"asset={((pi != null && pi.actions != null) ? pi.actions.name : "-")}");
        var attack = pi != null ? pi.actions.FindAction("Player/Attack", false) : null;
        sb.AppendLine($"attack={(attack != null)} enabled={(attack != null && attack.enabled)} " +
                      $"controls={(attack != null ? attack.controls.Count : -1)}");

        if (mouse != null)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            InputSystem.Update();
            sb.AppendLine($"after inject: leftButton={mouse.leftButton.isPressed} " +
                          $"attackIsPressed={(attack != null && attack.IsPressed())} " +
                          $"attackValue={(attack != null ? attack.ReadValue<float>() : -1f)}");
        }

        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        var cached = typeof(Slotgun.Weapon).GetField("_attackAction", bf);
        var cachedAction = cached != null ? (InputAction)cached.GetValue(d.weapon) : null;
        sb.AppendLine($"weaponCachedAction={(cachedAction != null)} sameInstance={(cachedAction == attack)}");
        sb.AppendLine($"ammo={d.weapon.Ammo} bossHp={(d.CurrentBoss != null ? d.CurrentBoss.Current : 0f):0}");

        if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 0 });
        return sb.ToString();
    }
}
