using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Live (play-mode) reading of the grip: where the wrist bones actually ended up after the
/// Animator's IK pass, against where the Grip/Foregrip empties and the pistol mesh are.
/// </summary>
public static class HandLive
{
    public static void Execute()
    {
        var log = new StringBuilder("[HandLive]\n");

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[HandLive] no Player"); return; }

        var body = player.transform.Find("Body");
        var animator = body != null ? body.GetComponent<Animator>() : null;
        var arms = body != null ? body.GetComponent<FirstPersonArms>() : null;
        var loadout = player.GetComponent<PlayerLoadout>();
        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;

        if (animator == null || arms == null || weapon == null) { Debug.Log("[HandLive] missing rig"); return; }

        log.Append("playing=").Append(Application.isPlaying)
           .Append(" currentSlot=").Append(loadout != null && loadout.Current != null ? loadout.Current.ToString() : "NULL")
           .Append(" isMelee=").Append(loadout != null && loadout.Current != null && loadout.Current.IsMelee)
           .Append(" weaponActive=").Append(weapon.gameObject.activeInHierarchy)
           .Append(" armScriptEnabled=").Append(arms.enabled)
           .Append("\n");

        var grip = weapon.Find("Grip");
        var foregrip = weapon.Find("Foregrip");

        Vector3 eye = cam.transform.position;

        var rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

        log.Append("grip world=").Append(grip.position.ToString("F4"))
           .Append(" distFromEye=").Append(Vector3.Distance(eye, grip.position).ToString("F3")).Append("\n");
        log.Append("foregrip world=").Append(foregrip.position.ToString("F4"))
           .Append(" distFromEye=").Append(Vector3.Distance(eye, foregrip.position).ToString("F3")).Append("\n");

        if (rHand != null)
            log.Append("RIGHT hand=").Append(rHand.position.ToString("F4"))
               .Append(" -> grip=").Append(Vector3.Distance(rHand.position, grip.position).ToString("F3"))
               .Append("  forearmDir=").Append((rHand.position - rLower.position).normalized.ToString("F3"))
               .Append("  palmRight=").Append(rHand.right.ToString("F3"))
               .Append("  palmUp=").Append(rHand.up.ToString("F3"))
               .Append("  palmFwd=").Append(rHand.forward.ToString("F3"))
               .Append("\n");

        if (lHand != null)
            log.Append("LEFT  hand=").Append(lHand.position.ToString("F4"))
               .Append(" -> foregrip=").Append(Vector3.Distance(lHand.position, foregrip.position).ToString("F3"))
               .Append("  palmRight=").Append(lHand.right.ToString("F3"))
               .Append("  palmUp=").Append(lHand.up.ToString("F3"))
               .Append("  palmFwd=").Append(lHand.forward.ToString("F3"))
               .Append("\n");

        // Reach budget per hand, using the same numbers the IK script uses.
        foreach (var side in new[] { AvatarIKGoal.RightHand, AvatarIKGoal.LeftHand })
        {
            bool right = side == AvatarIKGoal.RightHand;
            var hand = right ? rHand : lHand;
            var lower = right ? rLower : lLower;
            Transform point = right ? grip : foregrip;
            if (hand == null || lower == null || point == null) continue;

            var upper = hand.parent != null ? hand.parent.parent : null;
            if (upper == null) continue;

            Vector3 target = point.position - point.forward * (right ? arms.gripBack : arms.foregripBack)
                                            - point.up * (right ? arms.gripDown : arms.foregripDown);

            float armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            float reach = Vector3.Distance(upper.position, target);
            float t = Mathf.InverseLerp(armLength * arms.reachLimit, armLength * 1.6f, reach);
            float weight = (right ? 1f : 0.95f) * (1f - t);

            log.Append(right ? "REACH right" : "REACH left ")
               .Append(" shoulder=").Append(upper.position.ToString("F4"))
               .Append(" arm=").Append(armLength.ToString("F3"))
               .Append(" reach=").Append(reach.ToString("F3"))
               .Append(" band=").Append((armLength * arms.reachLimit).ToString("F3"))
               .Append("..").Append((armLength * 1.6f).ToString("F3"))
               .Append(" weight=").Append(weight.ToString("F2"))
               .Append("\n");
        }

        // The pistol's real size on screen, and where its handle is.
        foreach (var mr in weapon.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            if (mr.name != "M1911" && mr.name != "Magazin" && mr.name != "Slide") continue;

            Bounds b = mr.bounds;
            log.Append("mesh ").Append(mr.name)
               .Append(" worldCenter=").Append(b.center.ToString("F4"))
               .Append(" worldSize=").Append(b.size.ToString("F3"))
               .Append(" distFromEye=").Append(Vector3.Distance(eye, b.center).ToString("F3"))
               .Append("\n");
        }

        Debug.Log(log.ToString());
    }
}
