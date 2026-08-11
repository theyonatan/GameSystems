using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public sealed class BackgroundExclusionVolume : MonoBehaviour
{
    [SerializeField]
    private BoxCollider volume;

    public BoxCollider Volume => volume;

    public bool IntersectsSphere(Vector3 worldCenter, float worldRadius)
    {
        if (volume == null || !isActiveAndEnabled)
            return false;

        Vector3 local = volume.transform.InverseTransformPoint(worldCenter) - volume.center;
        Vector3 half = volume.size * 0.5f;
        Vector3 lossy = volume.transform.lossyScale;
        float minimumScale = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
        float localRadius = worldRadius / minimumScale;

        Vector3 closest = new Vector3(
            Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y),
            Mathf.Clamp(local.z, -half.z, half.z));

        return (local - closest).sqrMagnitude <= localRadius * localRadius;
    }

    private void Reset()
    {
        volume = GetComponent<BoxCollider>();
        volume.isTrigger = true;
    }
}
