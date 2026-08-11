using System;
using System.Collections.Generic;
using UnityEngine;

public enum BackgroundIslandLayer
{
    Near,
    Middle,
    Far
}

[Flags]
public enum BackgroundIslandLayerMask
{
    None = 0,
    Near = 1 << 0,
    Middle = 1 << 1,
    Far = 1 << 2,
    All = Near | Middle | Far
}

public static class BackgroundIslandLayerExtensions
{
    public static BackgroundIslandLayerMask ToMask(this BackgroundIslandLayer layer)
    {
        return (BackgroundIslandLayerMask)(1 << (int)layer);
    }
}

public enum BackgroundIslandSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Hero
}

public enum BackgroundSidePreference
{
    Either,
    Left,
    Right
}

[Serializable]
public sealed class BackgroundIntegrationSettings
{
    [Tooltip("Optional. When empty, the component finds IslandRouteGenerator on a parent.")]
    public IslandRouteGenerator RouteGenerator;

    [Tooltip("Optional parent for the generated root. When empty, this component's transform is used.")]
    public Transform GeneratedParent;

    public bool GenerateWithRoute = true;
    public bool ClearWithRoute = true;

    [Tooltip("Uses the playable route's last seed, combined with Seed Offset.")]
    public bool UseRouteSeed = true;

    public int SeedOffset = 137031;
    public int Seed = 24680;

    [Tooltip("Allow route generation events in Edit Mode to rebuild the background preview.")]
    public bool GenerateEditModePreview = true;
}

[Serializable]
public sealed class BackgroundDistributionSettings
{
    [Tooltip("Inclusive one-based numbered route island where background distribution begins.")]
    [Min(1)]
    public int StartIslandIndex = 1;

    [Tooltip("Inclusive final numbered route island. -1 includes the complete route and Beacon.")]
    [Min(-1)]
    public int EndIslandIndex = -1;

    [Tooltip("Distance to extrapolate scenery before the selected route range.")]
    [Min(0f)]
    public float ExtendBeforeRoute = 30f;

    [Tooltip("Distance to extrapolate scenery beyond the selected route range.")]
    [Min(0f)]
    public float ExtendBeyondRoute = 70f;

    [Tooltip("Minimum horizontal distance from the playable route centerline, before prefab radius is added.")]
    [Min(0f)]
    public float RouteCorridorClearance = 35f;

    [Tooltip("Additional clearance around every playable Placement Bounds volume.")]
    [Min(0f)]
    public float PlayableBoundsClearance = 6f;

    [Tooltip("Additional clearance between background prefab placement radii.")]
    [Min(0f)]
    public float BackgroundBoundsClearance = 5f;

    [Tooltip("Enlarge Small islands only when they remain standalone layer scenery. Scenic-cluster centers, satellites and landmarks are never boosted.")]
    public bool BoostStandaloneSmallIslands = true;

    [Tooltip("Minimum extra scale applied to standalone Small layer islands.")]
    [Min(1f)]
    public float MinimumStandaloneSmallScaleMultiplier = 1.2f;

    [Tooltip("Maximum extra scale applied to standalone Small layer islands.")]
    [Min(1f)]
    public float MaximumStandaloneSmallScaleMultiplier = 1.6f;

    [Tooltip("Longitudinal distribution cell length. Empty cells create deliberate scenic gaps.")]
    [Min(1f)]
    public float CellSize = 75f;

    [Range(0f, 100f)]
    public float LeftSideWeight = 50f;

    [Range(0f, 100f)]
    public float RightSideWeight = 50f;

    [Tooltip("Valid candidates sampled before weighted density-volume selection.")]
    [Min(1)]
    public int CandidatesPerIsland = 10;

    [Tooltip("Maximum random candidate attempts for each requested island.")]
    [Min(1)]
    public int MaximumPlacementAttempts = 50;

