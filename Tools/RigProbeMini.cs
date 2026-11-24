using UnityEngine;

public static class RigProbeMini
{
    public static void Execute()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[MINI] no Player"); return; }
        var cam = player.GetComponentInChildren<Camera>(true);
        Debug.Log("[MINI] cam=" + (cam != null ? cam.transform.position.ToString("F3") : "NULL"));
        var weapon = cam.transform.Find("Weapon");
        Debug.Log("[MINI] weapon=" + (weapon != null ? weapon.localPosition.ToString("F3") + " vp=" + cam.WorldToViewportPoint(weapon.position).ToString("F3") : "NULL"));
        var grip = weapon != null ? weapon.Find("Grip") : null;
        Debug.Log("[MINI] grip=" + (grip != null ? "vp=" + cam.WorldToViewportPoint(grip.position).ToString("F3") : "NULL"));
        var fore = weapon != null ? weapon.Find("Foregrip") : null;
        Debug.Log("[MINI] fore=" + (fore != null ? "vp=" + cam.WorldToViewportPoint(fore.position).ToString("F3") : "NULL"));
        var m = weapon != null ? weapon.Find("ViewModel/Pistol/M1911") : null;
        Debug.Log("[MINI] m1911=" + (m != null ? "euler=" + m.eulerAngles.ToString("F1") + " vp=" + cam.WorldToViewportPoint(m.position).ToString("F3") : "NULL"));
    }
}
