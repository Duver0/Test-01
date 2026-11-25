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
        [Tooltip("Every low-poly enemy body in the project, minus the player's own character. Each wave body picks one at random, so a sector is a mixed crowd of zombies, skeletons, knights and demons.")]
        public GameObject[] enemyModels;
        [Tooltip("Zombie pack characters, used only when enemyModels is empty.")]
        public GameObject[] zombieModels;
        [Tooltip("Body used by gunners and medics when enemyModels is empty.")]
        public GameObject skeletonModel;
        [Tooltip("Body used by brutes when enemyModels is empty.")]
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
            // The wave is still a fight, though: twenty bodies from the first sector, three more
            // each sector after that, so it is never a walkover.
            int total = 20 + Mathf.Max(0, round - 1) * 3;
            int brutes = Mathf.Max(1, Mathf.RoundToInt(total * 0.25f));
            int gunners = Mathf.Max(1, Mathf.RoundToInt(total * 0.35f));
            int zombies = Mathf.Max(0, total - brutes - gunners);
            Total = brutes + gunners + zombies;

            float health = 24f + 5f * round;
            float damageScale = 0.65f + 0.07f * round;
            float bodyHeight = TargetBodyHeight(player);

            for (int i = 0; i < brutes; i++)
                SpawnUnit(EnemyRole.Brute, health, damageScale, player, center, radius, i, bodyHeight);
            for (int i = 0; i < gunners; i++)
                SpawnUnit(EnemyRole.Gunner, health, damageScale, player, center, radius, 100 + i, bodyHeight);
            for (int i = 0; i < zombies; i++)
                SpawnZombie(player, center, radius, bodyHeight);
        }

        /// <summary>
        /// How tall the player's own body is, so the wave matches them. Measured off the humanoid
        /// under the player rather than the player root: the root also carries the camera and the
        /// weapon viewmodel, which would inflate the reading.
        /// </summary>
        static float TargetBodyHeight(PlayerController player)
        {
            if (player == null) return ModelHeight.Fallback;

            var animator = player.GetComponentInChildren<Animator>(true);
            float height = animator != null ? ModelHeight.Measure(animator.transform) : 0f;

            return height > 0.5f ? height : ModelHeight.Fallback;
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
                       Vector3 center, float radius, int seed, float bodyHeight)
        {
            var go = new GameObject(role.ToString());
            go.transform.position = SpawnPoint(player, center, radius);

            var unit = go.AddComponent<EnemyUnit>();
            unit.Initialize(role, health, player, null, center, radius, damageScale, seed * 7919 + _round,
                            ModelFor(role), bodyHeight);

            // The wave closes in and swings: these are the packs' melee bodies, not the squad boss's
            // gunner and medic, which keep their ranged and support jobs.
            unit.meleeOnly = true;

            unit.Died += OnUnitDied;
            _units.Add(unit);
        }

        void SpawnZombie(PlayerController player, Vector3 center, float radius, float bodyHeight)
        {
            var go = new GameObject("Zombie");
            go.transform.position = SpawnPoint(player, center, radius);

            var zombie = go.AddComponent<ZombieEnemyUnit>();
            zombie.Initialize(go.transform.position, player, RandomZombieModel(), bodyHeight);
            zombie.Died += OnZombieDied;
            _zombies.Add(zombie);
        }

        GameObject ModelFor(EnemyRole role)
        {
            GameObject pick = PickRandom(enemyModels);
            if (pick != null) return pick;

            return role == EnemyRole.Brute ? armoredSkeletonModel : skeletonModel;
        }

        GameObject RandomZombieModel()
        {
            GameObject pick = PickRandom(enemyModels);
            if (pick != null) return pick;

            return PickRandom(zombieModels);
        }

        /// <summary>One body from a pool, skipping empty slots, or null when the pool has nothing in it.</summary>
        static GameObject PickRandom(GameObject[] pool)
        {
            if (pool == null || pool.Length == 0) return null;

            int count = 0;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null) count++;
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;
                if (pick-- == 0) return pool[i];
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
