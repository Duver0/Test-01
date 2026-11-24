using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// A vestibule merchant stall: health or ammo. Prices and amounts live on the director so the
    /// physical stall, the HUD button and any future shop screen can never disagree.
    /// </summary>
    public class ShopStation : MonoBehaviour, IInteractable
    {
        public enum Kind { Health, Ammo }

        public Kind kind = Kind.Health;

        static GameDirector Director => GameDirector.Instance;

        public string Prompt
        {
            get
            {
                var d = Director;
                if (d == null) return string.Empty;

                return kind == Kind.Health
                    ? $"E · BOTIQUÍN +{d.healAmount:0} VIDA (${d.healPrice})"
                    : $"E · MUNICIÓN COMPLETA (${d.ammoPrice})";
            }
        }

        public bool Available
        {
            get
            {
                var d = Director;
                return d != null && d.Phase == RunPhase.Prep && !d.PrepUiOpen;
            }
        }

        public void Interact()
        {
            var d = Director;
            if (d == null) return;

            d.Say(kind == Kind.Health ? d.TryBuyHeal() : d.TryBuyAmmo());
        }
    }
}
