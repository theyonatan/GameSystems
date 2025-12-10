using UnityEngine;

public abstract class SegmentStreamingAlgorithm : MonoBehaviour
{
    public virtual Segment GenerateSegment() { return null; }
}