using System.IO;
using UnityEngine;

/// <summary>
/// Renders the viewmodel pistol from a chosen side with a throwaway camera and writes the frame to
/// disk, so the grip can be judged by eye. Nothing in the project is touched: the extra camera is
/// destroyed before the call returns and the PNG lands outside Assets.
/// </summary>
public static class GripRender
{
    const string OutFolder = @"C:\Users\duver_f40dzl4\AppData\Local\AuraForUnity\CapturedImages\827a4c3a2a47b881";

    public static void Execute(Newtonsoft.Json.Linq.JObject args)
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[GripRender] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null) { Debug.Log("[GripRender] no Weapon"); return; }

        Vector3 gun = weapon.position;
        Vector3 right = cam.transform.right;
        Vector3 forward = cam.transform.forward;

        Shot(gun + right * 0.34f + Vector3.up * 0.10f - forward * 0.10f, gun, "grip_right");
        Shot(gun - right * 0.34f + Vector3.up * 0.10f - forward * 0.10f, gun, "grip_left");
    }

    static void Shot(Vector3 from, Vector3 target, string name)
    {
        var go = new GameObject("[GripRender]");
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        camera.cullingMask = ~0;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);

        go.transform.position = from;
        go.transform.rotation = Quaternion.LookRotation((target - from).normalized, Vector3.up);

        const int Size = 640;
        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        camera.targetTexture = rt;
        camera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Directory.CreateDirectory(OutFolder);
        string path = Path.Combine(OutFolder, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());

        camera.targetTexture = null;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(tex);
        Object.Destroy(go);

        Debug.Log("[GripRender] wrote " + path + " from=" + from.ToString("F3"));
    }
}
