using UnityEngine;
using Slotgun;

public static class RigProbe3
{
    public static void Execute()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[RP3] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null) { Debug.LogError("[RP3] no camera"); return; }

        Debug.Log($"[RP3] playing={Application.isPlaying} camFov={cam.fieldOfView:F1} camPos={cam.transform.position.ToString("F3")}");

        var weapon = cam.transform.Find("Weapon");
        if (weapon == null) { Debug.LogError("[RP3] no weapon"); return; }

        Debug.Log($"[RP3] weapon local={weapon.localPosition.ToString("F3")} world={weapon.position.ToString("F3")} rot={weapon.eulerAngles.ToString("F1")} active={weapon.gameObject.activeInHierarchy}");
        Debug.Log($"[RP3] weapon fwd={weapon.forward.ToString("F2")} camFwd={cam.transform.forward.ToString("F2")}");

        var grip = weapon.Find("Grip");
        var fore = weapon.Find("Foregrip");
        if (grip != null) Debug.Log($"[RP3] grip viewport={cam.WorldToViewportPoint(grip.position).ToString("F3")}");
        if (fore != null) Debug.Log($"[RP3] foregrip viewport={cam.WorldToViewportPoint(fore.position).ToString("F3")}");

        var m1911 = weapon.Find("ViewModel/Pistol/M1911");
        if (m1911 != null)
        {
            Debug.Log($"[RP3] m1911 world={m1911.position.ToString("F3")} euler={m1911.eulerAngles.ToString("F1")} fwd={m1911.forward.ToString("F2")} active={m1911.gameObject.activeInHierarchy}");
            Debug.Log($"[RP3] m1911 viewport={cam.WorldToViewportPoint(m1911.position).ToString("F3")}");
        }

        var a = player.GetComponentInChildren<Animator>(true);
        if (a != null && a.isHuman)
        {
            var rh = a.GetBoneTransform(HumanBodyBones.RightHand);
            var lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var ru = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lu = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (rh != null) Debug.Log($"[RP3] R wrist={rh.position.ToString("F3")} vp={cam.WorldToViewportPoint(rh.position).ToString("F3")}");
            if (lh != null) Debug.Log($"[RP3] L wrist={lh.position.ToString("F3")} vp={cam.WorldToViewportPoint(lh.position).ToString("F3")}");
            if (ru != null && grip != null) Debug.Log($"[RP3] R upperArm={ru.position.ToString("F3")} distToGrip={Vector3.Distance(ru.position, grip.position):F3}");
            if (lu != null && fore != null) Debug.Log($"[RP3] L upperArm={lu.position.ToString("F3")} distToFore={Vector3.Distance(lu.position, fore.position):F3}");
        }

        var fpa = player.GetComponentInChildren<FirstPersonArms>(true);
        if (fpa != null)
            Debug.Log($"[RP3] fpa animator={(fpa.animator != null)} grip={(fpa.grip != null ? fpa.grip.name : "NULL")} fore={(fpa.foregrip != null ? fpa.foregrip.name : "NULL")} loadout={(fpa.loadout != null)} current={(fpa.loadout != null && fpa.loadout.Current != null ? fpa.loadout.Current.name : "NULL")}");
        else Debug.Log("[RP3] no FirstPersonArms");

        var anchor = player.GetComponent<BodyCameraAnchor>();
        Debug.Log($"[RP3] anchor={(anchor != null)} head={(anchor != null && anchor.headBone != null ? anchor.headBone.name : "NULL")} pivot={(anchor != null && anchor.pivot != null ? anchor.pivot.name + " local=" + anchor.pivot.localPosition.ToString("F3") : "NULL")}");
    }
}
