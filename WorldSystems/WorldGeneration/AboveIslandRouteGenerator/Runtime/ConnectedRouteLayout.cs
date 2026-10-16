using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A validated socket layout, independent of random pool ordering or weights.</summary>
[CreateAssetMenu(menuName = "Above/World Generation/Connected Route Layout")]
public sealed class ConnectedRouteLayout : ScriptableObject
{
    [Tooltip("Manually authored socket layout. Legacy saved webs retain their original validation.")]
    public bool IsAuthoredLayout;
    [Tooltip("Authored Battery layouts retain pickup/gate NavMesh checks. Other modes need their own gameplay validation.")]
    public bool ValidateBatteryNavigation;
    public int SourceSeed;
    public List<Piece> Pieces = new List<Piece>();
    public List<IslandRouteGenerator.ConnectedRouteEdge> Edges = new List<IslandRouteGenerator.ConnectedRouteEdge>();
    public List<SocketLink> SocketLinks = new List<SocketLink>();

    [Serializable]
    public sealed class SocketLink
    {
        public int FromPiece, ToPiece;
        public string FromSocket, ToSocket;
        public float PositionTolerance = .02f, AngleTolerance = .2f;
    }

    [Serializable]
    public sealed class Piece
    {
        public AboveRoutePiece Prefab;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        // Zero keeps the prefab scale for layouts saved before authoring support.
        public Vector3 Scale;
        public string Label;
        public IslandBiome Biome;
        public bool IsMainIsland, IsBeacon, IsClusterPiece;
        public int MainIndex;
        public List<Socket> UsedSockets = new List<Socket>();
    }

    [Serializable]
    public sealed class Socket
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
