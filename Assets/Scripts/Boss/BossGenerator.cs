using System.Collections.Generic;
using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Composes a boss from the costed module catalogue under a per-round difficulty budget.
    /// Randomness is seeded so a round's boss is reproducible, and the generator rejects
    /// combinations that are over budget or that reuse the previous body.
    /// </summary>
    public static class BossGenerator
    {
        public const int MinConfigurations = 8;

        public static int BudgetForRound(int round)
        {
            // Round 1: 12, Round 2: 16, Round 3: 20, then +3 per extra round.
            return 8 + round * 4;
        }

        public static BossConfig Generate(int round, int seed, BossBody? avoidBody = null, BossAttack? avoidAttack = null)
        {
            var rng = new System.Random(seed);
            int budget = BudgetForRound(round);

            // From round 2 on, roughly a third of the encounters are a squad of three bodies
            // instead of a single monster: same health pool, three different jobs to solve.
            if (round >= 2 && rng.NextDouble() < 0.34)
            {
                var squad = ComposeTrio(round, rng, budget);
                if (squad != null) return squad;
            }

            var bodies    = BossCatalog.Bodies;
            var movements = BossCatalog.Movements;
            var attacks   = BossCatalog.Attacks;
            var abilities = BossCatalog.Abilities;
            var modifiers = BossCatalog.Modifiers;

            BossConfig best = null;

            for (int attempt = 0; attempt < 512; attempt++)
            {
                var body = bodies[rng.Next(bodies.Length)];
                if (avoidBody.HasValue && body.kind == avoidBody.Value) continue;

                var movement  = movements[rng.Next(movements.Length)];
                var primary   = attacks[rng.Next(attacks.Length)];
                if (avoidAttack.HasValue && primary.kind == avoidAttack.Value) continue;

                var ability   = abilities[rng.Next(abilities.Length)];
                var modifier  = modifiers[rng.Next(modifiers.Length)];

                var secondaries = new List<int>();
                for (int i = 0; i < attacks.Length; i++) if (i != System.Array.IndexOf(attacks, primary)) secondaries.Add(i);
                bool hasSecondary = rng.NextDouble() < 0.55;
                var secondaryDef = attacks[secondaries[rng.Next(secondaries.Count)]];

                int cost = body.cost + movement.cost + primary.cost + ability.cost + modifier.cost;
                if (hasSecondary) cost += 1;

                if (cost > budget) continue;

                var cfg = new BossConfig
                {
                    round = round,
                    family = BossFamily.Single,
                    displayName = body.name,
                    body = body.kind,
                    movement = movement.kind,
                    primaryAttack = primary.kind,
                    hasSecondary = hasSecondary,
                    secondaryAttack = secondaryDef.kind,
                    ability = ability.kind,
                    modifier = modifier.kind,
                    budgetCost = cost,
                    moveSpeed = movement.speed * modifier.speedMul,
                    attackCooldown = primary.cooldown,
                    visualScale = body.scale,
                    primaryColor = body.primary,
                    secondaryColor = body.secondary,
                    damageScale = modifier.damageMul * (1f + 0.12f * (round - 1)),
                    maxHealth = (820f + 520f * round) * modifier.healthMul
                };

                // Prefer the most expensive legal combination (closest to the budget).
                if (best == null || cfg.budgetCost > best.budgetCost) best = cfg;
            }

            if (best != null) return best;

            // Fallback: cheapest legal combination so a boss always exists.
            var safeBody = BossCatalog.GetBody(BossBody.MultiHeadWorm);
            var safeMove = BossCatalog.GetMovement(BossMovement.Chase);
            var safeAtk  = BossCatalog.GetAttack(BossAttack.AimedVolley);
            return new BossConfig
            {
                round = round,
                family = BossFamily.Single,
                displayName = safeBody.name,
                body = safeBody.kind,
                movement = safeMove.kind,
                primaryAttack = safeAtk.kind,
                hasSecondary = false,
                secondaryAttack = BossAttack.AimedVolley,
                ability = BossAbility.None,
                modifier = BossModifier.None,
                budgetCost = safeBody.cost + safeMove.cost + safeAtk.cost,
                moveSpeed = safeMove.speed,
                attackCooldown = safeAtk.cooldown,
                visualScale = safeBody.scale,
                primaryColor = safeBody.primary,
                secondaryColor = safeBody.secondary,
                damageScale = 1f + 0.12f * (round - 1),
                maxHealth = 820f + 520f * round
            };
        }

        /// <summary>
        /// A squad encounter: one melee bruiser, one ranged gunner and one support medic sharing a
        /// single health pool. The family carries its own health multiplier because three moving
        /// targets live longer than one body with the same total.
        /// </summary>
        static BossConfig ComposeTrio(int round, System.Random rng, int budget)
        {
            var family = BossCatalog.GetFamily(BossFamily.Trio);
            var modifiers = BossCatalog.Modifiers;
            var modifier = modifiers[rng.Next(modifiers.Length)];

            int cost = family.cost + modifier.cost;
            if (cost > budget) return null;

            var names = BossCatalog.TrioNames;

            return new BossConfig
            {
                round = round,
                family = BossFamily.Trio,
                displayName = names[rng.Next(names.Length)],
                body = BossBody.SlotGolem,
                movement = BossMovement.Strafe,
                primaryAttack = BossAttack.AimedVolley,
                hasSecondary = false,
                secondaryAttack = BossAttack.MeleeSwing,
                ability = BossAbility.None,
                modifier = modifier.kind,
                budgetCost = cost,
                moveSpeed = 4.2f,
                attackCooldown = 2.0f,
                visualScale = 1f,
                primaryColor = Palette.CharacterSecondary,
                secondaryColor = Palette.Accent,
                damageScale = modifier.damageMul * (1f + 0.12f * (round - 1)),
                maxHealth = (820f + 520f * round) * modifier.healthMul * family.healthMul
            };
        }
    }
}
