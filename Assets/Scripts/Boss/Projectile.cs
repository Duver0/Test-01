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

        Vector3 _direction;
        float _dieAt;
        Transform _ignoreRoot;

        public static Projectile Spawn(Vector3 position, Vector3 direction, float damage, float speed,
                                       float radius, bool homing, PlayerController target, Color color,
                                       Transform ignoreRoot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BossProjectile";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);
            StylizedMaterials.Apply(go, color, true);

            var p = go.AddComponent<Projectile>();
            p._direction = direction.normalized;
            p.damage = damage;
            p.speed = speed;
            p.radius = radius;
            p.homing = homing;
            p.target = target;
            p._ignoreRoot = ignoreRoot;
            p._dieAt = Time.time + p.lifetime;
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
    }
}
