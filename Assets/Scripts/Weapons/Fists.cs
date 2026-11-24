using System.Collections;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Slot 1. When the magazines run dry this is what the player is left holding, and pressing 3
    /// with no third weapon also lands here. A short thrust that damages whatever is in front of
    /// the camera; no ammo, no reload, no magazine size.
    /// </summary>
    public class Fists : MonoBehaviour, IRuntimeWeapon
    {
        [Header("Wiring")]
        public PlayerController player;
        [Tooltip("Optional. The old floating box gloves. Left empty, the body's own hands are what the player punches with.")]
        public Transform viewmodel;
        public Transform leftFist;
        public Transform rightFist;

        [Header("Hit")]
        public float damage = 24f;
        public float punchesPerSecond = 2.6f;
        [Tooltip("How far in front of the eye the punch sphere is centred, in metres.")]
        public float reach = 2.7f;
        [Tooltip("Radius of the punch sphere. Generous on purpose: a fist should be forgiving.")]
        public float punchRadius = 0.9f;

        public string DisplayName => "PUÑOS";
        public bool IsMelee => true;
        public bool HasAnyAmmo => true;

        public event System.Action HitConfirmed;

        /// <summary>
        /// Fires on every punch with the hand that threw it (true = left). The visual is a reach in
        /// <see cref="FirstPersonArms"/>: the box gloves this used to animate are gone, the body has
        /// real hands now, and it is the pose that has to move.
        /// </summary>
        public event System.Action<bool> PunchThrust;

        const int BufferSize = 32;
        static readonly Collider[] Hits = new Collider[BufferSize];

        Vector3 _leftHome;
        Vector3 _rightHome;
        bool _held;
        bool _leftHand = true;
        float _nextPunch;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (leftFist != null) _leftHome = leftFist.localPosition;
            if (rightFist != null) _rightHome = rightFist.localPosition;
        }

        void Update()
        {
            if (_held) Punch();
        }

        public void SetTrigger(bool pressed)
        {
            if (!enabled) return;
            if (pressed && !_held) Punch();
            _held = pressed;
        }

        public void RequestReload()
        {
            // nothing to reload with your hands
        }

        public void SetVisible(bool visible)
        {
            if (viewmodel != null) viewmodel.gameObject.SetActive(visible);
        }

        public void SetInteractive(bool interactive)
        {
            enabled = interactive;
            if (!interactive) _held = false;
        }

        public bool Punch()
        {
            if (Time.time < _nextPunch) return false;
            _nextPunch = Time.time + 1f / Mathf.Max(0.4f, punchesPerSecond);

            Transform hand = _leftHand ? leftFist : rightFist;
            Vector3 home = _leftHand ? _leftHome : _rightHome;
            bool punchedLeft = _leftHand;
            _leftHand = !_leftHand;
            if (hand != null) StartCoroutine(Thrust(hand, home));
            PunchThrust?.Invoke(punchedLeft);

            if (player == null) return false;
            Transform eye = player.Eye;
            if (eye == null) return false;

            Vector3 center = eye.position + eye.forward * (reach * 0.55f);
            int count = Physics.OverlapSphereNonAlloc(center, punchRadius + 0.45f, Hits, ~0, QueryTriggerInteraction.Ignore);

            IDamageable best = null;
            float bestDistance = float.MaxValue;
            Vector3 bestPoint = center;

            for (int i = 0; i < count; i++)
            {
                var col = Hits[i];
                if (col == null) continue;
                // never punch yourself: the player's own CharacterController sits inside the sphere
                if (col.transform.IsChildOf(player.transform)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                Vector3 closest = col.ClosestPoint(center);
                float distance = (closest - center).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = target;
                bestPoint = closest;
            }

            if (best == null) return false;

            best.TakeDamage(damage, bestPoint, eye.position);
            HitConfirmed?.Invoke();
            return true;
        }

        IEnumerator Thrust(Transform hand, Vector3 home)
        {
            const float duration = 0.16f;
            float t = 0f;
            while (t < duration && hand != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                hand.localPosition = home + Vector3.forward * (0.26f * k) - Vector3.up * (0.05f * k);
                yield return null;
            }
            if (hand != null) hand.localPosition = home;
        }
    }
}
