using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Player life. It only ever moves when something hurts the player or the vestibule sells a heal:
    /// there is no passive regeneration, so every point lost in a fight has to be bought back.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;

        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        public event System.Action HealthChanged;
        public event System.Action Died;

        bool _dead;

        void Awake()
        {
            Current = maxHealth;
        }

        void Start()
        {
            HealthChanged?.Invoke();
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 from)
        {
            if (_dead) return;
            Current = Mathf.Max(0f, Current - Mathf.Max(0f, amount));
            HealthChanged?.Invoke();

            if (Current <= 0f && !_dead)
            {
                _dead = true;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (_dead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            HealthChanged?.Invoke();
        }

        public void ResetHealth()
        {
            _dead = false;
            Current = maxHealth;
            HealthChanged?.Invoke();
        }
    }
}
