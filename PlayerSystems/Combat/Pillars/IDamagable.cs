using UnityEngine;

/// <summary>
/// Interface for anything that can take damage.
/// This helps us avoid direct references to specific "Enemy" scripts.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}
