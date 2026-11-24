using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// Runtime check for the weak point and the new enemy shapes:
///   · every single-boss archetype carries a small red hit zone that a shot from the front reaches
///     while a shot from the side lands on the body
///   · hitting it deals exactly double the shot's damage and turns on a 2.5x speed surge
///   · the generator produces squad encounters from round 2 on, and never in round 1
///   · a squad is three bodies with three roles over one pooled health bar, its medic heals the
///     most damaged ally, and the encounter only reports Defeated when the pool is empty
/// Bosses are probed one per frame: BossVisual drops the primitive colliders with Destroy, which
/// lands at the end of the frame, so a probe has to wait a frame before its raycasts are honest.
/// </summary>
public static class BossVarietyTest
{
    static double _startedAt;
    static double _stageAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static int _probeIndex;
    static BossController _probe;
    static BossConfig _probeCfg;
    static BossBody _probeBody;
    static float _probeScale;

    static SquadBoss _squad;
    static EnemyUnit _brute;
    static float _bruteHurtAt;
    static bool _squadDefeated;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stageAt = _startedAt;
        _stage = 0;
        _done = false;
        _fails = 0;
        _probeIndex = 0;
        _probe = null;
        _squadDefeated = false;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[BOSSVAR] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[BOSSVAR] " + s);

    static void Check(bool ok, string what)
    {
        if (ok) Note("ok   · " + what);
        else { _fails++; Note("FAIL · " + what); }
    }

    static void Finish()
    {
        if (_done) return;
        _done = true;
        EditorApplication.update -= Tick;
        Debug.Log($"[BOSSVAR] DONE · {_fails} FAIL");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 120)
        {
            Note($"FAIL · timeout at stage {_stage}");
            Finish();
            return;
        }

        var director = GameDirector.Instance;
        if (director == null || director.player == null) return;

