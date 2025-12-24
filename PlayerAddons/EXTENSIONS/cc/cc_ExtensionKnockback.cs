using UnityEngine;

public class cc_ExtensionKnockback : MonoBehaviour, Knockbackable
{
    [Header("Forces")]
    [SerializeField] private float knockbackBackwardsForce = 12f;
    [SerializeField] private float knockbackUpForce = 6f;
    [SerializeField] private float decay = 18f;

    private CharacterController cc;
    private Vector3 externalVelocity;
    private MovementManager _movementManager;
    
    void Awake()
    {
        cc = GetComponent<CharacterController>();
        _movementManager = GetComponent<MovementManager>();
    }

    void Update()
    {
        if (externalVelocity.sqrMagnitude < 0.001f)
            return;

        if (!cc)
            cc = GetComponent<CharacterController>();
        
        // Apply injected motion (same way cc_fpState does)
        cc.Move(externalVelocity * Time.deltaTime);

        // Smooth decay
        externalVelocity = Vector3.Lerp(
            externalVelocity,
            Vector3.zero,
            decay * Time.deltaTime
        );
    }

    public void ApplyKnockback(Vector3 attackingPosition, float horizontalForce=0f, float verticalForce=0f)
    {
        // Direction away from attacker
        Vector3 dir = (transform.position - attackingPosition);
        dir.y = 0f;
        dir.Normalize();

        // Bias backward more than upward
        Vector3 knockback =
            dir * horizontalForce;

        externalVelocity = knockback;
        
        // Vertical impulse goes to the movement authority
        var currentState = _movementManager.GetCurrentState();
        if (currentState is cc_fpState fpsPlayer)
        {
            fpsPlayer.AddVerticalVelocity(verticalForce);
        }
        else if (currentState is cc_tpState tpsPlayer)
        {
            tpsPlayer.AddVerticalVelocity(verticalForce);
        }

        Debug.Log("Took Knockback!");
    }
}