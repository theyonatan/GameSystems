using System;
using UnityEngine;
using UnityEngine.Events;

public class StoryTrigger : MonoBehaviour
{
    public bool AllowRerun;
    public bool RunOnTrigger = true;
    public float RerunCooldown = 10f;
    
    public bool _triggerActivated;
    
    /// <summary>
    /// future Yonatan: feel free to create another story trigger script
    /// and add parameters to the UnityEvent from there.
    /// </summary>
    [SerializeField] private UnityEvent storyTrigger;

    private void OnTriggerEnter(Collider other)
    {
        if (!RunOnTrigger)
            return;
        
        if (_triggerActivated)
            return;

        if (other.CompareTag("Player"))
        {
            _triggerActivated = true;
            storyTrigger?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!AllowRerun ||
            !_triggerActivated ||
            !RunOnTrigger)
            return;
        
        if (other.CompareTag("Player"))
            Invoke(nameof(ResetTrigger), RerunCooldown);
    }

    private void ResetTrigger() => _triggerActivated = false;
}
