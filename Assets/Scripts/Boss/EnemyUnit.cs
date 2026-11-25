using System.Collections;
using UnityEngine;

namespace Slotgun
{
    /// <summary>What a squad member is for. Three jobs, three ways of moving, three threats.</summary>
    public enum EnemyRole { Brute, Gunner, Medic }

    /// <summary>
    /// One body of a squad boss. The Brute closes in and swipes, the Gunner keeps its distance and
    /// shoots, the Medic hangs back and heals whoever is hurt. Each carries the same small red weak
    /// point as a full boss, aimed at a smaller target; it doubles the shot and speeds that body up
    /// for five seconds.
    /// </summary>
    public class EnemyUnit : MonoBehaviour, IDamageable
    {
        public EnemyRole role;

        [Header("Support")]
        [Tooltip("Seconds between heals. The medic heals whoever is proportionally lowest.")]
        public float healInterval = 6f;
        [Tooltip("Fraction of the ally's max health restored per heal.")]
        public float healFraction = 0.22f;

        [Header("Weak point")]
        public float weakSurgeMultiplier = 1.55f;
        public float weakSurgeSeconds = 5f;

        [Header("Melee")]
        [Tooltip("Damage of one swing, before the boss damage scale is applied.")]
        public float meleeDamage = 14f;
        [Tooltip("Reach in metres at which a swing can land.")]
        public float meleeRange = 3.0f;
        [Tooltip("Seconds between swings while the player stays in reach.")]
        public float meleeInterval = 1.5f;
        [Tooltip("Seconds from the start of the swing animation to the hit, so backing off during the wind-up dodges it.")]
        public float meleeWindup = 0.28f;
        [Tooltip("A wave body fights only in melee whatever its role. The squad boss keeps its gunner and its medic.")]
        public bool meleeOnly;

        [Header("Crowd")]
        [Tooltip("Metres of personal space this body keeps from every other body. Its own capsule is about 0.8 m across, so anything near 1 m stops the bodies standing inside each other.")]
        public float separationSpacing = 1.15f;

        public float MaxHealth { get; private set; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        public event System.Action<EnemyUnit> Died;

        /// <summary>Animator controller shared by every Humanoid body in the game, loaded from Resources.</summary>
        const string HumanoidLocomotionResource = "HumanoidLocomotion";

        SquadBoss _squad;
        PlayerController _target;
        Vector3 _arenaCenter;
        float _arenaRadius;
        float _damageScale;

        Transform _visual;
        Transform _swingArm;
        Vector3 _swingArmHome;
        Transform _muzzle;
        BossWeakPoint _weak;
        Animator _rig;
        bool _usesModel;
        float _targetHeight = ModelHeight.Fallback;

        float _nextAttack;
        float _healAt;
        float _orbitDir = 1f;
        float _bobPhase;
        float _spawnAt;
        float _swingUntil = -1f;
        float _enragedUntil = -1f;
        float _weakUntil = -1f;
        bool _dead;

        public bool WeakSurgeActive => Time.time < _weakUntil;
        public bool Enraged => Time.time < _enragedUntil;

        /// <summary>Live speed multiplier for this body: weak-point surge and squad enrage.</summary>
        public float SpeedMultiplier
        {
            get
            {
                float surge = WeakSurgeActive ? weakSurgeMultiplier : 1f;
                return surge * (Enraged ? 1.25f : 1f);
            }
        }

        public float ChaseSpeed => BaseSpeed * SpeedMultiplier;

        float BaseSpeed => role == EnemyRole.Brute ? 3.6f : (role == EnemyRole.Gunner ? 4.0f : 4.6f);

        public void Initialize(EnemyRole unitRole, float health, PlayerController player, SquadBoss squad,
                               Vector3 center, float radius, float damageScale, int seed, GameObject model = null,
                               float targetHeight = 0f)
        {
            role = unitRole;
            _targetHeight = targetHeight > 0.01f ? targetHeight : ModelHeight.Fallback;
            MaxHealth = Mathf.Max(1f, health);
            Current = MaxHealth;
            _target = player;
            _squad = squad;
            _arenaCenter = center;
            _arenaRadius = radius;
            _damageScale = Mathf.Max(0.1f, damageScale);

            var rng = new System.Random(seed);
            _orbitDir = rng.NextDouble() < 0.5 ? 1f : -1f;
            _bobPhase = (float)rng.NextDouble() * 6.28f;

            gameObject.name = unitRole.ToString();
            BuildBody(model != null);
            if (model != null) BuildModel(model);
            else BuildVisual();

            _weak = BossVisual.AttachWeakPoint(transform, WeakPointLocal(), WeakPointRadius(), this, NotifyWeakHit);

            _nextAttack = Time.time + Random.Range(0.7f, 1.5f);
            _healAt = Time.time + 2f;
            _spawnAt = Time.time;

            CrowdSeparation.Register(transform, SolidRadius);
        }

        /// <summary>A brute is wider than the rest, so it asks for more room before it starts shoving.</summary>
        float SeparationSpacing => role == EnemyRole.Brute ? separationSpacing * 1.2f : separationSpacing;

        /// <summary>
        /// The centre distance below which this body's capsule is inside another. Measured off the
        /// capsule BuildBody fits: 0.40 radius for a brute, 0.33 for the rest, plus a margin so no
        /// two capsules ever touch on screen.
        /// </summary>
        float SolidRadius => role == EnemyRole.Brute ? 0.82f : 0.70f;

        // ---- health ---------------------------------------------------------

        public void TakeDamage(float amount, Vector3 point, Vector3 from)
        {
            if (_dead) return;

            Current = Mathf.Max(0f, Current - Mathf.Max(0f, amount));
            bool died = Current <= 0f;
            if (died) _dead = true;

            if (_squad != null) _squad.RefreshHealth();
            if (died) Die();
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f) return;
            Current = Mathf.Min(MaxHealth, Current + amount);
            if (_squad != null) _squad.RefreshHealth();
        }

