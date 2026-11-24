using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Slotgun
{
    /// <summary>
    /// Builds and owns the whole uGUI shell: HUD bars, weapon readout, the betting machine panel
    /// and the end screens. Nothing here is authored in the scene — the canvas is assembled in code
    /// so the layout stays versioned with the systems it reflects.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        static Font _font;
        static Font UiFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        GameDirector _director;
        Weapon _weapon;
        PlayerLoadout _loadout;
        PlayerHealth _health;
        PlayerController _player;

        Image _healthFill, _staminaFill, _bossFill, _hitMarker, _crosshair;
        Image _loadFill;
        Text _healthText, _moneyText, _roundText, _bossName, _ammoText, _statsText, _logText, _toastText;
        Text _resultText, _endTitle, _endBody, _promptText, _loadTitle, _loadPercent, _slotsText;
        GameObject _prepRoot, _endRoot, _bossRoot, _loadingRoot;
        RectTransform _root;

        // The machine only exists once the vestibule is streamed in, so every price label is
        // rewritten from it when it appears instead of being baked at Awake (which reads $0).
        readonly Text[] _spinLabels = new Text[3];
        readonly Text[] _directLabels = new Text[6];
        static readonly RiskTier[] SpinTiers = { RiskTier.Safe, RiskTier.Risky, RiskTier.Degen };
        const string Dash = "—";

        readonly List<string> _log = new List<string>();
        float _toastUntil;
        float _hitFlashUntil;
        int _loggedGrants;

        void Awake()
        {
            _director = GameDirector.Instance != null ? GameDirector.Instance : FindAnyObjectByType<GameDirector>();
            _weapon = FindAnyObjectByType<Weapon>();
            _loadout = FindAnyObjectByType<PlayerLoadout>();
            _health = FindAnyObjectByType<PlayerHealth>();
            _player = FindAnyObjectByType<PlayerController>();

            BuildUI();
        }

        void OnEnable()
        {
            if (_director != null)
            {
                _director.StateChanged += Refresh;
                _director.Message += Toast;
            }
            if (_weapon != null) _weapon.HitConfirmed += OnHit;
            if (_loadout != null && _loadout.fists != null) _loadout.fists.HitConfirmed += OnHit;
            Refresh();
        }

        void OnDisable()
        {
            if (_director != null)
            {
                _director.StateChanged -= Refresh;
                _director.Message -= Toast;
            }
            if (_weapon != null) _weapon.HitConfirmed -= OnHit;
            if (_loadout != null && _loadout.fists != null) _loadout.fists.HitConfirmed -= OnHit;
        }

        void Update()
        {
            if (_health != null && _healthFill != null)
                SetBar(_healthFill, _health.maxHealth <= 0f ? 0f : _health.Current / _health.maxHealth);

            if (_player != null && _staminaFill != null)
                SetBar(_staminaFill, _player.SprintFraction);

            if (_healthText != null && _health != null)
                _healthText.text = $"VIDA  {Mathf.CeilToInt(_health.Current)} / {Mathf.CeilToInt(_health.maxHealth)}";

            if (_director != null && _director.CurrentBoss != null && _bossFill != null)
                SetBar(_bossFill, _director.CurrentBoss.HealthFraction);

            if (_ammoText != null) _ammoText.text = AmmoReadout();
            if (_slotsText != null) _slotsText.text = SlotStrip();

            UpdateOverlay();

            if (_toastText != null && _toastUntil > 0f)
            {
                float a = Mathf.Clamp01((_toastUntil - Time.time) / 0.6f);
                var c = _toastText.color; c.a = a; _toastText.color = c;
                if (Time.time > _toastUntil) _toastText.text = string.Empty;
            }

            if (_hitMarker != null)
            {
                float t = Mathf.Clamp01((_hitFlashUntil - Time.time) / 0.12f);
                var c = _hitMarker.color; c.a = t; _hitMarker.color = c;
            }

            if (_weapon != null && _weapon.Grants.Count != _loggedGrants)
            {
                _loggedGrants = _weapon.Grants.Count;
                RebuildLog();
            }
        }

        void OnHit()
        {
            _hitFlashUntil = Time.time + 0.12f;
        }

        /// <summary>Current slot on top, the firearm's rounds and spare magazines underneath.</summary>
        string AmmoReadout()
        {
            var firearm = _loadout != null ? _loadout.AmmoWeapon : _weapon;
            string header = _loadout != null && _loadout.Current != null
                ? _loadout.Current.DisplayName
                : (firearm != null ? firearm.DisplayName : "SIN ARMA");

            if (firearm == null) return header;

            string body = firearm.IsReloading
                ? "RECARGANDO"
                : $"{firearm.Ammo} / {firearm.Stats.magazine}   ·   {firearm.SpareMags} CARG.";

            return header + "\n" + body;
        }

        string SlotStrip()
        {
            if (_loadout == null) return string.Empty;

            string one = Mark(1, "PUÑOS", _loadout.CurrentSlot == 1);
            string two = Mark(2, _loadout.slot2 != null ? _loadout.slot2.DisplayName : "—", _loadout.CurrentSlot == 2);
            string three = Mark(3, _loadout.slot3 != null ? _loadout.slot3.DisplayName : "—", _loadout.CurrentSlot == 3);
            return $"{one}    {two}    {three}";
        }

        static string Mark(int index, string name, bool active)
        {
            return active ? $"[{index} {name}]" : $"{index} {name}";
        }

        void Toast(string message)
        {
            if (_toastText == null) return;
            _toastText.text = message;
            var c = _toastText.color; c.a = 1f; _toastText.color = c;
            _toastUntil = Time.time + 3.2f;
        }

        // ---- refresh from run state ---------------------------------------

        void Refresh()
        {
            if (_director == null) return;

            bool prep = _director.Phase == RunPhase.Prep;
            bool fight = _director.Phase == RunPhase.Fight;
            bool loading = _director.Phase == RunPhase.Loading;
            bool over = _director.Phase == RunPhase.Dead || _director.Phase == RunPhase.RunComplete;

            if (_prepRoot != null) _prepRoot.SetActive(prep && _director.PrepUiOpen);
            if (_endRoot != null) _endRoot.SetActive(over);
            if (_bossRoot != null) _bossRoot.SetActive(fight && _director.CurrentBoss != null);
            if (_loadingRoot != null) _loadingRoot.SetActive(loading);

            if (_moneyText != null) _moneyText.text = $"$ {_director.Money}";
            if (_roundText != null)
            {
                // While the wave is alive there is no boss yet, so the sector line carries the
                // kill counter instead of an empty phase label.
                string wave = fight && _director.CurrentBoss == null && _director.WaveTotal > 0
                    ? $"  ·  OLEADA {_director.WaveTotal - _director.WaveRemaining}/{_director.WaveTotal}"
                    : string.Empty;

                _roundText.text = $"SECTOR {Mathf.Min(_director.Round, _director.totalRounds)} / {_director.totalRounds}  ·  {_director.SectorName}  ·  {PhaseLabel(_director.Phase)}{wave}";
            }

            if (_bossName != null && _director.CurrentBossConfig != null)
                _bossName.text = $"{_director.CurrentBossConfig.displayName}  ·  {(fight ? "FASE " + _director.CurrentBoss?.Phase : "PRÓXIMO")}";

            if (_resultText != null && prep && _director.CurrentBossConfig != null && string.IsNullOrEmpty(_resultText.text))
                _resultText.text = "SIGUIENTE: " + _director.CurrentBossConfig.Describe();

            if (over)
            {
                bool won = _director.Phase == RunPhase.RunComplete;
                if (_endTitle != null) _endTitle.text = won ? "RUN COMPLETA" : "TE HAN DESCUARTIZADO";
                if (_endBody != null)
                {
                    _endBody.text = won
                        ? $"Sobreviviste a {_director.totalRounds} jefes.\nSaldo final: ${_director.Money}\nÚltima recompensa: +${_director.LastReward}"
                        : $"Caíste en la ronda {_director.Round}.\nSaldo final: ${_director.Money}";
                }
            }

            RefreshStats();
            RefreshCosts();
            UpdateOverlay();
        }

        /// <summary>Per-frame chrome that is not driven by run-state change events.</summary>
        void UpdateOverlay()
        {
            if (_director == null) return;

            if (_crosshair != null) _crosshair.enabled = _director.Phase == RunPhase.Fight;

            if (_promptText != null)
            {
                var interactor = _director.interactor != null ? _director.interactor : PlayerInteractor.Instance;
                _promptText.text = interactor != null ? interactor.Prompt : string.Empty;
            }

            if (_loadingRoot != null && _loadingRoot.activeSelf)
            {
                float p = Mathf.Clamp01(_director.LoadProgress);
                if (_loadFill != null)
                {
                    var rt = _loadFill.rectTransform;
                    rt.anchorMax = new Vector2(p, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                if (_loadTitle != null) _loadTitle.text = _director.LoadLabel;
                if (_loadPercent != null) _loadPercent.text = $"{Mathf.RoundToInt(p * 100f)}%";
            }
        }

        static string PhaseLabel(RunPhase p)
        {
            switch (p)
            {
                case RunPhase.Loading:     return "CARGANDO";
                case RunPhase.Prep:        return "VESTÍBULO";
                case RunPhase.Fight:       return "COMBATE";
                case RunPhase.RunComplete: return "VICTORIA";
                default:                   return "DERROTA";
            }
        }

        void RefreshStats()
        {
            if (_weapon == null || _statsText == null) return;
            var s = _weapon.Stats;
            _statsText.text =
                $"{_weapon.DisplayName}\n" +
                $"DAÑO      {s.damage:0.0}\n" +
                $"CADENCIA  {s.fireRate:0.0} /s\n" +
                $"CARGADOR  {s.magazine}\n" +
                $"RETROCESO {s.recoil:0.00}\n" +
                $"PRECISIÓN {s.accuracy * 100f:0}%\n" +
                $"ALCANCE   {s.range:0} m";
        }

        void RebuildLog()
        {
            if (_logText == null || _weapon == null) return;

            _log.Clear();
            var grants = _weapon.Grants;
            int start = Mathf.Max(0, grants.Count - 7);
            for (int i = start; i < grants.Count; i++)
                _log.Add("· " + WeaponStatUtility.Describe(grants[i]));

            _logText.text = _log.Count == 0 ? "sin mejoras todavía" : string.Join("\n", _log);
        }

        static void SetBar(Image fill, float t)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---- construction --------------------------------------------------

        void BuildUI()
        {
            var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo.GetComponent<RectTransform>();

            BuildVitals();
            BuildBossBar();
            BuildWeaponReadout();
            BuildWeaponMinimatives();
            BuildCrosshair();
            BuildToast();
            BuildPrompt();
            BuildPrepPanel();
            BuildEndPanel();
            BuildLoadingPanel();
        }

        void BuildVitals()
        {
            var bar = Node(_root, "HealthBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                           new Vector2(32f, -32f), new Vector2(440f, 30f));
            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = new Color(Palette.UIInk.r, Palette.UIInk.g, Palette.UIInk.b, 0.85f);

            _healthFill = Node(bar, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>();
            _healthFill.color = Palette.CharacterSecondary;

            _healthText = Label(bar, "HealthText", "VIDA", 17, Palette.UIInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            _healthText.rectTransform.StretchAll();

            // Sprint charge, right under the health bar: a full bar pays for 6 s of sprint and refills in 3 s.
            var stamina = Node(_root, "StaminaBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                               new Vector2(32f, -66f), new Vector2(440f, 10f));
            var staminaBg = stamina.gameObject.AddComponent<Image>();
            staminaBg.color = new Color(Palette.UIInk.r, Palette.UIInk.g, Palette.UIInk.b, 0.85f);

            _staminaFill = Node(stamina, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>();
            _staminaFill.color = Palette.Accent;

            _moneyText = Label(_root, "Money", "$ 0", 34, Palette.Props, TextAnchor.UpperLeft, FontStyle.Bold);
            Anchor(_moneyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                   new Vector2(32f, -86f), new Vector2(320f, 44f));

            _roundText = Label(_root, "Round", "RONDA 1", 19, Palette.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            Anchor(_roundText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                   new Vector2(32f, -134f), new Vector2(520f, 28f));
        }

        void BuildBossBar()
        {
            var holder = Node(_root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                              new Vector2(0f, -28f), new Vector2(920f, 62f));
            _bossRoot = holder.gameObject;

            _bossName = Label(holder, "BossName", "JEFE", 24, Palette.CharacterPrimary, TextAnchor.UpperCenter, FontStyle.Bold);
            Anchor(_bossName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                   Vector2.zero, new Vector2(0f, 30f));

            var bar = Node(holder, "BossBarBg", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                           new Vector2(0f, -34f), new Vector2(0f, 22f));
            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = new Color(Palette.UIInk.r, Palette.UIInk.g, Palette.UIInk.b, 0.85f);

            _bossFill = Node(bar, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>();
            _bossFill.color = Palette.CharacterPrimary;

            _bossRoot.SetActive(false);
        }

        void BuildWeaponReadout()
        {
            _statsText = Label(_root, "Stats", "ARMA", 16, Palette.Accent, TextAnchor.LowerLeft, FontStyle.Normal);
            Anchor(_statsText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(32f, 34f), new Vector2(420f, 190f));

            _logText = Label(_root, "Log", "sin mejoras todavía", 15, Palette.CharacterSecondary, TextAnchor.LowerLeft, FontStyle.Normal);
            Anchor(_logText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(32f, 236f), new Vector2(420f, 170f));

            _ammoText = Label(_root, "Ammo", "0 / 0", 26, Palette.Props, TextAnchor.UpperRight, FontStyle.Bold);
            Anchor(_ammoText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                   new Vector2(-32f, -32f), new Vector2(460f, 66f));

            _slotsText = Label(_root, "Slots", string.Empty, 18, Palette.CharacterSecondary, TextAnchor.UpperRight, FontStyle.Bold);
            Anchor(_slotsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                   new Vector2(-32f, -104f), new Vector2(660f, 28f));
        }

        /// <summary>Muestra miniaturas pequeñas de las 3 armas en la parte inferior del HUD.</summary>
        void BuildWeaponMinimatives()
        {
            var miniRoot = Node(_root, "WeaponMinimatives", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -180f), new Vector2(900f, 60f));

            MiniSlot(miniRoot, "Slot1_Mini", "[1] PUÑOS", Palette.CharacterSecondary, 0f);

            string slot2Label = "[2] " + (_loadout != null && _loadout.slot2 != null ? _loadout.slot2.DisplayName : "—");
            MiniSlot(miniRoot, "Slot2_Mini", slot2Label, Palette.Props, 190f);

            MiniSlot(miniRoot, "Slot3_Mini", "[3] —", Palette.Walls, 380f);
        }

        /// <summary>
        /// One weapon card: a dark backdrop with the slot caption on top. The caption cannot live on
        /// the same GameObject as the backdrop — uGUI's Graphic is [DisallowMultipleComponent], so
        /// AddComponent&lt;Text&gt;() next to an Image returns null — hence the child label.
        /// </summary>
        void MiniSlot(Transform parent, string name, string caption, Color textColor, float x)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(180f, 50f);

            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var text = Label(go.transform, "Caption", caption, 16, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.StretchAll();
        }

        void BuildCrosshair()
        {
            _crosshair = Node(_root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              Vector2.zero, new Vector2(10f, 10f)).gameObject.AddComponent<Image>();
            _crosshair.color = Palette.Accent;
            _crosshair.raycastTarget = false;

            _hitMarker = Node(_root, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              Vector2.zero, new Vector2(30f, 30f)).gameObject.AddComponent<Image>();
            _hitMarker.color = new Color(Palette.Danger.r, Palette.Danger.g, Palette.Danger.b, 0f);
            _hitMarker.raycastTarget = false;
        }

        void BuildToast()
        {
            _toastText = Label(_root, "Toast", "", 22, Palette.Props, TextAnchor.UpperCenter, FontStyle.Bold);
            Anchor(_toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                   new Vector2(0f, -110f), new Vector2(1100f, 40f));
            _toastText.raycastTarget = false;
        }

        void BuildPrepPanel()
        {
            var panel = Node(_root, "PrepPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(-32f, 0f), new Vector2(546f, 800f));
            _prepRoot = panel.gameObject;

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(Palette.UIInk.r, Palette.UIInk.g, Palette.UIInk.b, 0.9f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = Label(panel, "Title", "TRAGAMONEDAS", 22, Palette.Props, TextAnchor.MiddleCenter, FontStyle.Bold);
            Sized(title.gameObject, 30f);

            var subtitle = Label(panel, "Subtitle", "dinero ficticio · configura el siguiente combate", 14, Palette.Walls, TextAnchor.MiddleCenter, FontStyle.Normal);
            Sized(subtitle.gameObject, 20f);
            subtitle.color = Color.Lerp(Palette.Walls, Color.white, 0.45f);

            _spinLabels[(int)RiskTier.Safe] = MakeButton(panel, SpinName(RiskTier.Safe) + "  $ " + Dash, Palette.Walls, 44f, Palette.UIInk, () => Spin(RiskTier.Safe));
            _spinLabels[(int)RiskTier.Risky] = MakeButton(panel, SpinName(RiskTier.Risky) + "  $ " + Dash, Palette.Props, 44f, Palette.UIInk, () => Spin(RiskTier.Risky));
            _spinLabels[(int)RiskTier.Degen] = MakeButton(panel, SpinName(RiskTier.Degen) + "  $ " + Dash, Palette.Danger, 44f, Color.white, () => Spin(RiskTier.Degen));

            var direct = Label(panel, "DirectLabel", "COMPRA DIRECTA (garantizada, más cara)", 14, Palette.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Sized(direct.gameObject, 24f);

            var gridGo = new GameObject("DirectGrid", typeof(RectTransform));
            gridGo.transform.SetParent(panel, false);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(233f, 42f);
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            gridGo.AddComponent<LayoutElement>().preferredHeight = 3f * 42f + 2f * 6f;

            for (int i = 0; i < 6; i++)
            {
                var stat = (WeaponStat)i;
                var captured = stat;
                _directLabels[i] = MakeGridButton(gridGo.transform, $"{WeaponStatUtility.Label(stat)}\n$ {Dash}", WeaponStatUtility.Tint(stat),
                                                  () => BuyDirect(captured));
            }

            _resultText = Label(panel, "Result", string.Empty, 13, Palette.CharacterSecondary, TextAnchor.MiddleCenter, FontStyle.Normal);
            Sized(_resultText.gameObject, 56f);

            var shopLabel = Label(panel, "ShopLabel", "TIENDA DEL VESTÍBULO", 14, Palette.CharacterSecondary, TextAnchor.MiddleCenter, FontStyle.Bold);
            Sized(shopLabel.gameObject, 22f);

            float heal = _director != null ? _director.healAmount : 50f;
            int healCost = _director != null ? _director.healPrice : 45;
            int ammoCost = _director != null ? _director.ammoPrice : 30;

            MakeButton(panel, $"BOTIQUÍN +{heal:0} VIDA  ${healCost}", Palette.CharacterSecondary, 42f, Palette.UIInk, BuyHeal);
            MakeButton(panel, $"MUNICIÓN COMPLETA  ${ammoCost}", Palette.Accent, 42f, Palette.UIInk, BuyAmmo);
            MakeButton(panel, "CERRAR  (ESC)", Palette.Walls, 42f, Palette.UIInk, ClosePanel);

            RefreshCosts();
        }

        void BuyHeal()
        {
            if (_director == null) return;
            string msg = _director.TryBuyHeal();
            ShowResult(msg, msg == "SIN FONDOS" || msg == "VIDA AL MÁXIMO" ? Palette.Danger : Palette.CharacterSecondary);
        }

        void BuyAmmo()
        {
            if (_director == null) return;
            string msg = _director.TryBuyAmmo();
            ShowResult(msg, msg == "SIN FONDOS" || msg == "MUNICIÓN AL MÁXIMO" ? Palette.Danger : Palette.Accent);
        }

        void ClosePanel()
        {
            if (_director != null) _director.CloseBetting();
        }

        void ShowResult(string text, Color color)
        {
            if (_resultText != null)
            {
                _resultText.text = text;
                _resultText.color = color;
            }
            Refresh();
        }

        /// <summary>
        /// Rewrites every price from the machine that is actually live. The panel is built in the core
        /// scene, long before the vestibule (and its machine) is streamed in, so the captions start as
        /// a dash and only show real numbers once <see cref="GameDirector.machine"/> exists.
        /// </summary>
        void RefreshCosts()
        {
            var machine = _director != null ? _director.machine : null;
            if (machine == null) return;

            for (int i = 0; i < _spinLabels.Length && i < SpinTiers.Length; i++)
            {
                if (_spinLabels[i] == null) continue;
                _spinLabels[i].text = SpinName(SpinTiers[i]) + "  $ " + machine.CostOf(SpinTiers[i]);
            }

            for (int i = 0; i < _directLabels.Length; i++)
            {
                if (_directLabels[i] == null) continue;
                _directLabels[i].text = $"{WeaponStatUtility.Label((WeaponStat)i)}\n$ {machine.directCost}";
            }
        }

        static string SpinName(RiskTier tier)
        {
            switch (tier)
            {
                case RiskTier.Safe:  return "SEGURO";
                case RiskTier.Risky: return "ARRIESGADO";
                default:             return "DEGENERADO";
            }
        }

        void Spin(RiskTier tier)
        {
            if (_director == null || _director.machine == null) return;
            var r = _director.machine.TrySpin(tier);
            if (_resultText != null)
            {
                _resultText.text = r.message;
                _resultText.color = r.color;
            }
            Refresh();
        }

        void BuyDirect(WeaponStat stat)
        {
            if (_director == null || _director.machine == null) return;
            var r = _director.machine.TryDirectPurchase(stat);
            if (_resultText != null)
            {
                _resultText.text = r.valid ? r.message : "SIN FONDOS";
                _resultText.color = r.valid ? r.color : Palette.Danger;
            }
            Refresh();
        }

        void BuildEndPanel()
        {
            var panel = Node(_root, "EndPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _endRoot = panel.gameObject;

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, 0.88f);

            _endTitle = Label(panel, "EndTitle", "FIN", 64, Palette.CharacterPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 120f), new Vector2(1400f, 90f));

            _endBody = Label(panel, "EndBody", string.Empty, 24, Palette.Props, TextAnchor.MiddleCenter, FontStyle.Normal);
            Anchor(_endBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 10f), new Vector2(1200f, 140f));

            var btnGo = new GameObject("RestartBtn", typeof(RectTransform));
            btnGo.transform.SetParent(panel, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            Anchor(btnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, -110f), new Vector2(380f, 64f));

            var img = btnGo.AddComponent<Image>();
            img.color = Palette.CharacterSecondary;
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => { if (_director != null) _director.Restart(); });

            var label = Label(btnRt, "Label", "OTRA RUN", 26, Palette.UIInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.StretchAll();

            _endRoot.SetActive(false);
        }

        void BuildPrompt()
        {
            _promptText = Label(_root, "Prompt", string.Empty, 22, Palette.Props, TextAnchor.LowerCenter, FontStyle.Bold);
            Anchor(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                   new Vector2(0f, 132f), new Vector2(1200f, 36f));
            _promptText.raycastTarget = false;
        }

        /// <summary>Full-screen cover used while a map is being streamed in.</summary>
        void BuildLoadingPanel()
        {
            var panel = Node(_root, "LoadingPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _loadingRoot = panel.gameObject;

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, 0.94f);

            _loadTitle = Label(panel, "LoadTitle", "CARGANDO", 40, Palette.Props, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(_loadTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 60f), new Vector2(1400f, 60f));

            var bar = Node(panel, "LoadBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(0f, -10f), new Vector2(720f, 18f));
            var bgBar = bar.gameObject.AddComponent<Image>();
            bgBar.color = new Color(Palette.UIInk.r, Palette.UIInk.g, Palette.UIInk.b, 0.9f);

            _loadFill = Node(bar, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>();
            _loadFill.color = Palette.Accent;

            _loadPercent = Label(panel, "LoadPct", "0%", 20, Palette.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(_loadPercent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, -56f), new Vector2(300f, 30f));

            _loadingRoot.SetActive(false);
        }

        // ---- primitives ----------------------------------------------------

        RectTransform Node(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                           Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Anchor(rt, anchorMin, anchorMax, pivot, position, size);
            return rt;
        }

        static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                           Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Sized(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        static Text Label(Transform parent, string name, string content, int size, Color color,
                          TextAnchor align, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        Text MakeButton(Transform parent, string label, Color bg, float height, Color textColor, System.Action onClick)
        {
            var go = new GameObject("Btn_" + Sanitize(label), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bg;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Sized(go, height);

            var text = Label(go.transform, "Label", label, 19, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.StretchAll();
            return text;
        }

        Text MakeGridButton(Transform parent, string label, Color bg, System.Action onClick)
        {
            var go = new GameObject("Grid_" + Sanitize(label), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bg;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = Label(go.transform, "Label", label, 14, Palette.UIInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.StretchAll();
            return text;
        }

        static string Sanitize(string s)
        {
            return s.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("$", string.Empty).Replace("%", string.Empty);
        }
    }

    static class RectTransformExtensions
    {
        public static void StretchAll(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
