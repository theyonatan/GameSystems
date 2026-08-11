using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AboveRoutePiece : MonoBehaviour
{
    [Tooltip("Leave empty to allow this prefab in every biome.")]
    [SerializeField]
    private IslandBiome[] allowedBiomes = Array.Empty<IslandBiome>();

    [Tooltip("Socket components belonging to this prefab. Use Refresh Prefab References after adding/removing socket children.")]
    [SerializeField]
    private IslandSocket[] sockets = Array.Empty<IslandSocket>();

    [Tooltip("Disabled or trigger BoxColliders describing the piece's reserved generation space. These are not gameplay colliders.")]
    [SerializeField]
    private BoxCollider[] placementBounds = Array.Empty<BoxCollider>();

    public IReadOnlyList<IslandBiome> AllowedBiomes => allowedBiomes;
    public IReadOnlyList<IslandSocket> Sockets => sockets;
    public IReadOnlyList<BoxCollider> PlacementBounds => placementBounds;
    public bool HasGeneratedContext { get; private set; }
    public IslandBiome GeneratedBiome { get; private set; }
    public bool IsGeneratedMainIsland { get; private set; }
    public int GeneratedMainIndex { get; private set; }
    public bool IsGeneratedBeacon { get; private set; }
    public bool IsGeneratedClusterPiece { get; private set; }

    public void InitializeGeneratedContext(
        IslandBiome biome,
        bool isMainIsland,
        int mainIndex,
        bool isBeacon,
        bool isClusterPiece = false)
    {
        HasGeneratedContext = true;
        GeneratedBiome = biome;
        IsGeneratedMainIsland = isMainIsland;
        GeneratedMainIndex = mainIndex;
        IsGeneratedBeacon = isBeacon;
        IsGeneratedClusterPiece = isClusterPiece;
    }

    public bool SupportsBiome(IslandBiome biome)
    {
        if (allowedBiomes == null || allowedBiomes.Length == 0)
            return true;

        for (int i = 0; i < allowedBiomes.Length; i++)
        {
            if (allowedBiomes[i] == biome)
                return true;
        }

        return false;
    }

    public int GetSocketCount(SocketUsage usage, SocketRouteUsage routeUsage)
    {
        int count = 0;
        if (sockets == null)
            return count;

        for (int i = 0; i < sockets.Length; i++)
        {
            IslandSocket socket = sockets[i];
            if (socket != null && socket.CanBeUsedAs(usage) && socket.SupportsRoute(routeUsage))
                count++;
        }

        return count;
    }

    public IslandSocket GetSocket(int index)
    {
        if (sockets == null || index < 0 || index >= sockets.Length)
            return null;

        return sockets[index];
    }

    public void CollectSocketIndices(
        SocketUsage usage,
        SocketRouteUsage routeUsage,
        HashSet<int> excludedIndices,
        List<int> results)
    {
        results.Clear();
        if (sockets == null)
            return;

        for (int i = 0; i < sockets.Length; i++)
        {
            if (excludedIndices != null && excludedIndices.Contains(i))
                continue;

            IslandSocket socket = sockets[i];
            if (socket == null)
                continue;

            if (socket.CanBeUsedAs(usage) && socket.SupportsRoute(routeUsage))
                results.Add(i);
        }
    }

    public bool HasUsablePlacementBounds()
    {
        if (placementBounds == null || placementBounds.Length == 0)
            return false;

        for (int i = 0; i < placementBounds.Length; i++)
        {
            if (placementBounds[i] != null)
                return true;
        }

        return false;
    }

    [ContextMenu("Refresh Prefab References")]
    public void RefreshPrefabReferences()
    {
        sockets = GetComponentsInChildren<IslandSocket>(true);
    }

    protected virtual void Reset()
    {
        RefreshPrefabReferences();
    }
}
