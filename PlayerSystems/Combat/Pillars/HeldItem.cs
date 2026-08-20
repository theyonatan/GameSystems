using UnityEngine;

public class HeldItem : MonoBehaviour
{
    [SerializeField] private GameObject defaultItemPrefab;
    private GameObject _spawnedItem;
    
    public bool HasItem => _spawnedItem != null;
    
    private bool _visualsVisible = true;

    #region Equipping

    public GameObject Equip(GameObject itemPrefab)
    {
        if (_spawnedItem)
            Destroy(_spawnedItem);

        if (!itemPrefab)
            return null;

        _spawnedItem = Instantiate(itemPrefab, transform);
        _spawnedItem.transform.localPosition = Vector3.zero;
        _spawnedItem.transform.localRotation = Quaternion.identity;
        _spawnedItem.transform.localScale = Vector3.one;
        
        ApplyVisualVisibility();
        
        return _spawnedItem;
    }
    
    public void EquipDefault()
    {
        Equip(defaultItemPrefab);
    }

    public void Unequip(bool destroyObject=true)
    {
        if (!_spawnedItem) return;

        if (destroyObject)
            Destroy(_spawnedItem);
        _spawnedItem = null;
    }
    
    #endregion
    
    #region Visuals

    public void SetVisualsVisible(bool visible)
    {
        _visualsVisible = visible;
        ApplyVisualVisibility();
    }

    private void ApplyVisualVisibility()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer)
                itemRenderer.enabled = _visualsVisible;
        }
    }

    private void OnTransformChildrenChanged()
    {
        ApplyVisualVisibility();
    }
    
    #endregion
}
