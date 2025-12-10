using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public string CheckpointName;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Transform transformPosition;

    private void Awake()
    {
        position = transform.position;
        transformPosition = transform;
    }

    static Checkpoint GetCheckpointByName(string checkpointName)
    {
        // are there any checkpoints?
        Checkpoint[] checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        if (checkpoints.Length == 0)
        {
            Debug.LogError("No Checkpoints Found!!!");
            return null;
        }

        // get the specific one
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint.CheckpointName == checkpointName)
                return checkpoint;
        }

        Debug.LogError("Checkpoint " + checkpointName + " Doesn't Exist!");
        return null;
    }
}
