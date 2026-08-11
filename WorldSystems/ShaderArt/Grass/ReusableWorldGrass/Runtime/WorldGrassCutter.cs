using UnityEngine;

public sealed class WorldGrassCutter : MonoBehaviour
{
    [SerializeField]
    private WorldGrassManager grassManager;

    [Min(0f)]
    [SerializeField]
    private float radius = 1f;

    [SerializeField]
    private bool cutWhileMoving = true;

    private Vector3 previousPosition;

    private void Awake()
    {
        if (grassManager == null)
            grassManager = FindFirstObjectByType<WorldGrassManager>();

        previousPosition = transform.position;
    }

    private void Update()
    {
        if (!cutWhileMoving || grassManager == null || transform.position == previousPosition)
            return;

        grassManager.CutGrass(transform.position, radius);
        previousPosition = transform.position;
    }

    public void CutNow()
    {
        if (grassManager != null)
            grassManager.CutGrass(transform.position, radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
