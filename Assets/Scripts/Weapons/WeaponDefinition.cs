using UnityEngine;

namespace Slotgun
{
    [CreateAssetMenu(menuName = "Slotgun/Weapon Definition", fileName = "WeaponDefinition")]
    public class WeaponDefinition : ScriptableObject
    {
        public string displayName = "SLOTGUN";
        [Tooltip("Unupgraded stats. Every betting upgrade is applied on top of these.")]
        public WeaponStats baseStats = WeaponStats.Default;

        [Header("Ballistics")]
        public int pelletsPerShot = 1;
        [Tooltip("Cone half-angle in degrees at accuracy 0. At accuracy 1 it collapses to ~0.")]
        public float spreadMaxDegrees = 7f;
        public bool automatic = true;

        [Header("Look")]
        public Color bodyColor = Palette.CharacterSecondary;
        public Color tracerColor = Palette.Accent;
    }
}
