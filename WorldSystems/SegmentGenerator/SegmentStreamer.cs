using UnityEngine;

[RequireComponent(typeof(SegmentStreamingAlgorithm))]
public class SegmentStreamer : MonoBehaviour
{
    // A simple Singleton approach for easy access
    public static SegmentStreamer Instance;
    private SegmentStreamingAlgorithm _segmentStreamingAlgorithm;

    private void Awake()
    {
        // Basic singleton check
        if (Instance == null)
        {
            Instance = this;
            _segmentStreamingAlgorithm = GetComponent<SegmentStreamingAlgorithm>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Spawns a random segment at the Exit Point (Connected to the Entry Point)
    /// </summary>
    /// <param name="exitTransform">Old segment's Exit, Where we want to align the new segment's entrance.</param>
    public void SpawnRandomSegment(Transform exitTransform)
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
        Segment newSegment = Instantiate(chosenSegmentPrefab);

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
}
