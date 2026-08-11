using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public sealed class BackgroundDensityVolume : MonoBehaviour
{
    [SerializeField]
    private BoxCollider volume;

    [Tooltip("Candidate preference inside this volume. 0 blocks candidates; 1 is neutral; values above 1 attract scenery.")]
    [Range(0f, 5f)]
    [SerializeField]
    private float densityMultiplier = 1.5f;

    public float DensityMultiplier => densityMultiplier;

    public bool Contains(Vector3 worldPosition)
    {
        if (volume == null || !isActiveAndEnabled)
            return false;

        Vector3 local = volume.transform.InverseTransformPoint(worldPosition) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    private void Reset()
    {
        volume = GetComponent<BoxCollider>();
        volume.isTrigger = true;
    }
}
