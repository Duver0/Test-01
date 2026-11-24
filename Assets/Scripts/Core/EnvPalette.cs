using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Colours for the world only: ground, walls, props and sky.
    ///
    /// <see cref="Palette"/> stays the source for the HUD, the bosses and the effect colours — a
    /// readable HUD and a strong red weak point need saturation. The arena itself no longer uses it:
    /// the neon casino (violet floors, magenta walls, glowing trim) is gone, and the world is now
    /// dressed out of the imported low-poly packs, so these are the earth, stone and wood tones the
    /// packs already use.
    /// </summary>
    public static class EnvPalette
    {
        public static readonly Color Ground   = Hex("#8A6F52"); // packed earth
        public static readonly Color Stone    = Hex("#9A9384"); // arena walls
        public static readonly Color Wood     = Hex("#8B6A46"); // crates, gates, pylons
        public static readonly Color WoodDark = Hex("#5E4630"); // pillars, dark joinery
        public static readonly Color Iron     = Hex("#4A4E52"); // trim, baseboards, lanterns

        public static readonly Color Sky       = Hex("#8FB7D6"); // zenith
        public static readonly Color SkyGround = Hex("#7C7A6A"); // horizon / ground bounce
        public static readonly Color Fog       = Hex("#B9CBD6"); // daylight haze
        public static readonly Color Sun       = Hex("#FFF2DC"); // key light
        public static readonly Color Fill      = Hex("#9FB4C4"); // cool bounce
        public static readonly Color Ambient   = Hex("#A9BFD2"); // flat fallback when there is no sky

        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;

        /// <summary>True when two colours are the same material colour, allowing for serialisation rounding.</summary>
        public static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.006f
                                                  && Mathf.Abs(a.g - b.g) < 0.006f
                                                  && Mathf.Abs(a.b - b.b) < 0.006f;
    }
}
