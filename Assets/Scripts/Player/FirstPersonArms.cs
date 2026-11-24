using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Makes the body's own hands hold what is on screen.
    ///
    /// The pistol is a camera-space viewmodel, so without this it hangs in the air in the corner of
    /// the screen with nothing touching it — which is exactly how it reads. This runs on the same
    /// GameObject as the body's Animator, and in the animation's IK pass it pulls both wrists onto
    /// the grip and the foregrip of whatever is out, so the hands you see holding the gun are the
    /// low-poly human's hands, attached where they should be and bobbing with it.
    ///
    /// When a melee slot is out the hands are released back to the clip, and a punch is delivered as
    /// a short reach toward the aim point instead of the old pair of floating boxes.
    ///
    /// Requires the Animator layer's IK Pass to be enabled, or Unity never calls OnAnimatorIK.
    /// </summary>
    public class FirstPersonArms : MonoBehaviour
    {
        [Header("Wiring")]
        public Animator animator;
        public PlayerController player;
        public PlayerLoadout loadout;

        [Tooltip("Empty on the weapon viewmodel, placed on the pistol grip.")]
        public Transform grip;

        [Tooltip("Empty on the weapon viewmodel, placed where the support hand wraps.")]
        public Transform foregrip;

        [Header("Grip")]
        [Range(0f, 1f)] public float handPositionWeight = 1f;

        [Tooltip("How much of the grip's rotation the palm takes. Partial, because the bone axes do not match the prop's.")]
        [Range(0f, 1f)] public float handRotationWeight = 0.6f;

        [Tooltip("How far back and down the elbows are pulled, in metres.")]
        public float elbowDrop = 0.22f;

        [Tooltip("Fraction of the arm's own length at which the grip starts to be given up, so a shot straight up does not dislocate a shoulder.")]
        [Range(0.5f, 1f)] public float reachLimit = 0.96f;

        [Header("Punch")]
        public float punchDuration = 0.18f;
        public float punchReach = 1.05f;
        [Range(0f, 1f)] public float punchWeight = 0.85f;

        Fists _fists;
        float _punchStart = -99f;
        int _punchSide;
        float _armLength = -1f;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (loadout == null) loadout = GetComponentInParent<PlayerLoadout>();

            _fists = loadout != null ? loadout.fists : GetComponentInParent<Fists>();
        }

        void OnEnable()
        {
            if (_fists == null) return;
            _fists.PunchThrust -= OnPunchThrust;
            _fists.PunchThrust += OnPunchThrust;
        }

        void OnDisable()
        {
            if (_fists != null) _fists.PunchThrust -= OnPunchThrust;
        }

        void OnPunchThrust(bool leftHand)
        {
            _punchStart = Time.time;
            _punchSide = leftHand ? 0 : 1;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            if (HoldingFirearm() && grip != null) GripWeapon();
            else PunchPose();
        }

        bool HoldingFirearm()
        {
            return loadout != null && loadout.Current != null && !loadout.Current.IsMelee;
        }

        void GripWeapon()
        {
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rightHand == null) return;

            Quaternion handRotation = grip.rotation;

            SetHand(AvatarIKGoal.RightHand, rightHand, grip.position, handRotation, ReachWeight(rightHand, grip.position, 1f));
            if (leftHand != null && foregrip != null)
                SetHand(AvatarIKGoal.LeftHand, leftHand, foregrip.position, foregrip.rotation, ReachWeight(leftHand, foregrip.position, 0.95f));
        }

        /// <summary>
        /// A hand that cannot reach the grip would otherwise be dragged out of its socket, so the
        /// weight fades near the end of the arm's own reach and the clip takes back over.
        /// </summary>
        float ReachWeight(Transform hand, Vector3 target, float scale)
        {
            var upper = hand.parent != null ? hand.parent.parent : null;   // wrist -> forearm -> upper arm
            if (upper == null) return handPositionWeight * scale;

            float reach = Vector3.Distance(upper.position, target);
            float armLength = ArmLength(upper, hand);

            float t = Mathf.InverseLerp(armLength * 0.8f, armLength * reachLimit, reach);
            return handPositionWeight * scale * (1f - t);
        }

        float ArmLength(Transform upper, Transform hand)
        {
            if (_armLength > 0f) return _armLength;

            var lower = hand.parent;
            if (lower == null) return _armLength = 0.6f;

            _armLength = Vector3.Distance(upper.position, lower.position) +
                         Vector3.Distance(lower.position, hand.position);
            return _armLength;
        }

        void SetHand(AvatarIKGoal goal, Transform hand, Vector3 position, Quaternion rotation, float weight)
        {
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight * handRotationWeight);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);

            var upper = hand.parent != null ? hand.parent.parent : null;
            if (upper == null || elbowDrop <= 0f) return;

            var hintGoal = goal == AvatarIKGoal.RightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;

            // Elbows hang down and out of the way rather than flaring up beside the ears.
            Vector3 chord = position - upper.position;
            Vector3 hint = upper.position + chord * 0.45f - Vector3.up * elbowDrop;

            animator.SetIKHintPositionWeight(hintGoal, weight);
            animator.SetIKHintPosition(hintGoal, hint);
        }

        void PunchPose()
        {
            ResetHand(AvatarIKGoal.RightHand);
            ResetHand(AvatarIKGoal.LeftHand);

            float elapsed = Time.time - _punchStart;
            if (elapsed < 0f || elapsed > punchDuration) return;

            var eye = player != null ? player.Eye : null;
            if (eye == null) return;

            var goal = _punchSide == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            var bone = animator.GetBoneTransform(_punchSide == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (bone == null) return;

            // Sines out and back, so the fist snaps forward and the clip catches the return.
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / punchDuration) * Mathf.PI);
            Vector3 target = eye.position + eye.forward * punchReach + Vector3.down * 0.18f;

            animator.SetIKPositionWeight(goal, punchWeight * k);
            animator.SetIKPosition(goal, target);
        }

        void ResetHand(AvatarIKGoal goal)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
        }
    }
}
