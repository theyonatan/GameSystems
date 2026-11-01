using UnityEngine;

/// <summary>Optional traversal velocity integrated into the normal collision-aware movement step.</summary>
public interface ICharacterControllerVelocityOverride
{
    bool TryGetTraversalVelocity(float deltaTime, out Vector3 velocity);
    void CancelTraversalVelocity();
}
