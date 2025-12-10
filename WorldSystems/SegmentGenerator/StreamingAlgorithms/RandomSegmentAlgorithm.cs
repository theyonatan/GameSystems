using UnityEngine;

public class RandomSegmentAlgorithm : SegmentStreamingAlgorithm
{
    [SerializeField] private Segment[] segments;
    
    public override Segment GenerateSegment()
    {
        if (segments == null || segments.Length == 0)
            return null;
        
        int randomIndex = Random.Range(0, segments.Length);
        return segments[randomIndex];
    }
}
