using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Moves an imported humanoid body: either by playing clips through an Animator, or — when the
    /// rig has no usable clips — by posing the legs and arms from code.
    ///
    /// The clip path is the one to prefer: the Blink "FREE Low Poly Human" pack in this project
    /// ships Humanoid clips (Idle / RunForward / Sprint), and because they are Humanoid they
    /// retarget onto any Humanoid avatar, so the same controller drives the player, the zombies and
    /// the skeletons. Speed is fed to the controller's "Speed" float, 0 = idle, 1 = run, 2 = sprint.
    ///
    /// The procedural path exists for rigs with no clips at all (the zombie and skeleton "Lite"
    /// packs import with zero animation clips), and it supports both naming schemes in the project:
    /// the zombie packs' rigify bones (thigh.L / shin.L / upper_arm.L) and the Polytope skeletons'
    /// bones (PT_LeftUpLeg / PT_LeftLeg / PT_LeftArm).
    /// </summary>
    public class ModelLocomotion : MonoBehaviour
    {
        [Header("Clip-driven (preferred)")]
        [Tooltip("When set, the rig is animated by humanoid clips through this Animator instead of being posed by hand.")]
        public Animator animator;

        [Tooltip("Animator float that selects the locomotion blend: 0 idle, 1 run, 2 sprint.")]
        public string speedParameter = "Speed";

        [Tooltip("Metres per second that map to Speed = 1 (the run clip).")]
        public float runSpeed = 4f;

        [Tooltip("Metres per second that map to Speed = 2 (the sprint clip).")]
        public float sprintSpeed = 8f;

        [Header("Procedural fallback (rigs with no clips)")]
        [Tooltip("Degrees the thigh swings either side of rest at full speed.")]
        public float legSwing = 34f;

        [Tooltip("Degrees the upper arm counter-swings at full speed.")]
        public float armSwing = 20f;

        [Tooltip("Metres per second that count as a full stride.")]
        public float fullSpeed = 4f;

        [Tooltip("Cycle radians per metre travelled — a longer stride means a slower cycle.")]
        public float stridePerMetre = 2.4f;

        [Tooltip("Constant forward lean of the torso while moving, in degrees. Zombies hunch.")]
        public float lean = 6f;

        Transform _leftUpLeg, _rightUpLeg, _leftLeg, _rightLeg;
        Transform _leftArm, _rightArm, _leftForeArm, _rightForeArm, _spine;

        Quaternion _leftUpLegRest, _rightUpLegRest, _leftLegRest, _rightLegRest;
        Quaternion _leftArmRest, _rightArmRest, _leftForeArmRest, _rightForeArmRest, _spineRest;

        Vector3 _lastPosition;
        float _phase;
        float _speed;

        bool UsesClips => animator != null && animator.runtimeAnimatorController != null;

        void Awake()
        {
            _lastPosition = transform.position;
            if (!UsesClips) Resolve();
        }

        void Resolve()
        {
            _leftUpLeg = Find("thigh.l", "pt_leftupleg");
            _rightUpLeg = Find("thigh.r", "pt_rightupleg");
            _leftLeg = Find("shin.l", "pt_leftleg");
            _rightLeg = Find("shin.r", "pt_rightleg");
            _leftArm = Find("upper_arm.l", "pt_leftarm");
            _rightArm = Find("upper_arm.r", "pt_rightarm");
            _leftForeArm = Find("forearm.l", "pt_leftforearm");
            _rightForeArm = Find("forearm.r", "pt_rightforearm");
            _spine = Find("spine", "pt_spine2");

            if (_leftUpLeg != null) _leftUpLegRest = _leftUpLeg.localRotation;
            if (_rightUpLeg != null) _rightUpLegRest = _rightUpLeg.localRotation;
            if (_leftLeg != null) _leftLegRest = _leftLeg.localRotation;
            if (_rightLeg != null) _rightLegRest = _rightLeg.localRotation;
            if (_leftArm != null) _leftArmRest = _leftArm.localRotation;
            if (_rightArm != null) _rightArmRest = _rightArm.localRotation;
            if (_leftForeArm != null) _leftForeArmRest = _leftForeArm.localRotation;
            if (_rightForeArm != null) _rightForeArmRest = _rightForeArm.localRotation;
            if (_spine != null) _spineRest = _spine.localRotation;
        }

        Transform Find(params string[] names)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.Trim().ToLowerInvariant();
                for (int i = 0; i < names.Length; i++)
                    if (n == names[i]) return t;
            }
            return null;
        }

        void LateUpdate()
        {
            float speed = Measure();

            if (UsesClips)
            {
                DriveAnimator(speed);
                return;
            }

            if (_leftUpLeg == null) return;   // not a humanoid rig: nothing to drive

            float amp = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeed));
            _phase += speed * stridePerMetre * Time.deltaTime;
            if (_phase > 6283.185f) _phase -= 6283.185f;   // keep the sine argument sane over long sessions

            float stride = Mathf.Sin(_phase) * legSwing * amp;

            Swing(_leftUpLeg, _leftUpLegRest, stride);
            Swing(_rightUpLeg, _rightUpLegRest, -stride);

            // A knee only folds one way, so the shin follows the back half of the step.
            Swing(_leftLeg, _leftLegRest, Mathf.Max(0f, -Mathf.Sin(_phase)) * 46f * amp);
            Swing(_rightLeg, _rightLegRest, Mathf.Max(0f, Mathf.Sin(_phase)) * 46f * amp);

            float arms = stride * (armSwing / Mathf.Max(1f, legSwing));
            Swing(_leftArm, _leftArmRest, -arms);
            Swing(_rightArm, _rightArmRest, arms);

            // A whisper of elbow bend so the forearms are not dead straight.
            Swing(_leftForeArm, _leftForeArmRest, -8f * amp);
            Swing(_rightForeArm, _rightForeArmRest, -8f * amp);

            // Forward lean plus a breathing sway, which is the whole idle tell.
            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.9f;
            Swing(_spine, _spineRest, lean * amp + breathe);
        }

        /// <summary>Planar speed of the body, smoothed, read from how far it actually moved.</summary>
        float Measure()
        {
            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;

            float instant = Time.deltaTime > 0.0001f ? delta.magnitude / Time.deltaTime : 0f;
            instant = Mathf.Clamp(instant, 0f, Mathf.Max(fullSpeed, sprintSpeed) * 1.5f);

            _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-12f * Time.deltaTime));
            _lastPosition = transform.position;
            return _speed;
        }

        void DriveAnimator(float speed)
        {
            float run = Mathf.Max(0.01f, runSpeed);
            float sprint = Mathf.Max(run + 0.01f, sprintSpeed);

            // Below the run speed the blend walks idle->run; above it, run->sprint.
            float value = speed <= run
                ? Mathf.Lerp(0f, 1f, speed / run)
                : Mathf.Lerp(1f, 2f, Mathf.Clamp01((speed - run) / (sprint - run)));

            animator.SetFloat(speedParameter, value);
        }

        /// <summary>
        /// Rotates a bone around the character's lateral axis, applied on top of the rest pose it
        /// was authored with. Working in world space keeps it independent of both rigs' bone axes.
        /// </summary>
        void Swing(Transform bone, Quaternion rest, float degrees)
        {
            if (bone == null) return;
            if (bone.parent == null || Mathf.Abs(degrees) < 0.001f)
            {
                bone.localRotation = rest;
                return;
            }

            bone.rotation = Quaternion.AngleAxis(degrees, transform.right) * (bone.parent.rotation * rest);
        }
    }
}
