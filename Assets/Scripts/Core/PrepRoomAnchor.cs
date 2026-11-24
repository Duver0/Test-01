using UnityEngine;

namespace Slotgun
{
    /// <summary>Wiring for the between-combats vestibule: spawn point, exit door and the slot machine.</summary>
    public class PrepRoomAnchor : MonoBehaviour
    {
        public Transform playerSpawn;
        public DoorPortal exitDoor;
        public BettingMachine machine;
    }
}
