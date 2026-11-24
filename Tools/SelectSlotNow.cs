using UnityEngine;
using Slotgun;

/// <summary>Editor-side slot selector, used to eyeball the first-person models without the mouse.</summary>
public static class SelectSlotNow
{
    public static void Execute()
    {
        var d = GameDirector.Instance;
        if (d == null || d.loadout == null)
        {
            Debug.Log("[WPNTEST] no director/loadout to switch");
            return;
        }

        d.loadout.SelectSlot(1);

        Debug.Log($"[WPNTEST] forced slot {d.loadout.CurrentSlot} '{d.loadout.Current.DisplayName}' · " +
                  $"weapon mag {d.weapon.Ammo}/{d.weapon.Stats.magazine} spare {d.weapon.SpareMags} · " +
                  $"gun visible {(d.weapon.viewmodel != null && d.weapon.viewmodel.gameObject.activeSelf)} · " +
                  $"fists visible {(d.loadout.fists.viewmodel != null && d.loadout.fists.viewmodel.gameObject.activeSelf)}");
    }
}
