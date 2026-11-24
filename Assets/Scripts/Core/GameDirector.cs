using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Slotgun
{
    public enum RunPhase { Loading, Fight, Prep, RunComplete, Dead }

    /// <summary>
    /// Single owner of the run: money, sector, phase, the generated boss, and every scene
    /// transition. The five sectors and the vestibule are streamed additively one at a time, so
    /// only the map being played is ever resident; the finished map is unloaded before the next
    /// one is touched.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Wiring")]
        public PlayerController player;
        public PlayerHealth playerHealth;
        public Weapon weapon;
        public PlayerLoadout loadout;
        public PlayerInteractor interactor;
        public SectorCatalog sectors;

        [Header("Scenes")]
        public string coreSceneName = "Slotgun_Core";
        public string prepSceneName = "Slotgun_PrepRoom";

        [Header("Arena (filled from the streamed sector)")]
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 30f;

        [Header("Enemy models (optional)")]
        [Tooltip("Zombie pack characters used for the shambling bodies of a wave.")]
        public GameObject[] zombieModels;
        [Tooltip("Body used by gunners, medics and the wave's gunner bodies.")]
        public GameObject skeletonModel;
        [Tooltip("Body used by brutes.")]
        public GameObject armoredSkeletonModel;

        [Header("Economy")]
        public int startingMoney = 180;
        public int healPrice = 45;
        public float healAmount = 50f;
        public int ammoPrice = 30;

        [Header("Rewards")]
        public int rewardBase = 70;
        public int rewardPerRound = 35;
        public int luckyBonus = 60;

        public int Money { get; private set; }
        public int Round { get; private set; } = 1;
        public int totalRounds = 5;

        public RunPhase Phase { get; private set; } = RunPhase.Loading;
        public BossConfig CurrentBossConfig { get; private set; }
        public IBossEncounter CurrentBoss { get; private set; }
        public BettingMachine machine { get; private set; }

        /// <summary>Bodies the current wave started with, for the HUD counter.</summary>
        public int WaveTotal => _wave != null ? _wave.Total : 0;

        /// <summary>Bodies of the wave still standing. Zero once the boss is on the field.</summary>
        public int WaveRemaining => _wave != null ? _wave.Alive : 0;

        public int LastReward { get; private set; }
        public bool PrepUiOpen { get; private set; }
        public string SectorName { get; private set; } = string.Empty;
        public string LoadLabel { get; private set; } = string.Empty;

        public float LoadProgress => _streamer != null ? _streamer.Progress : 0f;

        public Transform PlayerTransform => player != null ? player.transform : null;

        /// <summary>Doors only hand off while a map is live and no transition is already running.</summary>
        public bool DoorsArmed => !_transitioning && (Phase == RunPhase.Fight || Phase == RunPhase.Prep);

        public event System.Action StateChanged;
        public event System.Action<string> Message;

        SceneStreamer _streamer;
        WaveSpawner _wave;
        Scene _coreScene;
        SectorAnchor _anchor;
        DoorPortal _prepExit;
        AsyncOperation _preloadOp;
        bool _nextSectorReady;
        bool _transitioning;

        System.Random _rng;
        BossBody? _lastBody;
        BossAttack? _lastAttack;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            _coreScene = gameObject.scene;
            _rng = new System.Random(System.Environment.TickCount);

            _streamer = GetComponent<SceneStreamer>();
            if (_streamer == null) _streamer = gameObject.AddComponent<SceneStreamer>();

            if (weapon == null) weapon = FindAnyObjectByType<Weapon>();
            if (loadout == null) loadout = FindAnyObjectByType<PlayerLoadout>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        }

        void Start()
        {
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;

            Money = startingMoney;
            Round = 1;
            GenerateBossFor(Round);

            Phase = RunPhase.Loading;
            LoadLabel = "ABRIENDO SECTOR 1";
            SetCursor(false);
            SetGameplayActive(false);
            StateChanged?.Invoke();

            StartCoroutine(BootRoutine());
        }

        void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (PrepUiOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseBetting();
        }

        // ---- sector data ----------------------------------------------------

        SectorDefinition DefFor(int round)
        {
            if (sectors == null || sectors.Count == 0) return null;
            return sectors.Get(Mathf.Clamp(round - 1, 0, sectors.Count - 1));
        }

        string SceneNameFor(int round)
        {
            var def = DefFor(round);
            return def != null && !string.IsNullOrEmpty(def.sceneName) ? def.sceneName : $"Slotgun_Sector_{round}";
        }

        void NameFor(int round)
        {
            var def = DefFor(round);
            SectorName = def != null ? def.displayName : $"SECTOR {round}";
        }

        // ---- economy --------------------------------------------------------

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Money < amount) return false;
            Money -= amount;
            StateChanged?.Invoke();
            return true;
        }

        public void AddMoney(int amount)
        {
            Money = Mathf.Max(0, Money + amount);
            StateChanged?.Invoke();
        }

        public void Say(string text)
        {
            if (!string.IsNullOrEmpty(text)) Message?.Invoke(text);
        }

        public string TryBuyHeal()
        {
            if (playerHealth == null) return "SIN PACIENTE";
            if (playerHealth.Current >= playerHealth.maxHealth - 0.01f) return "VIDA AL MÁXIMO";
            if (Money < healPrice) return "SIN FONDOS";

            TrySpend(healPrice);
            playerHealth.Heal(healAmount);
            return $"BOTIQUÍN · +{healAmount:0} VIDA";
        }

        public string TryBuyAmmo()
        {
            if (weapon == null) return "SIN ARMA";
            if (weapon.IsAmmoFull) return "MUNICIÓN AL MÁXIMO";
            if (Money < ammoPrice) return "SIN FONDOS";

            TrySpend(ammoPrice);
            weapon.RefillAmmo();
            return $"MUNICIÓN · {weapon.Stats.magazine} BALAS + 1 CARGADOR";
        }

        // ---- flow: boot -----------------------------------------------------

        IEnumerator BootRoutine()
        {
            yield return LoadSectorRoutine(Round);
            BeginFight();
        }

        IEnumerator LoadSectorRoutine(int round)
        {
            NameFor(round);
            Phase = RunPhase.Loading;
            LoadLabel = $"SECTOR {round} · {SectorName}";
            SetCursor(false);
            SetGameplayActive(false);
            StateChanged?.Invoke();

            yield return _streamer.Load(SceneNameFor(round));

            if (LightingRig.Instance != null) LightingRig.Instance.Apply(DefFor(round));
            if (_coreScene.IsValid()) SceneManager.SetActiveScene(_coreScene);

            _anchor = FindAnyObjectByType<SectorAnchor>();
            if (_anchor != null)
            {
                arenaCenter = _anchor.arenaCenter;
                arenaRadius = _anchor.arenaRadius;
            }
        }

        void BeginFight()
        {
            if (_anchor != null && _anchor.playerSpawn != null && player != null)
                player.Teleport(_anchor.playerSpawn.position, _anchor.playerSpawn.eulerAngles.y);

            Phase = RunPhase.Fight;
            SpawnWave();
            SetCursor(true);
            SetGameplayActive(true);
            if (loadout != null) loadout.SetGameplayEnabled(true);
            else if (weapon != null) weapon.enabled = true;
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Every sector opens with a wave of grunts. The boss is only built once the last of them is
        /// down, so the player always clears the arena floor before the real fight starts.
        /// </summary>
        void SpawnWave()
        {
            if (_wave == null)
            {
                _wave = GetComponent<WaveSpawner>();
                if (_wave == null) _wave = gameObject.AddComponent<WaveSpawner>();
            }

            _wave.Cleared -= OnWaveCleared;
            _wave.Cleared += OnWaveCleared;
            _wave.zombieModels = zombieModels;
            _wave.skeletonModel = skeletonModel;
            _wave.armoredSkeletonModel = armoredSkeletonModel;
            _wave.Spawn(Round, player, arenaCenter, arenaRadius);

            Say($"OLEADA · ELIMINA A LOS {_wave.Total} ENEMIGOS");
        }

        void OnWaveCleared()
        {
            if (Phase != RunPhase.Fight || CurrentBoss != null) return;

            SpawnBoss();
            StateChanged?.Invoke();
            Say($"{CurrentBossConfig.displayName} · RONDA {Round}");
        }

        void GenerateBossFor(int round)
        {
            CurrentBossConfig = BossGenerator.Generate(round, _rng.Next(int.MaxValue), _lastBody, _lastAttack);

            // A squad has no single body or primary attack, so it must not poison the next roll.
            if (CurrentBossConfig.family == BossFamily.Single)
            {
                _lastBody = CurrentBossConfig.body;
                _lastAttack = CurrentBossConfig.primaryAttack;
            }
        }

        /// <summary>
        /// Builds the encounter the generator rolled. A single boss is one capsule-driven monster; a
        /// trio is a squad that presents the same interface over three separate bodies.
        /// </summary>
        void SpawnBoss()
        {
            Vector3 spawn = _anchor != null && _anchor.bossSpawn != null
                ? _anchor.bossSpawn.position
                : arenaCenter + Vector3.forward * (arenaRadius * 0.45f);

            var go = new GameObject("Boss");
            go.transform.position = spawn;

            IBossEncounter boss;

            if (CurrentBossConfig.family == BossFamily.Trio)
            {
                var squad = go.AddComponent<SquadBoss>();
                boss = squad;
                squad.skeletonModel = skeletonModel;
                squad.armoredSkeletonModel = armoredSkeletonModel;
                squad.Initialize(CurrentBossConfig, player, arenaCenter, arenaRadius);
            }
            else
            {
                go.AddComponent<CapsuleCollider>();
                var single = go.AddComponent<BossController>();
                boss = single;
                single.Initialize(CurrentBossConfig, player, arenaCenter, arenaRadius);
            }

            CurrentBoss = boss;
            boss.Defeated += OnBossDefeated;
        }

        // ---- flow: boss defeated --------------------------------------------

        void OnBossDefeated(IBossEncounter boss)
        {
            if (boss != null) boss.Defeated -= OnBossDefeated;

            int reward = rewardBase + rewardPerRound * Round
                       + (boss != null && boss.Config != null && boss.Config.modifier == BossModifier.Lucky ? luckyBonus : 0);
            LastReward = reward;
            AddMoney(reward);

            CurrentBoss = null;
            if (boss is Component component) Destroy(component.gameObject, 2.6f);

            if (Round >= totalRounds)
            {
                Phase = RunPhase.RunComplete;
                SetCursor(false);
                SetGameplayActive(false);
                StateChanged?.Invoke();
                Say($"JEFE FINAL DERROTADO · +${reward}");
                return;
            }

            // The next boss is rolled now, so the vestibule can announce it before it exists.
            GenerateBossFor(Round + 1);

            if (_anchor != null && _anchor.exitDoor != null) _anchor.exitDoor.Unlock();
            StateChanged?.Invoke();
            Say($"JEFE DERROTADO · +${reward} · LA PUERTA SE ABRE");
        }

        // ---- flow: arena -> vestibule ---------------------------------------

        public void OnDoorReached(DoorPortal door)
        {
            if (door == null || _transitioning) return;

            if (door.way == DoorWay.ArenaExit)
            {
                if (Phase != RunPhase.Fight || CurrentBoss != null) return;
                if (Round >= totalRounds) return;
                StartCoroutine(EnterPrepRoom());
                return;
            }

            if (Phase != RunPhase.Prep) return;
            if (!_nextSectorReady)
            {
                Say("EL SIGUIENTE SECTOR TODAVÍA SE ESTÁ CARGANDO");
                door.Lock();
                return;
            }

            StartCoroutine(EnterNextSector());
        }

        IEnumerator EnterPrepRoom()
        {
            _transitioning = true;
            Phase = RunPhase.Loading;
            LoadLabel = "DESCARGANDO SECTOR · ABRIENDO VESTÍBULO";
            PrepUiOpen = false;
            SetCursor(false);
            SetGameplayActive(false);
            StateChanged?.Invoke();

            CurrentBoss = null;
            machine = null;
            _anchor = null;
            if (_wave != null) _wave.Clear();

            // Free the arena before anything else is brought in.
            yield return _streamer.Unload(_streamer.CurrentScene);

            yield return _streamer.Load(prepSceneName);
            if (LightingRig.Instance != null) LightingRig.Instance.Apply(null);
            if (_coreScene.IsValid()) SceneManager.SetActiveScene(_coreScene);

            var prep = FindAnyObjectByType<PrepRoomAnchor>();
            machine = prep != null && prep.machine != null ? prep.machine : FindAnyObjectByType<BettingMachine>();
            _prepExit = prep != null ? prep.exitDoor : FindAnyObjectByType<DoorPortal>();

            if (prep != null && prep.playerSpawn != null && player != null)
                player.Teleport(prep.playerSpawn.position, prep.playerSpawn.eulerAngles.y);

            if (_prepExit != null) _prepExit.Lock();

            Phase = RunPhase.Prep;
            PrepUiOpen = false;
            SetCursor(true);
            SetGameplayActive(true);
            if (loadout != null) loadout.SetGameplayEnabled(false);
            else if (weapon != null) weapon.enabled = false;
            StateChanged?.Invoke();
            Say("VESTÍBULO · COMPRA, APUESTA Y SAL AL SIGUIENTE SECTOR");

            _transitioning = false;
            StartCoroutine(PreloadNextSector());
        }

        /// <summary>
        /// Streams the next arena in while the player shops. It is loaded but not activated, so the
        /// walk through the vestibule door is instant and the vestibule is still the only live map.
        /// </summary>
        IEnumerator PreloadNextSector()
        {
            _nextSectorReady = false;

            int next = Round + 1;
            string sceneName = SceneNameFor(next);

            if (_streamer.IsLoaded(sceneName))
            {
                _nextSectorReady = true;
                if (_prepExit != null) _prepExit.Unlock();
                yield break;
            }

            var op = _streamer.Preload(sceneName);
            if (op == null)
            {
                // Already resident or unavailable: the door opens and Activate falls back to Load.
                _nextSectorReady = true;
                if (_prepExit != null) _prepExit.Unlock();
                Say("SIGUIENTE SECTOR LISTO · LA PUERTA ESTÁ ABIERTA");
                yield break;
            }

            _preloadOp = op;

            // With activation withheld, progress stops at 0.9 — that is the "ready" marker.
            while (op.progress < 0.89f) yield return null;

            _nextSectorReady = true;
            if (_prepExit != null) _prepExit.Unlock();
            Say("SIGUIENTE SECTOR LISTO · LA PUERTA ESTÁ ABIERTA");
        }

        // ---- flow: vestibule -> next arena ----------------------------------

        IEnumerator EnterNextSector()
        {
            _transitioning = true;
            Phase = RunPhase.Loading;
            LoadLabel = "ENTRANDO AL SIGUIENTE SECTOR";
            PrepUiOpen = false;
            SetCursor(false);
            SetGameplayActive(false);
            StateChanged?.Invoke();

            // Activate the arena before unloading the vestibule: Unity serialises scene operations,
            // and an unload never completes while a load is parked waiting for activation.
            int next = Round + 1;
            yield return _streamer.Activate(_preloadOp, SceneNameFor(next));
            _preloadOp = null;

            yield return _streamer.Unload(prepSceneName);

            _preloadOp = null;
            _nextSectorReady = false;
            _prepExit = null;

            Round++;
            NameFor(Round);
            // The boss for this round was generated when the previous one died.
            if (CurrentBossConfig == null || CurrentBossConfig.round != Round) GenerateBossFor(Round);

            if (LightingRig.Instance != null) LightingRig.Instance.Apply(DefFor(Round));
            if (_coreScene.IsValid()) SceneManager.SetActiveScene(_coreScene);

            _anchor = FindAnyObjectByType<SectorAnchor>();
            if (_anchor != null)
            {
                arenaCenter = _anchor.arenaCenter;
                arenaRadius = _anchor.arenaRadius;
            }

            Phase = RunPhase.Fight;
            BeginFight();
            _transitioning = false;
        }

        // ---- flow: end -------------------------------------------------------

        void OnPlayerDied()
        {
            if (Phase == RunPhase.Dead) return;
            Phase = RunPhase.Dead;
            PrepUiOpen = false;
            SetCursor(false);
            SetGameplayActive(false);
            StateChanged?.Invoke();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(coreSceneName, LoadSceneMode.Single);
        }

        // ---- vestibule UI ----------------------------------------------------

        public void OpenBetting()
        {
            if (Phase != RunPhase.Prep || PrepUiOpen) return;
            PrepUiOpen = true;
            if (player != null) player.enabled = false;
            if (loadout != null) loadout.SetGameplayEnabled(false);
            else if (weapon != null) weapon.enabled = false;
            SetCursor(false);
            StateChanged?.Invoke();
        }

        public void CloseBetting()
        {
            if (!PrepUiOpen) return;
            PrepUiOpen = false;
            if (Phase == RunPhase.Prep && player != null) player.enabled = true;
            if (Phase == RunPhase.Prep) SetCursor(true);
            StateChanged?.Invoke();
        }

        // ---- plumbing --------------------------------------------------------

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Movement and shooting are live only while a boss is on the field or the vestibule is open.</summary>
        void SetGameplayActive(bool active)
        {
            if (player != null) player.enabled = active;
            if (loadout != null) loadout.SetGameplayEnabled(active);
            else if (weapon != null) weapon.enabled = active;
        }

        /// <summary>
        /// Called by WaveSpawner when the enemy wave phase is complete.
        /// Transitions to boss fight if currently in wave spawning state.
        /// </summary>
        public void CompleteWavePhase()
        {
            // If we're in a state where a wave just finished and boss should spawn
            if (Phase == RunPhase.Fight && CurrentBoss == null)
            {
                // Boss already spawned or none set - just signal
                StateChanged?.Invoke();
                return;
            }

            // Signal that wave phase is complete - the normal GameDirector flow will handle boss spawning
            // This is typically called after the wave spawner finishes its 1-minute timer or all enemies are eliminated
            Debug.Log("[GameDirector] Wave phase complete - proceeding with boss encounter logic");
            StateChanged?.Invoke();
        }
    }
}
