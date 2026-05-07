using UnityEngine;

public interface IEquipableHeldItem
{
    bool IsEquipped { get; }
    
    string EquipLocation { get; }
    string EquipType { get; }
    
    // Equip is independent, we can call it multiple times.
    // this is important after skin swaps where the iten dies visually but stays alive in extension.
    void Equip();
    void Unequip();
    
    // Used after skin swaps / reference refreshes.
    // If this item is equipped in extension, re-find the new HeldItem area and spawn the visual again.
    void RefreshEquippedVisuals()
    {
        if (!IsEquipped)
            return;

        if (this is IRefreshPlayerReferences refreshable)
            refreshable.RefreshPlayerReferences();

        Equip();
    }
}
