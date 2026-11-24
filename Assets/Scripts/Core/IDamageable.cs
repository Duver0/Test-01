using UnityEngine;

namespace Slotgun
{
    /// <summary>Anything the player's bullets or the boss's attacks can hurt.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 point, Vector3 from);
    }
}
