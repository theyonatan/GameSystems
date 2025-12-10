using UnityEngine;

public class ExtensionInteractier : MonoBehaviour
{
    private InputDirector inputDirector;

    public Transform InteractorSource;
    public float InteractRange;

    // Start is called before the first frame update
    void Start()
    {
        inputDirector = GetComponent<InputDirector>();
        inputDirector.OnInteractPressed += OnPressedInteract;
    }

    void OnPressedInteract()
    {
        Debug.Log("Player Pressed Interact");
        Ray ray = new(InteractorSource.position, InteractorSource.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out Interactable interactObj))
                interactObj.Interact();
            else
            {
                Debug.Log(hitInfo.collider.gameObject.name  + " bellow: " + hitInfo.collider.transform.parent.name + " is not interactable");
            }
        }
    }

    private void OnDestroy()
    {
        inputDirector.OnInteractPressed -= OnPressedInteract;
    }

    // Update is called once per frame
    void Update()
    {
        Ray r = new(InteractorSource.position, InteractorSource.forward);
        if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange))
        {
            if (hitInfo.collider.gameObject.TryGetComponent(out Interactable interactObj))
                interactObj.MarkAsInteractable();
        }
    }
}
