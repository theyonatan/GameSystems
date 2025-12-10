using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class PotionItem : InventoryItem, ItemAction, Interactable
{
    private void Awake()
    {
        DisplayableInInventoryMenu = true;
    }

    public void Interact()
    {
        Debug.Log("MUCH <3");
        InventorySingleton.Instance.AddItem(this);
        Cursor.visible = true;
        Destroy(gameObject);
    }

    public void Use()
    {
        Debug.Log("Healled by 1200f!");
        StatsSingleton.Instance.IncreamentStat(StatType.Health, 1200f);
    }

    public void MarkAsInteractable()
    {
        Debug.Log("I am Interactable!");
    }
}
