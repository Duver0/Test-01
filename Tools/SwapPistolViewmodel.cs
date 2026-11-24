using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SwapPistolViewmodel
{
    const string PrefabPath = "Assets/Low Poly Weapons VOL.1/Prefabs/M1911.prefab";
    const string MatPath    = "Assets/Low Poly Weapons VOL.1/Gun_MAT.mat";
    const string WeaponPath  = "Player/CameraPivot/MainCamera/Weapon";

    public static void Execute()
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        Debug.Log("[PISTOL] render pipeline = " + (rp != null ? rp.GetType().Name : "<built-in>"));

        EnsureUrpMaterial();

        var weapon = GameObject.Find(WeaponPath);
        if (weapon == null) { Debug.LogError("[PISTOL] Weapon object not found"); return; }

        var weaponT = weapon.transform;
        var vm = weaponT.Find("ViewModel");
        if (vm == null) { Debug.LogError("[PISTOL] ViewModel not found"); return; }

        // The blocky procedural gun this scene shipped with: hide it, the asset takes its place.
        string[] oldParts = { "LowPolyModel", "Cube_0", "Cube_1", "Cube_2", "Cylinder_3", "Sphere_4" };
        foreach (var part in oldParts)
        {
            var child = vm.Find(part);
            if (child != null) child.gameObject.SetActive(false);
            else Debug.Log("[PISTOL] no child named " + part);
        }

        var existing = vm.Find("Pistol");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[PISTOL] M1911 prefab not found at " + PrefabPath); return; }

        var pistol = (GameObject)PrefabUtility.InstantiatePrefab(prefab, vm);
        pistol.name = "Pistol";

        var t = pistol.transform;
        t.localPosition = new Vector3(0f, 0.015f, 0.02f);
        // The mesh points its muzzle down -Z (grip sits at +Z), so it needs a half turn to face the camera's forward.
        t.localRotation = Quaternion.Euler(0f, 180f, 0f);
        t.localScale = Vector3.one * 2f;

        // Slide runs z -0.15 (muzzle) .. +0.07 (rear) with its centre line a few mm under the origin.
        Vector3 barrelTip = t.TransformPoint(new Vector3(0f, -0.005f, -0.152f));

        var muzzle = weaponT.Find("Muzzle");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weaponT, false);
        }
        muzzle.position = barrelTip;

        var weaponComp = weapon.GetComponent<Slotgun.Weapon>();
        if (weaponComp != null) weaponComp.viewmodel = vm;

        EditorUtility.SetDirty(weapon);
        EditorSceneManager.MarkSceneDirty(weapon.scene);
        EditorSceneManager.SaveScene(weapon.scene);

        Debug.Log($"[PISTOL] installed pistolWorld={pistol.transform.position} scale={t.lossyScale} " +
                  $"muzzleWorld={muzzle.position} muzzleLocal={muzzle.localPosition}");
    }

    static void EnsureUrpMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogWarning("[PISTOL] Gun_MAT not found at " + MatPath); return; }

        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogWarning("[PISTOL] URP/Lit shader unavailable — leaving the material alone"); return; }
        if (mat.shader == urp) return;

        Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

        Debug.Log($"[PISTOL] converting Gun_MAT from {mat.shader.name} to URP/Lit (color={color}, tex={(tex != null ? tex.name : "none")})");

        mat.shader = urp;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }
}
