using System.IO;
using System.Text;
using UnityEngine;
using Slotgun;

public static class GripCapture
{
    const string Out = @"C:\Users\duver_f40dzl4\AppData\Local\AuraForUnity\CapturedImages\grip_view.png";

    public static void Execute()
    {
        var sb = new StringBuilder();

        // --- pick the game camera -------------------------------------------------
        Camera cam = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c.cameraType != CameraType.Game) continue;
            if (!c.gameObject.activeInHierarchy || !c.enabled) continue;
            cam = c;
        }

        if (cam == null) { Debug.Log("GRIP: no game camera"); return; }
        sb.AppendLine("camera=" + cam.name + " fov=" + cam.fieldOfView + " rt=" + Screen.width + "x" + Screen.height);

        // --- who is out -----------------------------------------------------------
        var loadout = Object.FindAnyObjectByType<PlayerLoadout>();
        if (loadout != null)
        {
            var w = loadout.Current;
            var wb = w as MonoBehaviour;
            sb.AppendLine("weapon=" + (wb != null ? wb.name : "?") + " melee=" + (w != null && w.IsMelee));
        }

        // --- hand vs grip geometry -------------------------------------------------
        var arms = Object.FindAnyObjectByType<FirstPersonArms>();
        if (arms == null) sb.AppendLine("arms=null");
        else
        {
            var an = arms.animator;
            var rh = an != null ? an.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var lh = an != null ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            sb.AppendLine("arms on=" + arms.gameObject.name +
                          " grip=" + (arms.grip != null ? arms.grip.position.ToString("F3") : "null") +
                          " foregrip=" + (arms.foregrip != null ? arms.foregrip.position.ToString("F3") : "null"));
            if (rh != null && arms.grip != null)
                sb.AppendLine("RHand=" + rh.position.ToString("F3") +
                              " distToGrip=" + Vector3.Distance(rh.position, arms.grip.position).ToString("F4"));
            if (lh != null && arms.foregrip != null)
                sb.AppendLine("LHand=" + lh.position.ToString("F3") +
                              " distToForegrip=" + Vector3.Distance(lh.position, arms.foregrip.position).ToString("F4"));
        }

        // --- weak point colour match ----------------------------------------------
        var wp = Object.FindAnyObjectByType<BossWeakPoint>();
        if (wp == null) sb.AppendLine("weakpoint=null");
        else
        {
            var r = wp.GetComponentInChildren<Renderer>();
            string a = "no renderer";
            if (r != null && r.sharedMaterial != null)
            {
                var m = r.sharedMaterial;
                Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                a = m.name + " base=" + col.ToString("F3");
            }
            sb.AppendLine("weakpoint on=" + wp.gameObject.name + " at=" + wp.transform.position.ToString("F3") + " " + a);
        }

        Debug.Log("GRIP:\n" + sb);

        // --- high-res capture -----------------------------------------------------
        int w2 = 1600, h2 = 900;
        var rt = new RenderTexture(w2, h2, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(w2, h2, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w2, h2), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(Out));
        File.WriteAllBytes(Out, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Debug.Log("GRIP: wrote " + Out);
    }
}