    [Tooltip("Distance around a biome boundary where either neighboring biome may be selected.")]
    [Min(0f)]
    public float BiomeTransitionBlendDistance = 25f;
}

[Serializable]
public sealed class BackgroundScenicClusterSettings
{
    public bool Enabled = true;

    [Tooltip("Chance for each eligible already-placed background island to become a cluster center. The Maximum Clusters cap still applies.")]
    [Range(0f, 100f)]
    public float ClusterChance = 25f;

    [Min(1)]
    public int MinimumSatelliteIslands = 1;

    [Min(1)]
    public int MaximumSatelliteIslands = 2;

    [Min(0f)]
    public float MinimumSpreadRadius = 35f;

    [Min(0f)]
    public float MaximumSpreadRadius = 70f;

    [Tooltip("Minimum clear edge-to-edge gap between a center island and its satellites. This intentionally replaces normal layer spacing inside a scenic cluster.")]
    [Min(0f)]
    public float MinimumSurfaceGap = 4f;

    public float MinimumHeightOffset = -18f;
    public float MaximumHeightOffset = 18f;

    [Min(0.01f)]
    public float MinimumScaleMultiplier = 0.6f;

    [Min(0.01f)]
    public float MaximumScaleMultiplier = 0.85f;

    [Tooltip("Largest prefab size allowed for satellites.")]
    public BackgroundIslandSize MaximumSatelliteSize = BackgroundIslandSize.Medium;

    [Tooltip("Cluster members may reuse a prefab even when the global background Repeat Gap would temporarily block it.")]
    public bool IgnoreRepeatGapForSatellites = true;

    [Tooltip("Island-count budget slots kept available while regular layers are planned, so regular single islands cannot consume the entire budget before scenic groups are built. Unused reserved slots may remain empty.")]
    [Min(0)]
    public int ReservedSatelliteSlots = 6;

    [Tooltip("0 means unlimited: every center that passes Cluster Chance may form a group until island/visual budgets are full.")]
    [Min(0)]
    public int MaximumClustersPerRun;

    public bool KeepCenterBiome = true;
}

[Serializable]
public sealed class BackgroundPerformanceSettings
{
    [Min(0)]
    public int MaximumBackgroundIslands = 24;

    [Tooltip("Generation stops when the sum of BackgroundIsland Visual Cost reaches this value. 0 disables the budget.")]
    [Min(0)]
    public int MaximumVisualCost = 60;

    public bool DisableGeneratedColliders = true;
    public bool DisableFarShadows = true;
    public bool WarnWhenFarPrefabHasNoLODGroup = true;
    public bool WarnWhenRequestedCountCannotFit = true;
}

[Serializable]
public sealed class BackgroundLayerSettings
{
    public string Name = "Layer";
    public BackgroundIslandLayer Layer;
    public bool Enabled = true;

    [Min(0)]
    public int MinimumCount = 2;

    [Min(0)]
    public int MaximumCount = 4;

    [Tooltip("Requested islands per 100 world units of selected route length, clamped by Min/Max Count.")]
    [Min(0f)]
    public float DensityPer100Units = 1.5f;

    [Min(0f)]
    public float MinimumLateralDistance = 60f;

    [Min(0f)]
    public float MaximumLateralDistance = 120f;

    public float MinimumHeightOffset = -30f;
    public float MaximumHeightOffset = 40f;

    [Min(0f)]
    public float MinimumSpacing = 45f;

    [Min(0.01f)]
    public float MinimumScaleMultiplier = 0.8f;

    [Min(0.01f)]
    public float MaximumScaleMultiplier = 1f;

    public BackgroundIslandSize MaximumSize = BackgroundIslandSize.Medium;

    [Range(0f, 100f)]
    public float EmptyCellChance = 20f;

    [Min(1)]
    public int MaximumIslandsPerCell = 1;

    public bool CastShadows = true;