        switch (_stage)
        {
            case 0:
                if (_probe != null)
                {
                    MeasureProbe();
                    Object.Destroy(_probe.gameObject);
                    _probe = null;
                    break;
                }

                if (_probeIndex >= BossCatalog.Bodies.Length)
                {
                    _stage = 1;
                    _stageAt = Now;
                    break;
                }

                CreateProbe(_probeIndex++);
                break;

            case 1:
                SweepGenerator();
                _stage = 2;
                _stageAt = Now;
                break;

            case 2:
                SpawnSquad(director);
                _stage = 3;
                _stageAt = Now;
                break;

            case 3:
                // The medic's first heal lands ~2 s after the squad spawns.
                if (Now - _stageAt < 2.8) return;
                Check(_brute != null && _brute.Current > _bruteHurtAt + 1f,
                      $"medic heals the most damaged ally ({_bruteHurtAt:0.0} -> {(_brute != null ? _brute.Current : 0f):0.0})");
                KillSquad();
                _stage = 4;
                _stageAt = Now;
                break;

            case 4:
                Check(_squadDefeated, "squad reports Defeated only once the pool is empty");
                Check(_squad == null || _squad.Current <= 0.01f, "pooled health reaches zero");
                if (_squad != null) Object.Destroy(_squad.gameObject);
                _stage = 5;
                break;

            case 5:
                Note("all checks executed");
                Finish();
                break;
        }
    }

    // ---- weak point on every single-boss archetype ------------------------

    static void CreateProbe(int index)
    {
        var def = BossCatalog.Bodies[index];
        _probeBody = def.kind;
        _probeScale = def.scale;

        _probeCfg = new BossConfig
        {
            round = 1,
            family = BossFamily.Single,
            displayName = def.name,
            body = def.kind,
            movement = BossMovement.Chase,
            primaryAttack = BossAttack.AimedVolley,
            hasSecondary = false,
            secondaryAttack = BossAttack.AimedVolley,
            ability = BossAbility.None,
            modifier = BossModifier.None,
            budgetCost = 0,
            maxHealth = 1000f,
            damageScale = 1f,
            moveSpeed = 3f,
            attackCooldown = 3f,
            visualScale = def.scale,
            primaryColor = def.primary,
            secondaryColor = def.secondary
        };

        var go = new GameObject("Probe_" + def.kind);
        go.transform.position = new Vector3(index * 40f, 40f, 0f);
        go.AddComponent<CapsuleCollider>();

        _probe = go.AddComponent<BossController>();
        _probe.Initialize(_probeCfg, null, Vector3.zero, 400f);   // no target: the probe never moves
    }

    static void MeasureProbe()
    {
        var wp = _probe.GetComponentInChildren<BossWeakPoint>();
        if (wp == null)
        {
            Check(false, $"{_probeBody}: has a weak point");
            return;
        }

        var col = wp.GetComponent<Collider>();
        Vector3 aim = col.bounds.center;

        bool frontHitsZone = false;
        if (Physics.Raycast(aim + Vector3.forward * 8f, Vector3.back, out RaycastHit front, 20f, ~0,
                            QueryTriggerInteraction.Ignore))
        {
            frontHitsZone = front.collider.GetComponentInParent<IRangedHitZone>() == wp;
            if (!frontHitsZone) Note($"      front hit '{front.collider.name}' instead of the circle");
        }

        bool sideHitsBody = false;
        Vector3 sideFrom = _probe.transform.position + Vector3.up * (1.5f * _probeScale) + Vector3.right * 8f;
        if (Physics.Raycast(sideFrom, Vector3.left, out RaycastHit side, 20f, ~0, QueryTriggerInteraction.Ignore))
            sideHitsBody = side.collider.GetComponentInParent<IRangedHitZone>() == null;

        Check(frontHitsZone, $"{_probeBody}: a shot from the front lands on the weak point");
        Check(sideHitsBody, $"{_probeBody}: a shot from the side lands on the body");

        // the circle must be small next to the body it sits on
        float zoneDiameter = col.bounds.size.x;
        float bodyRadius = 1.05f * _probeScale * 2f;
        Check(zoneDiameter < bodyRadius * 0.5f,
              $"{_probeBody}: the circle is small ({zoneDiameter:0.00} m across vs a {bodyRadius:0.00} m body)");

        float before = _probe.Current;
        wp.RangedHit(10f, aim, aim + Vector3.forward * 8f);
        float lost = before - _probe.Current;

        Check(Mathf.Abs(lost - 20f) < 0.5f, $"{_probeBody}: weak point takes 2x ({lost:0.0} from a 10 shot)");
        Check(_probe.WeakSurgeActive, $"{_probeBody}: the hit turns the surge on");
        Check(_probe.SpeedMultiplier >= 2.49f, $"{_probeBody}: movement is x{_probe.SpeedMultiplier:0.00}");
    }

    // ---- generator: squads from round 2 on --------------------------------

    static void SweepGenerator()
    {
        int trios = 0, singles = 0, roundOneTrios = 0, broken = 0;

        for (int round = 1; round <= 5; round++)
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var cfg = BossGenerator.Generate(round, seed * 7919 + round * 13);
                if (cfg == null || cfg.maxHealth <= 0f || string.IsNullOrEmpty(cfg.displayName))
                {
                    broken++;
                    continue;
                }

                if (cfg.family == BossFamily.Trio)
                {
                    trios++;
                    if (round < 2) roundOneTrios++;
                }
                else singles++;
            }
        }

        Check(broken == 0, $"every generated config is sane ({broken} broken)");
        Check(trios > 0, $"squad encounters appear ({trios} trios / {singles} singles)");
        Check(roundOneTrios == 0, "round 1 is always a single boss");

        bool sawCharger = false, sawKiter = false, sawSwing = false, sawBeam = false;
        for (int seed = 0; seed < 400; seed++)
        {
            var cfg = BossGenerator.Generate(3, seed * 104729 + 7);
            if (cfg == null || cfg.family != BossFamily.Single) continue;
            if (cfg.movement == BossMovement.Charger) sawCharger = true;
            if (cfg.movement == BossMovement.Kiter) sawKiter = true;
            if (cfg.primaryAttack == BossAttack.MeleeSwing || cfg.secondaryAttack == BossAttack.MeleeSwing) sawSwing = true;
            if (cfg.primaryAttack == BossAttack.SightBeam || cfg.secondaryAttack == BossAttack.SightBeam) sawBeam = true;
        }

        Check(sawCharger && sawKiter, $"both new movement modules roll (charger {sawCharger}, kiter {sawKiter})");
        Check(sawSwing && sawBeam, $"both new attack modules roll (swing {sawSwing}, beam {sawBeam})");
    }

    // ---- a live squad ------------------------------------------------------

    static void SpawnSquad(GameDirector director)
    {
        var cfg = new BossConfig
        {
            round = 2,
            family = BossFamily.Trio,
            displayName = "PRUEBA DE TRÍO",
            body = BossBody.SlotGolem,
            movement = BossMovement.Strafe,
            primaryAttack = BossAttack.AimedVolley,
            hasSecondary = false,
            secondaryAttack = BossAttack.MeleeSwing,
            ability = BossAbility.None,
            modifier = BossModifier.None,
            budgetCost = 4,
            maxHealth = 1500f,
            damageScale = 1f,
            moveSpeed = 4f,
            attackCooldown = 2f,
            visualScale = 1f,
            primaryColor = Palette.CharacterSecondary,
            secondaryColor = Palette.Accent
        };

        // Out of the live fight's reach: high up, 40 m to the side.
        Vector3 spawn = director.player.transform.position + Vector3.up * 45f + Vector3.right * 40f;

        var go = new GameObject("SquadProbe");
        go.transform.position = spawn;

        _squad = go.AddComponent<SquadBoss>();
        _squad.Initialize(cfg, director.player, spawn, 15f);
        _squad.Defeated += _ => _squadDefeated = true;

        Check(_squad.Units.Count == 3, $"a squad is three bodies ({_squad.Units.Count})");

        bool brute = false, gunner = false, medic = false;
        float sum = 0f;
        for (int i = 0; i < _squad.Units.Count; i++)
        {
            var u = _squad.Units[i];
            if (u == null) continue;
            sum += u.MaxHealth;
            if (u.role == EnemyRole.Brute) { brute = true; _brute = u; }
            if (u.role == EnemyRole.Gunner) gunner = true;
            if (u.role == EnemyRole.Medic) medic = true;
        }

        Check(brute && gunner && medic, "the squad is melee + ranged + support");
        Check(Mathf.Abs(sum - _squad.MaxHealth) < 0.5f && Mathf.Abs(_squad.MaxHealth - 1500f) < 0.5f,
              $"pooled bar is the sum of the bodies ({_squad.MaxHealth:0.0})");

        int zones = go.GetComponentsInChildren<BossWeakPoint>().Length;
        Check(zones == 3, $"each body carries a weak point ({zones})");

        if (_brute == null)
        {
            Check(false, "brute present for the damage test");
            return;
        }

        float poolBefore = _squad.Current;
        _brute.TakeDamage(300f, _brute.transform.position, _brute.transform.position + Vector3.forward);
        Check(Mathf.Abs((poolBefore - _squad.Current) - 300f) < 1f,
              $"damage to one body moves the pooled bar ({poolBefore:0.0} -> {_squad.Current:0.0})");

        var bruteZone = _brute.GetComponentInChildren<BossWeakPoint>();
        float unitBefore = _brute.Current;
        bruteZone.RangedHit(10f, _brute.transform.position, _brute.transform.position + Vector3.forward);
        Check(Mathf.Abs((unitBefore - _brute.Current) - 20f) < 0.5f,
              $"a squad body's weak point also doubles ({(unitBefore - _brute.Current):0.0})");
        Check(_brute.WeakSurgeActive && _brute.SpeedMultiplier >= 2.49f,
              $"a squad body surges too (x{_brute.SpeedMultiplier:0.00})");

        _bruteHurtAt = _brute.Current;
        Note($"brute hurt to {_bruteHurtAt:0.0} of {_brute.MaxHealth:0.0}; waiting for the medic");
    }

    static void KillSquad()
    {
        if (_squad == null) return;

        for (int i = _squad.Units.Count - 1; i >= 0; i--)
        {
            var unit = _squad.Units[i];
            if (unit == null || !unit.IsAlive) continue;
            unit.TakeDamage(unit.Current + 5f, unit.transform.position, unit.transform.position + Vector3.forward);
        }
    }
}
