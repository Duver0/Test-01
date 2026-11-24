using UnityEngine;

namespace Slotgun
{
    /// <summary>Per-sector wiring: where the player enters, where the boss appears and which door leaves.</summary>
    public class SectorAnchor : MonoBehaviour
    {
        public SectorDefinition definition;
        public Transform playerSpawn;
        public Transform bossSpawn;

        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 30f;

        public DoorPortal exitDoor;
    }
}
