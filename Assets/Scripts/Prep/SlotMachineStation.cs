using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// The physical slot machine in the vestibule. Using it opens the betting interface, which is
    /// where the next combat is configured (risk tier, or a guaranteed direct purchase).
    /// </summary>
    public class SlotMachineStation : MonoBehaviour, IInteractable
    {
        public string Prompt => "E · ABRIR TRAGAMONEDAS";

        public bool Available
        {
            get
            {
                var d = GameDirector.Instance;
                return d != null && d.Phase == RunPhase.Prep && !d.PrepUiOpen;
            }
        }

        public void Interact()
        {
            var d = GameDirector.Instance;
            if (d != null) d.OpenBetting();
        }
    }
}
