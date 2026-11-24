using System.Collections;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Zombie enemy variant with reduced health and speed compared to normal enemies.
    /// Shares core components (health, movement, weak point) from EnemyUnit but with zombie stats.
    /// </summary>
    public class ZombieEnemyUnit : MonoBehaviour, IDamageable
    {
        [Header("Zombie Stats")]
        [Tooltip("Maximum health for the zombie (normal enemies have higher health)")]
        public float maxHealth = 25f;
        
        [Tooltip("Movement speed multiplier")]
        public float moveSpeed = 3.5f;
        
        [Tooltip("Weak point surge multiplier when hit")]
        public float weakSurgeMultiplier = 1.5f;
        
        [Tooltip("Duration of weak point surge in seconds")]
        public float weakSurgeSeconds = 5f;

        [Header("Attack")]
        [Tooltip("Damage of one swipe.")]
        public float contactDamage = 9f;
        [Tooltip("Seconds between swipes while the player is in reach.")]
        public float attackInterval = 1.5f;
        [Tooltip("Reach in metres. A zombie only hurts what it can touch.")]
        public float attackRange = 2.1f;

        [Header("Visuals")]
        public Color zombieColor = new Color(0.2f, 0.2f, 0.2f, 1f);  // Dark grayish
        public Color accentColor = new Color(0.8f, 0.4f, 0f, 1f);    // Orange-ish for wounds

        // Cached components
        private Transform _visual;
        private BossWeakPoint _weak;
        private CapsuleCollider _collider;
        private PlayerController _target;
        private bool _usesModel;
        
        // State
        private float _currentHealth;
        private float _spawnTime;

        public bool IsAlive => _currentHealth > 0f;
        private Vector3 _home;
        private float _weakUntil = -1f;
        private float _nextAttack;
        private bool _dead;
        
        // Death event
        public event System.Action<ZombieEnemyUnit> Died;

        /// <summary>Animator controller shared by every Humanoid body in the game, loaded from Resources.</summary>
        const string HumanoidLocomotionResource = "HumanoidLocomotion";

        // Weak surge property
        public bool WeakSurgeActive => Time.time < _weakUntil;

        /// <summary>
        /// Initialize the zombie enemy.
        /// </summary>
        public void Initialize(Vector3 spawnPosition, PlayerController player, GameObject model = null)
        {
            _currentHealth = maxHealth;
            _target = player;
            _home = spawnPosition;
            transform.position = spawnPosition;
            _spawnTime = Time.time;
            
            // Setup collider
            _collider = gameObject.AddComponent<CapsuleCollider>();
            if (model != null)
            {
                // Fit the capsule to the imported body: the character is thinner than the blocky one.
                _collider.center = new Vector3(0f, 0.95f, 0f);
                _collider.height = 1.9f;
                _collider.radius = 0.34f;
            }
            else
            {
                _collider.center = new Vector3(0f, 0.9f, 0f);
                _collider.height = 1.8f;
                _collider.radius = 0.42f;
            }
            
            // Setup visual
            if (model != null) BuildModel(model);
            else BuildVisual();
            
            // Attach weak point
            _weak = BossVisual.AttachWeakPoint(transform, WeakPointLocal(), WeakPointRadius(), this, NotifyWeakHit);
            
            _spawnTime = Time.time;
        }

        void Update()
        {
            if (!IsAlive) return;
            
            // Despawn after 1 minute if not killed
            if (Time.time - _spawnTime > 60f)
            {
                Die();
                return;
            }
            
            // Movement toward player
            MoveTowardsPlayer();
            Attack();
            
        }

        void MoveTowardsPlayer()
        {
            if (_target == null) return;
            
            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            
            if (dist < 0.001f) return;
            
            Vector3 dir = to / dist;
            
            // A body whose weak point was just hit surges: that is the whole price of the red circle.
            float speed = moveSpeed * (WeakSurgeActive ? weakSurgeMultiplier : 1f);

            // Move toward player
            transform.position += dir * speed * Time.deltaTime;
            
            // Keep within reasonable bounds (30 m of where it spawned)
            Vector3 offset = transform.position - _home;
            offset.y = 0f;
            if (offset.magnitude > 30f)
            {
                Vector3 clamped = _home + offset.normalized * 30f;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
            }
            
            // Rotate to face player
            if (dist > 0.2f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
            }
        }

        void Attack()
        {
            if (_target == null || Time.time < _nextAttack) return;

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > attackRange) return;

            _nextAttack = Time.time + Mathf.Max(0.2f, attackInterval);

            var health = _target.GetComponent<IDamageable>();
            if (health != null && health.IsAlive)
                health.TakeDamage(contactDamage, _target.transform.position, transform.position);
        }

        void BuildModel(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            go.name = "ZombieVisual";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // The pack characters stand about 2.2 m tall; bring them down to a 1.9 m body.
            go.transform.localScale = Vector3.one * 0.85f;

            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

            // The zombie packs ship Humanoid rigs, so the shared clip controller drives the stride;
            // the hunched procedural pose is only the fallback when the clips are missing.
            var walk = go.AddComponent<ModelLocomotion>();
            walk.legSwing = 24f;
            walk.armSwing = 15f;
            walk.lean = 10f;
            walk.stridePerMetre = 2.0f;
            walk.fullSpeed = Mathf.Max(1f, moveSpeed);
            walk.runSpeed = Mathf.Max(1f, moveSpeed);
            walk.sprintSpeed = Mathf.Max(2f, moveSpeed * 1.6f);

            var rig = go.GetComponentInChildren<Animator>();
            if (rig != null && rig.avatar != null && rig.avatar.isHuman)
            {
                var clips = Resources.Load<RuntimeAnimatorController>(HumanoidLocomotionResource);
                if (clips != null)
                {
                    rig.applyRootMotion = false;
                    rig.runtimeAnimatorController = clips;
                    walk.animator = rig;
                }
            }

            _visual = go.transform;
            _usesModel = true;
        }

        void BuildVisual()
        {
            _visual = new GameObject("ZombieVisual").transform;
            _visual.SetParent(transform, false);
            
            // Zombie body - dark main color with orange accents
            Color main = zombieColor;
            Color trim = accentColor;
            Color dark = Color.Lerp(main, Color.black, 0.7f);
            
            // Body cube
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(1.0f, 1.3f, 0.8f), Vector3.zero, main);
            
            // Head sphere
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0f, 1.7f, 0f), Vector3.one * 0.6f, Vector3.zero, main, true);
            
            // Orange eyes/accent
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0.15f, 1.75f, 0.2f), Vector3.one * 0.12f, Vector3.zero, accentColor, true);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(-0.15f, 1.75f, 0.2f), Vector3.one * 0.12f, Vector3.zero, accentColor, true);
            
            // Jaw/opening
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.4f, 0.5f), new Vector3(0.6f, 0.3f, 0.4f), Vector3.zero, dark);
            
            // Arm spikes
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(-0.5f, 0.7f, 0f), new Vector3(0.15f, 0.6f, 0.15f), new Vector3(0f, 90f, 0f), dark);
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0.5f, 0.7f, 0f), new Vector3(0.15f, 0.6f, 0.15f), new Vector3(0f, 90f, 0f), dark);
            
            // Leg bones
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(-0.3f, 0.2f, 0.2f), new Vector3(0.1f, 0.5f, 0.1f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0.3f, 0.2f, 0.2f), new Vector3(0.1f, 0.5f, 0.1f), Vector3.zero, dark);
        }

        Vector3 WeakPointLocal()
        {
            // Always in front of the chest: centred inside the capsule the body collider is hit
            // first and the red circle can never be shot.
            return _usesModel
                ? new Vector3(0f, 1.25f, 0.50f)
                : new Vector3(0f, 1.10f, 0.62f);
        }

        float WeakPointRadius() => 0.15f;

        void NotifyWeakHit()
        {
            _weakUntil = Time.time + weakSurgeSeconds;
        }

        // ---- health ----

        public void TakeDamage(float amount, Vector3 point, Vector3 from)
        {
            if (_dead) return;
            
            _currentHealth = Mathf.Max(0f, _currentHealth - Mathf.Max(0f, amount));
            bool died = _currentHealth <= 0f;
            
            if (died) _dead = true;
            
            if (died) Die();
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f) return;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        }

        // ---- death ----

        void Die()
        {
            if (_weak != null) _weak.Kill();
            if (_visual != null) _visual.gameObject.SetActive(false);
            
            SpawnDebris();
            Died?.Invoke(this);
            
            Destroy(gameObject, 2.2f);
        }

        void SpawnDebris()
        {
            for (int i = 0; i < 6; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "ZombieDebris";
                var col = chunk.GetComponent<Collider>();
                if (col != null) Destroy(col);
                
                chunk.transform.position = transform.position + Vector3.up * Random.Range(0.3f, 1.6f);
                chunk.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
                chunk.transform.rotation = Random.rotation;
                
                // Zombie-like materials
                StylizedMaterials.Apply(chunk, i % 2 == 0 ? Color.gray : new Color(0.5f, 0.5f, 0.5f));
                
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(2f, 5f), ForceMode.VelocityChange);
                Destroy(chunk, 2.2f);
            }
        }
    }
}