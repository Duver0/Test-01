using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slotgun;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Puts the Blink "FREE Low Poly Human" pack on the player as its body.
    ///
    /// The pack is the only rig in this project that ships real animation clips, and its avatar is a
    /// valid Humanoid, so the shared <c>HumanoidLocomotion</c> blend drives it exactly as it drives
    /// the zombies and the skeletons. The body follows the player's yaw (yaw lives on the Player
    /// root, pitch on the camera pivot) and plays Idle / RunForward / Sprint from its own speed.
    ///
    /// The head is hidden: the first-person camera sits at 1.62 m, which is inside the skull, so
    /// leaving it on would fill the screen with the inside of a face. Everything from the neck down
    /// stays, which gives the player a shadow and something to look at when aiming at the floor.
    /// </summary>
    public static class PlayerBody
    {
        public const string PrefabPath =
            "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";

        public const string ControllerPath = "Assets/Resources/HumanoidLocomotion.controller";

        /// <summary>Skin, hair and face parts — everything the camera would be sitting inside.</summary>
        static readonly string[] HiddenParts =
        {
            "Head", "Neck", "Eyebrows", "Eyelashes", "Eyes", "Hair7", "Beard6", "Moustache4"
        };

        /// <summary>The mesh is 2.00 m tall with its feet at -0.05; the controller is 1.80 m.</summary>
        const float BodyScale = 0.9f;
        const float FootLift = 0.045f;

        [MenuItem("Slotgun/Install Player Body (FREE Low Poly Human)", false, 42)]
        public static void InstallInActiveScene()
        {
            GameObject player = null;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Player") { player = root; break; }

            if (player == null)
            {
                Debug.LogError("[Body] no 'Player' root in " + EditorSceneManager.GetActiveScene().name);
                return;
            }

            var body = Install(player);
            if (body == null) return;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);

            Debug.Log("[Body] installed on '" + player.name + "' in " + scene.name);
        }

        /// <summary>
        /// Builds (or rebuilds) the player's body. Safe to call twice: an existing "Body" child is
        /// replaced, so this is the single source of truth for the playable character's look.
        /// </summary>
        public static GameObject Install(GameObject player)
        {
            if (player == null) return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Body] Blink human prefab not found at " + PrefabPath + " — player stays headless.");
                return null;
            }

            var old = player.transform.Find("Body");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
            body.name = "Body";

            body.transform.localPosition = new Vector3(0f, FootLift, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * BodyScale;

            // ---- animation -------------------------------------------------
            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null) animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;

            // The body is mostly behind the camera, so it must keep animating while its renderers
            // are off screen — otherwise it freezes mid stride and snaps when the player looks down.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var driver = body.GetComponent<ModelLocomotion>();
            if (driver == null) driver = body.AddComponent<ModelLocomotion>();

            var pc = player.GetComponent<PlayerController>();
            driver.animator = animator;
            driver.speedParameter = "Speed";
            driver.runSpeed = pc != null ? pc.walkSpeed : 6.4f;
            driver.sprintSpeed = pc != null ? pc.sprintSpeed : 9.4f;

            // ---- one armour set, not two: starter cloth is the fresh-run look ---
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("PlateSet1_")) t.gameObject.SetActive(false);
                else if (t.name.StartsWith("Starter_")) t.gameObject.SetActive(true);
            }

            // ---- hide the head the camera sits inside --------------------------
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                for (int i = 0; i < HiddenParts.Length; i++)
                    if (r.gameObject.name == HiddenParts[i]) { r.enabled = false; break; }

            // Limbs that leave the frustum would otherwise pop out while looking around.
            foreach (var s in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                s.updateWhenOffscreen = true;

            return body;
        }
    }
}
