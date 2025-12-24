using UnityEngine;

public interface Knockbackable
{
    public void ApplyKnockback(Vector3 attackingPosition, float horizontalForce=0f, float verticalForce=0f);
}
