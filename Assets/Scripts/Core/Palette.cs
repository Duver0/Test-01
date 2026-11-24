using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// Single source of colour for the whole game. Every material, light, particle and UI colour
    /// reads from here so the art bible cannot drift. Never use Color.gray / default materials.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Floor             = Hex("#3B2358"); // bruised violet ground
        public static readonly Color Walls             = Hex("#6A3F9E"); // circus plum
        public static readonly Color CharacterPrimary  = Hex("#FF5FA2"); // bubblegum flesh
        public static readonly Color CharacterSecondary= Hex("#8BF05A"); // toxic lime
        public static readonly Color Props             = Hex("#FFC23D"); // casino gold
        public static readonly Color Shadow            = Hex("#140B24"); // tar black
        public static readonly Color Accent            = Hex("#39E6FF"); // jackpot cyan
        public static readonly Color Danger            = Hex("#FF3B30"); // boss damage / low health
        public static readonly Color UIInk             = Hex("#0B0514");

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
