using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Single source of colour for the whole game. Every material, light, particle and UI colour
    /// reads from here so the art bible cannot drift. Never use Color.gray / default materials.
    /// </summary>
    public static class Palette
    {
        // Earth tones, not neon. The casino palette this used to hold (violet ground, plum walls,
        // bubblegum pink, toxic lime, jackpot cyan) is gone: the arenas are dressed from the imported
        // low-poly packs in daylight, and the HUD, the bosses and the effects follow the same muted
        // family so nothing on screen reads as neon any more.
        //
        // Two values are load-bearing and must stay recognisable: Danger is still red (a boss weak
        // point is a red circle), and CharacterSecondary still reads as "good" for the health bar.
        public static readonly Color Floor             = Hex("#6E5A44"); // packed earth
        public static readonly Color Walls             = Hex("#8E877A"); // warm grey stone
        public static readonly Color CharacterPrimary  = Hex("#B4725A"); // terracotta
        public static readonly Color CharacterSecondary= Hex("#7E9463"); // moss green
        public static readonly Color Props             = Hex("#C29B52"); // aged brass
        public static readonly Color Shadow            = Hex("#1A1611"); // warm near-black
        public static readonly Color Accent            = Hex("#5F8FA6"); // dusty steel blue
        public static readonly Color Danger            = Hex("#C33A2E"); // deep red
        public static readonly Color UIInk             = Hex("#16120D"); // warm ink

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
