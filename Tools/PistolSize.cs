using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>True local-mesh size of the pistol and the hand, so a scale can be chosen from real numbers.</summary>
public static class PistolSize
{
    public static void Execute()
    {
        var log = new StringBuilder("[PistolSize]\n");

        var player = GameObject.Find("Player");
        var body = player.transform.Find("Body");
        var animator = body.GetComponent<Animator>();
        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam.transform.Find("Weapon");

        foreach (var name in new[] { "M1911", "Magazin", "Slide", "Trigger", "Hammer" })
        {
            var t = FindDeep(weapon, name);
            if (t == null) { log.Append(name).Append(" = not found\n"); continue; }

            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) { log.Append(name).Append(" = no mesh\n"); continue; }

            Vector3 local = mf.sharedMesh.bounds.size;
            Vector3 lossy = t.lossyScale;
            Vector3 world = Vector3.Scale(local, lossy);

            log.Append(name)
               .Append(" meshLocalSize=").Append(local.ToString("F3"))
               .Append(" lossyScale=").Append(lossy.ToString("F3"))
               .Append(" trueWorldSize=").Append(world.ToString("F4"))
               .Append(" localCenter=").Append(mf.sharedMesh.bounds.center.ToString("F3"))
               .Append("\n");
        }

        // hand span, to judge the pistol against the body that has to hold it
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var mid = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        var tip = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
        var thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbDistal);
        if (hand != null && mid != null)
            log.Append("hand->middleProximal=").Append(Vector3.Distance(hand.position, mid.position).ToString("F4"))
               .Append(" middleProximal->tip=").Append(tip != null ? Vector3.Distance(mid.position, tip.position).ToString("F4") : "-")
               .Append(" thumbTip->hand=").Append(thumb != null ? Vector3.Distance(thumb.position, hand.position).ToString("F4") : "-")
               .Append("\n");

        Debug.Log(log.ToString());
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
