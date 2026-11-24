using System.Reflection;
using UnityEngine;

public static class FovProbe
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static Component Find(string typeName)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    static Camera Cam(Component player)
    {
        if (player == null) return null;
        var f = player.GetType().GetField("cam", Flags);
        return f != null ? f.GetValue(player) as Camera : null;
    }

    public static void Execute()
    {
        var player = Find("PlayerController");
        var cam = Cam(player);
        if (cam == null) { Debug.Log("[FovProbe] no camera"); return; }

        var gravity = player.GetType().GetField("gravity", Flags).GetValue(player);
        var jump = player.GetType().GetField("jumpHeight", Flags).GetValue(player);
        Debug.Log($"[FovProbe] fov={cam.fieldOfView:F2} gravity={gravity} jumpHeight={jump}");
    }

    public static void Nudge()
    {
        var player = Find("PlayerController");
        var cam = Cam(player);
        if (cam == null) { Debug.Log("[FovProbe] no camera"); return; }

        cam.fieldOfView = 60f;
        Debug.Log("[FovProbe] nudged fov to 60, the per-frame lerp should pull it back to fovNormal=80");
    }
}
