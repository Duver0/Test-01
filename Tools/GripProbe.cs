using UnityEngine;
using Slotgun;

public static class GripProbe
{
    public static void Execute()
    {
        var arms = Object.FindAnyObjectByType<FirstPersonArms>();
        if (arms == null) { Debug.Log("GP8 no arms"); return; }
        var an = arms.animator;
        var rh = an.GetBoneTransform(HumanBodyBones.RightHand);
        var ru = an.GetBoneTransform(HumanBodyBones.RightUpperArm);

        Debug.Log("GP8 t=" + Time.time.ToString("F2") +
                  " frame=" + Time.frameCount +
                  " norm=" + an.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("F3") +
                  " reachLimit=" + arms.reachLimit +
                  " rhand=" + rh.position.ToString("F3") +
                  " grip=" + arms.grip.position.ToString("F3") +
                  " d=" + Vector3.Distance(rh.position, arms.grip.position).ToString("F3") +
                  " reach=" + Vector3.Distance(ru.position, arms.grip.position).ToString("F3"));
    }
}
