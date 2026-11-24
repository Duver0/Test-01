namespace Slotgun
{
    /// <summary>
    /// Everything the player can hold. PlayerLoadout only talks to this surface, so slot 3 can be
    /// filled with any future weapon without the input, HUD or director knowing about it.
    /// </summary>
    public interface IRuntimeWeapon
    {
        string DisplayName { get; }

        /// <summary>True for the fists: no magazine, no reload.</summary>
        bool IsMelee { get; }

        /// <summary>False only when a firearm has an empty magazine and no spare magazines left.</summary>
        bool HasAnyAmmo { get; }

        /// <summary>Trigger state, polled every frame by PlayerController.</summary>
        void SetTrigger(bool pressed);

        /// <summary>R key. Melee ignores it.</summary>
        void RequestReload();

        /// <summary>Show or hide the first-person model (weapon switching).</summary>
        void SetVisible(bool visible);

        /// <summary>Enable/disable firing without hiding the model (vestibule, betting panel).</summary>
        void SetInteractive(bool interactive);
    }
}
