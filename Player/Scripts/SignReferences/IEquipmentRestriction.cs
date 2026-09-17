using UnityEngine;

/// <summary>Optional player-local policy; shared equipment has no dependency on a game's objectives.</summary>
public interface IEquipmentRestriction
{
    bool BlocksEquipment { get; }
}

public static class EquipmentRestrictions
{
    public static bool IsBlocked(Component player)
    {
        if (!player) return false;
        foreach (var restriction in player.GetComponents<IEquipmentRestriction>())
            if (restriction.BlocksEquipment) return true;
        return false;
    }
}
