using UnityEngine;
using UnityEngine.Events;

public class StoryTriggerNetworked : MonoBehaviour
{
    public bool AllowRerun;
    public bool RunOnTrigger = true;
    public float RerunCooldown = 10f;
    
    public bool _triggerActivated;
    
    [System.Serializable]
    public class StoryTriggerEvent : UnityEvent<int> { }
    [SerializeField] private StoryTriggerEvent storyTrigger;

    private void OnTriggerEnter(Collider other)
    {
        if (!RunOnTrigger || _triggerActivated)
            return;

        if (other.CompareTag("Player") && other.TryGetComponent(out Player player))
        {
            int playerId = player.PlayerId;
            _triggerActivated = true;
            storyTrigger?.Invoke(playerId);
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
