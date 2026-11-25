using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Runtime boss: phases, movement pattern, attack rotation and one ability, all driven by the
    /// module combination BossGenerator chose. Built entirely from primitives (see BossVisual).
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class BossController : MonoBehaviour, IDamageable, IBossEncounter
    {
        public BossConfig config;
        public PlayerController target;
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 26f;

        [Header("Weak point")]
        [Tooltip("How much faster the boss moves for weakSurgeSeconds after its red circle is shot.")]
        public float weakSurgeMultiplier = 1.55f;
        [Tooltip("Radius of the red hit sphere. Small on purpose: hitting it is a skill shot, not the default way to delete the health bar.")]
        public float weakPointRadius = 0.18f;
        public float weakSurgeSeconds = 5f;

        [Header("Third-health retaliation")]
        [Tooltip("Ammo packs shed each time the boss loses a third of its health.")]
        public int ammoDropsPerThreshold = 3;
        [Tooltip("How long the punishing burst lasts after a threshold is crossed.")]
        public float burstSeconds = 3.4f;
        public float burstDamageMultiplier = 1.45f;
        public float burstSpeedMultiplier = 1.35f;

        public float MaxHealth { get; private set; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public int Phase { get; private set; } = 1;
        public string DisplayName => config != null ? config.displayName : "BOSS";
        public float HealthFraction => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Current / MaxHealth);
        public BossConfig Config => config;

        public event System.Action BossStateChanged;
        public event System.Action<IBossEncounter> Defeated;

        readonly List<Transform> _spinners = new List<Transform>();
        static readonly float[] DropThresholds = { 0.66f, 0.33f };
        int _dropIndex;
        float _burstUntil = -1f;
        Transform _visual;
        Coroutine _attackRoutine;
        float _shieldUntil;
        float _enrageMul = 1f;
        bool _enraged;
        float _orbitDir = 1f;
        float _hopTimer;
        Vector3 _hopVelocity;
        float _blinkAt;
        bool _dead;

        float _chargeAt;
        float _chargeEnd;
        Vector3 _chargeDir;
        float _weakUntil = -1f;
        BossWeakPoint _weakPoint;

        /// <summary>True for a few seconds after the red circle is shot: the boss speeds up.</summary>
        public bool WeakSurgeActive => Time.time < _weakUntil;

        /// <summary>
        /// Every live movement multiplier in one place: phase ramp, enrage, the threshold burst and
        /// the weak-point surge. The surge and the burst are combined with max rather than
        /// multiplied so a weak-point hit during a retaliation cannot stack into an unreadable dash.
        /// </summary>
        public float SpeedMultiplier
        {
            get
            {
                float surge = WeakSurgeActive ? weakSurgeMultiplier : 1f;
                float burst = InBurst ? burstSpeedMultiplier : 1f;
                return PhaseSpeedMul() * _enrageMul * Mathf.Max(surge, burst);
            }
        }

        /// <summary>Current top ground speed: the number the player's sprint rule measures itself against.</summary>
        public float ChaseSpeed => config != null ? config.moveSpeed * SpeedMultiplier : 0f;

        public void Initialize(BossConfig cfg, PlayerController player, Vector3 center, float radius)
        {
            config = cfg;
            target = player;
            arenaCenter = center;
            arenaRadius = radius;

            MaxHealth = cfg.maxHealth;
            Current = MaxHealth;
            Phase = 1;

            var capsule = GetComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.5f * cfg.visualScale, 0f);
            capsule.height = 3.1f * cfg.visualScale;
            capsule.radius = 1.05f * cfg.visualScale;

            _visual = BossVisual.Build(cfg, transform, _spinners);
            _weakPoint = BossVisual.AttachWeakPoint(_visual, BossVisual.WeakPointLocal(cfg.body), weakPointRadius, this, NotifyWeakHit);

            int seed = Mathf.Abs(cfg.displayName.GetHashCode());
            _orbitDir = (seed % 2 == 0) ? 1f : -1f;
            _blinkAt = Time.time + 7f;
            _chargeAt = Time.time + 1.6f;

            _attackRoutine = StartCoroutine(AttackLoop());
            BossStateChanged?.Invoke();
        }

        void Update()
        {
            if (_dead) return;

            Animate();
            UpdatePhase();
            Movement();
            Abilities();
        }

        void Animate()
        {
            if (_visual != null)
                _visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 1.9f) * 0.09f, 0f);

            for (int i = 0; i < _spinners.Count; i++)
            {
                if (_spinners[i] == null) continue;
                _spinners[i].Rotate(0f, 0f, -(120f + i * 18f) * Time.deltaTime * (config != null ? 1f : 1f), Space.Self);
            }
        }

        void UpdatePhase()
        {
            float f = HealthFraction;
            int next = f > 0.66f ? 1 : (f > 0.33f ? 2 : 3);
            if (next == Phase) return;
            Phase = next;
            BossStateChanged?.Invoke();
        }

        float PhaseSpeedMul() => 1f + (Phase - 1) * 0.22f;

        /// <summary>True for a few seconds after a third of the health bar is gone.</summary>
        public bool InBurst => Time.time < _burstUntil;

        void Movement()
        {
            if (target == null) return;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.001f ? to / dist : Vector3.forward;

            float speed = config.moveSpeed * SpeedMultiplier;
            Vector3 desired = Vector3.zero;

            switch (config.movement)
            {
                case BossMovement.Chase:
                    desired = dist > 3.4f ? dir : Vector3.zero;
                    break;

                case BossMovement.Strafe:
                    desired = Vector3.Cross(Vector3.up, dir) * _orbitDir;
                    if (dist > 11f) desired += dir;
                    else if (dist < 6f) desired -= dir;
                    break;

                case BossMovement.Orbit:
                    desired = Vector3.Cross(Vector3.up, dir) * _orbitDir;
                    desired += dir * Mathf.Clamp((dist - 12f) * 0.18f, -0.7f, 0.7f);
                    break;

                case BossMovement.Hopper:
                    _hopTimer -= Time.deltaTime;
                    if (_hopTimer <= 0f)
                    {
                        _hopTimer = 1.15f;
                        _hopVelocity = dir * speed * 1.9f;
                    }
                    desired = _hopVelocity;
                    _hopVelocity = Vector3.Lerp(_hopVelocity, Vector3.zero, Time.deltaTime * 2.6f);
                    break;

                case BossMovement.Charger:
                    // Wind up in place, then commit to one straight dash along the last heading.
                    if (Time.time >= _chargeAt)
                    {
                        _chargeDir = dir;
                        _chargeEnd = Time.time + 0.55f;
                        _chargeAt = _chargeEnd + 1.5f;
                        StartCoroutine(Pulse(Palette.Danger));
                    }
                    desired = Time.time < _chargeEnd ? _chargeDir * 3.1f : dir * 0.3f;
                    break;

                case BossMovement.Kiter:
                    // Backs off and circles at range; only closes in if the player hides.
                    desired = -dir * 1.4f + Vector3.Cross(Vector3.up, dir) * (0.8f * _orbitDir);
                    if (dist > 19f) desired = Vector3.Cross(Vector3.up, dir) * _orbitDir + dir * 0.2f;
                    break;
            }

            if (desired.sqrMagnitude > 0.0001f)
            {
                transform.position += desired.normalized * speed * Time.deltaTime;
            }

            // keep the boss inside the arena
            Vector3 offset = transform.position - arenaCenter;
            offset.y = 0f;
            if (offset.magnitude > arenaRadius)
            {
                Vector3 clamped = arenaCenter + offset.normalized * arenaRadius;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
            }

            if (dist > 0.2f)
            {
                Quaternion want = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * 3.2f);
            }
        }

        void Abilities()
        {
            switch (config.ability)
            {
                case BossAbility.Enrage:
                    if (!_enraged && HealthFraction <= 0.5f)
                    {
                        _enraged = true;
                        _enrageMul = 1.35f;
                        StartCoroutine(Pulse(Palette.Danger));
                        BossStateChanged?.Invoke();
                    }
                    break;

                case BossAbility.Shield:
                    if (Time.time > _shieldUntil + 6f)
                    {
                        _shieldUntil = Time.time + 2.6f;
                        StartCoroutine(Pulse(Palette.Accent));
                    }
                    break;

                case BossAbility.Blink:
                    if (Time.time > _blinkAt && target != null)
                    {
                        _blinkAt = Time.time + 6.5f;
                        Vector3 around = target.transform.position - target.transform.forward * 8f;
                        Vector3 dir = (around - arenaCenter);
                        dir.y = 0f;
                        if (dir.magnitude > arenaRadius) around = arenaCenter + dir.normalized * arenaRadius;
                        transform.position = new Vector3(around.x, transform.position.y, around.z);
                        StartCoroutine(Pulse(Palette.CharacterSecondary));
                    }
                    break;
            }
        }

        IEnumerator Pulse(Color color)
        {
            if (_visual == null) yield break;
            float t = 0f;
            while (t < 0.35f && _visual != null)
            {
                t += Time.deltaTime;
                _visual.localScale = Vector3.one * config.visualScale * (1f + Mathf.Sin(t / 0.35f * Mathf.PI) * 0.08f);
                yield return null;
            }
            if (_visual != null) _visual.localScale = Vector3.one * config.visualScale;
        }

        // ---- attacks -------------------------------------------------------

        IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(1.5f);

            while (!_dead)
            {
                float cd = config.attackCooldown / Mathf.Max(0.2f, PhaseSpeedMul() * _enrageMul);
                yield return new WaitForSeconds(cd * Random.Range(0.85f, 1.2f));
                if (_dead) yield break;

                yield return StartCoroutine(ExecuteAttack(config.primaryAttack));

                if (config.hasSecondary && Random.value < 0.5f)
                {
                    yield return new WaitForSeconds(0.8f);
                    if (_dead) yield break;
                    yield return StartCoroutine(ExecuteAttack(config.secondaryAttack));
                }
            }
        }

        Vector3 CorePosition => transform.position + Vector3.up * (1.6f * config.visualScale);

        float AttackDamage(BossAttack attack)
        {
            return BossCatalog.GetAttack(attack).damage * config.damageScale
                 * (1f + (Phase - 1) * 0.15f)
                 * (InBurst ? burstDamageMultiplier : 1f);
        }

        IEnumerator ExecuteAttack(BossAttack attack)
        {
            switch (attack)
            {
                case BossAttack.RadialBurst:
                {
                    int count = 12 + Phase * 2;
                    for (int i = 0; i < count; i++)
                    {
                        float ang = i * (360f / count);
                        Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                        Fire(dir, AttackDamage(attack), 16f, 0.36f, false, Palette.CharacterPrimary);
                    }
                    yield return new WaitForSeconds(0.2f);
                    break;
                }

                case BossAttack.AimedVolley:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (target == null) break;
                        Vector3 dir = (target.Eye.position - CorePosition).normalized;
                        dir = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0f) * dir;
                        Fire(dir, AttackDamage(attack), 26f, 0.3f, false, Palette.Props);
                        yield return new WaitForSeconds(0.16f);
                    }
                    break;
                }

                case BossAttack.GroundSlam:
                {
                    float radius = 6.5f + Phase;
                    StartCoroutine(Telegraph(transform.position, radius, 0.75f));
                    yield return new WaitForSeconds(0.75f);

                    if (target != null)
                    {
                        Vector3 d = target.transform.position - transform.position;
                        d.y = 0f;
                        if (d.magnitude <= radius)
                            DamagePlayer(AttackDamage(attack), target.transform.position);
                    }
                    StartCoroutine(SlamRing(radius));
                    yield return new WaitForSeconds(0.25f);
                    break;
                }

                case BossAttack.SpinSpray:
                {
                    float end = Time.time + 1.7f;
                    float ang = 0f;
                    while (Time.time < end && !_dead)
                    {
                        ang += 17f;
                        Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                        Fire(dir, AttackDamage(attack), 15f, 0.34f, false, Palette.CharacterSecondary);
                        yield return new WaitForSeconds(0.085f);
                    }
                    break;
                }

                case BossAttack.HomingOrb:
                {
                    int orbs = 1 + Phase;
                    for (int i = 0; i < orbs; i++)
                    {
                        if (target == null) break;
                        Vector3 dir = (target.Eye.position - CorePosition).normalized;
                        dir = Quaternion.Euler(0f, (i - orbs / 2f) * 26f, 0f) * dir;
                        Fire(dir, AttackDamage(attack), 9f, 0.5f, true, Palette.Accent);
                        yield return new WaitForSeconds(0.25f);
                    }
                    break;
                }

                case BossAttack.StompRing:
                {
                    int count = 18;
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * (360f / count);
                        Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Vector3 origin = transform.position + Vector3.up * 0.4f + dir * 1.2f;
                        var p = Projectile.Spawn(origin, dir, AttackDamage(attack), 12f, 0.42f, false, target, Palette.Props, transform);
                        p.lifetime = 5f;
                    }
                    yield return new WaitForSeconds(0.3f);
                    break;
                }

                case BossAttack.MeleeSwing:
                {
                    // The only boss attack that punishes standing in its face: a wide frontal paw.
                    float reach = 4.6f;
                    StartCoroutine(Telegraph(transform.position, reach, 0.5f));
                    yield return new WaitForSeconds(0.5f);

                    if (target != null)
                    {
                        Vector3 d = target.transform.position - transform.position;
                        d.y = 0f;
                        float dist = d.magnitude;
                        float facing = dist > 0.001f ? Vector3.Dot(d / dist, transform.forward) : 1f;
                        if (dist <= reach && facing > 0.15f)
                            DamagePlayer(AttackDamage(attack), target.transform.position);
                    }

                    StartCoroutine(SlamRing(reach));
                    yield return new WaitForSeconds(0.25f);
                    break;
                }

                case BossAttack.SightBeam:
                {
                    // Locks a thin red line on the player, then burns along it for a second: the
                    // damage only lands while line of sight is unbroken, so pillars are cover.
                    var beam = BuildBeam();

                    float lockEnd = Time.time + 0.7f;
                    while (Time.time < lockEnd && !_dead)
                    {
                        if (target != null) DrawBeam(beam, target.Eye.position, 0.03f);
                        yield return null;
                    }

                    float fireEnd = Time.time + 1.1f;
                    while (Time.time < fireEnd && !_dead)
                    {
                        if (target == null) break;

                        Vector3 eye = CorePosition;
                        DrawBeam(beam, target.Eye.position, 0.17f);

                        Vector3 to = target.Eye.position - eye;
                        float dist = to.magnitude;
                        if (dist > 0.01f && HasLineOfSight(eye, to / dist, dist))
                            DamagePlayer(AttackDamage(attack) * Time.deltaTime, target.Eye.position);

                        yield return null;
                    }

                    if (beam != null) Destroy(beam);
                    break;
                }
            }
        }

        GameObject BuildBeam()
        {
            var go = new GameObject("SightBeam");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = StylizedMaterials.Solid(Palette.Danger, true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return go;
        }

        void DrawBeam(GameObject beam, Vector3 to, float width)
        {
            if (beam == null) return;
            var lr = beam.GetComponent<LineRenderer>();
            if (lr == null) return;
            lr.widthMultiplier = width;
            lr.SetPosition(0, CorePosition);
            lr.SetPosition(1, to);
        }

        /// <summary>True while nothing solid stands between the beam origin and the player.</summary>
        bool HasLineOfSight(Vector3 origin, Vector3 dir, float distance)
        {
            if (target == null) return false;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, distance + 0.6f, ~0, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(target.transform);
        }

        void Fire(Vector3 dir, float damage, float speed, float radius, bool homing, Color color)
        {
            Projectile.Spawn(CorePosition + dir.normalized * (1.6f * config.visualScale), dir, damage, speed, radius,
                             homing, target, color, transform);
        }

        void DamagePlayer(float amount, Vector3 point)
        {
            if (target == null) return;
            var health = target.GetComponent<IDamageable>();
            if (health != null && health.IsAlive) health.TakeDamage(amount, point, transform.position);
        }

        IEnumerator Telegraph(Vector3 position, float radius, float duration)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "SlamTelegraph";
            var col = disc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            disc.transform.position = position + Vector3.up * 0.05f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            StylizedMaterials.Apply(disc, Palette.Danger, true);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                disc.transform.localScale = new Vector3(radius * 2f * Mathf.Lerp(0.4f, 1f, t / duration), 0.03f,
                                                      radius * 2f * Mathf.Lerp(0.4f, 1f, t / duration));
                yield return null;
            }
            Destroy(disc);
        }

        IEnumerator SlamRing(float radius)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SlamRing";
            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ring.transform.position = transform.position + Vector3.up * 0.08f;
            StylizedMaterials.Apply(ring, Palette.Props, true);

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float r = Mathf.Lerp(0.5f, radius, t / 0.35f);
                ring.transform.localScale = new Vector3(r * 2f, 0.04f, r * 2f);
                yield return null;
            }
            Destroy(ring);
        }

        // ---- damage / death ------------------------------------------------

        public void TakeDamage(float amount, Vector3 point, Vector3 from)
        {
            if (_dead) return;

            float mul = Time.time < _shieldUntil ? 0.25f : 1f;
            Current = Mathf.Max(0f, Current - Mathf.Max(0f, amount) * mul);
            BossStateChanged?.Invoke();

            if (Current <= 0f)
            {
                Die();
                return;
            }

            CheckThresholds();
        }

        /// <summary>
        /// Each third of health lost sheds ammo for the player and triggers a short, harder-hitting
        /// retaliation: the boss speeds up and every attack lands 45% harder for a few seconds.
        /// </summary>
        void CheckThresholds()
        {
            float fraction = HealthFraction;
            while (_dropIndex < DropThresholds.Length && fraction <= DropThresholds[_dropIndex])
            {
                _dropIndex++;
                DropAmmo();
                StartCoroutine(SpecialBurst());
            }
        }

        void DropAmmo()
        {
            AmmoPickup.SpawnBurst(transform.position, Mathf.Max(1, ammoDropsPerThreshold), arenaCenter, arenaRadius);
        }

        /// <summary>
        /// A shot landed on the weak point. The damage bonus already went through TakeDamage; this
        /// only buys the boss a burst of speed, refreshed by every further hit. It is deliberately
        /// silent — no message, no sound, no pulse — because the point is invisible: announcing the
        /// hit would hand the player its position for free.
        /// </summary>
        public void NotifyWeakHit()
        {
            _weakUntil = Time.time + weakSurgeSeconds;
        }

        IEnumerator SpecialBurst()
        {
            _burstUntil = Time.time + burstSeconds;
            StartCoroutine(Pulse(Palette.Danger));

            // a short tell before the retaliation so the player can break line of sight
            yield return new WaitForSeconds(0.45f);
            if (_dead) yield break;

            yield return StartCoroutine(ExecuteAttack(BossAttack.RadialBurst));
            if (_dead) yield break;

            yield return StartCoroutine(ExecuteAttack(BossAttack.AimedVolley));

            while (!_dead && Time.time < _burstUntil) yield return null;
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;

            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            if (_weakPoint != null) _weakPoint.Kill();

            if (config.modifier == BossModifier.Volatile && target != null)
            {
                Vector3 d = target.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < 9f) DamagePlayer(60f * config.damageScale, target.transform.position);
            }

            SpawnDebris();

            if (_visual != null) _visual.gameObject.SetActive(false);

            Defeated?.Invoke(this);
        }

        void SpawnDebris()
        {
            for (int i = 0; i < 22; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "BossDebris";
                var col = chunk.GetComponent<Collider>();
                if (col != null) Destroy(col);
                chunk.transform.position = transform.position + Vector3.up * Random.Range(0.6f, 2.6f);
                chunk.transform.localScale = Vector3.one * Random.Range(0.14f, 0.4f);
                chunk.transform.rotation = Random.rotation;
                StylizedMaterials.Apply(chunk, i % 3 == 0 ? config.secondaryColor : config.primaryColor);

                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.4f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 1.2f) * Random.Range(4f, 9f), ForceMode.VelocityChange);
                Destroy(chunk, 2.5f);
            }
        }
    }
}