        /// <summary>The squad's own retaliation: every surviving body attacks faster for a moment.</summary>
        public void Enrage(float seconds)
        {
            _enragedUntil = Mathf.Max(_enragedUntil, Time.time + seconds);
        }

        void NotifyWeakHit()
        {
            _weakUntil = Time.time + weakSurgeSeconds;
        }

        // ---- body -----------------------------------------------------------

        void BuildBody(bool fitted)
        {
            var cap = gameObject.AddComponent<CapsuleCollider>();

            if (fitted)
            {
                // A humanoid model is far thinner than the blocky body it replaces, so the capsule
                // hugs the silhouette instead of the old chunky proportions.
                switch (role)
                {
                    case EnemyRole.Brute:
                        cap.center = new Vector3(0f, 1.00f, 0f); cap.height = 2.0f; cap.radius = 0.40f; break;
                    default:
                        cap.center = new Vector3(0f, 0.95f, 0f); cap.height = 1.9f; cap.radius = 0.33f; break;
                }
                return;
            }

            switch (role)
            {
                case EnemyRole.Brute:
                    cap.center = new Vector3(0f, 1.05f, 0f); cap.height = 2.1f; cap.radius = 0.58f; break;
                case EnemyRole.Gunner:
                    cap.center = new Vector3(0f, 0.95f, 0f); cap.height = 1.9f; cap.radius = 0.44f; break;
                default:
                    cap.center = new Vector3(0f, 0.9f, 0f); cap.height = 1.8f; cap.radius = 0.42f; break;
            }
        }

        Vector3 WeakPointLocal()
        {
            // A random spot on the torso, kept clear of the capsule: inside it the body collider is
            // hit first and the point could never be shot. It wears the body's own material, so it
            // is meant to be hunted for rather than spotted from across the arena.
            float surface = _usesModel
                ? (role == EnemyRole.Brute ? 0.50f : 0.44f)
                : (role == EnemyRole.Brute ? 0.66f : 0.52f);

            return BossVisual.RandomSurfacePoint(surface, 0.75f, 1.50f);
        }

        float WeakPointRadius() => role == EnemyRole.Brute ? 0.13f : 0.11f;

