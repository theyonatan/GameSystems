using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public int Count = 1;
    TextMeshProUGUI CountText;
    Image image;
    [HideInInspector] public InventoryItem item;
    [HideInInspector] public Transform parentAfterDrag;
    
    public void InitialiseItem(InventoryItem newItem)
    {
        image = GetComponent<Image>();
        CountText = GetComponentInChildren<TextMeshProUGUI>();

        item = newItem;
        image.sprite = item.Icon;
        RefreshCount();
    }

    public void RefreshCount()
    {
        CountText.text = Count.ToString();
        CountText.gameObject.SetActive(Count > 1);
    }

    #region UI Actions
    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root.GetComponentInChildren<Canvas>().transform);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag);
        image.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        InventoryManager._Instance.UseItem(this);
    }
    #endregion
}
