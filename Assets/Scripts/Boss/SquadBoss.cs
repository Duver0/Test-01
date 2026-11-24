using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// A squad encounter: three bodies with three jobs (melee, ranged, support) sharing one health
    /// bar. It presents itself as a single boss — same interface, same bar, same door — while every
    /// body is an independent EnemyUnit with its own weak point and its own way of hurting you.
    /// </summary>
    public class SquadBoss : MonoBehaviour, IBossEncounter
    {
        public BossConfig config;
        public PlayerController target;
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 26f;

        [Header("Models (optional)")]
        [Tooltip("Body used by the gunner and the medic.")]
        public GameObject skeletonModel;
        [Tooltip("Body used by the brute.")]
        public GameObject armoredSkeletonModel;

        [Header("Third-health retaliation")]
        [Tooltip("Ammo packs shed each time the squad loses a third of its pooled health.")]
        public int ammoDropsPerThreshold = 3;
        [Tooltip("How long the surviving bodies attack faster after a threshold is crossed.")]
        public float enrageSeconds = 3.5f;

        public float MaxHealth { get; private set; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public int Phase { get; private set; } = 1;
        public string DisplayName => config != null ? config.displayName : "TRÍO";
        public float HealthFraction => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Current / MaxHealth);
        public BossConfig Config => config;

        /// <summary>Fastest living body, so the player's sprint rule also works against a squad.</summary>
        public float ChaseSpeed
        {
            get
            {
                float best = 0f;
                for (int i = 0; i < _units.Count; i++)
                    if (_units[i] != null && _units[i].IsAlive) best = Mathf.Max(best, _units[i].ChaseSpeed);
                return best;
            }
        }
        public IReadOnlyList<EnemyUnit> Units => _units;

        public event System.Action BossStateChanged;
        public event System.Action<IBossEncounter> Defeated;

        readonly List<EnemyUnit> _units = new List<EnemyUnit>();
        static readonly float[] DropThresholds = { 0.66f, 0.33f };
        int _dropIndex;
        bool _dead;

        public void Initialize(BossConfig cfg, PlayerController player, Vector3 center, float radius)
        {
            config = cfg;
            target = player;
            arenaCenter = center;
            arenaRadius = radius;

            float pool = Mathf.Max(150f, cfg.maxHealth);

            // The brute is the bulk, the gunner the pressure, the medic the small fragile one.
            SpawnUnit(EnemyRole.Brute,  pool * 0.45f, 4.2f, 0f);
            SpawnUnit(EnemyRole.Gunner, pool * 0.33f, 4.8f, 120f);
            SpawnUnit(EnemyRole.Medic,  pool * 0.22f, 5.4f, 240f);

            MaxHealth = 0f;
            for (int i = 0; i < _units.Count; i++) MaxHealth += _units[i].MaxHealth;

            Current = MaxHealth;
            Phase = 1;
            BossStateChanged?.Invoke();
        }

        void SpawnUnit(EnemyRole role, float health, float distance, float angle)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 position = transform.position + dir * distance;

            Vector3 flat = position - arenaCenter;
            flat.y = 0f;
            float limit = Mathf.Max(2f, arenaRadius - 3f);
            if (flat.magnitude > limit) position = arenaCenter + flat.normalized * limit;

            position.y = transform.position.y;

            var go = new GameObject(role.ToString());
            go.transform.position = position;
            go.transform.SetParent(transform, true);

            var unit = go.AddComponent<EnemyUnit>();
            unit.Initialize(role, health, target, this, arenaCenter, arenaRadius,
                            config != null ? config.damageScale : 1f, 101 + (int)role * 17,
                            role == EnemyRole.Brute ? armoredSkeletonModel : skeletonModel);
            unit.Died += OnUnitDied;
            _units.Add(unit);
        }

        void OnUnitDied(EnemyUnit unit)
        {
            if (unit != null) unit.Died -= OnUnitDied;
            RefreshHealth();
        }

        /// <summary>
        /// Every body reports here after taking damage or being healed, so the single health bar,
        /// the phase and the third-health thresholds all read the pooled total.
        /// </summary>
        public void RefreshHealth()
        {
            if (_dead) return;

            float sum = 0f;
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit != null && unit.IsAlive) sum += unit.Current;
            }

            Current = Mathf.Max(0f, sum);

            int next = HealthFraction > 0.66f ? 1 : (HealthFraction > 0.33f ? 2 : 3);
            if (next != Phase)
            {
                Phase = next;
                BossStateChanged?.Invoke();
            }

            CheckThresholds();

            if (Current <= 0f) Die();
        }

        void CheckThresholds()
        {
            float fraction = HealthFraction;
            while (_dropIndex < DropThresholds.Length && fraction <= DropThresholds[_dropIndex])
            {
                _dropIndex++;
                DropAmmo();
                EnrageAll();
            }
        }

        void DropAmmo()
        {
            AmmoPickup.SpawnBurst(Centroid(), Mathf.Max(1, ammoDropsPerThreshold), arenaCenter, arenaRadius);
        }

        Vector3 Centroid()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive) continue;
                sum += unit.transform.position;
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }

        void EnrageAll()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit != null && unit.IsAlive) unit.Enrage(enrageSeconds);
            }

            if (GameDirector.Instance != null)
                GameDirector.Instance.Say("EL TRÍO SE ENFURECE · RECOGE LA MUNICIÓN");
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            Current = 0f;
            Defeated?.Invoke(this);
        }
    }
}
