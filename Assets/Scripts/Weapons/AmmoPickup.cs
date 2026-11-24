using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Ammo the boss sheds every time it loses a third of its health. Scatters in a small ring
    /// around the body and is collected by walking over it; it tops the spare magazines of the
    /// carried firearm back up so a long fight never becomes unwinnable.
    /// </summary>
    public class AmmoPickup : MonoBehaviour
    {
        [Header("Pickup")]
        [Tooltip("Spare magazines handed to the player on pickup.")]
        public int magazines = 1;
        public float pickupRadius = 2.1f;
        public float lifetime = 28f;

        float _age;
        float _seed;

        /// <summary>Ring of packs around a point, clamped inside the arena so none land in a wall.</summary>
        public static void SpawnBurst(Vector3 origin, int count, Vector3 arenaCenter, float arenaRadius)
        {
            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) + Random.Range(-20f, 20f);
                Vector3 offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad))
                               * Random.Range(1.8f, 3.4f);

                Vector3 position = origin + offset;
                position.y = 0f;

                Vector3 flat = position - arenaCenter;
                flat.y = 0f;
                if (flat.magnitude > arenaRadius)
                    position = arenaCenter + flat.normalized * arenaRadius;

                position.y = 0.45f;
                Spawn(position);
            }
        }

        public static AmmoPickup Spawn(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "AmmoPickup";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(0.46f, 0.32f, 0.62f);
            StylizedMaterials.Apply(go, Palette.Props, true);

            // glowing cap so the pack reads against the sector floor
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var capCollider = cap.GetComponent<Collider>();
            if (capCollider != null) Destroy(capCollider);
            cap.name = "Glow";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            cap.transform.localScale = new Vector3(0.7f, 0.5f, 0.6f);
            StylizedMaterials.Apply(cap, Palette.Accent, true);

            return go.AddComponent<AmmoPickup>();
        }

        void Update()
        {
            _age += Time.deltaTime;

            transform.Rotate(0f, 130f * Time.deltaTime, 0f, Space.World);
            transform.position = new Vector3(
                transform.position.x,
                0.45f + Mathf.Sin(Time.time * 2.4f + _seed) * 0.09f,
                transform.position.z);

            if (_age > lifetime)
            {
                Destroy(gameObject);
                return;
            }

            var director = GameDirector.Instance;
            if (director == null) return;

            // Packs belong to the fight that dropped them: they vanish the moment the map changes,
            // so nothing is left floating in the vestibule.
            if (director.Phase != RunPhase.Fight)
            {
                Destroy(gameObject);
                return;
            }

            var player = director.PlayerTransform;
            if (player == null) return;

            Vector3 target = player.position + Vector3.up * 0.9f;
            if ((transform.position - target).sqrMagnitude > pickupRadius * pickupRadius) return;

            Weapon weapon = director.loadout != null ? director.loadout.AmmoWeapon : director.weapon;
            if (weapon == null)
            {
                Destroy(gameObject);
                return;
            }

            bool added = weapon.AddSpareMags(magazines);
            director.Say(added ? $"MUNICIÓN · +{magazines} CARGADOR" : "CARGADORES AL MÁXIMO");
            Destroy(gameObject);
        }

        void Start()
        {
            _seed = Random.Range(0f, 6.28f);
        }
    }
}
