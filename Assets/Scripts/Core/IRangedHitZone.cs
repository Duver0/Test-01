using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// A small collider that intercepts ranged fire before the body behind it. Weapons hand the hit
    /// to the zone instead of straight to IDamageable, so the zone owns its own damage multiplier
    /// and its own reaction. Melee and enemy projectiles never see it: hitting a weak point with a
    /// fist or with a boss shot resolves against the body like any other hit.
    /// </summary>
    public interface IRangedHitZone
    {
        bool Active { get; }
        void RangedHit(float amount, Vector3 point, Vector3 from);
    }
}
