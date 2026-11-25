using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slotgun;

/// <summary>
/// Applies the new viewmodel placement to the open scene: a real-sized pistol, held further out,
/// with the two grip empties sitting on the pistol's own handle instead of above it.
/// The same numbers live in Assets/Editor/FirstPersonRig.cs, so a re-install reproduces this.
/// </summary>
public static class ApplyGrip
{
    static readonly Vector3 WeaponPosition = new Vector3(0.115f, -0.115f, 0.34f);
    const float WeaponScale = 0.30f;
    static readonly Vector3 GripFromHandle = new Vector3(0f, 0.10f, -0.06f);
    static readonly Vector3 ForegripFromHandle = new Vector3(-0.05f, -0.09f, -0.02f);

    public static void Execute()
    {
        var log = new StringBuilder("[ApplyGrip]\n");

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[ApplyGrip] no Player"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        var weapon = cam != null ? cam.transform.Find("Weapon") : null;
        if (weapon == null) { Debug.Log("[ApplyGrip] no Weapon"); return; }

        weapon.localPosition = WeaponPosition;
        weapon.localRotation = Quaternion.identity;
        weapon.localScale = Vector3.one * WeaponScale;

        Vector3 handle = Vector3.zero;
        bool found = false;
        foreach (var mr in weapon.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name != "Magazin") continue;
            handle = weapon.InverseTransformPoint(mr.bounds.center);
            found = true;
            break;
        }

        var m1911 = weapon.Find("ViewModel/Pistol/M1911");
        if (!found && m1911 != null) handle = weapon.InverseTransformPoint(m1911.position);

        var grip = weapon.Find("Grip");
        var foregrip = weapon.Find("Foregrip");
        if (grip != null) grip.localPosition = handle + GripFromHandle;
        if (foregrip != null) foregrip.localPosition = handle + ForegripFromHandle;

        var body = player.transform.Find("Body");
        var arms = body != null ? body.GetComponent<FirstPersonArms>() : null;
        if (arms != null)
        {
            arms.gripBack = 0.03f;
            arms.gripDown = 0.012f;
            arms.foregripBack = 0.03f;
            arms.foregripDown = 0.016f;
        }

        log.Append("weapon pos=").Append(weapon.localPosition.ToString("F3"))
           .Append(" scale=").Append(weapon.localScale.x.ToString("F2")).Append("\n")
           .Append("handle(weapon space)=").Append(handle.ToString("F4")).Append(" fromMagazine=").Append(found).Append("\n")
           .Append("grip=").Append(grip != null ? grip.localPosition.ToString("F4") : "NULL").Append("\n")
           .Append("foregrip=").Append(foregrip != null ? foregrip.localPosition.ToString("F4") : "NULL").Append("\n");

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);

        Debug.Log(log.ToString());
    }
}
