/// <summary>
/// Optional owner-driven traversal layered onto the normal third-person controller.
/// Returning false preserves ordinary jumping/gravity. External force extensions
/// continue to move the controller independently; implementations must not clear them.
/// </summary>
public interface ICharacterControllerTraversal
{
    bool TryUpdateVerticalVelocity(ref float velocity, bool jumpHeld, bool grounded,
        bool canMove, float deltaTime);
    bool TryGetAirControl(out float speed, out float acceleration);
}
