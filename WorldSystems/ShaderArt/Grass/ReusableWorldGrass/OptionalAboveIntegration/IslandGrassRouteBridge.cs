using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class IslandBiomeGrassPreset
{
    public IslandBiome Biome;
    public SO_GrassSettings GrassPreset;
}

[RequireComponent(typeof(IslandRouteGenerator))]
public sealed class IslandGrassRouteBridge : MonoBehaviour
{
    [Tooltip("Optional. The bridge requests one combined rebuild after route generation.")]
    [SerializeField]
    private WorldGrassManager worldGrassManager;

    [Tooltip("This is the only place where island biomes are connected to grass presets.")]
    [SerializeField]
    private List<IslandBiomeGrassPreset> biomePresets =
        new List<IslandBiomeGrassPreset>();

    private IslandRouteGenerator routeGenerator;

    private void Awake()
    {
        routeGenerator = GetComponent<IslandRouteGenerator>();
    }

    private void OnEnable()
    {
        if (routeGenerator == null)
            routeGenerator = GetComponent<IslandRouteGenerator>();

        routeGenerator.PieceInstantiated += ApplyPreset;
        routeGenerator.RouteGenerated += HandleRouteGenerated;
    }

    private void OnDisable()
    {
        if (routeGenerator == null)
            return;

        routeGenerator.PieceInstantiated -= ApplyPreset;
        routeGenerator.RouteGenerated -= HandleRouteGenerated;
    }

    private void ApplyPreset(AboveRoutePiece piece)
    {
        if (piece == null || !piece.HasGeneratedContext)
            return;

        SO_GrassSettings preset = FindPreset(piece.GeneratedBiome);
        if (preset == null)
            return;

        GrassSource[] sources = piece.GetComponentsInChildren<GrassSource>(true);
        for (int i = 0; i < sources.Length; i++)
            sources[i].SetRuntimePreset(preset);
    }

    private void HandleRouteGenerated()
    {
        if (worldGrassManager != null)
            worldGrassManager.RequestRebuild();
        else
            WorldGrassManager.NotifySourcesChanged();
    }
    
    public void ApplyPresetToPiece(AboveRoutePiece piece)
    {
        ApplyPreset(piece);
    }

    private SO_GrassSettings FindPreset(IslandBiome biome)
    {
        for (int i = 0; i < biomePresets.Count; i++)
        {
            IslandBiomeGrassPreset entry = biomePresets[i];
            if (entry != null && entry.Biome == biome)
                return entry.GrassPreset;
        }

        return null;
    }
}
