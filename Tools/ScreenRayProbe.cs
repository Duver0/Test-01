using System.Text;
using UnityEngine;

public static class ScreenRayProbe
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[Ray] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        sb.AppendLine("[Ray] cam pos=" + cam.transform.position + " fwd=" + cam.transform.forward);

        Vector2[] points =
        {
            new Vector2(0.80f, 0.20f),   // lower right, where the green mass sits
            new Vector2(0.72f, 0.30f),
            new Vector2(0.55f, 0.15f),
            new Vector2(0.50f, 0.50f),   // screen centre
        };

        foreach (var p in points)
        {
            var ray = cam.ViewportPointToRay(new Vector3(p.x, p.y, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Collide))
            {
                sb.AppendLine("  " + p + " -> no hit");
                continue;
            }

            var path = hit.collider.transform.name;
            var t = hit.collider.transform.parent;
            for (int i = 0; i < 6 && t != null; i++) { path = t.name + "/" + path; t = t.parent; }

            var r = hit.collider.GetComponent<Renderer>();
            string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-";
            sb.AppendLine("  " + p + " -> " + path + " d=" + hit.distance.ToString("F2") + " mat=" + mat);
        }

        // what is actually rendering in front of the camera, by frustum proximity
        sb.AppendLine("[Ray] body parts world positions:");
        var body = player.transform.Find("Body");
        var anim = body != null ? body.GetComponent<Animator>() : null;
        if (anim != null)
        {
            var w = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var e = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var s = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            sb.AppendLine("  R wrist=" + w.position + " elbow=" + e.position + " shoulder=" + s.position);
            sb.AppendLine("  cam-to-wrist=" + Vector3.Distance(cam.transform.position, w.position).ToString("F3"));
            var lw = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            sb.AppendLine("  L wrist=" + lw.position);
        }

        Debug.Log(sb.ToString());
    }
}
