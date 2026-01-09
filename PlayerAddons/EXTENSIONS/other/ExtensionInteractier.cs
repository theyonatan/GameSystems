using System;
using System.Collections.Generic;
using UnityEngine;

public class ExtensionInteractier : MonoBehaviour, IPlayerBehavior
{
    protected InputDirector _inputDirector;
    private bool _unsubscribedFromDefaultInteract = false;
    private int _interactionMask;
    public List<string> InteractableTypes;

    private Transform _camTransform;
    public Transform InteractorSource;
    public float InteractRange;
    private Interactable _lastInteractedObj;
    
    public bool DisplayDebugInteract;

    // Start is called before the first frame update
    public void StartPlayer()
    {
        _inputDirector = GetComponent<InputDirector>();
        _inputDirector.OnInteractPressed += OnPressedInteract;
        
        // get ignore layers
        int playerLayer = LayerMask.NameToLayer("Player");
        int selfLayer = LayerMask.NameToLayer("Self");
        
        // default: hit anything
        _interactionMask = Physics.DefaultRaycastLayers;
        
        // exclude player and self
        if (playerLayer != -1)
            _interactionMask &= ~(1 << playerLayer);
        if (selfLayer != -1)
            _interactionMask &= ~(1 << selfLayer);
        
        // get camera for direction
        if (TryGetComponent(out Player player))
            _camTransform = player.GetCamera().transform;
        else if (Camera.main != null)
            _camTransform = Camera.main.transform;
        else
            _camTransform = InteractorSource.transform;
    }

    protected void OnPressedInteract()
    {
        Debug.Log("Player Pressed Interact");
        Ray ray = new(InteractorSource.position, _camTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange, _interactionMask))
        {
            if (TryGetInteractable(hitInfo, out var interactObj))
                interactObj.Interact();
            else if (hitInfo.collider != null && hitInfo.collider.transform.parent != null)
            {
                Debug.Log(hitInfo.collider.gameObject.name  + " bellow: " + hitInfo.collider.transform.parent.name + " is not interactable");
            }
        }
    }

    // Update is called once per frame
    public void UpdatePlayer()
    {
        Ray r = new(InteractorSource.position, _camTransform.forward);
        if (DisplayDebugInteract) 
            Debug.DrawRay(r.origin, r.direction * InteractRange, Color.mediumPurple);
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange, _interactionMask))
        {
            if (TryGetInteractable(hitInfo, out var interactObj))
            {
                interactObj.MarkAsInteractable();
                _lastInteractedObj = interactObj;
            }
            else if (_lastInteractedObj != null)
            {
                _lastInteractedObj.StopMarking();
                _lastInteractedObj = null;
            }
        }
    }

    public void OnDestroyPlayer()
    {
        UnsubscribeFromDefaultInteract();
    }

    protected void UnsubscribeFromDefaultInteract()
    {
        if (_unsubscribedFromDefaultInteract)
            return;
        
        _unsubscribedFromDefaultInteract = true;
        _inputDirector.OnInteractPressed -= OnPressedInteract;
    }
    
    private bool TryGetInteractable(RaycastHit hit, out Interactable interactable)
    {
        if (hit.collider.gameObject.TryGetComponent(out Interactable interactObj))
        {
            if (InteractableTypes.Contains(interactObj.InteractableType))
            {
                interactable = interactObj;
                return true;
            }
            if (string.IsNullOrEmpty(interactObj.InteractableType))
                Debug.LogError($"Forgot To Assign InteractableType to {interactObj.GetType()}");
        }

        interactable = null;
        return false;
    }
}