        void BuildModel(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            go.name = "Visual";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // The packs do not agree on scale. Fit the instance to the player's own height instead of
            // trusting a per-pack factor, so a wave is made of bodies the player's own size.
            go.transform.localScale = Vector3.one;
            ModelHeight.Fit(go.transform, _targetHeight);

            // Imported characters sometimes carry their own colliders; the capsule above is the one
            // that counts.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

            // The body wears a Humanoid rig, so the shared clip controller drives the stride; the
            // procedural pose is only the fallback for a rig whose clips are missing.
            var walk = go.AddComponent<ModelLocomotion>();
            walk.legSwing = role == EnemyRole.Brute ? 26f : 34f;
            walk.armSwing = role == EnemyRole.Brute ? 14f : 20f;
            walk.lean = role == EnemyRole.Brute ? 9f : 4f;
            walk.fullSpeed = Mathf.Max(1f, BaseSpeed);
            walk.runSpeed = Mathf.Max(1f, BaseSpeed);
            walk.sprintSpeed = Mathf.Max(2f, BaseSpeed * 1.6f);

            var rig = go.GetComponentInChildren<Animator>();
            if (rig != null && rig.avatar != null && rig.avatar.isHuman)
            {
                var clips = Resources.Load<RuntimeAnimatorController>(HumanoidLocomotionResource);
                if (clips != null)
                {
                    rig.applyRootMotion = false;
                    rig.runtimeAnimatorController = clips;
                    walk.animator = rig;
                    _rig = rig;   // the melee swing is a clip, so the animator is kept for triggers
                }
            }

            _visual = go.transform;
            _usesModel = true;
        }

        void BuildVisual()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            Color main = role == EnemyRole.Brute ? Palette.CharacterPrimary
                       : role == EnemyRole.Gunner ? Palette.Props
                       : Palette.Accent;
            Color trim = role == EnemyRole.Brute ? Palette.Props
                       : role == EnemyRole.Gunner ? Palette.CharacterSecondary
                       : Palette.CharacterSecondary;
            Color dark = Color.Lerp(main, Palette.Shadow, 0.72f);

            if (role == EnemyRole.Brute) BuildBrute(main, trim, dark);
            else if (role == EnemyRole.Gunner) BuildGunner(main, trim, dark);
            else BuildMedic(main, trim, dark);
        }

