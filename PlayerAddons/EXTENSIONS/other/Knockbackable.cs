using UnityEngine;

public interface Knockbackable
{
    public void ApplyKnockback(Vector3 attackingPosition, float kncokbackForce=0f, float launchAngle=0f);
}
