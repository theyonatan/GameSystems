using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class InventoryManager : MonoBehaviour
{
    InventorySlot[] InventorySlots;
    DraggableItem InventoryItemPrefab;

    public static InventoryManager _Instance;

    private void Awake()
    {
        if (_Instance == null)
            _Instance = this;
        else
            Destroy(this);

        // collect all slots
        _Instance.InventorySlots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.InstanceID);

        // order slots by position set in editor
        Array.Sort(_Instance.InventorySlots, (slot1, slot2) => slot1.SlotID.CompareTo(slot2.SlotID));

        // Load Addressables
        GetItemDisplayerObject();
    }

    async void GetItemDisplayerObject()
    {
        if (InventoryItemPrefab != null)
            return;

        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("DisplayedItem");

        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Failed to load InventoryItemPrefab from Addressable with address: {{DisplayedItem}}");
            return;
        }

        InventoryItemPrefab = handle.Result.gameObject.GetComponent<DraggableItem>();
    }

    public bool AddGeneralItem(InventoryItem item)
    {
        // Update existing items
        foreach (InventorySlot slot in InventorySlots)
        {
            DraggableItem itemInSlot = slot.GetComponentInChildren<DraggableItem>();

            if (itemInSlot == null)
                continue;

            // does the item already exist in that slot?
            if (itemInSlot.item.Name == item.Name &&
                itemInSlot.Count < itemInSlot.item.MaxCount &&
                itemInSlot.item.Stackable)
            {
                itemInSlot.Count++;
                itemInSlot.RefreshCount();
                return true;
            }
        }

        // Item doesn't exist. Add it to an empty slot
        foreach (InventorySlot slot in InventorySlots)
        {
            DraggableItem itemInSlot = slot.GetComponentInChildren<DraggableItem>();

            // is the slot empty?
            if (itemInSlot == null)
            {
                SpawnNewItem(item, slot);
                return true;
            }
        }

        return false;
    }

    void SpawnNewItem(InventoryItem item, InventorySlot slot)
    {
        DraggableItem inventoryItem = Instantiate(InventoryItemPrefab, slot.transform);
        inventoryItem.InitialiseItem(item);
    }

    public void UseItem(DraggableItem itemInSlot)
    {
        InventoryItem item = itemInSlot.item;

        itemInSlot.Count--;
        itemInSlot.RefreshCount();

        if (itemInSlot.Count == 0)
            Destroy(itemInSlot.gameObject);

        if (item is ItemAction usableItem)
            usableItem.Use();
    }
}
