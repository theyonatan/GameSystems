using FishNet.Object;
using UnityEngine;

public enum GeneratedWorldPieceKind : byte
{
    Route,
    Background
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class GeneratedWorldNetworkPiece : NetworkBehaviour
{
    public bool IsInitialized { get; private set; }
    public int GenerationOrder { get; private set; } = -1;
    public GeneratedWorldPieceKind Kind { get; private set; }

    public IslandBiome Biome { get; private set; }

    public bool IsMainIsland { get; private set; }
    public int MainIslandIndex { get; private set; } = -1;
    public bool IsBeacon { get; private set; }
    public bool IsClusterPiece { get; private set; }

    public BackgroundIslandLayer BackgroundLayer { get; private set; }

    [Server]
    public void ServerInitializeRoute(
        int generationOrder,
        IslandBiome biome,
        bool isMainIsland,
        int mainIslandIndex,
        bool isBeacon,
        bool isClusterPiece)
    {
        ApplyRouteContext(
            generationOrder,
            biome,
            isMainIsland,
            mainIslandIndex,
            isBeacon,
            isClusterPiece);

        ObserversInitializeRoute(
            generationOrder,
            (int)biome,
            isMainIsland,
            mainIslandIndex,
            isBeacon,
            isClusterPiece);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversInitializeRoute(
        int generationOrder,
        int biome,
        bool isMainIsland,
        int mainIslandIndex,
        bool isBeacon,
        bool isClusterPiece)
    {
        ApplyRouteContext(
            generationOrder,
            (IslandBiome)biome,
            isMainIsland,
            mainIslandIndex,
            isBeacon,
            isClusterPiece);
    }

    [Server]
    public void ServerInitializeBackground(
        int generationOrder,
        IslandBiome biome,
        BackgroundIslandLayer layer)
    {
        ApplyBackgroundContext(generationOrder, biome, layer);

        ObserversInitializeBackground(
            generationOrder,
            (int)biome,
            (int)layer);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversInitializeBackground(
        int generationOrder,
        int biome,
        int layer)
    {
        ApplyBackgroundContext(
            generationOrder,
            (IslandBiome)biome,
            (BackgroundIslandLayer)layer);
    }

    private void ApplyRouteContext(
        int generationOrder,
        IslandBiome biome,
        bool isMainIsland,
        int mainIslandIndex,
        bool isBeacon,
        bool isClusterPiece)
    {
        GenerationOrder = generationOrder;
        Kind = GeneratedWorldPieceKind.Route;
        Biome = biome;

        IsMainIsland = isMainIsland;
        MainIslandIndex = mainIslandIndex;
        IsBeacon = isBeacon;
        IsClusterPiece = isClusterPiece;

        AboveRoutePiece routePiece =
            GetComponentInChildren<AboveRoutePiece>(true);

        if (routePiece != null)
        {
            routePiece.InitializeGeneratedContext(
                biome,
                isMainIsland,
                mainIslandIndex,
                isBeacon,
                isClusterPiece);
        }

        IsInitialized = true;
    }

    private void ApplyBackgroundContext(
        int generationOrder,
        IslandBiome biome,
        BackgroundIslandLayer layer)
    {
        GenerationOrder = generationOrder;
        Kind = GeneratedWorldPieceKind.Background;
        Biome = biome;
        BackgroundLayer = layer;

        BackgroundIsland backgroundIsland =
            GetComponentInChildren<BackgroundIsland>(true);

        if (backgroundIsland != null)
            backgroundIsland.InitializeGeneratedContext(biome, layer);

        IsInitialized = true;
    }
}