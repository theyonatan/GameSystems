using UnityEngine;

public sealed class ConnectionIsland : AboveRoutePiece
{
    [SerializeField]
    private IslandConnectionType connectionType = IslandConnectionType.Normal;

    [SerializeField, HideInInspector] private bool authoredOnly;
    public bool AuthoredOnly => authoredOnly;

    public IslandConnectionType ConnectionType => connectionType;
}
