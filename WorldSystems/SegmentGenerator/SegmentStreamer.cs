using UnityEngine;

[RequireComponent(typeof(SegmentStreamingAlgorithm))]
public class SegmentStreamer : MonoBehaviour
{
    // A simple Singleton approach for easy access
    public static SegmentStreamer Instance;
    private SegmentStreamingAlgorithm _segmentStreamingAlgorithm;

    private void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            Debug.LogWarning($"this Segment Streamer is default: {Instance.name} " +
                             $"used by {name}");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Multiple SegmentStreamers detected. " +
                             $"Notice {Instance.name} is the default Instance. " +
                             $"This one ({name}) will still work if referenced directly.");
        }
        
        _segmentStreamingAlgorithm = GetComponent<SegmentStreamingAlgorithm>();
    }

    /// <summary>
    /// Spawns a random segment at the Exit Point (Connected to the Entry Point)
    /// </summary>
    /// <param name="exitTransform">Old segment's Exit, Where we want to align the new segment's entrance.</param>
    /// <param name="defaultParent">Parent of object to spawn</param>
    public void SpawnRandomSegment(Transform exitTransform, Transform defaultParent=null)
    {
        // Pick one segment prefab randomly
        Segment chosenSegmentPrefab = _segmentStreamingAlgorithm.GenerateSegment();
        if (chosenSegmentPrefab == null)
        {
            Debug.LogError("Error choosing a segment to spawn!\n" +
                           "spawning nothing.");
            return;
        }

        // Instantiate the new segment
        Segment newSegment = Instantiate(chosenSegmentPrefab, defaultParent);

        // Find the 'Entrance' transform inside the new segment (so we can align it correctly)
        Transform entrance = newSegment.EntrancePoint;
        if (entrance != null)
        {
            // Align rotation first
            var deltaRot = Quaternion.Inverse(entrance.rotation) * newSegment.transform.rotation;
            newSegment.transform.rotation = exitTransform.rotation * deltaRot;
            
            // We want the segment's 'Entrance' to match up exactly with the spawnTransform's position
            Vector3 offset = newSegment.transform.position - entrance.position;
            newSegment.transform.position = exitTransform.position + offset;

            // turn off the exit transform
            exitTransform.gameObject.SetActive(false);
            
            newSegment.OnSegmentSpawned(exitTransform.parent);
        }
        else
        {
            Debug.LogWarning("No Entrance transform found in the newly spawned segment. " +
                             "Make sure your segment prefab has a child named 'Entrance'!");
        }
    }
    
    /// <summary>
    /// Spawns a random prefab at any world position WITHOUT Entrance/Exit alignment.
    /// Perfect for side props, obstacles, collectibles on random X/Y at fixed-ahead Z.
    /// </summary>
    public void SpawnRandomFree(Vector3 spawnPosition, Transform parent = null)
    {
        Segment chosenSegmentPrefab = _segmentStreamingAlgorithm.GenerateSegment();
        if (!chosenSegmentPrefab)
        {
            Debug.LogError("Error choosing a segment to spawn!");
            return;
        }

        // Instantiate at exact position + prefab's original rotation
        Segment newSegment = Instantiate(chosenSegmentPrefab, spawnPosition, chosenSegmentPrefab.transform.rotation, parent);
    
        newSegment.OnSegmentSpawned(null); // no previous segment for free spawns
    }
}
