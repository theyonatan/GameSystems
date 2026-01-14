using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DecalColliderSync : MonoBehaviour
{
    public BoxCollider TargetCollider; // assign your child BoxCollider in the Inspector
    public Vector3 Padding = new Vector3(0.001f, 0.001f, 0.001f); // tiny pad to avoid clipping

    private void Reset()
    {
        if (TargetCollider == null) TargetCollider = GetComponentInChildren<BoxCollider>();
    }

    private void LateUpdate()
    {
        var projector = GetComponent<DecalProjector>();
        if (!projector || !TargetCollider) return;

        // URP DecalProjector exposes size (Width, Height, Depth) as a Vector3
        Vector3 size = projector.size + Padding;
        TargetCollider.size = size;

        // Keep the collider centered on the projector pivot
        // Child collider Transform should be localPosition = projector.pivot
        TargetCollider.transform.localPosition = projector.pivot;
        TargetCollider.transform.localRotation = Quaternion.identity;
        TargetCollider.transform.localScale = Vector3.one;
    }
}