        void BuildBrute(Color main, Color trim, Color dark)
        {
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(1.15f, 1.25f, 0.9f), Vector3.zero, main);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.0f, 0.47f), new Vector3(1.0f, 0.9f, 0.06f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0.06f), Vector3.one * 0.62f, Vector3.zero, trim);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0f, 1.88f, 0.34f), Vector3.one * 0.18f, Vector3.zero, Palette.Accent, true);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(-0.72f, 1.15f, 0f), new Vector3(0.3f, 0.85f, 0.3f), new Vector3(0f, 0f, 8f), dark);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0.72f, 1.15f, 0f), new Vector3(0.3f, 0.85f, 0.3f), new Vector3(0f, 0f, -8f), dark);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(-0.8f, 0.6f, 0.12f), Vector3.one * 0.52f, Vector3.zero, main);
            _swingArm = BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0.8f, 0.6f, 0.12f), Vector3.one * 0.52f, Vector3.zero, main);
            _swingArmHome = _swingArm.localPosition;
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(-0.35f, 0.14f, 0f), new Vector3(0.24f, 0.16f, 0.24f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0.35f, 0.14f, 0f), new Vector3(0.24f, 0.16f, 0.24f), Vector3.zero, dark);
        }

        void BuildGunner(Color main, Color trim, Color dark)
        {
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.46f, 0.85f, 0.46f), Vector3.zero, main);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f), new Vector3(0.62f, 0.12f, 0.58f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.42f, Vector3.zero, trim);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.74f, 0.2f), new Vector3(0.3f, 0.1f, 0.14f), Vector3.zero, Palette.Accent, true);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(-0.42f, 1.22f, 0f), new Vector3(0.22f, 0.32f, 0.22f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0.42f, 1.18f, 0.3f), new Vector3(0.2f, 0.2f, 0.95f), Vector3.zero, trim);
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0.42f, 1.18f, 0.86f), new Vector3(0.12f, 0.18f, 0.12f), new Vector3(90f, 0f, 0f), dark);

            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_visual, false);
            _muzzle.localPosition = new Vector3(0.42f, 1.18f, 1.0f);

            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(-0.22f, 0.14f, 0f), new Vector3(0.18f, 0.14f, 0.18f), Vector3.zero, dark);
            BossVisual.Part(_visual, PrimitiveType.Cylinder, new Vector3(0.22f, 0.14f, 0f), new Vector3(0.18f, 0.14f, 0.18f), Vector3.zero, dark);
        }

        void BuildMedic(Color main, Color trim, Color dark)
        {
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.86f, 1.02f, 0.8f), Vector3.zero, main);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.56f, 0f), new Vector3(0.62f, 0.34f, 0.56f), Vector3.zero, dark);
            // the green cross is the medic's whole silhouette tell
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.02f, 0.42f), new Vector3(0.15f, 0.66f, 0.06f), Vector3.zero, trim, true);
            BossVisual.Part(_visual, PrimitiveType.Cube, new Vector3(0f, 1.02f, 0.42f), new Vector3(0.46f, 0.15f, 0.06f), Vector3.zero, trim, true);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(-0.18f, 1.62f, 0.26f), Vector3.one * 0.14f, Vector3.zero, Palette.Props, true);
            BossVisual.Part(_visual, PrimitiveType.Sphere, new Vector3(0.18f, 1.62f, 0.26f), Vector3.one * 0.14f, Vector3.zero, Palette.Props, true);
            BossVisual.Part(_visual, PrimitiveType.Capsule, new Vector3(-0.55f, 0.9f, -0.1f), new Vector3(0.12f, 0.34f, 0.12f), new Vector3(0f, 0f, 34f), dark);
            BossVisual.Part(_visual, PrimitiveType.Capsule, new Vector3(0.55f, 0.86f, -0.05f), new Vector3(0.12f, 0.3f, 0.12f), new Vector3(0f, 0f, -38f), dark);
        }

        // ---- frame ----------------------------------------------------------

        void Update()
        {
            if (_dead || _target == null) return;
            if (Time.time - _spawnAt < 0.35f) return;

            Movement();
            Combat();
            Animate();
        }

        void Movement()
        {
            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.001f ? to / dist : transform.forward;

            Vector3 desired;
            if (meleeOnly || role == EnemyRole.Brute)
            {
                // A wave body has no ranged role: it walks in and swings, whatever its label says.
                desired = dist > 2.2f ? dir : Vector3.zero;
            }
            else if (role == EnemyRole.Gunner)
            {
                desired = Vector3.Cross(Vector3.up, dir) * _orbitDir;
                if (dist > 15f) desired += dir * 1.3f;
                else if (dist < 8.5f) desired -= dir * 1.5f;
            }
            else
            {
                desired = Vector3.Cross(Vector3.up, dir) * (_orbitDir * 0.7f);
                if (dist < 15f) desired -= dir * 1.7f;
                else if (dist > 24f) desired += dir * 0.6f;
            }

            desired += CrowdSeparation.Push(transform.position, transform, SeparationSpacing);

            if (desired.sqrMagnitude > 0.0001f)
            {
                // Clamped rather than normalised: a body pushing through a wall of other bodies is
                // slowed by them instead of being deflected at full speed, which is what makes the
                // crowd read as solid.
                Vector3 step = Vector3.ClampMagnitude(desired, 1f);
                transform.position += step * BaseSpeed * SpeedMultiplier * Time.deltaTime;
            }

            Vector3 offset = transform.position - _arenaCenter;
            offset.y = 0f;
            if (offset.magnitude > _arenaRadius)
            {
                Vector3 clamped = _arenaCenter + offset.normalized * _arenaRadius;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
            }

            if (dist > 0.2f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
        }

        void Combat()
        {
            float dist = FlatDistance();

            if (meleeOnly || role == EnemyRole.Brute)
            {
                if (dist > meleeRange || Time.time < _nextAttack) return;
                _nextAttack = Time.time + meleeInterval * (Enraged ? 0.6f : 1f);
                StartCoroutine(Swing());
                return;
            }

            switch (role)
            {
                case EnemyRole.Gunner:
                    if (dist > 26f || Time.time < _nextAttack) return;
                    _nextAttack = Time.time + 2.1f * (Enraged ? 0.6f : 1f);
                    StartCoroutine(Volley());
                    break;

                default:
                    if (Time.time >= _healAt) TryHeal();
                    break;
            }
        }

        float FlatDistance()
        {
            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        /// <summary>
        /// One melee swing, played as a humanoid clip through the shared controller. Damage lands on
        /// the animation's contact frame rather than the frame the swing was chosen, so a player who
        /// backs off during the wind-up is not hit by a swing that never reached them.
        /// </summary>
        IEnumerator Swing()
        {
            _swingUntil = Time.time + 0.45f;
            if (_rig != null) _rig.SetTrigger(ModelLocomotion.AttackTrigger);

            yield return new WaitForSeconds(meleeWindup);

            if (_dead || _target == null) yield break;
            if (FlatDistance() > meleeRange + 0.6f) yield break;

            DamagePlayer(meleeDamage * _damageScale);
        }

        void DamagePlayer(float amount)
        {
            var health = _target.GetComponent<IDamageable>();
            if (health != null && health.IsAlive)
                health.TakeDamage(amount, _target.transform.position, transform.position);
        }

        IEnumerator Volley()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_dead || _target == null) yield break;

                Vector3 origin = _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.2f;
                Vector3 dir = (_target.Eye.position - origin).normalized;
                dir = Quaternion.Euler(Random.Range(-2.5f, 2.5f), Random.Range(-2.5f, 2.5f), 0f) * dir;

                Projectile.Spawn(origin, dir, 7f * _damageScale, 21f, 0.24f, false, _target, Palette.Props, transform);
                yield return new WaitForSeconds(0.16f);
            }
        }

        void TryHeal()
        {
            _healAt = Time.time + healInterval;
            if (_squad == null) return;

            EnemyUnit worst = null;
            float lowest = 0.98f;
            var units = _squad.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || !u.IsAlive) continue;
                float f = u.MaxHealth <= 0f ? 1f : u.Current / u.MaxHealth;
                if (f < lowest) { lowest = f; worst = u; }
            }

            if (worst == null) return;   // nobody is hurt enough to be worth the cast

            worst.Heal(worst.MaxHealth * healFraction);
            StartCoroutine(HealBeam(worst));
        }

        IEnumerator HealBeam(EnemyUnit unit)
        {
            var go = new GameObject("HealBeam");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = StylizedMaterials.Solid(Palette.CharacterSecondary, true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float t = 0f;
            while (t < 0.5f && unit != null)
            {
                t += Time.deltaTime;
                Vector3 a = transform.position + Vector3.up * 1.85f;
                Vector3 b = unit.transform.position + Vector3.up * 1.0f;
                lr.widthMultiplier = 0.08f;
                lr.SetPosition(0, a);
                lr.SetPosition(1, Vector3.Lerp(a, b, Mathf.Clamp01(t / 0.18f)));
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        void Animate()
        {
            if (_visual != null)
                _visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3.1f + _bobPhase) * 0.05f, 0f);

            if (_swingArm != null)
            {
                Vector3 p = _swingArmHome;
                float t = Mathf.Clamp01((_swingUntil - Time.time) / 0.4f);
                if (t > 0f) p.z += Mathf.Sin(t * Mathf.PI) * 0.85f;
                _swingArm.localPosition = p;
            }
        }

        void Die()
        {
            CrowdSeparation.Unregister(transform);
            if (_weak != null) _weak.Kill();
            if (_visual != null) _visual.gameObject.SetActive(false);

            SpawnDebris();
            if (Died != null) Died(this);

            Destroy(gameObject, 2.2f);
        }

        void OnDestroy()
        {
            CrowdSeparation.Unregister(transform);
        }

        void SpawnDebris()
        {
            for (int i = 0; i < 8; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "UnitDebris";
                var col = chunk.GetComponent<Collider>();
                if (col != null) Destroy(col);

                chunk.transform.position = transform.position + Vector3.up * Random.Range(0.3f, 1.6f);
                chunk.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);
                chunk.transform.rotation = Random.rotation;
                StylizedMaterials.Apply(chunk, i % 2 == 0 ? Palette.CharacterSecondary : Palette.Props);

                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 1.1f) * Random.Range(3f, 7f), ForceMode.VelocityChange);
                Destroy(chunk, 2.2f);
            }
        }
    }
}
