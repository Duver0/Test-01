using System.Reflection;
using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>
/// Exercises the real integration path a squad encounter takes in game: GameDirector.SpawnBoss with a
/// Trio config, then the reward/next-roll bookkeeping after the pool empties. BossVarietyTest built a
/// SquadBoss by hand, so this is the only check that the director branch, the HUD-facing CurrentBoss
/// and OnBossDefeated all survive a trio.
/// </summary>
public static class TrioSpawnTest
{
    static double _startedAt;
    static int _stage;
    static bool _done;
    static int _fails;

    static SquadBoss _squad;
    static int _moneyBefore;
    static int _roundBefore;

    public static void Execute()
    {
        _startedAt = EditorApplication.timeSinceStartup;
        _stage = 0;
        _done = false;
        _fails = 0;

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[TRIOSPAWN] armed");
    }

    static double Now => EditorApplication.timeSinceStartup;
    static void Note(string s) => Debug.Log("[TRIOSPAWN] " + s);

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
        Debug.Log($"[TRIOSPAWN] DONE · {_fails} FAIL");
    }

    static void Tick()
    {
        if (_done) return;

        if (Now - _startedAt > 45)
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
            {
                _roundBefore = director.Round;
                _moneyBefore = director.Money;

                // Clear whatever the live run spawned so the squad is unambiguous.
                if (director.CurrentBoss is Component live) Object.Destroy(live.gameObject);

                var cfg = new BossConfig
                {
                    round = director.Round,
                    family = BossFamily.Trio,
                    displayName = "TRÍO DE PRUEBA",
                    body = BossBody.SlotGolem,
                    movement = BossMovement.Strafe,
                    primaryAttack = BossAttack.AimedVolley,
                    hasSecondary = false,
                    secondaryAttack = BossAttack.MeleeSwing,
                    ability = BossAbility.None,
                    modifier = BossModifier.None,
                    budgetCost = 4,
                    maxHealth = 900f,
                    damageScale = 1f,
                    moveSpeed = 4f,
                    attackCooldown = 2f,
                    visualScale = 1f,
                    primaryColor = Palette.CharacterSecondary,
                    secondaryColor = Palette.Accent
                };

                typeof(GameDirector)
                    .GetProperty("CurrentBossConfig", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(director, cfg);

                typeof(GameDirector)
                    .GetMethod("SpawnBoss", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(director, null);

                _stage = 1;
                return;
            }

            case 1:
            {
                _squad = director.CurrentBoss as SquadBoss;
                Check(_squad != null, "the director builds a SquadBoss for a trio config");

                if (_squad == null) { Finish(); return; }

                Check(_squad.Units.Count == 3, $"the spawned squad has three bodies ({_squad.Units.Count})");
                Check(_squad.transform.childCount >= 3,
                      $"the bodies are children of the boss root ({_squad.transform.childCount})");
                Check(_squad.gameObject.GetComponent<Collider>() == null,
                      "the squad root has no capsule of its own");
                Check(_squad.gameObject.GetComponentsInChildren<BossWeakPoint>().Length == 3,
                      "each spawned body carries a weak point");
                Check(Mathf.Abs(_squad.MaxHealth - 900f) < 0.5f,
                      $"the pooled bar came from the config ({_squad.MaxHealth:0.0})");

                float distance = Vector3.Distance(_squad.transform.position, director.player.transform.position);
                Note($"squad rooted {distance:0.0} m from the player, phase {_squad.Phase}");

                for (int i = 0; i < _squad.Units.Count; i++)
                {
                    var unit = _squad.Units[i];
                    if (unit == null || !unit.IsAlive) continue;
                    unit.TakeDamage(unit.Current + 5f, unit.transform.position,
                                    unit.transform.position + Vector3.forward);
                }

                _stage = 2;
                return;
            }

            case 2:
            {
                Check(director.CurrentBoss == null, "emptying the pool clears the director's CurrentBoss");
                Check(director.Money > _moneyBefore,
                      $"the trio pays its reward (${_moneyBefore} -> ${director.Money})");

                var next = director.CurrentBossConfig;
                Check(next != null && next.round == _roundBefore + 1,
                      $"the next round is rolled before the door opens (round {next?.round})");

                Note("all checks executed");
                Finish();
                return;
            }
        }
    }
}
