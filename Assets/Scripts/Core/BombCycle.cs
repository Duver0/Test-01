
using UnityEngine;
using System.Collections;

namespace Slotgun
{
    /// <summary>
    /// Cycle de iluminación-explosión para bombas ambientales en el escenario.
    /// - Al recibir un raycast, comienza a parpadear cada vez más rápido durante 3 segundos.
    /// - Al terminar, explota y causa daño a cualquier boss dentro del radio.
    /// </summary>
    public class BombCycle : MonoBehaviour, IDamageable
    {
        [Header("Lights & FX")]
        public Light[] indicatorLights;
        public float pulseSpeed = 0.5f; // initial blink rate Hz
        public float speedIncreasePerSec = 0.15f; // how fast the blink speeds up
        public Color pulseColor = Color.yellow;
        public float explosionDamage = 25f;
        public float explosionRadius = 6f;
        
        [Header("Behavior")]
        public bool isActive = true;
        float _timer;
        float _blinkTimer;
        bool _exploded = false;
        Renderer _renderer;
        
        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                // Ensure we have a base material color
                if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    // Store original
                }
            }
            _timer = 3f;
            _blinkTimer = 0f;
        }
        
void Update()
        {
            if (!_exploded && isActive)
            {
                _timer -= Time.deltaTime;
                _blinkTimer += Time.deltaTime;

                // Speed up blinking as timer decreases (increasing rate)
                float progress = 1f - (_timer / 3f); // 0 at start, 1 at end
                float targetHz = pulseSpeed + progress * speedIncreasePerSec;
                float blinkInterval = 1f / Mathf.Max(0.1f, targetHz);

                // Pulse the lights
                if (indicatorLights != null && indicatorLights.Length > 0)
                {
                    bool shouldFlash = (_blinkTimer % blinkInterval) < (blinkInterval * 0.5f);
                    foreach (var light in indicatorLights)
                    {
                        if (light != null)
                        {
                            light.intensity = shouldFlash ? 3f : 0.5f;
                            light.color = pulseColor;
                        }
                    }
                }

                // Also change material emissive
                if (_renderer != null)
                {
                    var mat = _renderer.sharedMaterial;
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                    {
                        float flashPct = Mathf.PingPong(_blinkTimer, blinkInterval) / blinkInterval;
                        mat.SetColor("_EmissionColor", pulseColor * flashPct * 0.8f);
                    }
                }

                if (_timer <= 0f && !_exploded)
                {
                    Explode();
                }
            }
        }
        
        void Explode()
        {
            _exploded = true;
            isActive = false;
            
            // Stop blinking
            if (indicatorLights != null)
            {
                foreach (var light in indicatorLights)
                {
                    if (light != null) light.intensity = 0f;
                }
            }
            
            DamageEncounter();
            
            // Self-destruct after a moment
            Destroy(gameObject, 2f);
        }

        /// <summary>
        /// The live encounter is an <see cref="IBossEncounter"/>, which carries no transform or damage
        /// entry point, so the explosion resolves the concrete encounter: a single body takes the hit
        /// once, a squad has every body inside the radius burned separately.
        /// </summary>
        void DamageEncounter()
        {
            var director = GameDirector.Instance;
            if (director == null || director.CurrentBoss == null) return;

            var encounter = director.CurrentBoss as MonoBehaviour;
            if (encounter == null) return;

            if (encounter is SquadBoss squad)
            {
                var units = squad.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null || !unit.IsAlive) continue;

                    float unitDist = Vector3.Distance(transform.position, unit.transform.position);
                    if (unitDist > explosionRadius) continue;

                    float unitFalloff = 1f - (unitDist / explosionRadius);
                    unit.TakeDamage(explosionDamage * unitFalloff, unit.transform.position, transform.position);
                }
                return;
            }

            var target = encounter as IDamageable;
            if (target == null || !target.IsAlive) return;

            float dist = Vector3.Distance(transform.position, encounter.transform.position);
            if (dist > explosionRadius) return;

            float falloff = 1f - (dist / explosionRadius);
            target.TakeDamage(explosionDamage * falloff, encounter.transform.position, transform.position);
        }
        
        public bool IsAlive => !_exploded;

        public void TakeDamage(float amount, Vector3 point, Vector3 from)
        {
            if (_exploded) return;
            // Hitting a bomb triggers immediate explosion
            Explode();
        }
    }
}
