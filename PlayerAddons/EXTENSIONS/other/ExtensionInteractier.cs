using UnityEngine;

public class ExtensionInteractier : MonoBehaviour
{
    private InputDirector _inputDirector;
    private int _interactionMask;

    private Transform _camTransform;
    public Transform InteractorSource;
    public float InteractRange;
    private Interactable _lastInteractedObj;

    // Start is called before the first frame update
    void Start()
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

    void OnPressedInteract()
    {
        Debug.Log("Player Pressed Interact");
        Ray ray = new(InteractorSource.position, _camTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange, _interactionMask))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out Interactable interactObj))
                interactObj.Interact();
            else if (hitInfo.collider != null && hitInfo.collider.transform.parent != null)
            {
                Debug.Log(hitInfo.collider.gameObject.name  + " bellow: " + hitInfo.collider.transform.parent.name + " is not interactable");
            }
        }
    }

    private void OnDestroy()
    {
        _inputDirector.OnInteractPressed -= OnPressedInteract;
    }

    // Update is called once per frame
    void Update()
    {
        Ray r = new(InteractorSource.position, _camTransform.forward);
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange, _interactionMask))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out Interactable interactObj))
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
}
