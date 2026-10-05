using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A validated socket layout, independent of random pool ordering or weights.</summary>
[CreateAssetMenu(menuName = "Above/World Generation/Connected Route Layout")]
public sealed class ConnectedRouteLayout : ScriptableObject
{
    public int SourceSeed;
    public List<Piece> Pieces = new List<Piece>();
    public List<IslandRouteGenerator.ConnectedRouteEdge> Edges = new List<IslandRouteGenerator.ConnectedRouteEdge>();

    [Serializable]
    public sealed class Piece
    {
        public AboveRoutePiece Prefab;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
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
