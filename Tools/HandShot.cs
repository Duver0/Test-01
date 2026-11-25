using System.IO;
using UnityEngine;
using Slotgun;

public static class HandShot
{
    const string Out = @"C:\Users\duver_f40dzl4\AppData\Local\AuraForUnity\CapturedImages\hand_closeup.png";

    public static void Execute()
    {
        var arms = Object.FindAnyObjectByType<FirstPersonArms>();
        if (arms == null || arms.grip == null) { Debug.Log("HS: no arms/grip"); return; }
        var main = Object.FindAnyObjectByType<Camera>();
        var grip = arms.grip;

        var go = new GameObject("HandShotCam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.01f;
        cam.cullingMask = ~0;

        Vector3 fwd = main.transform.forward, right = main.transform.right, up = main.transform.up;
        go.transform.position = grip.position + right * 0.42f + up * 0.06f - fwd * 0.12f;
        go.transform.LookAt(grip.position + fwd * 0.08f);

        int w = 900, h = 900;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(Out));
        File.WriteAllBytes(Out, tex.EncodeToPNG());
        string where = go.transform.position.ToString("F2");
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(tex);
        rt.Release();

        Debug.Log("HS: wrote " + Out + " from " + where);
    }
}
