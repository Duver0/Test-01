using UnityEngine;

namespace Slotgun
{
    public enum BossBody      { SlotGolem, MultiHeadWorm, CrabFridge, BalloonToad, ClockworkBlob }
    public enum BossMovement  { Chase, Strafe, Orbit, Hopper, Charger, Kiter }
    public enum BossAttack    { RadialBurst, AimedVolley, GroundSlam, SpinSpray, HomingOrb, StompRing, MeleeSwing, SightBeam }
    public enum BossAbility   { None, Enrage, Shield, Blink }
    public enum BossModifier  { None, Glass, Tank, Swift, Volatile, Lucky }
    public enum BossFamily    { Single, Trio }

    /// <summary>Everything the runtime boss needs, assembled by BossGenerator.</summary>
    [System.Serializable]
    public sealed class BossConfig
    {
        public string displayName;
        public int round;

        public BossFamily family;
        public BossBody body;
        public BossMovement movement;
        public BossAttack primaryAttack;
        public bool hasSecondary;
        public BossAttack secondaryAttack;
        public BossAbility ability;
        public BossModifier modifier;

        public int budgetCost;
        public float maxHealth;
        public float damageScale;
        public float moveSpeed;
        public float attackCooldown;
        public float visualScale;
        public Color primaryColor;
        public Color secondaryColor;

        public string Describe()
        {
            if (family == BossFamily.Trio)
                return $"TRÍO / melee + distancia + soporte / {modifier}  (cost {budgetCost})";

            string sec = hasSecondary ? $" + {secondaryAttack}" : string.Empty;
            string ab = ability == BossAbility.None ? string.Empty : $" · {ability}";
            return $"{body} / {movement} / {primaryAttack}{sec}{ab} / {modifier}  (cost {budgetCost})";
        }
    }

    /// <summary>
    /// Costed module catalogue. Every module carries a difficulty cost; the generator composes a
    /// boss whose total cost fits the round's budget, which keeps generated bosses readable and
    /// fair instead of "random monster soup".
    /// </summary>
    public static class BossCatalog
    {
        public struct BodyDef
        {
            public BossBody kind; public string name; public int cost;
            public float scale; public Color primary; public Color secondary;
        }

        public struct FamilyDef
        {
            public BossFamily kind; public string name; public int cost; public float healthMul;
        }

        public struct MovementDef
        {
            public BossMovement kind; public string name; public int cost;
            public float speed; public float preferredRange;
        }

        public struct AttackDef
        {
            public BossAttack kind; public string name; public int cost;
            public float cooldown; public float damage;
        }

        public struct AbilityDef
        {
            public BossAbility kind; public string name; public int cost;
        }

        public struct ModifierDef
        {
            public BossModifier kind; public string name; public int cost;
            public float healthMul; public float damageMul; public float speedMul;
        }

        static readonly Color Plum = Color.Lerp(Palette.Walls, Palette.CharacterPrimary, 0.55f);

        public static readonly FamilyDef[] Families =
        {
            new FamilyDef { kind = BossFamily.Single, name = "JEFE ÚNICO", cost = 0, healthMul = 1.00f },
            new FamilyDef { kind = BossFamily.Trio,   name = "TRÍO",       cost = 4, healthMul = 0.82f }
        };

        /// <summary>Names a squad encounter can roll, so trios do not all announce themselves the same way.</summary>
        public static readonly string[] TrioNames =
        {
            "TRÍO DE LA BANCA", "LA MESA DE TRES", "CÁMARA DE COMPENSACIÓN"
        };

        public static readonly BodyDef[] Bodies =
        {
            new BodyDef { kind = BossBody.SlotGolem,     name = "EL TRAGAPERRAS",       cost = 4, scale = 1.15f,
                          primary = Palette.CharacterPrimary,   secondary = Palette.Props },
            new BodyDef { kind = BossBody.MultiHeadWorm, name = "GUSANO TRICÉFALO",     cost = 3, scale = 1.05f,
                          primary = Palette.CharacterSecondary, secondary = Palette.CharacterPrimary },
            new BodyDef { kind = BossBody.CrabFridge,    name = "NEVERA CANGREJO",      cost = 4, scale = 1.10f,
                          primary = Palette.Props,              secondary = Palette.Accent },
            new BodyDef { kind = BossBody.BalloonToad,   name = "SAPO GLOBO",           cost = 3, scale = 1.25f,
                          primary = Palette.Accent,             secondary = Palette.CharacterPrimary },
            new BodyDef { kind = BossBody.ClockworkBlob, name = "BLOQUE DE ENGRANAJES", cost = 5, scale = 1.00f,
                          primary = Plum,                       secondary = Palette.Props }
        };

