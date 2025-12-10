using UnityEngine;
using System.Collections;

/// <summary>
/// A simple, stationary enemy that takes damage and dies when health is depleted.
/// Implements IDamageable for compatibility with the gun system.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PuppetEnemy : MonoBehaviour, IDamageable
{
    [Header("Puppet Enemy Settings")]
    [SerializeField] private float maxHealth = 100f;
    private Rigidbody rb;

    public float knockbackForce = 10f;
    public float stillThreshold = 0.2f;
    private float currentHealth;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Initialize health
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Applies damage to the enemy. If health is zero or below, the enemy dies.
    /// </summary>
    /// <param name="damage">Amount of damage to apply.</param>
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
    }

    /// <summary>
    /// Handles the enemy's death.
    /// Rewards the player and destroys this GameObject.
    /// </summary>
    private void Die()
    {
        // Destroy the enemy GameObject
        Destroy(gameObject);
    }

    // <summary>
    // take knockback
    // <summary>
    private IEnumerator ApplyKnockback(Vector3 knockback)
    {
        // disable rigidbody while knockback
        rb.isKinematic = false;
        rb.useGravity = true;

        // Add knockback force
        rb.AddForce(knockback * knockbackForce, ForceMode.Impulse);

        // Wait until velocity is minimal
        yield return new WaitForFixedUpdate();
        yield return new WaitUntil(() => rb.linearVelocity.magnitude < stillThreshold);

        // Re-enable rigidbody
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
