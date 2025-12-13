using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface Interactable
{
    void Interact();
    void MarkAsInteractable();
    void StopMarking()
    {
        Debug.Log("Stop marking interactable");
    }
}
