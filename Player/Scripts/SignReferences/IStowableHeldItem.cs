/// <summary>
/// Optional temporary stowing for equipment. Network implementations must be called on the server.
/// Stowing remembers the previous item; releasing restores it unless the player is despawning.
/// Explicit Unequip while stowed must cancel restoration.
/// </summary>
public interface IStowableHeldItem : IEquipableHeldItem
{
    void SetStowed(bool stowed, bool restorePrevious = true);
}
