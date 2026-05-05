using UnityEngine;

public class HeldItem : MonoBehaviour
{
    [SerializeField] private GameObject defaultItemPrefab;
    private GameObject _spawnedItem;
    
    public bool HasItem => _spawnedItem != null;

    public void Equip(GameObject itemPrefab)
    {
        if (_spawnedItem)
            Destroy(_spawnedItem);

        if (!itemPrefab)
            return;

        _spawnedItem = Instantiate(itemPrefab, transform);
        _spawnedItem.transform.localPosition = Vector3.zero;
        _spawnedItem.transform.localRotation = Quaternion.identity;
        _spawnedItem.transform.localScale = Vector3.one;
    }
    
    public void EquipDefault()
    {
        Equip(defaultItemPrefab);
    }

    public void Unequip()
    {
        if (!_spawnedItem) return;

        Destroy(_spawnedItem);
        _spawnedItem = null;
    }
}
