/// <summary>A runtime speed factor, applied to the current movement state's authored speed without editing shared data.</summary>
public interface IMovementSpeedModifier
{
    float MovementSpeedMultiplier { get; }
}
