using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Checks whether the palm orientation FirstPersonArms asks for is the one the wrist actually ends
/// up with. The script turns the hand from two axes it reads off the rig — the wrist-to-knuckle line
/// and the thumb crossed with it — so if the live directions do not match the ones asked for, the
/// pistol sits in the fist at the wrong angle and no amount of position tuning can fix it.
/// </summary>
public static class PalmCheck
{
    public static void Execute()
    {
        var log = new StringBuilder("[PalmCheck]\n");

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[PalmCheck] no Player"); return; }

        var body = player.transform.Find("Body");
        var animator = body != null ? body.GetComponent<Animator>() : null;
        var arms = body != null ? body.GetComponent<FirstPersonArms>() : null;
        if (animator == null || arms == null) { Debug.Log("[PalmCheck] no rig"); return; }

        if (arms.grip == null) { Debug.Log("[PalmCheck] no grip"); return; }

        Hand(log, animator, "RIGHT", HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal,
             HumanBodyBones.RightThumbDistal, arms.grip, true);
        Hand(log, animator, "LEFT ", HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal,
             HumanBodyBones.LeftThumbDistal, arms.foregrip != null ? arms.foregrip : arms.grip, false);

        Debug.Log(log.ToString());
    }

    static void Hand(StringBuilder log, Animator animator, string label, HumanBodyBones handBone,
                     HumanBodyBones knuckleBone, HumanBodyBones thumbBone, Transform point, bool right)
    {
        var hand = animator.GetBoneTransform(handBone);
        var knuckle = animator.GetBoneTransform(knuckleBone);
        var thumbT = animator.GetBoneTransform(thumbBone);
        if (hand == null || knuckle == null || point == null) { log.Append(label).Append(" missing bones\n"); return; }

        // What the hand is actually doing this frame, after the IK pass.
        Vector3 fingers = (knuckle.position - hand.position).normalized;
        Vector3 thumb = thumbT != null ? (thumbT.position - hand.position).normalized : Vector3.zero;
        Vector3 back = right ? Vector3.Cross(thumb, fingers) : Vector3.Cross(fingers, thumb);
        back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.zero;

        // What FirstPersonArms asked for.
        Vector3 wantFingers = (point.forward - point.up * 0.3f).normalized;
        Vector3 wantBack = right ? point.right : -point.right;

        log.Append(label)
           .Append(" wantFingers=").Append(wantFingers.ToString("F2"))
           .Append(" got=").Append(fingers.ToString("F2"))
           .Append(" err=").Append(Vector3.Angle(fingers, wantFingers).ToString("F1")).Append("deg")
           .Append("\\n");

        log.Append("      wantBack=").Append(wantBack.ToString("F2"))
           .Append(" got=").Append(back.ToString("F2"))
           .Append(" err=").Append(back == Vector3.zero ? -1f : Vector3.Angle(back, wantBack)).Append("deg")
           .Append(" thumb=").Append(thumb.ToString("F2"))
           .Append(" handFwd=").Append(hand.forward.ToString("F2"))
           .Append("\\n");
    }
}
