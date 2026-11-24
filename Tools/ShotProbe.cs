using System.Reflection;
using UnityEngine;

public static class ShotProbe
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static Component Find(string typeName)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    public static void Execute()
    {
        var weapon = Find("Weapon");
        var player = Find("PlayerController");
        if (weapon == null || player == null) { Debug.Log("[SHOT] missing weapon or player"); return; }

        var muzzleField = weapon.GetType().GetField("muzzle", Flags);
        var muzzle = muzzleField != null ? muzzleField.GetValue(weapon) as Transform : null;

        var eyeProp = player.GetType().GetProperty("Eye", Flags);
        var eye = eyeProp != null ? eyeProp.GetValue(player) as Transform : null;

        var tryFire = weapon.GetType().GetMethod("TryFire", Flags);
        object fired = tryFire != null ? tryFire.Invoke(weapon, null) : "no TryFire";

        string tracer = "none";
        foreach (var lr in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (lr == null || !lr.name.StartsWith("Tracer")) continue;
            tracer = lr.GetPosition(0).ToString("F2");
            break;
        }

        Debug.Log($"[SHOT] fired={fired} muzzle={(muzzle == null ? "NULL" : muzzle.position.ToString("F2"))} " +
                  $"eye={(eye == null ? "NULL" : eye.position.ToString("F2"))} tracerStart={tracer}");
    }
}
