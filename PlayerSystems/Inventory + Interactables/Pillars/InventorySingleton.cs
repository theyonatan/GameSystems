using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventorySingleton
{
    static InventorySingleton mInstance;

    public static InventorySingleton Instance
    {
        get
        {
            if (mInstance == null)
            {
                mInstance = new InventorySingleton();

                // maybe I should load this from save data. Oh Well...
                mInstance.m_items = new List<InventoryItem>();
            }
            return mInstance;
        }
    }

    private List<InventoryItem> m_items;

    public void AddItem(InventoryItem item)
    {
        m_items.Add(item);
        InventoryManager._Instance.AddGeneralItem(item);
    }

    public void RemoveItem(InventoryItem item)
    {
        m_items.Remove(item);
    }

    public IEnumerable<InventoryItem> RerieveAllItems(ItemType itemType = ItemType.None)
    {
        if (itemType == ItemType.None)
            return m_items;
        else
            return m_items.Where(item => item.Type == itemType);
    }

    public IEnumerable<InventoryItem> RerieveAllItemsDisplayable(ItemType itemType = ItemType.None)
    {
        if (itemType == ItemType.None)
            return m_items.Where(item => item.DisplayableInInventoryMenu);
        else
            return m_items.Where(item => item.Type == itemType && item.DisplayableInInventoryMenu);
    }

    // From this point on the code is system independent.
    // feel free to remove all of this if you don't want the gui.
    public void EnableInventory()
    {

    }
}
