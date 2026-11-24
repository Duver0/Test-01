namespace Slotgun
{
    /// <summary>Anything the player can use with E while standing next to it.</summary>
    public interface IInteractable
    {
        /// <summary>Prompt shown on the HUD while this is the closest usable thing.</summary>
        string Prompt { get; }

        /// <summary>False hides the prompt and blocks the interaction (wrong phase, UI open...).</summary>
        bool Available { get; }

        void Interact();
    }
}
