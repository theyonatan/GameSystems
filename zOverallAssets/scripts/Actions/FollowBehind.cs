using UnityEngine;

/// <summary>
/// Follows behind another object using a local offset.
/// Rotates to face movement direction while traveling.
/// When close enough to destination, smoothly rotates to match reference rotation.
/// </summary>
public class FollowBehind : MonoBehaviour
{
    public GameObject ObjectToFollow;
    public GameObject Orientation;

    public Vector3 Distance;

    [Range(0.01f, 20f)]
    public float Damping = 5f;

    [Header("Rotation")]
    [Range(0.01f, 20f)]
    public float RotationDamping = 5f;

    [Header("Arrival")]
    public float ArrivalThreshold = 0.05f;
    
    [Header("Float")]
    public float FloatHeight = 0.5f;
    public float FloatSpeed = 1f;
    float _offset;

    void Start()
    {
        _offset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void LateUpdate()
    {
        if (!ObjectToFollow) return;

        // float (move) to reference
        Transform reference = Orientation ? Orientation.transform : ObjectToFollow.transform;
        Vector3 targetPosition = reference.TransformPoint(Distance);

        // float in place
        float floatOffset = Mathf.Sin(Time.time * FloatSpeed + _offset) * FloatHeight;
        targetPosition.y += floatOffset;

        // Smooth position
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Damping * Time.deltaTime
        );

        Vector3 toTarget = targetPosition - transform.position;
        if (toTarget.magnitude > ArrivalThreshold)
        {
            // Still moving → look in movement direction
            Vector3 direction = toTarget.normalized;
            
            // we only care about horizontal magnitude, character might float in place.
            float horizontalMag = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;

            if (horizontalMag > 1f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    lookRotation,
                    RotationDamping * Time.deltaTime
                );
            }
        }
        else
        {
            // Arrived → match reference rotation
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                reference.rotation,
                RotationDamping * Time.deltaTime
            );
        }
    }
}