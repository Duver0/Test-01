using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// The wave that stands between the player and the sector boss. Bodies are built at runtime, so
    /// the arena needs no prefab wiring: the director asks for a wave, the player clears it, and
    /// Cleared fires once the last body is down. Size and toughness climb with the sector.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Models (optional)")]
        [Tooltip("Zombie pack characters, picked at random for the shambling bodies.")]
        public GameObject[] zombieModels;
        [Tooltip("Body used by gunners and medics.")]
        public GameObject skeletonModel;
        [Tooltip("Body used by brutes.")]
        public GameObject armoredSkeletonModel;

        readonly List<EnemyUnit> _units = new List<EnemyUnit>();
        readonly List<ZombieEnemyUnit> _zombies = new List<ZombieEnemyUnit>();

        /// <summary>Bodies this wave started with.</summary>
        public int Total { get; private set; }

        /// <summary>Bodies still standing. A corpse that has already died does not count.</summary>
        public int Alive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _units.Count; i++)
                    if (_units[i] != null && _units[i].IsAlive) n++;
                for (int i = 0; i < _zombies.Count; i++)
                    if (_zombies[i] != null && _zombies[i].IsAlive) n++;
                return n;
            }
        }

        /// <summary>Fired once, the moment the last body of the wave goes down.</summary>
        public event System.Action Cleared;

        bool _cleared;
        int _round;

        public void Spawn(int round, PlayerController player, Vector3 center, float radius)
        {
            Clear();
            _round = round;
            _cleared = false;

            // A squad of brutes and gunners, plus zombies from the second sector on, is enough
            // variety without turning the warm-up into a second boss fight.
            int brutes = Mathf.Clamp(1 + round / 2, 1, 3);
            int gunners = Mathf.Clamp(1 + round / 2, 1, 3);
            int zombies = round >= 2 ? Mathf.Min(2, round - 1) : 0;
            Total = brutes + gunners + zombies;

            float health = 24f + 5f * round;
            float damageScale = 0.65f + 0.07f * round;

            for (int i = 0; i < brutes; i++)
                SpawnUnit(EnemyRole.Brute, health, damageScale, player, center, radius, i);
            for (int i = 0; i < gunners; i++)
                SpawnUnit(EnemyRole.Gunner, health, damageScale, player, center, radius, 100 + i);
            for (int i = 0; i < zombies; i++)
                SpawnZombie(player, center, radius);
        }

        /// <summary>Wipes the wave from outside — used when the sector is left or the run restarts.</summary>
        public void Clear()
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i] != null) Destroy(_units[i].gameObject);
            for (int i = 0; i < _zombies.Count; i++)
                if (_zombies[i] != null) Destroy(_zombies[i].gameObject);

            _units.Clear();
            _zombies.Clear();
            _cleared = false;
            Total = 0;
        }

        void SpawnUnit(EnemyRole role, float health, float damageScale, PlayerController player,
                       Vector3 center, float radius, int seed)
        {
            var go = new GameObject(role.ToString());
            go.transform.position = SpawnPoint(player, center, radius);

            var unit = go.AddComponent<EnemyUnit>();
            unit.Initialize(role, health, player, null, center, radius, damageScale, seed * 7919 + _round,
                            ModelFor(role));
            unit.Died += OnUnitDied;
            _units.Add(unit);
        }

        void SpawnZombie(PlayerController player, Vector3 center, float radius)
        {
            var go = new GameObject("Zombie");
            go.transform.position = SpawnPoint(player, center, radius);

            var zombie = go.AddComponent<ZombieEnemyUnit>();
            zombie.Initialize(go.transform.position, player, RandomZombieModel());
            zombie.Died += OnZombieDied;
            _zombies.Add(zombie);
        }

        GameObject ModelFor(EnemyRole role)
        {
            return role == EnemyRole.Brute ? armoredSkeletonModel : skeletonModel;
        }

        GameObject RandomZombieModel()
        {
            if (zombieModels == null || zombieModels.Length == 0) return null;

            int count = 0;
            for (int i = 0; i < zombieModels.Length; i++)
                if (zombieModels[i] != null) count++;
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < zombieModels.Length; i++)
            {
                if (zombieModels[i] == null) continue;
                if (pick-- == 0) return zombieModels[i];
            }
            return null;
        }

        void OnUnitDied(EnemyUnit unit)
        {
            unit.Died -= OnUnitDied;
            CheckCleared();
        }

        void OnZombieDied(ZombieEnemyUnit zombie)
        {
            zombie.Died -= OnZombieDied;
            CheckCleared();
        }

        void CheckCleared()
        {
            if (_cleared || Alive > 0) return;
            _cleared = true;
            Cleared?.Invoke();
        }

        /// <summary>A ring around the arena, never on top of the player.</summary>
        static Vector3 SpawnPoint(PlayerController player, Vector3 center, float radius)
        {
            Vector3 last = center + Vector3.forward * (radius * 0.8f);

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(radius * 0.55f, radius * 0.85f);
                last = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                if (player == null) return last;

                Vector3 flat = player.transform.position - last;
                flat.y = 0f;
                if (flat.magnitude > 12f) return last;
            }

            return last;
        }
    }
}
