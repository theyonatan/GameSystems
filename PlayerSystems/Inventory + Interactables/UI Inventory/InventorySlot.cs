using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    public int SlotID;

    public void OnDrop(PointerEventData eventData)
    {
        // item already exists on this slot
        if (transform.childCount != 0)
            return;

        GameObject dropped = eventData.pointerDrag;
        DraggableItem draggableItem = dropped.GetComponent<DraggableItem>();

        // a different item was dragged here
        if (draggableItem == null)
            return;

        draggableItem.parentAfterDrag = transform;
    }
}
