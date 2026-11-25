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

        [Tooltip("Roll of the right palm around the handle, in degrees. Only needed if a pack's hand bone disagrees with its own knuckles.")]
        public float rightHandRoll = 0f;

        [Tooltip("Roll of the left palm around the handle, in degrees.")]
        public float leftHandRoll = 0f;

        [Tooltip("The wrist is pulled back along the weapon so the palm closes on the grip instead of passing through the pistol.")]
        public float gripBack = 0.03f;

        [Tooltip("And dropped a little, so the fingers come round under the trigger guard rather than through the slide.")]
        public float gripDown = 0.012f;

        [Tooltip("The support hand sits further forward on the frame, so it needs less of both.")]
        public float foregripBack = 0.03f;
        public float foregripDown = 0.016f;

        [Tooltip("How far back and down the elbows are pulled, in metres.")]
        public float elbowDrop = 0.22f;

        [Tooltip("Fraction of the arm's own length at which the grip starts to be given up. Above 1 the hand is allowed to over-extend slightly, which is what lets a short-armed rig still hold a viewmodel pistol.")]
        [Range(0.9f, 1.6f)] public float reachLimit = 1.15f;

        [Header("Punch")]
        public float punchDuration = 0.18f;
        public float punchReach = 1.05f;
        [Range(0f, 1f)] public float punchWeight = 0.85f;

        Fists _fists;
        float _punchStart = -99f;
        int _punchSide;
        float _armLength = -1f;

        // The palm turn is applied to the bones themselves in LateUpdate. It is captured during the
        // IK pass instead, because that is the only moment the pre-IK clip pose can be read.
        Transform _palmRightBone;
        Transform _palmLeftBone;
        Quaternion _palmRightTurn;
        Quaternion _palmLeftTurn;
        float _palmRightWeight;
        float _palmLeftWeight;
        bool _palmsCaptured;

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

        /// <summary>
        /// Writes the palm turn onto the two wrist bones after the animator has posed the body. The
        /// rotation IK goal is ignored on this rig's avatar — measured, the wrist-to-knuckle line came
        /// out 80 degrees off what was asked for and the back of the hand 70 degrees off — which is why
        /// the pistol used to sit in the fist sideways no matter how the position was tuned.
        /// </summary>
        void LateUpdate()
        {
            if (!_palmsCaptured) return;
            _palmsCaptured = false;

            if (_palmRightBone != null && _palmRightWeight > 0.001f)
                _palmRightBone.rotation = Quaternion.Slerp(_palmRightBone.rotation, _palmRightTurn, _palmRightWeight);

            if (_palmLeftBone != null && _palmLeftWeight > 0.001f)
                _palmLeftBone.rotation = Quaternion.Slerp(_palmLeftBone.rotation, _palmLeftTurn, _palmLeftWeight);
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

            // The Grip and Foregrip empties sit on the pistol's own handle - the magazine mesh and the
            // front strap - so the target is the palm's contact point, not a guess in the weapon's
            // local space. Everything else here only pulls the wrist back off that point.
            SetHand(AvatarIKGoal.RightHand, rightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbDistal,
                    grip, gripBack, gripDown, 1f, rightHandRoll);

            if (leftHand != null && foregrip != null)
                SetHand(AvatarIKGoal.LeftHand, leftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbDistal,
                        foregrip, foregripBack, foregripDown, 0.95f, leftHandRoll);
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

            // The pistol's grip sits a little beyond this rig's own arm length — 0.52 m of arm against a
            // 0.56 m reach — so the band starts at the arm length itself and the hand is allowed to
            // stretch a few percent rather than let go. Fading from 0.8 of the arm (the old band)
            // zeroed the weight outright, which is why the hand hung beside the gun instead of on it.
            float t = Mathf.InverseLerp(armLength * reachLimit, armLength * 1.6f, reach);
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

        void SetHand(AvatarIKGoal goal, Transform hand, HumanBodyBones knuckleBone, HumanBodyBones thumbBone,
                     Transform point, float back, float down, float scale, float roll)
        {
            Vector3 target = point.position - point.forward * back - point.up * down;
            float weight = ReachWeight(hand, target, scale);

            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, target);

            // Rotation is taken off the IK goal on purpose: it does not reach the bone on this rig, so
            // the turn is captured here and written in LateUpdate, after the pose has been evaluated.
            animator.SetIKRotationWeight(goal, 0f);
            Quaternion palm = PalmRotation(hand, knuckleBone, thumbBone, point, goal == AvatarIKGoal.RightHand, roll);

            if (goal == AvatarIKGoal.RightHand)
            {
                _palmRightBone = hand;
                _palmRightTurn = palm;
                _palmRightWeight = weight;
            }
            else
            {
                _palmLeftBone = hand;
                _palmLeftTurn = palm;
                _palmLeftWeight = weight;
            }

            _palmsCaptured = true;

            var upper = hand.parent != null ? hand.parent.parent : null;
            if (upper == null || elbowDrop <= 0f) return;

            var hintGoal = goal == AvatarIKGoal.RightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;

            // Elbows hang down and out of the way rather than flaring up beside the ears.
            Vector3 chord = target - upper.position;
            Vector3 hint = upper.position + chord * 0.45f - Vector3.up * elbowDrop;

            animator.SetIKHintPositionWeight(hintGoal, weight);
            animator.SetIKHintPosition(hintGoal, hint);
        }

        /// <summary>
        /// Places the wrist on the grip, then turns the hand so the palm closes on it. Both axes are
        /// read off the rig itself instead of being assumed: the knuckle bone gives the finger
        /// direction, and the thumb crossed with it gives which way the palm faces. A pack whose hand
        /// is bound the other way round therefore still holds the pistol rather than back-handing it,
        /// and no per-pack rotation offset has to be dialled in by eye.
        /// </summary>
        Quaternion PalmRotation(Transform hand, HumanBodyBones knuckleBone, HumanBodyBones thumbBone,
                                Transform point, bool right, float roll)
        {
            Vector3 fingers = BoneDirection(hand, knuckleBone, hand.forward);

            // Knuckles point down the barrel, back of the hand faces out along the weapon's own right.
            Vector3 wantFingers = point.forward - point.up * 0.3f;
            Vector3 wantBack = right ? point.right : -point.right;

            Quaternion align = Quaternion.FromToRotation(fingers, wantFingers);
            Quaternion turned = align * hand.rotation;

            Vector3 thumb = BoneDirection(hand, thumbBone, Vector3.zero);
            if (thumb.sqrMagnitude > 0.5f)
            {
                // Thumb cross knuckles is the palm's normal, i.e. the way the back of the hand faces.
                Vector3 back = right ? Vector3.Cross(thumb, fingers) : Vector3.Cross(fingers, thumb);
                if (back.sqrMagnitude > 0.01f)
                {
                    Vector3 backNow = align * back.normalized;
                    turned = Quaternion.FromToRotation(backNow, wantBack) * turned;
                }
            }

            if (Mathf.Abs(roll) > 0.01f) turned = Quaternion.AngleAxis(roll, wantFingers) * turned;
            return turned;
        }

        Vector3 BoneDirection(Transform from, HumanBodyBones bone, Vector3 fallback)
        {
            var t = animator.GetBoneTransform(bone);
            if (t == null) return fallback;

            Vector3 d = t.position - from.position;
            return d.sqrMagnitude > 0.000001f ? d.normalized : fallback;
        }

        void PunchPose()
        {
            _palmsCaptured = false;   // a punch is the clip's own hand again
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
