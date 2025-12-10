using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class MilestoneUnlockAlgorithm : SegmentStreamingAlgorithm
{
    [SerializeField] private MilestoneSegment[] segments;
    [SerializeField] private int countSpawnedSegments;

    private Segment PickRandom(Segment[] arr) => arr[Random.Range(0, arr.Length)];

    public override Segment GenerateSegment()
    {
        if (segments == null || segments.Length == 0) { return null; }
        
        // find the highest milestone we can create segments from
        MilestoneSegment chosenMilestone = segments[0];
        foreach (var milestone in segments)
        {
            if (countSpawnedSegments > milestone.MilestoneToActivate)
                chosenMilestone = milestone;
        }
        
        Debug.Log(chosenMilestone.Name);
        countSpawnedSegments++;
        return PickRandom(chosenMilestone.AvailableSegments);
    }
}

[Serializable]
public class MilestoneSegment
{
    public string Name;
    public int MilestoneToActivate;
    public Segment[] AvailableSegments;
}