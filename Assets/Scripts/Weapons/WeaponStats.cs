using UnityEngine;

namespace Slotgun
{
    public enum WeaponStat { Damage = 0, FireRate = 1, Magazine = 2, Recoil = 3, Accuracy = 4, Range = 5 }

    public enum Rarity { Common, Uncommon, Rare, Legendary }

    /// <summary>
    /// The six independent weapon stats. Everything the betting machine grants flows through here,
    /// so an upgrade always changes real behaviour (fire rate -> shots/sec, recoil -> camera kick,
    /// accuracy -> cone spread, range -> hitscan distance).
    /// </summary>
    [System.Serializable]
    public struct WeaponStats
    {
        public float damage;      // per pellet
        public float fireRate;    // shots per second
        public int   magazine;    // rounds per magazine
        public float recoil;      // 0..1 kick strength per shot
        public float accuracy;    // 0..1, 1 = pinpoint
        public float range;       // metres

        public static WeaponStats Default => new WeaponStats
        {
            damage = 11f,
            fireRate = 6.5f,
            magazine = 15,
            recoil = 0.55f,
            accuracy = 0.86f,
            range = 70f
        };

        public float Get(WeaponStat stat)
        {
            switch (stat)
            {
                case WeaponStat.Damage:   return damage;
                case WeaponStat.FireRate: return fireRate;
                case WeaponStat.Magazine: return magazine;
                case WeaponStat.Recoil:   return recoil;
                case WeaponStat.Accuracy: return accuracy;
                default:                  return range;
            }
        }

        /// <summary>Base stats + every granted upgrade, in the enum's index order.</summary>
        public static WeaponStats Compose(WeaponStats b, System.Collections.Generic.IList<UpgradeGrant> grants)
        {
            float[] sums = new float[6];
            if (grants != null)
            {
                for (int i = 0; i < grants.Count; i++)
                    sums[(int)grants[i].stat] += grants[i].magnitude;
            }

            return new WeaponStats
            {
                damage   = b.damage   * (1f + sums[0]),
                fireRate = b.fireRate * (1f + sums[1]),
                magazine = Mathf.Max(1, Mathf.RoundToInt(b.magazine * (1f + sums[2]))),
                recoil   = Mathf.Max(0.015f, b.recoil * (1f - Mathf.Clamp(sums[3], 0f, 0.95f))),
                accuracy = Mathf.Clamp01(b.accuracy + sums[4]),
                range    = b.range * (1f + sums[5])
            };
        }
    }

    /// <summary>A single applied improvement. magnitude is a fraction: 0.12 = +12% better.</summary>
    [System.Serializable]
    public struct UpgradeGrant
    {
        public WeaponStat stat;
        public float magnitude;
        public Rarity rarity;
        public string source;

        public UpgradeGrant(WeaponStat stat, float magnitude, Rarity rarity, string source)
        {
            this.stat = stat;
            this.magnitude = magnitude;
            this.rarity = rarity;
            this.source = source;
        }
    }

    public static class WeaponStatUtility
    {
        public static string Label(WeaponStat stat)
        {
            switch (stat)
            {
                case WeaponStat.Damage:   return "DAMAGE";
                case WeaponStat.FireRate: return "FIRE RATE";
                case WeaponStat.Magazine: return "MAGAZINE";
                case WeaponStat.Recoil:   return "RECOIL";
                case WeaponStat.Accuracy: return "ACCURACY";
                default:                  return "RANGE";
            }
        }

        public static Color Tint(WeaponStat stat)
        {
            switch (stat)
            {
                case WeaponStat.Damage:   return Palette.Danger;
                case WeaponStat.FireRate: return Palette.Props;
                case WeaponStat.Magazine: return Palette.CharacterSecondary;
                case WeaponStat.Recoil:   return Palette.Accent;
                default:                  return Palette.CharacterPrimary;
            }
        }

        public static Color Tint(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:    return Palette.Walls;
                case Rarity.Uncommon:  return Palette.CharacterSecondary;
                case Rarity.Rare:      return Palette.Accent;
                default:               return Palette.Props;
            }
        }

        public static string Label(Rarity rarity)
        {
            return rarity.ToString().ToUpperInvariant();
        }

        /// <summary>One-line human summary used by the HUD log: "DAMAGE +50% [LEGENDARY]".</summary>
        public static string Describe(UpgradeGrant g)
        {
            int pct = Mathf.RoundToInt(g.magnitude * 100f);
            return $"{Label(g.stat)} {(g.stat == WeaponStat.Recoil ? "-" : "+")}{pct}% [{Label(g.rarity)}]";
        }
    }
}
