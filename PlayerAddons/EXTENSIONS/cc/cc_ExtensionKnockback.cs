using UnityEngine;

public class cc_ExtensionKnockback : MonoBehaviour, Knockbackable, IPlayerBehavior
{
    [Header("Impact")]
    [SerializeField] private float mass = 3f;
    [SerializeField] private float impactDecay = 5f;
    [SerializeField] private float minImpactMagnitude = 0.2f;

    private CharacterController cc;
    private Vector3 impact = Vector3.zero;
    
    public void AwakePlayer()
    {
        cc = GetComponent<CharacterController>();
    }

    public void UpdatePlayer()
    {
        if (!cc)
            cc = GetComponent<CharacterController>();
        if (!cc) return;
        
        // Apply injected motion (same way cc_fpState does)
        if (impact.magnitude > minImpactMagnitude)
            cc.Move(impact * Time.deltaTime);

        // Smooth decay
        impact = Vector3.Lerp(
            impact,
            Vector3.zero,
            impactDecay * Time.deltaTime
        );
    }

    /// <summary>
    /// Launch up works with any CC
    /// </summary>
    /// <param name="attackingPosition">Where the attacker is, inverse that is the direction of the knockback.</param>
    /// <param name="kncokbackForce">how strong up to push notice the mass I recommend using 3 with Decay 5</param>
    /// <param name="launchAngle">angle from front (0) to upward straight (1) I think front is 0 but not 100%</param>
    public void ApplyKnockback(Vector3 attackingPosition, float kncokbackForce = 0f, float launchAngle = 0f)
    {
        // Direction away from attacker
        Vector3 dir = transform.position - attackingPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = -transform.forward;
        else
            dir.Normalize();

        dir.y = launchAngle;

        AddImpact(dir, kncokbackForce);

        Debug.Log("Took Knockback!");
    }
    
    private void AddImpact(Vector3 dir, float force)
    {
        dir.Normalize();

        if (dir.y < 0)
            dir.y = -dir.y;

        impact += dir * force / mass;
    }
}