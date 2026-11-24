using UnityEngine;

namespace Slotgun
{
    public enum RiskTier { Safe, Risky, Degen }

    public struct SpinResult
    {
        public bool valid;      // the machine accepted the bet
        public bool success;    // the player got an upgrade
        public string message;
        public UpgradeGrant grant;
        public int moneyDelta;  // negative = money lost beyond the stake
        public Color color;
    }

    /// <summary>
    /// The differential mechanic. Two paths for every stat: a costly guaranteed purchase, or a
    /// cheap roulette spin with weighted odds — including the chance of walking away with nothing.
    /// </summary>
    public class BettingMachine : MonoBehaviour
    {
        public int directCost = 75;
        public int safeCost = 35;
        public int riskyCost = 70;
        public int degenCost = 140;

        public int CostOf(RiskTier tier)
        {
            switch (tier)
            {
                case RiskTier.Safe:  return safeCost;
                case RiskTier.Risky: return riskyCost;
                default:             return degenCost;
            }
        }

        GameDirector Director => GameDirector.Instance;

        public SpinResult TryDirectPurchase(WeaponStat stat)
        {
            var fail = new SpinResult { valid = false, message = "SIN FONDOS", color = Palette.Danger };

            if (Director == null || Director.weapon == null) return fail;
            if (!Director.TrySpend(directCost)) return fail;

            var grant = new UpgradeGrant(stat, 0.10f, Rarity.Common, "compra directa");
            Director.weapon.AddUpgrade(grant);

            return new SpinResult
            {
                valid = true,
                success = true,
                grant = grant,
                message = $"COMPRA: {WeaponStatUtility.Describe(grant)}",
                color = Palette.CharacterSecondary
            };
        }

        public SpinResult TrySpin(RiskTier tier)
        {
            int cost = CostOf(tier);
            var fail = new SpinResult { valid = false, message = "SIN FONDOS", color = Palette.Danger };

            if (Director == null || Director.weapon == null) return fail;
            if (!Director.TrySpend(cost)) return fail;

            float roll = Random.value;

            switch (tier)
            {
                case RiskTier.Safe:
                    // 55% common, 30% uncommon, 15% refund
                    if (roll < 0.55f) return Grant(0.08f, Rarity.Common);
                    if (roll < 0.85f) return Grant(0.16f, Rarity.Uncommon);
                    Director.AddMoney(cost);
                    return new SpinResult
                    {
                        valid = true, success = false, moneyDelta = cost,
                        message = "SEGURO: te devuelven la apuesta", color = Palette.Walls
                    };

                case RiskTier.Risky:
                    // 40% uncommon, 35% rare, 25% nothing
                    if (roll < 0.40f) return Grant(0.16f, Rarity.Uncommon);
                    if (roll < 0.75f) return Grant(0.28f, Rarity.Rare);
                    return new SpinResult
                    {
                        valid = true, success = false, moneyDelta = -cost,
                        message = "ARRIESGADO: la casa gana", color = Palette.Danger
                    };

                default:
                    // 28% rare, 22% legendary, 30% common, 20% backfire (extra loss)
                    if (roll < 0.28f) return Grant(0.28f, Rarity.Rare);
                    if (roll < 0.50f) return Grant(0.50f, Rarity.Legendary);
                    if (roll < 0.80f) return Grant(0.08f, Rarity.Common);

                    int extra = Mathf.Min(cost, Mathf.Max(0, Director.Money));
                    Director.TrySpend(extra);
                    return new SpinResult
                    {
                        valid = true, success = false, moneyDelta = -(cost + extra),
                        message = $"DEGENERADO: la máquina te despluma (-${cost + extra})", color = Palette.Danger
                    };
            }
        }

        SpinResult Grant(float magnitude, Rarity rarity)
        {
            var stat = (WeaponStat)Random.Range(0, 6);
            var grant = new UpgradeGrant(stat, magnitude, rarity, "ruleta");
            Director.weapon.AddUpgrade(grant);

            return new SpinResult
            {
                valid = true,
                success = true,
                grant = grant,
                message = $"JACKPOT: {WeaponStatUtility.Describe(grant)}",
                color = WeaponStatUtility.Tint(rarity)
            };
        }
    }
}
