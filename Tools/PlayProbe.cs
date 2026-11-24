using System.Reflection;
using System.Text;
using UnityEngine;

public static class PlayProbe
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[PlayProbe] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam.transform.Find("Weapon");

        sb.AppendLine("[PlayProbe] camera local=" + cam.transform.localPosition +
                      " pivot local=" + cam.transform.parent.localPosition);

        if (weapon != null)
        {
            sb.AppendLine("[PlayProbe] Weapon local=" + weapon.localPosition + " scale=" + weapon.localScale);
            Dump(weapon, 0, sb, 3);
        }

        var body = player.transform.Find("Body");
        var anim = body != null ? body.GetComponent<Animator>() : null;
        if (anim != null && anim.isHuman)
        {
            var wrist = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var lwrist = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var grip = weapon != null ? weapon.Find("Grip") : null;
            var fore = weapon != null ? weapon.Find("Foregrip") : null;

            sb.AppendLine("[PlayProbe] wristR=" + wrist.position + " grip=" + (grip != null ? grip.position.ToString() : "null") +
                          " dist=" + (grip != null ? Vector3.Distance(wrist.position, grip.position).ToString("F3") : "-"));
            sb.AppendLine("[PlayProbe] wristL=" + lwrist.position + " foregrip=" + (fore != null ? fore.position.ToString() : "null") +
                          " dist=" + (fore != null ? Vector3.Distance(lwrist.position, fore.position).ToString("F3") : "-"));
            sb.AppendLine("[PlayProbe] armR shoulder=" + anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position +
                          " elapsed");
        }

        var pc = player.GetComponent<Slotgun.PlayerController>();
        if (pc != null)
        {
            var f = typeof(Slotgun.PlayerController).GetField("_aimPitch", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(pc, 38f); sb.AppendLine("[PlayProbe] pitch set to 38"); }
        }

        Debug.Log(sb.ToString());
    }

    static void Dump(Transform t, int depth, StringBuilder sb, int maxDepth)
    {
        if (depth > maxDepth) return;
        string pad = new string(' ', depth * 2);
        var r = t.GetComponent<Renderer>();
        sb.AppendLine(pad + t.name + " active=" + t.gameObject.activeSelf +
                      " renderer=" + (r != null ? r.enabled.ToString() : "-") +
                      " local=" + t.localPosition.ToString("F3"));
        for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb, maxDepth);
    }
}
