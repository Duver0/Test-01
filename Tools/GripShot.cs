using UnityEditor;
using UnityEngine;

/// <summary>
/// Frames the Scene view on the viewmodel pistol from a chosen side, so the grip can be judged by
/// eye instead of by numbers: both hands, the fingers, and the web of the thumb against the handle.
/// Read-only — it only moves the Scene view camera.
/// </summary>
public static class GripShot
{
    /// <param name="args">{"side": "right" | "left" | "front"}</param>
    public static void Execute(Newtonsoft.Json.Linq.JObject args)
    {
        string side = args != null && args["side"] != null ? args["side"].ToString() : "right";

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[GripShot] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null) { Debug.Log("[GripShot] no Weapon"); return; }

        var view = SceneView.lastActiveSceneView;
        if (view == null) { Debug.Log("[GripShot] no active Scene view"); return; }

        Vector3 gun = weapon.position;
        Vector3 right = cam.transform.right;
        Vector3 up = Vector3.up;
        Vector3 forward = cam.transform.forward;

        Vector3 from = gun;
        switch (side)
        {
            case "left": from = gun - right * 0.5f + up * 0.10f - forward * 0.10f; break;
            case "front": from = gun + forward * 0.45f + up * 0.10f; break;
            default: from = gun + right * 0.5f + up * 0.10f - forward * 0.10f; break;
        }

        Vector3 look = gun - from;
        if (look.sqrMagnitude < 0.0001f) { Debug.Log("[GripShot] degenerate look"); return; }

        Quaternion rotation = Quaternion.LookRotation(look.normalized, up);
        view.LookAt(gun, rotation, 0.42f, false, true);
        view.Repaint();

        Debug.Log("[GripShot] side=" + side
                  + " gun=" + gun.ToString("F3")
                  + " size=" + view.size.ToString("F2")
                  + " camera=" + (view.camera != null ? view.camera.transform.position.ToString("F3") : "none"));
    }
}
