using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float stillThreshold = 0.2f;
    [SerializeField] private float worth = 30f;

    private float health;
    private Rigidbody rb;
    private NavMeshAgent navMesh;
    private IGoapAgent goapAgent;
    private Slider slider;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        navMesh = GetComponent<NavMeshAgent>();
        goapAgent = GetComponent<IGoapAgent>(); // could be null if not present
        slider = GetComponentInChildren<Slider>();

        slider.maxValue = maxHealth;
        slider.value = maxHealth;
        health = maxHealth;
    }

    private void Update()
    {
        slider.value = health;
    }

    /// <summary>
    /// Implementation of IDamageable interface.
    /// Takes in the damage amount and reduces health.
    /// </summary>
    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0f)
        {
            KillSelf();
        }
    }

    /// <summary>
    /// Called by other things (like the player's fist).
    /// Applies a knockback force to the enemy.
    /// </summary>
    public void FistHit(Vector3 hitPosition)
    {
        Vector3 pushDirection = (transform.position - hitPosition).normalized;
        StartCoroutine(ApplyKnockback(pushDirection));
    }

    private IEnumerator ApplyKnockback(Vector3 knockback)
    {
        // If there's a GOAP agent, disable it while knocked back
        if (goapAgent != null) goapAgent.enabled = false;
        navMesh.enabled = false;
        rb.isKinematic = false;
        rb.useGravity = true;

        // Add knockback force
        rb.AddForce(knockback * knockbackForce, ForceMode.Impulse);

        // Wait until velocity is minimal
        yield return new WaitForFixedUpdate();
        yield return new WaitUntil(() => rb.linearVelocity.magnitude < stillThreshold);

        // Re-enable pathfinding
        navMesh.Warp(transform.position);
        navMesh.enabled = true;
        if (goapAgent != null) goapAgent.enabled = true;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void KillSelf()
    {
        // reward player if you have a StatsSingleton
        StatsSingleton.Instance.IncreamentStat(StatType.Snow, worth);

        // destroy the enemy
        Destroy(gameObject);
    }
}
