/// <summary>Optional owner air-jump capability; the movement state owns input and gravity.</summary>
public interface ICharacterAirJump
{
    void UpdateAirJumpState(bool grounded, bool canMove);
    bool TryAirJump(float downwardAcceleration, out float launchVelocity);
    void CancelAirJump();
}
