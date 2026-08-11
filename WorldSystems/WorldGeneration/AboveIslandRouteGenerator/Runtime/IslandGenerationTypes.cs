using System;
using UnityEngine;

public enum IslandBiome
{
    Grass,
    GoldenTrees
}

[Flags]
public enum SocketUsage
{
    None = 0,
    Entry = 1 << 0,
    Exit = 1 << 1,
    Both = Entry | Exit
}

[Flags]
public enum SocketRouteUsage
{
    None = 0,
    MainRoute = 1 << 0,
    Detour = 1 << 1,
    Both = MainRoute | Detour
}

public enum IslandSize
{
    Small,
    Medium
}

public enum IslandRole
{
    Regular,
    Junction,
    DetourEndpoint
}

[Flags]
public enum IslandPhaseUsage
{
    None = 0,
    Linear = 1 << 0,
    Cluster = 1 << 1,
    Both = Linear | Cluster
}

public enum ClusterTopology
{
    Hub,
    Diamond,
    Ring,
    Braided
}

public enum IslandConnectionType
{
    Normal,
    LaunchPad,
    DropDown,
    Zipline
}

[Flags]
public enum IslandConnectionMask
{
    None = 0,
    Normal = 1 << 0,
    LaunchPad = 1 << 1,
    DropDown = 1 << 2,
    Zipline = 1 << 3,
    All = Normal | LaunchPad | DropDown | Zipline
}

public static class IslandConnectionTypeExtensions
{
    public static IslandConnectionMask ToMask(this IslandConnectionType type)
    {
        return (IslandConnectionMask)(1 << (int)type);
    }
}

[Serializable]
public sealed class BiomePhase
{
    public IslandBiome Biome;

    [Min(1)]
    public int MinimumIslands = 10;

    [Min(1)]
    public int MaximumIslands = 12;
}

[Serializable]
public sealed class ClusterTopologyWeights
{
    [Tooltip("Relative weight, not a percentage. 1/1/1/1 gives every topology a 25% share; 0 disables a topology.")]
    [Range(0f, 100f)]
    public float Hub = 35f;

    [Tooltip("Relative weight, not a percentage. Diamond prefers a branch that reconnects toward the spine.")]
    [Range(0f, 100f)]
    public float Diamond = 25f;

    [Tooltip("Relative weight, not a percentage. Ring prefers closure opportunities, but never requires an impossible physical loop.")]
    [Range(0f, 100f)]
    public float Ring = 15f;

    [Tooltip("Relative weight, not a percentage. Braided spreads branches across multiple spine anchors.")]
    [Range(0f, 100f)]
    public float Braided = 25f;
}

[Serializable]
public sealed class ClusterPhaseRule
{
    public bool Enabled = true;

    [Tooltip("Biome used by the inserted playable island group.")]
    public IslandBiome Biome;

    [Tooltip("Rolled independently for each allowed occurrence.")]
    [Range(0f, 100f)]
    public float ChancePercent = 35f;

    [Tooltip("Inclusive, one-based main-route index where the cluster spine may begin.")]
    [Min(1)]
    public int MinimumStartIndex = 4;

    [Tooltip("Inclusive maximum start index. -1 means any available index before the final base-route island.")]
    [Min(-1)]
    public int MaximumStartIndex = -1;

    [Tooltip("Maximum times this rule may be inserted. Chance is rolled once per possible occurrence.")]
    [Min(1)]
    public int MaximumOccurrencesPerRun = 1;

    [Tooltip("Number of numbered main-route islands inserted by this cluster. These islands advance special-island and Beacon timing.")]
    [Min(1)]
    public int MinimumSpineIslands = 3;

    [Min(1)]
    public int MaximumSpineIslands = 5;

    [Tooltip("Optional exact island forced into the middle of every generated cluster spine.")]
    public AboveIsland CenterpiecePrefab;

    [Tooltip("Playable side islands added around the spine. They do not advance main-route numbering or rhythm.")]
    [Min(0)]
    public int MinimumAdditionalIslands = 1;

    [Min(0)]
    public int MaximumAdditionalIslands = 3;

