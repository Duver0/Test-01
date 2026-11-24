using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;
using Slotgun.EditorTools;

/// <summary>
/// Installs the Blink "FREE Low Poly Human" as the playable body: a "Body" child of Player that
/// follows yaw, plays the shared Humanoid locomotion blend, and hides the head so the first-person
/// camera never sits inside a face.
/// </summary>
public static class InstallPlayerBody
{
    const string PrefabPath =
        "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";

    const string ControllerPath = "Assets/Resources/HumanoidLocomotion.controller";

    // The first-person camera sits at eye height inside the skull, so the whole head — skin, hair
    // and face parts — is hidden. Everything below the neck stays, which is what gives the player
    // a shadow and something to look at when aiming down.
    static readonly string[] HiddenParts =
    {
        "Head", "Neck", "Eyebrows", "Eyelashes", "Eyes", "Hair7", "Beard6", "Moustache4"
    };

    public static void Execute()
    {
        var log = new StringBuilder();

        // 1. The pack is authored for the Built-in pipeline; URP would draw it magenta.
        SectorScenery.UpgradeEnvironmentMaterials();

        // 2. Build the body.
        var scene = EditorSceneManager.GetActiveScene();
        GameObject player = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Player") { player = root; break; }

        if (player == null) { Debug.LogError("[Body] no Player root in " + scene.name); return; }

        var old = player.transform.Find("Body");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Body] prefab missing: " + PrefabPath); return; }

        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        body.name = "Body";

        // The mesh is 2.00 m tall with its feet at -0.05; the controller is 1.80 m with its feet at
        // the Player origin. 0.9 shrinks the head to the 1.80 m shell and the +0.045 lift puts the
        // soles exactly on the floor, so the model's eye line lands on the 1.62 m camera.
        body.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * 0.9f;

        // 3. Animation: the pack ships a valid Humanoid avatar, so the shared blend drives it.
        var animator = body.GetComponent<Animator>();
        if (animator == null) animator = body.AddComponent<Animator>();

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null) Debug.LogWarning("[Body] controller missing: " + ControllerPath);
        else animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
        // The body is mostly behind the camera, so it must keep animating while its renderers are
        // off screen — otherwise it freezes mid stride and snaps when the player looks down.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var driver = body.GetComponent<ModelLocomotion>();
        if (driver == null) driver = body.AddComponent<ModelLocomotion>();
        driver.animator = animator;
        driver.speedParameter = "Speed";
        driver.runSpeed = 6.4f;    // PlayerController.walkSpeed
        driver.sprintSpeed = 9.2f; // PlayerController.sprintSpeed

        // 4. One armour set, not two: the starter cloth is the fresh-run look.
        foreach (var t in body.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("PlateSet1_")) t.gameObject.SetActive(false);
            else if (t.name.StartsWith("Starter_")) t.gameObject.SetActive(true);
        }

        // 5. Hide the head so the camera is not inside it.
        int hidden = 0;
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            for (int i = 0; i < HiddenParts.Length; i++)
            {
                if (r.gameObject.name != HiddenParts[i]) continue;
                r.enabled = false;
                hidden++;
                break;
            }
        }

        // A skinned body whose bounds leave the frustum gets culled per-renderer; keep the parts
        // that matter alive so the limbs never pop out while looking around.
        foreach (var s in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            s.updateWhenOffscreen = true;

        log.AppendLine("[Body] installed under '" + player.name + "' parts hidden=" + hidden +
                       " controller=" + (controller != null ? controller.name : "MISSING") +
                       " animator=" + (animator.avatar != null ? animator.avatar.name : "no avatar"));

        log.AppendLine("[Body] renderers now enabled: ");
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            if (r.enabled) log.Append(" " + r.gameObject.name);
        log.AppendLine();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scene.path);
        Debug.Log(log.ToString());
    }
}
