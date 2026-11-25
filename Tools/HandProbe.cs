using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Dumps the geometry that decides whether the body's hands land on the pistol:
/// the viewmodel chain, the two grip empties, the pistol mesh bounds, and the
/// body's own shoulder/elbow/wrist bones in the editor bind pose.
/// </summary>
public static class HandProbe
{
    public static void Execute()
    {
        var log = new StringBuilder("[HandProbe]\n");

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[HandProbe] no Player"); return; }

        var body = player.transform.Find("Body");
        var animator = body != null ? body.GetComponent<Animator>() : null;
        var arms = body != null ? body.GetComponent<FirstPersonArms>() : null;

        // ---- viewmodel chain ----
        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null) { Debug.Log("[HandProbe] no Weapon under camera"); return; }

        Vector3 bodyPos = body.position;
        log.Append("bodyScale=").Append(body.localScale.x.ToString("F3"))
           .Append(" animatorHuman=").Append(animator != null && animator.isHuman)
           .Append("\n");

        DumpChain(log, weapon, 3);

        var grip = weapon.Find("Grip");
        var foregrip = weapon.Find("Foregrip");
        log.Append("grip local=").Append(grip != null ? grip.localPosition.ToString("F4") : "NULL")
           .Append("  world=").Append(grip != null ? (grip.position - bodyPos).ToString("F4") : "-")
           .Append("\n");
        log.Append("foregrip local=").Append(foregrip != null ? foregrip.localPosition.ToString("F4") : "NULL")
           .Append("  world=").Append(foregrip != null ? (foregrip.position - bodyPos).ToString("F4") : "-")
           .Append("\n");

        // pistol mesh bounds in weapon space, so we can tell where the model's handle actually is
        foreach (var mr in weapon.GetComponentsInChildren<MeshRenderer>(true))
        {
            var b = mr.bounds;
            Vector3 c = weapon.InverseTransformPoint(b.center);
            log.Append("  mesh ").Append(mr.name)
               .Append(" center(weapon)=").Append(c.ToString("F4"))
               .Append(" size=").Append(b.size.ToString("F3"))
               .Append("\n");
        }

        if (animator == null || !animator.isHuman || arms == null) { Debug.Log(log.ToString()); return; }

        // ---- bones, in body space (editor bind pose: no Animator.Update) ----
        var bones = new[]
        {
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.Chest
        };

        Vector3 RUpper = Vector3.zero, RLower = Vector3.zero, RHand = Vector3.zero;
        Vector3 LUpper = Vector3.zero, LLower = Vector3.zero, LHand = Vector3.zero;

        foreach (var b in bones)
        {
            var t = animator.GetBoneTransform(b);
            if (t == null) { log.Append("bone ").Append(b).Append(" = NULL\n"); continue; }

            Vector3 p = t.position - bodyPos;
            log.Append("bone ").Append(b.ToString().PadRight(16)).Append(p.ToString("F4")).Append("\n");

            switch (b)
            {
                case HumanBodyBones.RightUpperArm: RUpper = t.position; break;
                case HumanBodyBones.RightLowerArm: RLower = t.position; break;
                case HumanBodyBones.RightHand: RHand = t.position; break;
                case HumanBodyBones.LeftUpperArm: LUpper = t.position; break;
                case HumanBodyBones.LeftLowerArm: LLower = t.position; break;
                case HumanBodyBones.LeftHand: LHand = t.position; break;
            }
        }

        float rArm = Vector3.Distance(RUpper, RLower) + Vector3.Distance(RLower, RHand);
        float lArm = Vector3.Distance(LUpper, LLower) + Vector3.Distance(LLower, LHand);
        log.Append("armLength R=").Append(rArm.ToString("F3")).Append(" L=").Append(lArm.ToString("F3")).Append("\n");

        if (grip != null)
        {
            Vector3 target = grip.position - grip.forward * arms.gripBack - grip.up * arms.gripDown;
            float reach = Vector3.Distance(RUpper, target);
            log.Append("right hand target(world-from-body)=").Append((target - bodyPos).ToString("F4"))
               .Append("  reachFromShoulder=").Append(reach.ToString("F3"))
               .Append("  reachLimit=").Append(arms.reachLimit.ToString("F2"))
               .Append("  bandStart=").Append((rArm * arms.reachLimit).ToString("F3"))
               .Append("  weight=").Append((1f - Mathf.InverseLerp(rArm * arms.reachLimit, rArm * 1.6f, reach)).ToString("F2"))
               .Append("\n");
        }

        if (foregrip != null)
        {
            Vector3 target = foregrip.position - foregrip.forward * arms.foregripBack - foregrip.up * arms.foregripDown;
            float reach = Vector3.Distance(LUpper, target);
            log.Append("left hand target(world-from-body)=").Append((target - bodyPos).ToString("F4"))
               .Append("  reachFromShoulder=").Append(reach.ToString("F3"))
               .Append("  weight=").Append((0.95f * (1f - Mathf.InverseLerp(lArm * arms.reachLimit, lArm * 1.6f, reach))).ToString("F2"))
               .Append("\n");
            log.Append("grip->foregrip dist=").Append(Vector3.Distance(grip.position, foregrip.position).ToString("F3")).Append("\n");
        }

        Debug.Log(log.ToString());
    }

    static void DumpChain(StringBuilder log, Transform weapon, int depth)
    {
        log.Append("weapon localPos=").Append(weapon.localPosition.ToString("F4"))
           .Append(" localRot=").Append(weapon.localEulerAngles.ToString("F2"))
           .Append(" localScale=").Append(weapon.localScale.x.ToString("F3")).Append("\n");

        for (int i = 0; i < weapon.childCount; i++)
            DumpChild(log, weapon.GetChild(i), 1, depth);
    }

    static void DumpChild(StringBuilder log, Transform t, int level, int max)
    {
        log.Append(' ', level * 2).Append("- ").Append(t.name)
           .Append(" active=").Append(t.gameObject.activeSelf)
           .Append(" lp=").Append(t.localPosition.ToString("F3"))
           .Append(" ws=").Append(t.lossyScale.x.ToString("F4"))
           .Append("\n");

        if (level >= max) return;
        for (int i = 0; i < t.childCount; i++)
            DumpChild(log, t.GetChild(i), level + 1, max);
    }
}
