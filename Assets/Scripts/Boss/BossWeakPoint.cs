using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// The weak point mounted on a body. It is invisible and silent: no mesh, no glow, no pulse, no
    /// message. A ranged hit multiplies the shot's damage and then hands the owner a speed surge, so
    /// shooting one is a trade — double damage now, a much faster enemy for the next few seconds —
    /// and finding it is entirely on the player, by putting shots into the body and watching it move.
    /// It is deliberately not an IDamageable: melee and the body's own projectiles walk past it to
    /// the body behind.
    /// </summary>
    public class BossWeakPoint : MonoBehaviour, IRangedHitZone
    {
        [Tooltip("Shot damage is multiplied by this when the weak point is hit.")]
        public float damageMultiplier = 2f;

        IDamageable _owner;
        System.Action _onSurge;
        float _radius = 0.35f;
        bool _dead;

        public bool Active => !_dead && _owner != null && _owner.IsAlive;

        public void Initialize(IDamageable owner, System.Action onSurge, float radius)
        {
            _owner = owner;
            _onSurge = onSurge;

            // The hit sphere keeps the radius the point was placed with. It stays a solid collider and
            // never a trigger: bullets are cast with QueryTriggerInteraction.Ignore, so a trigger here
            // would make the point impossible to shoot.
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
        }

        public void RangedHit(float amount, Vector3 point, Vector3 from)
        {
            if (!Active) return;

            _owner.TakeDamage(Mathf.Max(0f, amount) * Mathf.Max(1f, damageMultiplier), point, from);
            if (_onSurge != null) _onSurge();
        }
    }
}
