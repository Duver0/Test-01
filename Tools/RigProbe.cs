using System.Text;
using UnityEngine;
using UnityEditor;

public static class RigProbe
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[RigProbe] no Player"); return; }

        var body = player.transform.Find("Body");
        if (body == null) { Debug.LogError("[RigProbe] no Body"); return; }

        sb.AppendLine("[RigProbe] --- bone names containing head/neck/spine/hand/arm/fore ---");
        foreach (var t in body.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("head") || n.Contains("neck") || n.Contains("spine") || n.Contains("chest") ||
                n.Contains("hand") || n.Contains("arm") || n.Contains("fore") || n.Contains("shoulder") ||
                n.Contains("clavicle"))
            {
                sb.AppendLine("  " + t.name + " local=" + t.localPosition.ToString("F3") + " children=" + t.childCount);
            }
        }

        var anim = body.GetComponent<Animator>();
        if (anim != null)
        {
            sb.AppendLine("[RigProbe] isHuman=" + anim.isHuman + " ctrl=" + anim.runtimeAnimatorController);
            if (anim.isHuman)
            {
                string[] parts = { "Head", "Neck", "Chest", "RightHand", "LeftHand", "RightLowerArm", "LeftLowerArm", "RightUpperArm", "LeftUpperArm" };
                foreach (var p in parts)
                {
                    var pose = (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), p);
                    var b = anim.GetBoneTransform(pose);
                    sb.AppendLine("  bone " + p + " = " + (b != null ? b.name : "NULL"));
                }
            }
        }

        // weapon viewmodel
        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null) { sb.AppendLine("[RigProbe] no Weapon under camera"); }
        else
        {
            sb.AppendLine("[RigProbe] --- Weapon tree ---");
            Dump(weapon, 0, sb);
        }

        var fists = cam != null ? cam.transform.Find("Fists") : null;
        if (fists != null) { sb.AppendLine("[RigProbe] --- Fists tree ---"); Dump(fists, 0, sb); }

        Debug.Log(sb.ToString());
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        string pad = new string(' ', depth * 2);
        var r = t.GetComponent<Renderer>();
        sb.AppendLine(pad + t.name + " local=" + t.localPosition.ToString("F3") +
                      " scale=" + t.localScale.ToString("F2") +
                      (r != null ? "  renderer=" + r.GetType().Name + " enabled=" + r.enabled : ""));
        for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
    }
}
