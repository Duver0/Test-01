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
        /// Camera-space placement of the pistol. This rig's arms are 0.53 m and the shoulders sit
        /// 0.16 m behind the eye, so a two-handed hold only exists close in: at 0.44 m the support
        /// hand falls 11 cm short of the gun, which is what read as a broken grip.
        /// </summary>
        static readonly Vector3 WeaponPosition = new Vector3(0.115f, -0.115f, 0.34f);

        /// <summary>
        /// Kept under 1 on purpose: the pack's pistol imports 0.345 m long, which is 1.6x a real 1911
        /// and nearly twice the width of the body's own hand. At 0.30 it reads 0.21 m, and the hand
        /// that holds it is finally in proportion with it.
        /// </summary>
        const float WeaponScale = 0.30f;

        // Grip points, as offsets from the pistol's own magazine mesh (its handle), in weapon space:
        // the palm wraps the backstrap, and the support hand closes under it on the front strap.
        static readonly Vector3 GripFromHandle = new Vector3(0f, 0.10f, -0.06f);
        static readonly Vector3 ForegripFromHandle = new Vector3(-0.05f, -0.09f, -0.02f);

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

            if (weapon != null)
            {
                weapon.localPosition = WeaponPosition;
                weapon.localRotation = Quaternion.identity;
                weapon.localScale = Vector3.one * WeaponScale;

                // The Grip empties are placed off the pistol's handle rather than off the mesh's
                // origin: the model's pivot sits well above and right of the magazine, which is why
                // the hands used to close on empty air beside the grip.
                Vector3 handle = HandleInWeaponSpace(weapon, m1911);
                MakePoint(weapon, "Grip", handle + GripFromHandle);
                MakePoint(weapon, "Foregrip", handle + ForegripFromHandle);
            }

            // ---- 4. the arms grip it ----
            var arms = body.GetComponent<FirstPersonArms>();
            if (arms == null) arms = body.gameObject.AddComponent<FirstPersonArms>();
            arms.animator = animator;
            arms.player = controller;
            arms.loadout = player.GetComponent<PlayerLoadout>();
            arms.grip = weapon != null ? weapon.Find("Grip") : null;
            arms.foregrip = weapon != null ? weapon.Find("Foregrip") : null;

            // Only the wrist offset is left to the script now that the empties sit on the handle.
            arms.gripBack = 0.03f;
            arms.gripDown = 0.012f;
            arms.foregripBack = 0.03f;
            arms.foregripDown = 0.016f;

            // This rig's arm is 0.52 m and the grip sits 0.52 m from the shoulder, so the reach band has
            // to start at the arm's length rather than short of it, or the weight is zero and the hand
            // never takes hold at all.
            arms.reachLimit = 1.15f;

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
               .Append(" grip=").Append(arms.grip != null ? arms.grip.localPosition.ToString("F3") : "NULL")
               .Append(" foregrip=").Append(arms.foregrip != null ? arms.foregrip.localPosition.ToString("F3") : "NULL")
               .Append(" glovesRemoved=").Append(fists != null ? "yes" : "no");

            return log.ToString();
        }

        /// <summary>
        /// Where the pistol's handle actually is, in the weapon's own space. The magazine mesh is the
        /// handle; the M1911 mesh's pivot is not, and using it put both hands above the gun.
        /// </summary>
        static Vector3 HandleInWeaponSpace(Transform weapon, Transform m1911)
        {
            foreach (var mr in weapon.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.name == "Magazin") return weapon.InverseTransformPoint(mr.bounds.center);

            return m1911 != null ? weapon.InverseTransformPoint(m1911.position) : Vector3.zero;
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
