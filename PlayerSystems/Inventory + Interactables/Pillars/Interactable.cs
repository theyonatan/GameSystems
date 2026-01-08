using UnityEngine;

public interface Interactable
{
    public string InteractableType { get; }
    
    void Interact();
    void MarkAsInteractable();
    void StopMarking()
    {
        Debug.Log("Stop marking interactable");
    }
}
