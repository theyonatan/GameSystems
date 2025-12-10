using UnityEngine;

public class ExtensionKnockback : MonoBehaviour, Knockbackable
{
    [SerializeField] private float knockbackBackwardsForce = 12f;
    [SerializeField] private float knockbackUpForce = 9f;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void ApplyKnockback(Vector3 attackingPosition)
    {
        Vector3 pushDirection = (transform.position - attackingPosition).normalized;
        Vector3 pushForce =  pushDirection * knockbackBackwardsForce;
        pushForce.y = knockbackUpForce;
        
        _rb.AddForce(pushForce, ForceMode.Impulse);
    }
}
