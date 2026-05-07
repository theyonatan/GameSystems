using UnityEngine;

public interface IEquipableHeldItem
{
    string EquipLocation { get; }
    string EquipType { get; }
    
    void Equip();
    void Unequip();
}