        public static readonly MovementDef[] Movements =
        {
            new MovementDef { kind = BossMovement.Chase,  name = "Persecución", cost = 2, speed = 4.4f, preferredRange = 0f },
            new MovementDef { kind = BossMovement.Strafe, name = "Hostigamiento", cost = 3, speed = 3.6f, preferredRange = 9f },
            new MovementDef { kind = BossMovement.Orbit,  name = "Órbita",       cost = 3, speed = 4.0f, preferredRange = 11f },
            new MovementDef { kind = BossMovement.Hopper, name = "Saltos",       cost = 2, speed = 3.2f, preferredRange = 0f },
            new MovementDef { kind = BossMovement.Charger, name = "Embestida",   cost = 3, speed = 3.8f, preferredRange = 0f },
            new MovementDef { kind = BossMovement.Kiter,  name = "Retirada",     cost = 3, speed = 4.2f, preferredRange = 15f }
        };

        public static readonly AttackDef[] Attacks =
        {
            new AttackDef { kind = BossAttack.RadialBurst, name = "Ráfaga radial",  cost = 3, cooldown = 3.4f, damage = 10f },
            new AttackDef { kind = BossAttack.AimedVolley, name = "Andanada",       cost = 2, cooldown = 2.2f, damage = 9f },
            new AttackDef { kind = BossAttack.GroundSlam,  name = "Golpe sísmico",  cost = 3, cooldown = 4.6f, damage = 22f },
            new AttackDef { kind = BossAttack.SpinSpray,   name = "Riego giratorio",cost = 4, cooldown = 5.2f, damage = 8f },
            new AttackDef { kind = BossAttack.HomingOrb,   name = "Orbe rastreador",cost = 3, cooldown = 3.8f, damage = 14f },
            new AttackDef { kind = BossAttack.StompRing,   name = "Anillo",         cost = 3, cooldown = 4.0f, damage = 11f },
            new AttackDef { kind = BossAttack.MeleeSwing,  name = "Zarpazo",        cost = 2, cooldown = 3.0f, damage = 18f },
            new AttackDef { kind = BossAttack.SightBeam,   name = "Rayo fijo",      cost = 4, cooldown = 5.4f, damage = 16f }
        };

        public static readonly AbilityDef[] Abilities =
        {
            new AbilityDef { kind = BossAbility.None,   name = "Ninguna",  cost = 0 },
            new AbilityDef { kind = BossAbility.Enrage, name = "Enfurecer", cost = 3 },
            new AbilityDef { kind = BossAbility.Shield, name = "Escudo",    cost = 2 },
            new AbilityDef { kind = BossAbility.Blink,  name = "Parpadeo",  cost = 3 }
        };

        public static readonly ModifierDef[] Modifiers =
        {
            new ModifierDef { kind = BossModifier.None,     name = "Normal",      cost = 0, healthMul = 1.00f, damageMul = 1.00f, speedMul = 1.00f },
            new ModifierDef { kind = BossModifier.Glass,    name = "Cristal",     cost = 2, healthMul = 0.65f, damageMul = 1.35f, speedMul = 1.10f },
            new ModifierDef { kind = BossModifier.Tank,     name = "Blindado",    cost = 3, healthMul = 1.60f, damageMul = 0.90f, speedMul = 0.85f },
            new ModifierDef { kind = BossModifier.Swift,    name = "Veloz",       cost = 2, healthMul = 0.90f, damageMul = 1.00f, speedMul = 1.35f },
            new ModifierDef { kind = BossModifier.Volatile, name = "Volátil",     cost = 2, healthMul = 0.85f, damageMul = 1.15f, speedMul = 1.05f },
            new ModifierDef { kind = BossModifier.Lucky,    name = "Afortunado",  cost = 1, healthMul = 1.10f, damageMul = 1.00f, speedMul = 1.00f }
        };

        public static BodyDef GetBody(BossBody k) { foreach (var d in Bodies) if (d.kind == k) return d; return Bodies[0]; }
        public static FamilyDef GetFamily(BossFamily k) { foreach (var d in Families) if (d.kind == k) return d; return Families[0]; }
        public static MovementDef GetMovement(BossMovement k) { foreach (var d in Movements) if (d.kind == k) return d; return Movements[0]; }
        public static AttackDef GetAttack(BossAttack k) { foreach (var d in Attacks) if (d.kind == k) return d; return Attacks[0]; }
        public static AbilityDef GetAbility(BossAbility k) { foreach (var d in Abilities) if (d.kind == k) return d; return Abilities[0]; }
        public static ModifierDef GetModifier(BossModifier k) { foreach (var d in Modifiers) if (d.kind == k) return d; return Modifiers[0]; }
    }
}
