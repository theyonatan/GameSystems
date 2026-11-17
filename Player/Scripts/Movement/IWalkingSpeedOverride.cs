/// <summary>Optional walk-relative speed override, independent of the sprint base speed.</summary>
public interface IWalkingSpeedOverride
{
    bool TryGetWalkingSpeedOverride(float walkingSpeed, out float speed);
}
