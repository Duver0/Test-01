using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Locks the first-person camera to the animated body it lives inside.
    ///
    /// The body runs a real run cycle, which bobs and sways the head; a camera bolted at a fixed
    /// height above the Player root does not move with it, so the two read as separate objects —
    /// the world stays still while the shoulders pump. This samples the head bone every frame and
    /// carries the camera with it, scaled and clamped so a fast clip cannot shake the view apart or
    /// push the near plane through a wall.
    ///
    /// Rotation is deliberately left alone: yaw lives on the Player root and pitch on the pivot
    /// itself, so writing the pivot's local position never fights the mouse look.
    /// </summary>
    public class BodyCameraAnchor : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The camera pivot. Empty resolves it from PlayerController.cam's parent.")]
        public Transform pivot;

        [Tooltip("The head bone. Empty resolves it from the humanoid Animator on the Body child.")]
        public Transform headBone;

        [Header("Follow")]
        [Tooltip("0 stays bolted to the Player root, 1 rides the head bone exactly. Partial is what keeps a run cycle from being nauseating.")]
        [Range(0f, 1f)] public float follow = 0.65f;

        [Tooltip("Metres the camera may drift from its resting spot, so one bad frame cannot throw the view.")]
        public float maxOffset = 0.14f;

        [Tooltip("Metres per second the camera may travel towards the bone; keeps a hitch from snapping the view.")]
        public float maxSpeed = 6f;

        Vector3 _rest;
        bool _ready;

        void Start()
        {
            Resolve();
        }

        void Resolve()
        {
            if (pivot == null)
            {
                var controller = GetComponent<PlayerController>();
                if (controller != null && controller.cam != null) pivot = controller.cam.transform.parent;
            }

            if (headBone == null)
            {
                var body = transform.Find("Body");
                var animator = body != null ? body.GetComponent<Animator>() : null;
                if (animator != null && animator.isHuman)
                    headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (pivot != null)
            {
                _rest = pivot.localPosition;
                _ready = true;
            }
        }

        void LateUpdate()
        {
            if (!_ready)
            {
                Resolve();
                if (!_ready) return;
            }

            Vector3 wanted = transform.InverseTransformPoint(headBone.position);
            Vector3 offset = (wanted - _rest) * follow;

            if (offset.sqrMagnitude > maxOffset * maxOffset)
                offset = offset.normalized * maxOffset;

            Vector3 target = _rest + offset;

            // A per-frame cap, not smoothing: the camera must stay welded to the body, but a hitch
            // in the animation should not teleport it.
            Vector3 current = pivot.localPosition;
            float step = maxSpeed * Time.deltaTime;
            pivot.localPosition = Vector3.MoveTowards(current, target, step);
        }
    }
}
