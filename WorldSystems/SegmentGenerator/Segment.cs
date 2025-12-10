using UnityEngine;

public class Segment : MonoBehaviour
{
    public Transform EntrancePoint;
    public Transform ExitPoint;

    public virtual void OnSegmentSpawned(Transform previousSegment)
    {
        
    }
}
