/// <summary>Player actions, such as dropping a carried objective, that take priority over a raycast.</summary>
public interface IPlayerInteractionHandler
{
    bool TryHandleInteraction();
}
