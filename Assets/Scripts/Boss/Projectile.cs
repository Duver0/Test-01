using UnityEngine;

namespace Slotgun
{
    /// <summary>Boss projectile. Swept sphere-cast so fast shots cannot tunnel through the player.</summary>
    public class Projectile : MonoBehaviour
    {
        public float speed = 18f;
        public float damage = 10f;
        public float lifetime = 7f;
        public float radius = 0.38f;
        public bool homing;
        public float homingStrength = 2.4f;
        public PlayerController target;

        // Sticky floor bomb properties
        public bool isSticky;
        public float stickyExplodeDelay = 3f;
        public float explosionRadius = 3.5f;
        public LayerMask explosionLayers = ~0;

        Vector3 _direction;
        float _dieAt;
        Transform _ignoreRoot;

        public static Projectile Spawn(Vector3 position, Vector3 direction, float damage, float speed,
                                       float radius, bool homing, PlayerController target, Color color,
                                       Transform ignoreRoot = null, bool isSticky = false,
                                       float stickyExplodeDelay = 3f, float explosionRadius = 3.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BossProjectile";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.transform.position = new Vector3(position.x, 0.5f, position.z); // Spawn on floor
            StylizedMaterials.Apply(go, color, true);

            var p = go.AddComponent<Projectile>();
            p._direction = direction.normalized;
            p.damage = damage;
            p.speed = speed;
            p.radius = radius;
            p.homing = homing;
            p.target = target;
            p._ignoreRoot = ignoreRoot;
            p.isSticky = isSticky;
            p.stickyExplodeDelay = stickyExplodeDelay;
            p.explosionRadius = explosionRadius;
            p._dieAt = Time.time + (isSticky ? stickyExplodeDelay : p.lifetime);
            return p;
        }

        void Update()
        {
            if (Time.time > _dieAt) { Destroy(gameObject); return; }

            if (homing && target != null)
            {
                Vector3 wanted = (target.Eye.position - transform.position).normalized;
                _direction = Vector3.Slerp(_direction, wanted, Time.deltaTime * homingStrength).normalized;
            }

            // Sticky floor bomb behavior
            if (isSticky)
            {
                // Check if time to explode has passed
                if (Time.time > _dieAt)
                {
                    Explode();
                    return;
                }
                
                // Stay on floor - if somehow elevated, drop to floor
                if (transform.position.y > 1f)
                {
                    transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
                }
                
                // Check for player stepping on it or proximity
                CheckExplosionTrigger();
                return;
            }

            float step = speed * Time.deltaTime;
            Vector3 next = transform.position + _direction * step;

            if (Physics.SphereCast(transform.position, radius, _direction, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_ignoreRoot != null && hit.collider.transform.IsChildOf(_ignoreRoot))
                {
                    transform.position = next;   // never shoot itself
                    return;
                }

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(damage, hit.point, transform.position);

                Destroy(gameObject);
                return;
            }

            transform.position = next;
        }

        void CheckExplosionTrigger()
        {
            // Explode after delay - simple timer-based
            // Could also add collision detection for player stepping on it
        }

        void Explode()
        {
            // Area damage explosion
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var hitCollider in hitColliders)
            {
                var damageable = hitCollider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    // Calculate distance-based damage (more damage near center)
                    float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                    float damageMultiplier = 1f - (distance / explosionRadius);
                    float finalDamage = damage * damageMultiplier;
                    
                    damageable.TakeDamage(finalDamage, transform.position, transform.position);
                }
            }
            
            // Visual explosion effect could be added here
            Destroy(gameObject);
        }
    }
}