    [Tooltip("Chance that an additional cluster island is Medium instead of Small.")]
    [Range(0f, 100f)]
    public float MediumAdditionalIslandChance = 15f;

    [Tooltip("If the rolled Small/Medium side-island size cannot be placed, retry the other size before rejecting the cluster attempt.")]
    public bool AllowAdditionalSizeFallback = true;

    [Tooltip("Chance that the final additional island uses a Detour Endpoint prefab, suitable for a reward/challenge dead end.")]
    [Range(0f, 100f)]
    public float RewardEndpointChance = 25f;

    [Tooltip("Chance per eligible opportunity to attempt an extra connection between already placed cluster islands.")]
    [Range(0f, 100f)]
    public float ExtraLinkChance = 35f;

    [Min(0)]
    public int MaximumExtraLinks = 2;

    [Tooltip("Maximum full side-to-side width of additional islands around the cluster spine.")]
    [Min(1f)]
    public float MaximumWidth = 80f;

    [Tooltip("Maximum full vertical range of additional islands around the average spine height.")]
    [Min(1f)]
    public float MaximumHeightRange = 30f;

    [Tooltip("Maximum turn away from the selected branch socket while building an additional-island chain.")]
    [Range(1f, 179f)]
    public float MaximumBranchHeadingAngle = 105f;

    [Tooltip("Cluster-only retries before the complete route attempt is rejected.")]
    [Min(1)]
    public int MaximumClusterAttempts = 12;

    [Tooltip("Maximum distance between a closing connection's exit and the target island socket.")]
    [Min(0.01f)]
    public float ExtraLinkPositionTolerance = 2.5f;

    [Tooltip("Maximum rotation difference between a closing connection's exit and the target island socket.")]
    [Range(0.1f, 90f)]
    public float ExtraLinkAngleTolerance = 15f;

    public ClusterTopologyWeights TopologyWeights = new ClusterTopologyWeights();
}

[Serializable]
public sealed class IslandBiomeChance
{
    public IslandBiome Biome;

    [Tooltip("Selection percentage inside this biome and island-size pool. Eligible values are normalized at generation time.")]
    [Range(0f, 100f)]
    public float ChancePercent = 100f;
}

[Serializable]
public sealed class IslandPoolEntry
{
    public AboveIsland Prefab;

    [Tooltip("Legacy/default chance used until a biome-specific value exists. 1.5 displays and edits the biome-specific values in organized pools.")]
    [Range(0f, 100f)]
    public float ChancePercent = 100f;

    [Tooltip("Optional per-biome chances. This lets a shared island have different odds in Grass and Golden Trees while each biome/size pool totals 100%.")]
    public System.Collections.Generic.List<IslandBiomeChance> BiomeChances =
        new System.Collections.Generic.List<IslandBiomeChance>();

    [Tooltip("How many other main/detour islands must appear before this prefab may repeat. 0 disables the restriction.")]
    [Min(0)]
    public int MinimumRepeatGap;

    [Tooltip("-1 means unlimited.")]
    [Min(-1)]
    public int MaximumPerRun = -1;

    public float GetChancePercent(IslandBiome biome)
    {
        if (BiomeChances != null)
        {
            for (int i = 0; i < BiomeChances.Count; i++)
            {
                IslandBiomeChance value = BiomeChances[i];
                if (value != null && value.Biome == biome)
                    return Mathf.Clamp(value.ChancePercent, 0f, 100f);
            }
        }

        return Mathf.Clamp(ChancePercent, 0f, 100f);
    }

    public bool HasPositiveChanceForAnyBiome()
    {
        if (BiomeChances == null || BiomeChances.Count == 0)
            return ChancePercent > 0f;

        for (int i = 0; i < BiomeChances.Count; i++)
        {
            if (BiomeChances[i] != null && BiomeChances[i].ChancePercent > 0f)
                return true;
        }

        return false;
    }
}

[Serializable]
public sealed class ConnectionPoolEntry
{
    public ConnectionIsland Prefab;

    [Range(0f, 100f)]
    public float ChancePercent = 100f;

    [Tooltip("How many other connections must appear before this prefab may repeat. 0 disables the restriction.")]
    [Min(0)]
    public int MinimumRepeatGap;

