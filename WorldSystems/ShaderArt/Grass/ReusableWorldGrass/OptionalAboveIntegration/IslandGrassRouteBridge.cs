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

    [Tooltip("Keep the cached grass ground map focused on the playable route instead of distant background islands. Texture resolution and refresh frequency are unchanged.")]
    [SerializeField] private bool focusGroundBlendOnRoute = true;

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
        routeGenerator.RouteCleared += ClearGroundCaptureRoot;
        RefreshGroundCaptureRoot();
    }

    private void OnDisable()
    {
        ClearGroundCaptureRoot();
        if (routeGenerator == null)
            return;

        routeGenerator.PieceInstantiated -= ApplyPreset;
        routeGenerator.RouteGenerated -= HandleRouteGenerated;
        routeGenerator.RouteCleared -= ClearGroundCaptureRoot;
    }

    private void ApplyPreset(AboveRoutePiece piece)
    {
        if (piece == null || !piece.HasGeneratedContext)
            return;

        // Also called for pieces received by network clients.
        // Network refresh also visits inactive pieces from the old route while
        // their destruction is deferred; they must not replace the live root.
        if (focusGroundBlendOnRoute && piece.gameObject.activeInHierarchy && piece.transform.parent != null)
            WorldGrassGroundBlend.RegisterCaptureRoot(this, piece.transform.parent);

        SO_GrassSettings preset = FindPreset(piece.GeneratedBiome);
        if (preset == null)
            return;

        GrassSource[] sources = piece.GetComponentsInChildren<GrassSource>(true);
        for (int i = 0; i < sources.Length; i++)
            sources[i].SetRuntimePreset(preset);
    }

    private void HandleRouteGenerated()
    {
        RefreshGroundCaptureRoot();
        if (worldGrassManager != null)
            worldGrassManager.RequestRebuild();
        else
            WorldGrassManager.NotifySourcesChanged();
    }

    private void RefreshGroundCaptureRoot()
    {
        if (!focusGroundBlendOnRoute) { ClearGroundCaptureRoot(); return; }
        // Match the generator's existing root recovery after an Editor reload.
        var parent = routeGenerator.Generation.GeneratedParent != null
            ? routeGenerator.Generation.GeneratedParent : routeGenerator.transform;
        // ClearGenerated disables the previous root before its deferred Destroy.
        // Transform.Find could return that old, identically named root this frame.
        foreach (Transform child in parent)
        {
            if (child.name != "[Generated Above Island Route]" || !child.gameObject.activeSelf)
                continue;
            WorldGrassGroundBlend.RegisterCaptureRoot(this, child);
            return;
        }
        ClearGroundCaptureRoot();
    }

    private void ClearGroundCaptureRoot() => WorldGrassGroundBlend.UnregisterCaptureRoot(this);
    
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
