using UnityEngine;

namespace Slotgun
{
    /// <summary>Ordered list of the sectors a run walks through. One asset, one source of truth.</summary>
    [CreateAssetMenu(menuName = "Slotgun/Sector Catalog", fileName = "SectorCatalog")]
    public class SectorCatalog : ScriptableObject
    {
        public SectorDefinition[] sectors = new SectorDefinition[0];

        public int Count => sectors != null ? sectors.Length : 0;

        public SectorDefinition Get(int index)
        {
            if (sectors == null || sectors.Length == 0) return null;
            return sectors[Mathf.Clamp(index, 0, sectors.Length - 1)];
        }
    }
}