    [Tooltip("-1 means unlimited.")]
    [Min(-1)]
    public int MaximumPerRun = -1;
}

[Serializable]
public sealed class SpecialIslandRule
{
    public AboveIsland IslandPrefab;

    [Tooltip("Inclusive, one-based main-route island number.")]
    [Min(1)]
    public int MinimumIndex = 1;

    [Tooltip("Inclusive, one-based main-route island number.")]
    [Min(1)]
    public int MaximumIndex = 1;
}

[Serializable]
public sealed class IslandGenerationSettings
{
    [Tooltip("The first island's entry socket is aligned to this transform. Its forward direction defines the overall route direction.")]
    public Transform RouteStart;

    [Tooltip("Generated objects are placed beneath a new child of this transform. If empty, the generator transform is used.")]
    public Transform GeneratedParent;

    public bool GenerateOnStart;
    public bool UseRandomSeed = true;
    public int Seed = 12345;

    [Min(1)]
    public int MaximumRouteAttempts = 30;

    [Tooltip("Generated after every biome phase. It is not included in the numbered island count.")]
    public AboveIsland BeaconIslandPrefab;

    [Tooltip("Optional exact connection used before the Beacon. Leave empty to use the normal connection table.")]
    public ConnectionIsland BeaconConnectionOverride;
}

[Serializable]
public sealed class IslandRhythmSettings
{
    public bool StartWithMediumIsland = true;

    [Tooltip("When enabled, every new biome phase begins with a medium island unless a special island overrides that slot.")]
    public bool RestartRhythmAtBiomeChange = true;

    [Min(0)]
    public int MinimumSmallIslandsAfterMedium = 2;

    [Min(0)]
    public int MaximumSmallIslandsAfterMedium = 3;

    [Tooltip("Chance for each optional small island above the minimum. With a 2-3 range, this is the chance of getting the third island.")]
    [Range(0f, 100f)]
    public float AdditionalSmallIslandChance = 15f;
}

[Serializable]
public sealed class IslandRouteShapeSettings
{
    [Tooltip("Maximum horizontal angle between a usable outgoing socket and the overall route direction.")]
    [Range(1f, 179f)]
    public float MaximumHeadingAngle = 55f;

    [Tooltip("Maximum sideways distance from the route's starting forward line.")]
    [Min(0f)]
    public float MaximumLateralDrift = 120f;

    [Tooltip("Required forward progress between logical islands. Set to 0 to allow fully sideways corrections.")]
    [Min(0f)]
    public float MinimumForwardProgressPerIsland = 1f;

    [Tooltip("Minimum generated height relative to Route Start.")]
    public float MinimumRelativeHeight = -60f;

    [Tooltip("Maximum generated height relative to Route Start.")]
    public float MaximumRelativeHeight = 60f;

    [Tooltip("Extra X/Z clearance added around every Placement Bounds box.")]
    [Min(0f)]
    public float HorizontalPlacementClearance = 2f;

    [Tooltip("Extra vertical clearance added around every Placement Bounds box.")]
    [Min(0f)]
    public float VerticalPlacementClearance = 1f;

    [Min(0f)]
    public float HeadingScoreWeight = 1f;

    [Min(0f)]
    public float LateralScoreWeight = 1.5f;

    [Min(0f)]
    public float HeightScoreWeight = 0.25f;

    [Tooltip("Only candidates this close to the best spatial score participate in percentage selection. Raise for more randomness; lower for straighter correction.")]
    [Min(0f)]
    public float CandidateScoreTolerance = 0.45f;
}

[Serializable]
public sealed class IslandDetourSettings
{
    public bool EnableDetours = true;

    [Range(0f, 100f)]
    public float JunctionDetourChance = 100f;

    [Min(0)]
    public int MaximumDetoursPerRun = 2;

    [Tooltip("Includes the final Detour Endpoint island.")]
    [Min(1)]
    public int MinimumIslands = 1;

    [Tooltip("Includes the final Detour Endpoint island.")]
    [Min(1)]
    public int MaximumIslands = 3;

    [Range(1f, 179f)]
    public float MaximumHeadingAngle = 75f;

    [Min(0f)]
    public float MaximumLateralDrift = 80f;
}
