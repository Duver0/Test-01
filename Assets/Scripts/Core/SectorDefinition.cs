using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// One streamed map of the run: the scene that holds it, how big it is and how it deviates
    /// from the art bible. Pure data so a sector can be retuned without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Slotgun/Sector Definition", fileName = "Sector")]
    public class SectorDefinition : ScriptableObject
    {
        public string displayName = "SECTOR";
        public string sceneName = "Slotgun_Sector_1";

        [Header("Layout")]
        public float arenaRadius = 30f;
        public float wallHeight = 7f;
        [Tooltip("Which arena prop layout the scene builder uses for this sector.")]
        public int pattern;

        [Header("Palette drift")]
        public Color floorTint = Palette.Floor;
        public Color wallTint = Palette.Walls;
        public Color accent = Palette.Accent;
        public float fogDensity = 0.011f;
    }
}
