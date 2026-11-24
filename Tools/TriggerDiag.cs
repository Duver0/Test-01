using System.Reflection;
using UnityEngine;

public static class TriggerDiag
{
    public static string Execute()
    {
        var d = Slotgun.GameDirector.Instance;
        if (d == null) return "NO DIRECTOR";

        var bf = BindingFlags.Instance | BindingFlags.NonPublic;
        var heldField = typeof(Slotgun.Weapon).GetField("_triggerHeld", bf);
        object held = heldField == null ? (object)"?" : heldField.GetValue(d.weapon);

        var pi = d.weapon.GetComponentInParent<UnityEngine.InputSystem.PlayerInput>();
        int id1 = pi.actions.GetInstanceID();
        int id2 = pi.actions.GetInstanceID();
        var a1 = pi.actions.FindAction("Player/Attack", false);
        var a2 = pi.actions.FindAction("Player/Attack", false);

        string attackInfo = "null";
        if (a1 != null)
            attackInfo = "enabled=" + a1.enabled + " pressed=" + a1.IsPressed() + " value=" + a1.ReadValue<float>() + " id=" + a1.GetInstanceID();

        string mouseInfo = "no mouse";
        if (UnityEngine.InputSystem.Mouse.current != null)
            mouseInfo = "leftPressed=" + UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;

        return "held=" + held + "\n" +
               "assetId1=" + id1 + " assetId2=" + id2 + "\n" +
               "map=" + (pi.currentActionMap != null ? pi.currentActionMap.name : "-") + "\n" +
               "attack " + attackInfo + "\n" +
               "sameAction=" + (a1 == a2) + "\n" +
               mouseInfo + "\n" +
               "ammo=" + d.weapon.Ammo + " bossHp=" + (d.CurrentBoss != null ? d.CurrentBoss.Current : 0f).ToString("0") + " phase=" + d.Phase;
    }
}
