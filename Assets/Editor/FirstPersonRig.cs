using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slotgun;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Wires the first-person feel to the body instead of to a detached camera.
    ///
    /// Three pieces:
    ///  * the camera rides the body's head bone, so a run cycle moves the view with the shoulders
    ///    instead of sliding out from under them (<see cref="BodyCameraAnchor"/>);
    ///  * the body's own hands grip the pistol through the animation IK pass, which is what stops
    ///    the viewmodel hanging in mid-air (<see cref="FirstPersonArms"/>);
    ///  * the floating box gloves are gone — the character has real hands.
    /// </summary>
    public static class FirstPersonRig
    {
        /// <summary>
        /// Camera-space placement of the pistol. High enough that the hand on the grip is inside the
        /// 80° frustum rather than just under it, and close enough that a 0.52 m arm can reach it.
        /// </summary>
        static readonly Vector3 WeaponPosition = new Vector3(0.135f, -0.10f, 0.42f);
        const float WeaponScale = 0.55f;

        [MenuItem("Slotgun/Install First-Person Body Rig", false, 43)]
        public static void InstallInActiveScene()
        {
            GameObject player = null;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Player") { player = root; break; }

            if (player == null)
            {
                Debug.LogError("[FirstPerson] no 'Player' root in the active scene");
                return;
            }

            var log = Install(player);
            Debug.Log(log);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);
        }

        public static string Install(GameObject player)
        {
            var log = new StringBuilder("[FirstPerson] ");

            var controller = player.GetComponent<PlayerController>();
            var body = player.transform.Find("Body");
            var animator = body != null ? body.GetComponent<Animator>() : null;

            if (controller == null || body == null || animator == null || !animator.isHuman)
            {
                return "[FirstPerson] player is missing PlayerController, Body or a humanoid Animator — skipped";
            }

            var cam = controller.cam != null ? controller.cam : player.GetComponentInChildren<Camera>(true);
            if (cam == null) return "[FirstPerson] no camera — skipped";

            // ---- 1. the IK pass has to exist or Unity never calls OnAnimatorIK ----
            EnableIKPass(PlayerBody.ControllerPath);

            // ---- 2. camera rides the head ----
            var anchor = player.GetComponent<BodyCameraAnchor>();
            if (anchor == null) anchor = player.AddComponent<BodyCameraAnchor>();
            anchor.pivot = cam.transform.parent;
            anchor.headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            anchor.follow = 0.65f;

            // ---- 3. the gun sits closer, so the body's arms can actually hold it ----
            var weapon = cam.transform.Find("Weapon");
            var m1911 = weapon != null ? weapon.Find("ViewModel/Pistol/M1911") : null;

            Vector3 gripLocal = new Vector3(0f, -0.06f, -0.06f);
            Vector3 foregripLocal = new Vector3(0f, -0.037f, 0.062f);

            if (weapon != null)
            {
                weapon.localPosition = WeaponPosition;
                weapon.localRotation = Quaternion.identity;
                weapon.localScale = Vector3.one * WeaponScale;

                if (m1911 != null)
                {
                    // Offsets are relative to the pistol mesh's own origin, so they survive a
                    // viewmodel swap.
                    Vector3 origin = weapon.InverseTransformPoint(m1911.position);
                    gripLocal += origin;
                    foregripLocal += origin;
                }

                MakePoint(weapon, "Grip", gripLocal);
                MakePoint(weapon, "Foregrip", foregripLocal);
            }

            // ---- 4. the arms grip it ----
            var arms = body.GetComponent<FirstPersonArms>();
            if (arms == null) arms = body.gameObject.AddComponent<FirstPersonArms>();
            arms.animator = animator;
            arms.player = controller;
            arms.loadout = player.GetComponent<PlayerLoadout>();
            arms.grip = weapon != null ? weapon.Find("Grip") : null;
            arms.foregrip = weapon != null ? weapon.Find("Foregrip") : null;

            // ---- 5. the box gloves are redundant now ----
            var fists = cam.transform.Find("Fists");
            if (fists != null)
            {
                var gloves = fists.Find("ViewModel");
                if (gloves != null) Object.DestroyImmediate(gloves.gameObject);

                var fistsComponent = fists.GetComponent<Fists>();
                if (fistsComponent != null)
                {
                    fistsComponent.viewmodel = null;
                    fistsComponent.leftFist = null;
                    fistsComponent.rightFist = null;
                }
            }

            log.Append("anchor=on pivot=").Append(anchor.pivot != null ? anchor.pivot.name : "NULL")
               .Append(" head=").Append(anchor.headBone != null ? anchor.headBone.name : "NULL")
               .Append(" grip=").Append(gripLocal.ToString("F3"))
               .Append(" foregrip=").Append(foregripLocal.ToString("F3"))
               .Append(" glovesRemoved=").Append(fists != null ? "yes" : "no");

            return log.ToString();
        }

        static Transform MakePoint(Transform parent, string name, Vector3 localPosition)
        {
            var existing = parent.Find(name);
            var point = existing != null ? existing : new GameObject(name).transform;

            if (existing == null) point.SetParent(parent, false);

            point.localPosition = localPosition;
            point.localRotation = Quaternion.identity;
            point.localScale = Vector3.one;
            return point;
        }

        static void EnableIKPass(string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogWarning("[FirstPerson] no controller at " + controllerPath);
                return;
            }

            var layers = controller.layers;
            bool changed = false;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].iKPass) continue;
                layers[i].iKPass = true;
                changed = true;
            }

            if (!changed) return;

            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
