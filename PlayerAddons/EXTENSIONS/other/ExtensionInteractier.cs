using System;
using System.Collections.Generic;
using UnityEngine;

public class ExtensionInteractier : MonoBehaviour, IPlayerBehavior, IRefreshPlayerReferences
{
    protected InputDirector _inputDirector;
    private bool _unsubscribedFromDefaultInteract = false;
    private int _interactionMask;
    public List<string> InteractableTypes;

    private Transform _camTransform;
    public Transform InteractorSource;
    public float InteractRange;
    private Interactable _lastInteractedObj;
    private IPlayerInteractionHandler[] _interactionHandlers;
    private readonly RaycastHit[] _interactionHits = new RaycastHit[32];
    
    public bool DisplayDebugInteract;

    // Start is called before the first frame update
    public void StartPlayer()
    {
        _interactionHandlers = GetComponents<IPlayerInteractionHandler>();
        _inputDirector = GetComponent<InputDirector>();
        _inputDirector.OnInteractPressed += OnPressedInteract;
        
        InteractorSource = GetComponentInChildren<CameraOrientation>().transform;
        
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
        if (_interactionHandlers != null)
            foreach (var handler in _interactionHandlers)
                if (handler.TryHandleInteraction())
                    return;

        Debug.Log("Player Pressed Interact");
        Ray ray = new(InteractorSource.position, _camTransform.forward);
        if (TryRaycastInteraction(ray, out RaycastHit hitInfo))
        {
            if (TryGetInteractable(hitInfo, out var interactObj))
                interactObj.Interact();
            else if (hitInfo.collider != null && hitInfo.collider.transform.parent != null)
            {
                Debug.Log(hitInfo.collider.gameObject.name  + " bellow: " + hitInfo.collider.transform.parent.name + " is not interactable");
            }
        }
    }

    public void UpdatePlayer()
    {
        Ray ray = new(InteractorSource.position, _camTransform.forward);

        if (DisplayDebugInteract)
            Debug.DrawRay(
                ray.origin,
                ray.direction * InteractRange,
                Color.mediumPurple);

        Interactable currentTarget = null;

        if (TryRaycastInteraction(ray, out RaycastHit hitInfo))
        {
            TryGetInteractable(hitInfo, out currentTarget);
        }

        if (!ReferenceEquals(_lastInteractedObj, currentTarget))
        {
            _lastInteractedObj?.StopMarking();
            _lastInteractedObj = currentTarget;
        }

        currentTarget?.MarkAsInteractable();
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

    private bool TryRaycastInteraction(Ray ray, out RaycastHit closest)
    {
        int count = Physics.RaycastNonAlloc(ray, _interactionHits, InteractRange,
            _interactionMask, QueryTriggerInteraction.Collide);
        // A full non-alloc buffer may omit the nearest hit; fall back only for that crowded case.
        RaycastHit[] hits = count == _interactionHits.Length
            ? Physics.RaycastAll(ray, InteractRange, _interactionMask, QueryTriggerInteraction.Collide)
            : _interactionHits;
        if (hits != _interactionHits) count = hits.Length;
        closest = default;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            // Some player helper triggers (e.g. ReadyDetectorCollider) use the
            // default layer, so the Player/Self layer mask alone is insufficient.
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
            closest = hits[i];
            distance = hits[i].distance;
        }
        return distance < float.PositiveInfinity;
    }
    
    private bool TryGetInteractable(RaycastHit hit, out Interactable interactable)
    {
        interactable = null;

        if (hit.collider == null)
            return false;
        
        // Normal case: collider and Interactable are on the same GameObject.
        if (hit.collider.TryGetComponent(out Interactable directInteractable))
            return ValidateInteractable(directInteractable, out interactable);
        
        // Compound object: (Check Parent) collider is on a direct child of the Interactable.
        Transform parent = hit.collider.transform.parent;

        if (parent != null &&
            parent.TryGetComponent(out Interactable parentInteractable))
        {
            return ValidateInteractable(parentInteractable, out interactable);
        }

        return false;
    }
    
    private bool ValidateInteractable(
        Interactable candidate,
        out Interactable interactable)
    {
        interactable = null;

        if (string.IsNullOrEmpty(candidate.InteractableType))
        {
            Debug.LogError(
                $"Forgot to assign InteractableType to {candidate.GetType()}");

            return false;
        }

        if (!InteractableTypes.Contains(candidate.InteractableType))
            return false;

        interactable = candidate;
        return true;
    }

    public void RefreshPlayerReferences()
    {
        InteractorSource = GetComponentInChildren<CameraOrientation>().transform;
    }
}
