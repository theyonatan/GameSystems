using UnityEngine;
using System.Collections.Generic;

public class HeldItem : MonoBehaviour
{
    [SerializeField] private GameObject defaultItemPrefab;
    private GameObject _spawnedItem;
    
    public bool HasItem => _spawnedItem != null;
    public GameObject EquippedItem => _spawnedItem;
    public bool IsStowed => _stowSources.Count > 0;
    private readonly HashSet<object> _stowSources = new();
    private readonly Dictionary<Collider, bool> _stowedColliders = new();
    
    private bool _visualsVisible = true;

    #region Equipping

    public GameObject Equip(GameObject itemPrefab)
    {
        if (IsStowed || EquipmentRestrictions.IsBlocked(GetComponentInParent<Player>()))
            return null;

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

    /// <summary>Preserves ordinary held props while hiding them and disabling their colliders.
    /// Networked weapon extensions separately despawn/restore their own network objects.</summary>
    public void SetStowed(object source, bool stowed)
    {
        if (source == null) throw new System.ArgumentNullException(nameof(source));
        bool changed = stowed ? _stowSources.Add(source) : _stowSources.Remove(source);
        if (!changed) return;
        ApplyStowedColliders();
        ApplyVisualVisibility();
    }

    private void ApplyStowedColliders()
    {
        if (IsStowed)
        {
            foreach (var itemCollider in GetComponentsInChildren<Collider>(true))
            {
                if (!_stowedColliders.ContainsKey(itemCollider))
                    _stowedColliders.Add(itemCollider, itemCollider.enabled);
                itemCollider.enabled = false;
            }
        }
        else
        {
            foreach (var entry in _stowedColliders)
                if (entry.Key) entry.Key.enabled = entry.Value;
            _stowedColliders.Clear();
        }
    }

    private void ApplyVisualVisibility()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer)
                itemRenderer.enabled = _visualsVisible && !IsStowed;
        }
    }

    private void OnTransformChildrenChanged()
    {
        ApplyStowedColliders();
        ApplyVisualVisibility();
    }
    
    #endregion
}
