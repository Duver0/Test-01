using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Slotgun
{
    /// <summary>
    /// Runtime weapon. Holds the applied upgrade list and derives live stats from it, so a grant
    /// from the betting machine changes the next shot's damage, cadence, spread and reach.
    /// Rounds live in a magazine plus a small stack of spare magazines: 15 in the gun and three
    /// spares is the whole loadout, and when both are empty the player is down to their fists.
    /// </summary>
    public class Weapon : MonoBehaviour, IRuntimeWeapon
    {
        [Header("Wiring")]
        public WeaponDefinition definition;
        public PlayerController player;
        public Transform muzzle;
        [Tooltip("First-person model. Hidden while another slot is selected.")]
        public Transform viewmodel;

        [Header("Ammo")]
        [Tooltip("Spare magazines carried into the run (the loaded magazine is not counted here).")]
        public int spareMags = 3;
        public int maxSpareMags = 6;

        readonly List<UpgradeGrant> _grants = new List<UpgradeGrant>();

        // Shared buffer so a pellet's hit walk does not allocate.
        static readonly RaycastHit[] _hitBuffer = new RaycastHit[24];

        public WeaponStats BaseStats => definition != null ? definition.baseStats : WeaponStats.Default;
        public WeaponStats Stats { get; private set; }
        public IReadOnlyList<UpgradeGrant> Grants => _grants;
        public int Ammo { get; private set; }
        public string DisplayName => definition != null ? definition.displayName : "WEAPON";

        public bool IsMelee => false;

        /// <summary>Spare magazines still carried. Each reload spends one.</summary>
        public int SpareMags { get; private set; }

        public int ReserveRounds => SpareMags * Mathf.Max(0, Stats.magazine);
        public bool HasAnyAmmo => Ammo > 0 || SpareMags > 0;
        public bool IsAmmoFull => Ammo >= Stats.magazine && SpareMags >= maxSpareMags;

        public event System.Action AmmoChanged;
        public event System.Action StatsChanged;
        public event System.Action HitConfirmed;
        public event System.Action OutOfAmmo;

        float _nextShotTime;
        bool _triggerHeld;
        float _reloadEndTime;
        float _flashUntil;
        GameObject _flash;

        public bool IsReloading => Time.time < _reloadEndTime;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            SpareMags = Mathf.Clamp(spareMags, 0, maxSpareMags);
            Recompute();
            Ammo = Stats.magazine;
            BuildMuzzleFlash();
        }

        void Start()
        {
            AmmoChanged?.Invoke();
            StatsChanged?.Invoke();
        }

        void Update()
        {
            if (_flash != null && _flash.activeSelf && Time.time > _flashUntil) _flash.SetActive(false);

            if (_triggerHeld && definition != null && definition.automatic) TryFire();
        }

        /// <summary>
        /// Called by PlayerController, which is the component that actually receives the input actions
        /// (PlayerInput sits on the Player root and SendMessages only reaches that same GameObject).
        /// </summary>
        public void SetTrigger(bool pressed)
        {
            if (!enabled) return;
            if (pressed && !_triggerHeld && definition != null && !definition.automatic) TryFire();
            _triggerHeld = pressed;
        }

        public void RequestReload()
        {
            if (enabled) BeginReload();
        }

        public bool TryFire()
        {
            if (definition == null) return false;
            if (IsReloading) return false;
            if (Time.time < _nextShotTime) return false;
            if (Ammo <= 0)
            {
                if (SpareMags > 0) BeginReload();
                else OutOfAmmo?.Invoke();
                return false;
            }

            _nextShotTime = Time.time + 1f / Mathf.Max(0.2f, Stats.fireRate);
            Ammo--;

            int pellets = Mathf.Max(1, definition.pelletsPerShot);
            for (int i = 0; i < pellets; i++) FirePellet();

            if (player != null) player.AddRecoil(Stats.recoil);

            MuzzleFlash();
            AmmoChanged?.Invoke();

            if (Ammo <= 0) BeginReload();
            return true;
        }

        public void BeginReload()
        {
            if (IsReloading || Ammo >= Stats.magazine) return;
            if (SpareMags <= 0) { AmmoChanged?.Invoke(); return; }
            _reloadEndTime = Time.time + 1.15f;
            Invoke(nameof(FinishReload), 1.15f);
        }

        /// <summary>Bought ammo in the vestibule: cancel any reload, top the magazine and add a spare.</summary>
        public void RefillAmmo()
        {
            CancelInvoke(nameof(FinishReload));
            _reloadEndTime = 0f;
            Ammo = Stats.magazine;
            SpareMags = Mathf.Clamp(SpareMags + 1, 0, maxSpareMags);
            AmmoChanged?.Invoke();
        }

        /// <summary>Dropped or bought spare magazines. Returns false when already at the cap.</summary>
        public bool AddSpareMags(int count)
        {
            if (count <= 0) return false;
            int before = SpareMags;
            SpareMags = Mathf.Clamp(SpareMags + count, 0, maxSpareMags);
            if (SpareMags == before) return false;
            AmmoChanged?.Invoke();
            return true;
        }

        void FinishReload()
        {
            if (!gameObject.activeInHierarchy) return;
            if (SpareMags <= 0) return;

            SpareMags--;
            Ammo = Stats.magazine;
            AmmoChanged?.Invoke();
        }

        // ---- slot switching -------------------------------------------------

        public void SetVisible(bool visible)
        {
            if (viewmodel != null) viewmodel.gameObject.SetActive(visible);
            if (!visible && _flash != null) _flash.SetActive(false);
        }

        public void SetInteractive(bool interactive)
        {
            if (!interactive)
            {
                CancelInvoke(nameof(FinishReload));
                _reloadEndTime = 0f;
                _triggerHeld = false;
                if (_flash != null) _flash.SetActive(false);
            }
            enabled = interactive;
        }

        void FirePellet()
        {
            Transform origin = player != null && player.Eye != null ? player.Eye : transform;

            float cone = Mathf.Lerp(definition.spreadMaxDegrees, 0.05f, Mathf.Clamp01(Stats.accuracy));
            Vector2 jitter = Random.insideUnitCircle * cone;
            Vector3 dir = Quaternion.Euler(jitter.y, jitter.x, 0f) * origin.forward;

            Vector3 end = origin.position + dir * Stats.range;

            if (TryRaycastTarget(origin.position, dir, Stats.range, out RaycastHit hit))
            {
                end = hit.point;

                // A boss weak point intercepts the shot before the body it is mounted on: it owns
                // its own damage multiplier and its own reaction instead of the body's.
                var zone = hit.collider.GetComponentInParent<IRangedHitZone>();
                if (zone != null && zone.Active)
                {
                    zone.RangedHit(Stats.damage, hit.point, origin.position);
                    HitConfirmed?.Invoke();
                }
                else
                {
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.TakeDamage(Stats.damage, hit.point, origin.position);
                        HitConfirmed?.Invoke();
                    }
                }

                SpawnImpactPuff(hit.point, hit.normal);
            }

            // The shot is aimed from the eye (so the crosshair never lies) but drawn from the barrel: a
            // tracer that starts at the camera reads as fire coming out of the player's chest.
            Transform barrel = muzzle != null ? muzzle : transform;
            SpawnTracer(barrel.position, end);
        }

        /// <summary>
        /// The eye sits inside the player's own CharacterController, and Unity hands back that controller
        /// as a 0 m hit as soon as the shot is angled down at a boss standing right in front of you — the
        /// shot then damages the player who fired it instead of the enemy. Walking the hits and taking the
        /// nearest one that is not part of the shooter keeps close-range fire on the target.
        /// </summary>
        bool TryRaycastTarget(Vector3 from, Vector3 dir, float distance, out RaycastHit hit)
        {
            hit = default;
            int count = Physics.RaycastNonAlloc(from, dir, _hitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var candidate = _hitBuffer[i];
                if (candidate.collider == null) continue;
                if (player != null && candidate.collider.transform.IsChildOf(player.transform)) continue;
                if (candidate.distance >= nearest) continue;

                nearest = candidate.distance;
                hit = candidate;
                found = true;
            }

            return found;
        }

        void SpawnTracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(null, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.widthMultiplier = 0.02f;
            lr.material = StylizedMaterials.Solid(definition != null ? definition.tracerColor : Palette.Accent, true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Destroy(go, 0.05f);
        }

        void SpawnImpactPuff(Vector3 point, Vector3 normal)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "ImpactPuff";
            puff.transform.position = point + normal * 0.05f;
            puff.transform.localScale = Vector3.one * 0.14f;
            var col = puff.GetComponent<Collider>();
            if (col != null) Destroy(col);
            StylizedMaterials.Apply(puff, Palette.Props, true);
            Destroy(puff, 0.08f);
        }

        void MuzzleFlash()
        {
            if (_flash == null) return;
            _flash.SetActive(true);
            _flashUntil = Time.time + 0.045f;
        }

        void BuildMuzzleFlash()
        {
            _flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _flash.name = "MuzzleFlash";
            var col = _flash.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _flash.transform.SetParent(muzzle != null ? muzzle : transform, false);
            _flash.transform.localPosition = Vector3.zero;
            _flash.transform.localScale = Vector3.one * 0.16f;
            StylizedMaterials.Apply(_flash, Palette.Accent, true);
            _flash.SetActive(false);
        }

        // ---- upgrades -------------------------------------------------------

        public void AddUpgrade(UpgradeGrant grant)
        {
            _grants.Add(grant);
            Recompute();

            if (grant.stat == WeaponStat.Magazine)
            {
                Ammo = Mathf.Min(Stats.magazine, Ammo + Mathf.Max(1, Stats.magazine - Ammo));
            }
            Ammo = Mathf.Clamp(Ammo, 0, Stats.magazine);

            AmmoChanged?.Invoke();
            StatsChanged?.Invoke();
        }

        void Recompute()
        {
            Stats = WeaponStats.Compose(BaseStats, _grants);
        }

        public static Color RarityColor(Rarity rarity)
        {
            return WeaponStatUtility.Tint(rarity);
        }
    }
}