    public static BackgroundLayerSettings NearDefaults()
    {
        return new BackgroundLayerSettings
        {
            Name = "Near",
            Layer = BackgroundIslandLayer.Near,
            MinimumCount = 2,
            MaximumCount = 4,
            DensityPer100Units = 1.25f,
            MinimumLateralDistance = 60f,
            MaximumLateralDistance = 115f,
            MinimumHeightOffset = -30f,
            MaximumHeightOffset = 40f,
            MinimumSpacing = 45f,
            MinimumScaleMultiplier = 0.75f,
            MaximumScaleMultiplier = 1f,
            MaximumSize = BackgroundIslandSize.Medium,
            EmptyCellChance = 25f,
            MaximumIslandsPerCell = 1,
            CastShadows = true
        };
    }

    public static BackgroundLayerSettings MiddleDefaults()
    {
        return new BackgroundLayerSettings
        {
            Name = "Middle",
            Layer = BackgroundIslandLayer.Middle,
            MinimumCount = 4,
            MaximumCount = 7,
            DensityPer100Units = 2f,
            MinimumLateralDistance = 120f,
            MaximumLateralDistance = 235f,
            MinimumHeightOffset = -75f,
            MaximumHeightOffset = 90f,
            MinimumSpacing = 70f,
            MinimumScaleMultiplier = 0.7f,
            MaximumScaleMultiplier = 1f,
            MaximumSize = BackgroundIslandSize.Large,
            EmptyCellChance = 20f,
            MaximumIslandsPerCell = 2,
            CastShadows = false
        };
    }

    public static BackgroundLayerSettings FarDefaults()
    {
        return new BackgroundLayerSettings
        {
            Name = "Far",
            Layer = BackgroundIslandLayer.Far,
            MinimumCount = 6,
            MaximumCount = 10,
            DensityPer100Units = 2.75f,
            MinimumLateralDistance = 240f,
            MaximumLateralDistance = 430f,
            MinimumHeightOffset = -150f,
            MaximumHeightOffset = 170f,
            MinimumSpacing = 105f,
            MinimumScaleMultiplier = 0.65f,
            MaximumScaleMultiplier = 1.1f,
            MaximumSize = BackgroundIslandSize.Large,
            EmptyCellChance = 15f,
            MaximumIslandsPerCell = 2,
            CastShadows = false
        };
    }
}

[Serializable]
public sealed class BackgroundIslandPoolEntry
{
    public BackgroundIsland Prefab;

    [Tooltip("Relative selection chance among background prefabs that support the requested biome, layer and size. This is spawn chance—not Visual Cost.")]
    [Range(0f, 100f)]
    public float ChancePercent = 100f;

    [Min(0)]
    public int MinimumRepeatGap;

    [Tooltip("-1 means unlimited.")]
    [Min(-1)]
    public int MaximumPerRun = -1;
}

[Serializable]
public sealed class BackgroundLandmarkRule
{
    public bool Enabled = true;
    public BackgroundIsland Prefab;

    [Range(0f, 100f)]
    public float ChancePercent = 100f;

    [Min(1)]
    public int MinimumRouteIndex = 1;

    [Tooltip("Inclusive one-based maximum. -1 uses the final selected route island.")]
    [Min(-1)]
    public int MaximumRouteIndex = -1;

    public BackgroundIslandLayer Layer = BackgroundIslandLayer.Far;
    public BackgroundSidePreference Side = BackgroundSidePreference.Either;

    [Tooltip("When enabled, lateral distance comes from the matching layer settings.")]
    public bool UseLayerDistance = true;

    [Min(0f)]
    public float MinimumLateralDistance = 180f;

    [Min(0f)]
    public float MaximumLateralDistance = 320f;

    public float MinimumHeightOffset = -80f;
    public float MaximumHeightOffset = 100f;

    [Min(0.01f)]
    public float MinimumScaleMultiplier = 0.85f;

    [Min(0.01f)]
    public float MaximumScaleMultiplier = 1.15f;

    [Min(1)]
    public int MaximumPerRun = 1;
}
