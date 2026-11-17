/// <summary>Optional held-item modifiers. Inactive providers leave normal jump/gravity untouched.</summary>
public interface ICharacterGravityModifier
{
    bool TryGetGravityModifiers(out float gravityScale, out float jumpVelocityScale, out float maximumFallSpeed);
}
