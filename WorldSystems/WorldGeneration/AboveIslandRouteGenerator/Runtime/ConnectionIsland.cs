using UnityEngine;

public sealed class ConnectionIsland : AboveRoutePiece
{
    [SerializeField]
    private IslandConnectionType connectionType = IslandConnectionType.Normal;

    public IslandConnectionType ConnectionType => connectionType;
}
