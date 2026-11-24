using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// The small red circle mounted on a boss body. A ranged hit multiplies the shot's damage and
    /// then hands the owner a speed surge, so shooting it is a trade: double damage now, a much
    /// faster enemy for the next few seconds. It is deliberately not an IDamageable — melee and the
    /// boss's own projectiles walk past it to the body behind.
    /// </summary>
    public class BossWeakPoint : MonoBehaviour, IRangedHitZone
    {
        [Tooltip("Shot damage is multiplied by this when the circle is hit.")]
        public float damageMultiplier = 2f;
        [Tooltip("Idle scale pulse, so the circle reads from across the arena.")]
        public float pulseAmount = 0.12f;

        IDamageable _owner;
        System.Action _onSurge;
        Transform _disc;
        Vector3 _discScale = Vector3.one;
        float _radius = 0.35f;
        float _flashUntil = -1f;
        bool _dead;

        public bool Active => !_dead && _owner != null && _owner.IsAlive;

        public void Initialize(IDamageable owner, Transform disc, System.Action onSurge, float radius)
        {
            _owner = owner;
            _onSurge = onSurge;
            _disc = disc;
            if (_disc != null) _discScale = _disc.localScale;
            _flashUntil = -1f;

            // The hit sphere keeps the radius the visual was built from. It stays a solid collider and
            // never a trigger: bullets are cast with QueryTriggerInteraction.Ignore, so a trigger here
            // would make the circle impossible to shoot.
            _radius = radius;
            var coll = GetComponent<SphereCollider>();
            if (coll != null)
            {
                coll.isTrigger = false;
                coll.radius = _radius;
            }
        }

        /// <summary>Called when the owner dies, so a corpse cannot keep selling double damage.</summary>
        public void Kill()
        {
            _dead = true;
            if (_disc != null) _disc.localScale = _discScale;
        }

        public void RangedHit(float amount, Vector3 point, Vector3 from)
        {
            if (!Active) return;

            _owner.TakeDamage(Mathf.Max(0f, amount) * Mathf.Max(1f, damageMultiplier), point, from);
            _flashUntil = Time.time + 0.2f;
            if (_onSurge != null) _onSurge();
        }

        void Update()
        {
            if (_disc == null) return;

            float bump = 1f + Mathf.Sin(Time.time * 7.5f) * pulseAmount;
            if (Time.time < _flashUntil) bump += 0.55f;
            _disc.localScale = _discScale * bump;
        }
    }
}
