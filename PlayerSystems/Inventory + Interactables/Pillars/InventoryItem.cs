using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InventoryItem : MonoBehaviour
{
    public string Name;
    public Sprite Icon;

    public bool Stackable;
    public bool PickupAble;
    public bool DisplayableInInventoryMenu;

    public int MaxCount = 1;

    public ItemType Type;
    public ItemAction Action;
}

public enum ItemType
{
    None,
    Modifier,
    Potion,
    World,
    Weapon
}
