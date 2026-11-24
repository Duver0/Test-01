using UnityEngine;

namespace Slotgun
{
    /// <summary>
    /// The three carried slots. 1 is always the fists, 2 the default sidearm, 3 is reserved for
    /// weapons added later. Selecting a slot with nothing in it falls back to the fists, and a
    /// firearm with no rounds left hands the player over to the fists the moment they pull the
    /// trigger. All weapon input funnels through here so PlayerController never needs to know
    /// which weapon is out.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("Slot 1: always available, no ammo.")]
        public Fists fists;
        [Tooltip("Slot 2: the default sidearm.")]
        public Weapon slot2;
        [Tooltip("Slot 3: weapons added later. Leave empty to show the fists.")]
        public Weapon slot3;

        public int CurrentSlot { get; private set; } = 2;
        public IRuntimeWeapon Current { get; private set; }

        /// <summary>False in the vestibule and while the betting panel is open: the model shows, the trigger is dead.</summary>
        public bool GameplayEnabled { get; private set; } = true;

        /// <summary>The firearm ammo pickups and the shop top up.</summary>
        public Weapon AmmoWeapon => slot2 != null ? slot2 : slot3;

        public event System.Action<int> SlotChanged;

        void Awake()
        {
            if (fists == null) fists = GetComponentInChildren<Fists>(true);

            if (slot2 == null || slot3 == null)
            {
                var weapons = GetComponentsInChildren<Weapon>(true);
                for (int i = 0; i < weapons.Length; i++)
                {
                    if (slot2 == null) slot2 = weapons[i];
                    else if (slot3 == null && weapons[i] != slot2) slot3 = weapons[i];
                }
            }
        }

        void Start()
        {
            CurrentSlot = slot2 != null ? 2 : 1;
            Current = WeaponInSlot(CurrentSlot);
            Apply();
            SlotChanged?.Invoke(CurrentSlot);
        }

        public IRuntimeWeapon WeaponInSlot(int slot)
        {
            switch (slot)
            {
                case 1:  return fists;
                case 2:  return slot2;
                case 3:  return slot3;
                default: return null;
            }
        }

        /// <summary>Number keys 1..3. An empty slot resolves to the fists.</summary>
        public void SelectSlot(int slot)
        {
            if (slot < 1 || slot > 3) return;

            if (WeaponInSlot(slot) == null) slot = 1;

            CurrentSlot = slot;
            Current = WeaponInSlot(slot);
            Apply();
            SlotChanged?.Invoke(CurrentSlot);
        }

        public void SetGameplayEnabled(bool enabled)
        {
            GameplayEnabled = enabled;
            if (!enabled && Current != null) Current.SetTrigger(false);
            if (Current != null) Current.SetInteractive(enabled);
        }

        public void SetTrigger(bool pressed)
        {
            if (!GameplayEnabled)
            {
                if (Current != null) Current.SetTrigger(false);
                return;
            }

            // Out of rounds and out of magazines: the trigger hands you over to your hands.
            if (pressed && Current != null && !Current.IsMelee && !Current.HasAnyAmmo && fists != null)
            {
                var director = GameDirector.Instance;
                if (director != null) director.Say("SIN MUNICIÓN · CUERPO A CUERPO");
                SelectSlot(1);
            }

            if (Current != null) Current.SetTrigger(pressed);
        }

        public void RequestReload()
        {
            if (!GameplayEnabled) return;
            if (Current != null) Current.RequestReload();
        }

        void Apply()
        {
            ApplySlot(fists, CurrentSlot == 1);
            ApplySlot(slot2, CurrentSlot == 2);
            ApplySlot(slot3, CurrentSlot == 3);
        }

        void ApplySlot(IRuntimeWeapon weapon, bool selected)
        {
            if (weapon == null) return;
            weapon.SetVisible(selected);
            weapon.SetInteractive(selected && GameplayEnabled);
        }
    }
}
