using UnityEngine;

/// <summary>
/// registered object in the story, that exists in the scene.
/// </summary>
public class StoryObject : MonoBehaviour
{
    public string Id;

    public Vector3 GetLocation()
    {
        return transform.position;
    }
}
