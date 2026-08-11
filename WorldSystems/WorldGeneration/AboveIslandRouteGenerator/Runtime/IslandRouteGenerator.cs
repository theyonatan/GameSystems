using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using Random = System.Random;

public sealed class IslandRouteGenerator : MonoBehaviour
{
    [SerializeField]
    private IslandGenerationSettings generation = new IslandGenerationSettings();

    [SerializeField]
    private IslandRhythmSettings rhythm = new IslandRhythmSettings();

    [SerializeField]
    private IslandRouteShapeSettings routeShape = new IslandRouteShapeSettings();

    [SerializeField]
    private IslandDetourSettings detours = new IslandDetourSettings();

    [Tooltip("Editor convenience: when a chance is edited, keep rows above it fixed and proportionally redistribute the remaining percentage across rows below it.")]
    [SerializeField]
    private bool autoEvenChances;

    [SerializeField]
    private List<BiomePhase> biomePhases = new List<BiomePhase>();

    [Tooltip("Optional playable island-group phases inserted into the numbered main route.")]
    [SerializeField]
    private List<ClusterPhaseRule> clusterPhases = new List<ClusterPhaseRule>();

    [SerializeField]
    private List<IslandPoolEntry> islandPrefabs = new List<IslandPoolEntry>();

    [SerializeField]
    private List<ConnectionPoolEntry> connectionPrefabs = new List<ConnectionPoolEntry>();

    [SerializeField]
    private List<SpecialIslandRule> specialIslands = new List<SpecialIslandRule>();

    [Header("Events")]
    [SerializeField]
    private UnityEvent onRouteGenerated = new UnityEvent();

    [SerializeField]
    private UnityEvent onRouteGenerationFailed = new UnityEvent();

    private const string GeneratedRootName = "[Generated Above Island Route]";

    private GameObject generatedRoot;
    private readonly List<AboveRoutePiece> generatedInstances = new List<AboveRoutePiece>();

    public IslandGenerationSettings Generation => generation;
    public IslandRhythmSettings Rhythm => rhythm;
    public IslandRouteShapeSettings RouteShape => routeShape;
    public IslandDetourSettings Detours => detours;
    public bool AutoEvenChances => autoEvenChances;
    public IReadOnlyList<BiomePhase> BiomePhases => biomePhases;
    public IReadOnlyList<ClusterPhaseRule> ClusterPhases => clusterPhases;
    public IReadOnlyList<IslandPoolEntry> IslandPrefabs => islandPrefabs;
    public IReadOnlyList<ConnectionPoolEntry> ConnectionPrefabs => connectionPrefabs;
    public IReadOnlyList<SpecialIslandRule> SpecialIslands => specialIslands;
    public IReadOnlyList<AboveRoutePiece> GeneratedInstances => generatedInstances;
    public int LastUsedSeed { get; private set; }
    public string LastFailureReason { get; private set; }
    public string LastGenerationDiagnostics { get; private set; }

    public event Action<AboveRoutePiece> PieceInstantiated;
    public event Action RouteGenerated;
    public event Action RouteCleared;

    private void Start()
    {
        if (generation.GenerateOnStart)
            GenerateRoute();
    }

    [ContextMenu("Generate Route")]
    public bool GenerateRoute()
    {
        if (!ValidateConfiguration(out string validationReport))
        {
            LastFailureReason = validationReport;
            LastGenerationDiagnostics = validationReport;
            Debug.LogError($"Island route generation configuration is invalid:\n{validationReport}", this);
            onRouteGenerationFailed.Invoke();
            return false;
        }

        int seed = generation.UseRandomSeed ? CreateRandomSeed() : generation.Seed;
        LastUsedSeed = seed;
        Random random = new Random(seed);

        RouteBuildState successfulState = null;
        string lastAttemptError = "No generation attempt ran.";

        for (int attempt = 0; attempt < generation.MaximumRouteAttempts; attempt++)
        {
            if (TryBuildCompleteRoute(random, out RouteBuildState state, out string attemptError))
            {
                successfulState = state;
                break;
            }

            lastAttemptError = attemptError;
        }

        if (successfulState == null)
        {
            LastFailureReason =
                $"Failed after {generation.MaximumRouteAttempts} attempts with seed {seed}. " +
                lastAttemptError;
            LastGenerationDiagnostics = LastFailureReason;
            Debug.LogError(LastFailureReason, this);
            onRouteGenerationFailed.Invoke();
            return false;
        }

        ClearGeneratedRoute();
        InstantiatePlan(successfulState.Pieces);

        LastFailureReason = string.Empty;
        LastGenerationDiagnostics = BuildSuccessfulGenerationReport(successfulState);
        if (successfulState.DetoursAttempted > successfulState.DetoursGenerated)
            Debug.LogWarning(LastGenerationDiagnostics, this);
        onRouteGenerated.Invoke();
        RouteGenerated?.Invoke();
        return true;
    }

    private string BuildSuccessfulGenerationReport(RouteBuildState state)
    {
        int islands = 0;
        int connections = 0;
        int clusterPieces = 0;
        for (int i = 0; i < state.Pieces.Count; i++)
        {
            PlannedPiece piece = state.Pieces[i];
            if (piece.Prefab is AboveIsland)
                islands++;
            else if (piece.Prefab is ConnectionIsland)
                connections++;
            if (piece.IsClusterPiece)
                clusterPieces++;
        }

        StringBuilder report = new StringBuilder();
        report.Append(
            $"Generated route with seed {LastUsedSeed}: {islands} islands, {connections} connections, " +
            $"{clusterPieces} cluster pieces. Detours: {state.DetoursGenerated}/{state.DetoursAttempted} generated.");
        if (state.DetourFailureSummaries.Count > 0)
        {
            report.Append(" Skipped detours: ");
            report.Append(string.Join(" | ", state.DetourFailureSummaries.ToArray()));
        }
        return report.ToString();
    }

    private static string DescribeSlot(PlannedIslandSlot slot)
    {
        if (slot.IsBeacon)
            return "Beacon";
        if (slot.ForcedPrefab != null)
            return $"forced '{slot.ForcedPrefab.name}'";
        return slot.ClusterOccurrenceId >= 0
            ? $"cluster spine, occurrence {slot.ClusterOccurrenceId + 1}"
            : $"linear phase {slot.PhaseIndex + 1}";
    }

    private static string BuildClusterFailureReport(
        Dictionary<ClusterTopology, int> attempts,
        Dictionary<ClusterTopology, string> failures)
    {
        List<string> counts = new List<string>();
        List<string> details = new List<string>();
        foreach (ClusterTopology topology in Enum.GetValues(typeof(ClusterTopology)))
        {
            if (!attempts.TryGetValue(topology, out int count) || count <= 0)
                continue;
            counts.Add($"{topology} x{count}");
            if (failures.TryGetValue(topology, out string reason) && !string.IsNullOrEmpty(reason))
                details.Add($"{topology}: {reason}");
        }

        string result = "Attempted topologies: " + string.Join(", ", counts.ToArray()) + ".";
        if (details.Count > 0)
            result += " Last rejection per topology: " + string.Join(" | ", details.ToArray());
        return result;
    }

    [ContextMenu("Clear Generated Route")]
    public void ClearGeneratedRoute()
    {
        generatedInstances.Clear();
        RouteCleared?.Invoke();

        if (generatedRoot == null)
        {
            Transform parent = generation.GeneratedParent != null
                ? generation.GeneratedParent
                : transform;

            Transform existing = parent.Find(GeneratedRootName);
            if (existing != null)
                generatedRoot = existing.gameObject;
        }

        if (generatedRoot == null)
            return;

        generatedRoot.SetActive(false);

        if (Application.isPlaying)
            Destroy(generatedRoot);
        else
            DestroyImmediate(generatedRoot);

        generatedRoot = null;
    }

    public bool ValidateConfiguration(out string report)
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        if (generation.RouteStart == null)
            errors.Add("Generation / Route Start is not assigned.");

        if (generation.MaximumRouteAttempts < 1)
            errors.Add("Generation / Maximum Route Attempts must be at least 1.");

        if (generation.BeaconIslandPrefab == null)
            errors.Add("Generation / Beacon Island Prefab is not assigned.");
        else
            ValidatePiece(generation.BeaconIslandPrefab, "Beacon Island", errors, warnings);

        if (biomePhases == null || biomePhases.Count == 0)
        {
            errors.Add("At least one Biome Phase is required.");
        }
        else
        {
            for (int i = 0; i < biomePhases.Count; i++)
            {
                BiomePhase phase = biomePhases[i];
                if (phase == null)
                {
                    errors.Add($"Biome Phase {i + 1} is null.");
                    continue;
                }

                if (phase.MinimumIslands < 1)
                    errors.Add($"Biome Phase {i + 1} has a minimum below 1.");

                if (phase.MaximumIslands < phase.MinimumIslands)
                    errors.Add($"Biome Phase {i + 1} has Maximum Islands below Minimum Islands.");
            }

            BiomePhase finalPhase = biomePhases[biomePhases.Count - 1];
            if (finalPhase != null && generation.BeaconIslandPrefab != null &&
                !generation.BeaconIslandPrefab.SupportsBiome(finalPhase.Biome))
            {
                errors.Add(
                    $"Beacon Island '{generation.BeaconIslandPrefab.name}' does not support the final biome " +
                    $"({finalPhase.Biome}). Add that biome to Allowed Biomes or leave the list empty.");
            }

            if (finalPhase != null && generation.BeaconConnectionOverride != null &&
                !generation.BeaconConnectionOverride.SupportsBiome(finalPhase.Biome))
            {
                errors.Add(
                    $"Beacon Connection Override '{generation.BeaconConnectionOverride.name}' does not support " +
                    $"the final biome ({finalPhase.Biome}).");
            }
        }

        if (rhythm.MaximumSmallIslandsAfterMedium < rhythm.MinimumSmallIslandsAfterMedium)
            errors.Add("Rhythm / Maximum Small Islands must be at least the minimum.");

        if (routeShape.MaximumRelativeHeight <= routeShape.MinimumRelativeHeight)
            errors.Add("Route Shape / Maximum Relative Height must be above Minimum Relative Height.");

        ValidateIslandPool(errors, warnings);
        ValidateConnectionPool(errors, warnings);
        ValidateClusterRules(errors, warnings);
        ValidateSpecialRules(errors, warnings);

        if (detours.EnableDetours)
        {
            if (detours.MaximumIslands < detours.MinimumIslands)
                errors.Add("Detours / Maximum Islands must be at least the minimum.");

            bool hasEndpoint = false;
            for (int i = 0; i < islandPrefabs.Count; i++)
            {
                IslandPoolEntry entry = islandPrefabs[i];
                if (entry != null && entry.Prefab != null && entry.Prefab.Role == IslandRole.DetourEndpoint)
                {
                    hasEndpoint = true;
                    break;
                }
            }

            if (!hasEndpoint)
                warnings.Add(
                    "Detours are enabled, but the island table has no Detour Endpoint prefab. " +
                    "1.5 will finish a detour with a normal Small island instead.");
        }

        StringBuilder builder = new StringBuilder();
        if (errors.Count > 0)
        {
            builder.AppendLine("Errors:");
            for (int i = 0; i < errors.Count; i++)
                builder.AppendLine($"- {errors[i]}");
        }

        if (warnings.Count > 0)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.AppendLine("Warnings:");
            for (int i = 0; i < warnings.Count; i++)
                builder.AppendLine($"- {warnings[i]}");
        }

        report = builder.Length == 0 ? "Configuration is valid." : builder.ToString().TrimEnd();
        return errors.Count == 0;
    }

    private void ValidateIslandPool(List<string> errors, List<string> warnings)
    {
        if (islandPrefabs == null || islandPrefabs.Count == 0)
        {
            errors.Add("The Island Prefab table is empty.");
            return;
        }

        HashSet<AboveIsland> seen = new HashSet<AboveIsland>();
        for (int i = 0; i < islandPrefabs.Count; i++)
        {
            IslandPoolEntry entry = islandPrefabs[i];
            if (entry == null || entry.Prefab == null)
            {
                errors.Add($"Island table row {i + 1} has no prefab.");
                continue;
            }

            if (!seen.Add(entry.Prefab))
                errors.Add($"Island prefab '{entry.Prefab.name}' appears more than once in the table.");

            if (!entry.HasPositiveChanceForAnyBiome())
                warnings.Add($"Island prefab '{entry.Prefab.name}' has a 0% chance and can only appear when forced as a special island.");

            if (entry.MaximumPerRun == 0)
                warnings.Add($"Island prefab '{entry.Prefab.name}' has Max / Run set to 0 and is disabled for normal generation. Use -1 for unlimited.");

            ValidatePiece(entry.Prefab, $"Island '{entry.Prefab.name}'", errors, warnings);
        }

        HashSet<IslandBiome> linearBiomes = new HashSet<IslandBiome>();
        for (int i = 0; i < biomePhases.Count; i++)
        {
            BiomePhase phase = biomePhases[i];
            if (phase == null || !linearBiomes.Add(phase.Biome))
                continue;

            ValidateIslandCategoryTotal(
                phase.Biome,
                IslandSize.Small,
                IslandPhaseUsage.Linear,
                errors,
                warnings);
            ValidateIslandCategoryTotal(
                phase.Biome,
                IslandSize.Medium,
                IslandPhaseUsage.Linear,
                errors,
                warnings);
        }

        HashSet<IslandBiome> clusterBiomes = new HashSet<IslandBiome>();
        int clusterRuleCount = clusterPhases != null ? clusterPhases.Count : 0;
        for (int i = 0; i < clusterRuleCount; i++)
        {
            ClusterPhaseRule rule = clusterPhases[i];
            if (rule == null || !rule.Enabled || rule.ChancePercent <= 0f ||
                !clusterBiomes.Add(rule.Biome))
            {
                continue;
            }

            ValidateIslandCategoryTotal(
                rule.Biome,
                IslandSize.Small,
                IslandPhaseUsage.Cluster,
                errors,
                warnings);
            ValidateIslandCategoryTotal(
                rule.Biome,
                IslandSize.Medium,
                IslandPhaseUsage.Cluster,
                errors,
                warnings);
        }
    }

    private void ValidateIslandCategoryTotal(
        IslandBiome biome,
        IslandSize size,
        IslandPhaseUsage phaseUsage,
        List<string> errors,
        List<string> warnings)
    {
        float total = 0f;
        int eligibleCount = 0;

        for (int i = 0; i < islandPrefabs.Count; i++)
        {
            IslandPoolEntry entry = islandPrefabs[i];
            if (entry == null || entry.Prefab == null)
                continue;

            if (!entry.Prefab.SupportsBiome(biome) ||
                entry.Prefab.Size != size ||
                entry.Prefab.Role == IslandRole.DetourEndpoint ||
                entry.GetChancePercent(biome) <= 0f || entry.MaximumPerRun == 0)
                continue;

            total += entry.GetChancePercent(biome);
            if (entry.Prefab.SupportsPhase(phaseUsage))
                eligibleCount++;
        }

        if (eligibleCount == 0)
        {
            errors.Add(
                $"No {size} {phaseUsage} islands support the {biome} biome.");
            return;
        }

        bool biomeHasLinearPhase = false;
        for (int i = 0; i < biomePhases.Count; i++)
        {
            if (biomePhases[i] != null && biomePhases[i].Biome == biome)
            {
                biomeHasLinearPhase = true;
                break;
            }
        }

        if ((phaseUsage == IslandPhaseUsage.Linear || !biomeHasLinearPhase) &&
            Mathf.Abs(total - 100f) > 0.01f)
        {
            warnings.Add(
                $"{biome} / {size} island pool percentages total {total:0.##}% instead of 100%. " +
                "Use Normalize 100% in the 1.5 biome/size pool.");
        }
    }

    private void ValidateConnectionPool(List<string> errors, List<string> warnings)
    {
        if (connectionPrefabs == null || connectionPrefabs.Count == 0)
        {
            errors.Add("The Connection Prefab table is empty.");
            return;
        }

        HashSet<ConnectionIsland> seen = new HashSet<ConnectionIsland>();
        for (int i = 0; i < connectionPrefabs.Count; i++)
        {
            ConnectionPoolEntry entry = connectionPrefabs[i];
            if (entry == null || entry.Prefab == null)
            {
                errors.Add($"Connection table row {i + 1} has no prefab.");
                continue;
            }

            if (!seen.Add(entry.Prefab))
                errors.Add($"Connection prefab '{entry.Prefab.name}' appears more than once in the table.");

            if (entry.ChancePercent <= 0f)
                warnings.Add($"Connection prefab '{entry.Prefab.name}' has a 0% chance.");

            if (entry.MaximumPerRun == 0)
                warnings.Add($"Connection prefab '{entry.Prefab.name}' has Max / Run set to 0 and is disabled. Use -1 for unlimited.");

            ValidatePiece(entry.Prefab, $"Connection '{entry.Prefab.name}'", errors, warnings);
        }

        if (generation.BeaconConnectionOverride != null)
        {
            ValidatePiece(
                generation.BeaconConnectionOverride,
                "Beacon Connection Override",
                errors,
                warnings);
        }

        foreach (IslandBiome biome in GetConfiguredBiomes())
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < connectionPrefabs.Count; i++)
            {
                ConnectionPoolEntry entry = connectionPrefabs[i];
                if (entry == null || entry.Prefab == null ||
                    !entry.Prefab.SupportsBiome(biome) ||
                    entry.ChancePercent <= 0f || entry.MaximumPerRun == 0)
                    continue;

                count++;
                total += entry.ChancePercent;
            }

            if (count == 0)
            {
                errors.Add($"No connection prefabs support the {biome} biome.");
            }
            else if (Mathf.Abs(total - 100f) > 0.01f)
            {
                warnings.Add(
                    $"{biome} connection percentages total {total:0.##}% instead of 100%. " +
                    "They will be normalized while generating.");
            }
        }
    }

    private void ValidateClusterRules(List<string> errors, List<string> warnings)
    {
        if (clusterPhases == null)
            return;

        int maximumBaseRouteLength = 0;
        for (int i = 0; i < biomePhases.Count; i++)
        {
            BiomePhase phase = biomePhases[i];
            if (phase != null)
                maximumBaseRouteLength += Mathf.Max(0, phase.MaximumIslands);
        }

        for (int i = 0; i < clusterPhases.Count; i++)
        {
            ClusterPhaseRule rule = clusterPhases[i];
            if (rule == null)
            {
                errors.Add($"Island Group Phase row {i + 1} is null.");
                continue;
            }

            if (!rule.Enabled)
                continue;

            string label = $"Island Group Phase {i + 1} ({rule.Biome})";

            if (rule.ChancePercent <= 0f)
                warnings.Add($"{label} has a 0% chance and will never be inserted.");

            if (rule.MinimumStartIndex < 1)
                errors.Add($"{label} has a Minimum Start Index below 1.");

            if (rule.MaximumStartIndex != -1 &&
                rule.MaximumStartIndex < rule.MinimumStartIndex)
            {
                errors.Add($"{label} has Maximum Start Index below Minimum Start Index.");
            }

            if (rule.MinimumStartIndex > maximumBaseRouteLength)
            {
                errors.Add(
                    $"{label} starts at {rule.MinimumStartIndex}, but the maximum base route " +
                    $"has {maximumBaseRouteLength} islands.");
            }

            if (rule.MaximumOccurrencesPerRun < 1)
                errors.Add($"{label} must allow at least one occurrence.");

            if (rule.MinimumSpineIslands < 1 ||
                rule.MaximumSpineIslands < rule.MinimumSpineIslands)
            {
                errors.Add($"{label} has an invalid Spine Islands range.");
            }

            if (rule.MinimumAdditionalIslands < 0 ||
                rule.MaximumAdditionalIslands < rule.MinimumAdditionalIslands)
            {
                errors.Add($"{label} has an invalid Additional Islands range.");
            }

            if (rule.MaximumClusterAttempts < 1)
                errors.Add($"{label} must allow at least one cluster attempt.");

            if (rule.MaximumWidth <= 0f || rule.MaximumHeightRange <= 0f)
                errors.Add($"{label} must have positive width and height limits.");

            ClusterTopologyWeights weights = rule.TopologyWeights;
            float topologyTotal = weights == null
                ? 0f
                : weights.Hub + weights.Diamond + weights.Ring + weights.Braided;
            if (topologyTotal <= 0f)
                errors.Add($"{label} has no topology with a positive weight.");

            if (rule.CenterpiecePrefab != null)
            {
                if (!rule.CenterpiecePrefab.SupportsBiome(rule.Biome))
                {
                    errors.Add(
                        $"{label}'s Centerpiece '{rule.CenterpiecePrefab.name}' does not " +
                        $"support the {rule.Biome} biome.");
                }

                ValidatePiece(
                    rule.CenterpiecePrefab,
                    $"{label} Centerpiece '{rule.CenterpiecePrefab.name}'",
                    errors,
                    warnings);
            }

            bool hasBranchAnchor = false;
            bool hasRewardEndpoint = false;
            bool hasAdditionalEntry = false;
            bool hasAdditionalIsland = false;
            for (int prefabIndex = 0; prefabIndex < islandPrefabs.Count; prefabIndex++)
            {
                IslandPoolEntry entry = islandPrefabs[prefabIndex];
                if (entry == null || entry.Prefab == null || entry.GetChancePercent(rule.Biome) <= 0f ||
                    entry.MaximumPerRun == 0 || !entry.Prefab.SupportsBiome(rule.Biome) ||
                    !entry.Prefab.SupportsPhase(IslandPhaseUsage.Cluster))
                {
                    continue;
                }

                if (entry.Prefab.Role == IslandRole.DetourEndpoint)
                {
                    if (HasRouteEntry(entry.Prefab, SocketRouteUsage.Detour))
                        hasRewardEndpoint = true;
                    continue;
                }

                if (HasClusterBranchSocketLayout(entry.Prefab))
                    hasBranchAnchor = true;

                if (HasRouteEntry(entry.Prefab, SocketRouteUsage.Detour))
                    hasAdditionalEntry = true;

                if (HasDistinctRouteEntryExit(
                        entry.Prefab,
                        SocketRouteUsage.Detour))
                {
                    hasAdditionalIsland = true;
                }
            }

            bool hasClusterConnection = false;
            for (int connectionIndex = 0;
                 connectionIndex < connectionPrefabs.Count;
                 connectionIndex++)
            {
                ConnectionPoolEntry entry = connectionPrefabs[connectionIndex];
                if (entry == null || entry.Prefab == null || entry.ChancePercent <= 0f ||
                    entry.MaximumPerRun == 0 || !entry.Prefab.SupportsBiome(rule.Biome))
                {
                    continue;
                }

                if (HasDistinctRouteEntryExit(
                        entry.Prefab,
                        SocketRouteUsage.Detour))
                {
                    hasClusterConnection = true;
                    break;
                }
            }

            if (rule.MinimumAdditionalIslands > 0 && !hasBranchAnchor &&
                (rule.CenterpiecePrefab == null ||
                 !HasClusterBranchSocketLayout(rule.CenterpiecePrefab)))
            {
                warnings.Add(
                    $"{label} requests side islands, but no eligible cluster island has " +
                    "three distinct usable sockets: a Main Route entry, a different Main " +
                    "Route continuation exit, and a different Detour/Both branch exit.");
            }

            if (rule.MinimumAdditionalIslands > 0 && !hasAdditionalEntry)
            {
                warnings.Add(
                    $"{label} requests side islands, but no eligible regular Cluster island " +
                    "has a Detour/Both entry socket.");
            }

            if (rule.MinimumAdditionalIslands > 1 && !hasAdditionalIsland)
            {
                warnings.Add(
                    $"{label} can request a side-island chain, but no eligible regular Cluster island " +
                    "has distinct Detour/Both entry and exit sockets for an intermediate chain step.");
            }

            if (rule.MinimumAdditionalIslands > 0 && !hasClusterConnection)
            {
                warnings.Add(
                    $"{label} requests side islands, but no eligible connection prefab has " +
                    "distinct Detour/Both entry and exit sockets for this biome.");
            }

            if (rule.RewardEndpointChance > 0f && !hasRewardEndpoint)
            {
                warnings.Add(
                    $"{label} can roll a reward endpoint, but the island table has no " +
                    $"Cluster-compatible Detour Endpoint for {rule.Biome}. It will fall back " +
                    "to a regular side island.");
            }
        }
    }

    private static bool HasClusterBranchSocketLayout(AboveIsland island)
    {
        if (island == null || island.Sockets == null || island.Sockets.Count < 3)
            return false;

        for (int entryIndex = 0; entryIndex < island.Sockets.Count; entryIndex++)
        {
            IslandSocket entry = island.GetSocket(entryIndex);
            if (entry == null || !entry.CanBeUsedAs(SocketUsage.Entry) ||
                !entry.SupportsRoute(SocketRouteUsage.MainRoute))
            {
                continue;
            }

            for (int continuationIndex = 0;
                 continuationIndex < island.Sockets.Count;
                 continuationIndex++)
            {
                if (continuationIndex == entryIndex)
                    continue;

                IslandSocket continuation = island.GetSocket(continuationIndex);
                if (continuation == null ||
                    !continuation.CanBeUsedAs(SocketUsage.Exit) ||
                    !continuation.SupportsRoute(SocketRouteUsage.MainRoute))
                {
                    continue;
                }

                for (int branchIndex = 0;
                     branchIndex < island.Sockets.Count;
                     branchIndex++)
                {
                    if (branchIndex == entryIndex || branchIndex == continuationIndex)
                        continue;

                    IslandSocket branch = island.GetSocket(branchIndex);
                    if (branch != null && branch.CanBeUsedAs(SocketUsage.Exit) &&
                        branch.SupportsRoute(SocketRouteUsage.Detour))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasRouteEntry(
        AboveRoutePiece piece,
        SocketRouteUsage routeUsage)
    {
        if (piece == null || piece.Sockets == null)
            return false;

        for (int i = 0; i < piece.Sockets.Count; i++)
        {
            IslandSocket socket = piece.GetSocket(i);
            if (socket != null && socket.CanBeUsedAs(SocketUsage.Entry) &&
                socket.SupportsRoute(routeUsage))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDistinctRouteEntryExit(
        AboveRoutePiece piece,
        SocketRouteUsage routeUsage)
    {
        if (piece == null || piece.Sockets == null || piece.Sockets.Count < 2)
            return false;

        for (int entryIndex = 0; entryIndex < piece.Sockets.Count; entryIndex++)
        {
            IslandSocket entry = piece.GetSocket(entryIndex);
            if (entry == null || !entry.CanBeUsedAs(SocketUsage.Entry) ||
                !entry.SupportsRoute(routeUsage))
            {
                continue;
            }

            for (int exitIndex = 0; exitIndex < piece.Sockets.Count; exitIndex++)
            {
                if (exitIndex == entryIndex)
                    continue;

                IslandSocket exit = piece.GetSocket(exitIndex);
                if (exit != null && exit.CanBeUsedAs(SocketUsage.Exit) &&
                    exit.SupportsRoute(routeUsage))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ValidateSpecialRules(List<string> errors, List<string> warnings)
    {
        int maximumPossibleRouteLength = 0;
        for (int i = 0; i < biomePhases.Count; i++)
        {
            if (biomePhases[i] != null)
                maximumPossibleRouteLength += Mathf.Max(0, biomePhases[i].MaximumIslands);
        }

        if (clusterPhases != null)
        {
            for (int i = 0; i < clusterPhases.Count; i++)
            {
                ClusterPhaseRule rule = clusterPhases[i];
                if (rule == null || !rule.Enabled || rule.ChancePercent <= 0f)
                    continue;

                maximumPossibleRouteLength +=
                    Mathf.Max(0, rule.MaximumOccurrencesPerRun) *
                    Mathf.Max(0, rule.MaximumSpineIslands);
            }
        }

        for (int i = 0; i < specialIslands.Count; i++)
        {
            SpecialIslandRule rule = specialIslands[i];
            if (rule == null || rule.IslandPrefab == null)
            {
                errors.Add($"Special Island row {i + 1} has no prefab.");
                continue;
            }

            if (rule.MinimumIndex < 1 || rule.MaximumIndex < rule.MinimumIndex)
                errors.Add($"Special Island '{rule.IslandPrefab.name}' has an invalid index range.");

            if (rule.MinimumIndex > maximumPossibleRouteLength)
            {
                errors.Add(
                    $"Special Island '{rule.IslandPrefab.name}' starts at {rule.MinimumIndex}, " +
                    $"but the maximum possible main route has {maximumPossibleRouteLength} islands.");
            }

            if (rule.IslandPrefab.Role == IslandRole.DetourEndpoint)
                warnings.Add($"Special Island '{rule.IslandPrefab.name}' is marked Detour Endpoint but is scheduled on the main route.");

            ValidatePiece(rule.IslandPrefab, $"Special Island '{rule.IslandPrefab.name}'", errors, warnings);
        }
    }

    private HashSet<IslandBiome> GetConfiguredBiomes()
    {
        HashSet<IslandBiome> configured = new HashSet<IslandBiome>();
        if (biomePhases == null)
            return configured;

        for (int i = 0; i < biomePhases.Count; i++)
        {
            if (biomePhases[i] != null)
                configured.Add(biomePhases[i].Biome);
        }

        if (clusterPhases != null)
        {
            for (int i = 0; i < clusterPhases.Count; i++)
            {
                ClusterPhaseRule rule = clusterPhases[i];
                if (rule != null && rule.Enabled && rule.ChancePercent > 0f)
                    configured.Add(rule.Biome);
            }
        }

        return configured;
    }

    private static void ValidatePiece(
        AboveRoutePiece piece,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (piece == null)
            return;

        if (piece.Sockets == null || piece.Sockets.Count == 0)
            errors.Add($"{label} has no sockets.");

        if (piece.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.Both) == 0)
            errors.Add($"{label} has no entry-capable socket.");

        if (piece.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.Both) == 0 &&
            (!(piece is AboveIsland island) || island.Role != IslandRole.DetourEndpoint))
        {
            errors.Add($"{label} has no exit-capable socket.");
        }

        if (!piece.HasUsablePlacementBounds())
            errors.Add($"{label} has no Placement Bounds BoxCollider assigned.");

        Vector3 scale = piece.transform.localScale;
        if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
            warnings.Add($"{label} uses non-uniform root scale. Uniform prefab root scale is recommended.");
    }

    private bool TryBuildCompleteRoute(
        Random random,
        out RouteBuildState state,
        out string error)
    {
        state = new RouteBuildState();
        error = string.Empty;

        if (!TryBuildSlots(random, out List<PlannedIslandSlot> slots, out error))
            return false;

        Vector3 mainForward = FlattenDirection(generation.RouteStart.forward, Vector3.forward);
        RouteFrame mainFrame = new RouteFrame(
            generation.RouteStart.position,
            mainForward,
            generation.RouteStart.position.y,
            routeShape.MaximumHeadingAngle,
            routeShape.MaximumLateralDrift,
            0f);

        PlannedIslandSlot firstSlot = slots[0];
        SocketPose routeStartPose = new SocketPose(
            generation.RouteStart.position,
            generation.RouteStart.rotation);

        PlacementDiagnostics firstDiagnostics = new PlacementDiagnostics();
        if (!TryPlaceFirstIsland(
                state,
                random,
                routeStartPose,
                firstSlot,
                mainFrame,
                firstDiagnostics,
                out PlannedPiece currentIsland))
        {
            error =
                $"Could not place main-route island 1 ({firstSlot.Biome}, {firstSlot.Size}, {DescribeSlot(firstSlot)}). " +
                firstDiagnostics.BuildSummary();
            return false;
        }

        for (int slotIndex = 1; slotIndex < slots.Count; slotIndex++)
        {
            PlannedIslandSlot slot = slots[slotIndex];
            PlacementDiagnostics diagnostics = new PlacementDiagnostics();
            if (!TryPlaceConnectedIsland(
                    state,
                    random,
                    currentIsland,
                    null,
                    slot,
                    SocketRouteUsage.MainRoute,
                    mainFrame,
                    true,
                    null,
                    MainIslandSelection,
                    diagnostics,
                    out PlannedPiece nextIsland))
            {
                error =
                    $"Could not place main-route island {slot.Index} " +
                    $"({slot.Biome}, {slot.Size}, {DescribeSlot(slot)}). " +
                    diagnostics.BuildSummary();
                return false;
            }

            currentIsland = nextIsland;
        }

        PlannedIslandSlot beaconSlot = new PlannedIslandSlot
        {
            Index = slots.Count + 1,
            Biome = slots[slots.Count - 1].Biome,
            Size = generation.BeaconIslandPrefab.Size,
            ForcedPrefab = generation.BeaconIslandPrefab,
            IsBeacon = true,
            CountsAsMainIsland = false
        };

        PlacementDiagnostics beaconDiagnostics = new PlacementDiagnostics();
        if (!TryPlaceConnectedIsland(
                state,
                random,
                currentIsland,
                null,
                beaconSlot,
                SocketRouteUsage.MainRoute,
                mainFrame,
                false,
                generation.BeaconConnectionOverride,
                ExactIslandSelection,
                beaconDiagnostics,
                out _))
        {
            error =
                "Could not place the Beacon island after the main route. " +
                beaconDiagnostics.BuildSummary();
            return false;
        }

        if (!GenerateClusterPhases(state, random, out string clusterError))
        {
            error = clusterError;
            return false;
        }

        if (detours.EnableDetours && detours.MaximumDetoursPerRun > 0)
            GenerateDetours(state, random);

        return true;
    }

    private bool TryBuildSlots(
        Random random,
        out List<PlannedIslandSlot> slots,
        out string error)
    {
        slots = new List<PlannedIslandSlot>();
        error = string.Empty;

        for (int phaseIndex = 0; phaseIndex < biomePhases.Count; phaseIndex++)
        {
            BiomePhase phase = biomePhases[phaseIndex];
            int count = NextInclusive(random, phase.MinimumIslands, phase.MaximumIslands);
            for (int i = 0; i < count; i++)
            {
                slots.Add(new PlannedIslandSlot
                {
                    Index = slots.Count + 1,
                    Biome = phase.Biome,
                    PhaseIndex = phaseIndex,
                    PhaseUsage = IslandPhaseUsage.Linear,
                    CountsAsMainIsland = true
                });
            }
        }

        InsertClusterPhases(random, slots);

        for (int i = 0; i < slots.Count; i++)
            slots[i].Index = i + 1;

        if (!TryResolveSpecialIslands(random, slots, out error))
            return false;

        int smallRemaining = rhythm.StartWithMediumIsland
            ? 0
            : PickSmallRunLength(random);

        int previousPhase = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            PlannedIslandSlot slot = slots[i];
            bool phaseChanged = slot.PhaseIndex != previousPhase;
            if (phaseChanged && rhythm.RestartRhythmAtBiomeChange)
                smallRemaining = 0;

            previousPhase = slot.PhaseIndex;

            if (slot.ForcedPrefab != null)
            {
                slot.Size = slot.ForcedPrefab.Size;
            }
            else
            {
                slot.Size = smallRemaining > 0 ? IslandSize.Small : IslandSize.Medium;
            }

            if (slot.Size == IslandSize.Medium)
            {
                smallRemaining = PickSmallRunLength(random);
            }
            else if (smallRemaining > 0)
            {
                smallRemaining--;
            }
        }

        return true;
    }

    private void InsertClusterPhases(Random random, List<PlannedIslandSlot> slots)
    {
        if (clusterPhases == null || clusterPhases.Count == 0 || slots.Count == 0)
            return;

        int nextPhaseIdentity = biomePhases.Count;
        int nextClusterOccurrenceId = 0;

        for (int ruleIndex = 0; ruleIndex < clusterPhases.Count; ruleIndex++)
        {
            ClusterPhaseRule rule = clusterPhases[ruleIndex];
            if (rule == null || !rule.Enabled || rule.ChancePercent <= 0f)
                continue;

            int occurrenceLimit = Mathf.Max(1, rule.MaximumOccurrencesPerRun);
            for (int occurrence = 0; occurrence < occurrenceLimit; occurrence++)
            {
                if (!RollPercent(random, rule.ChancePercent))
                    continue;

                int minimum = Mathf.Max(1, rule.MinimumStartIndex);
                int configuredMaximum = rule.MaximumStartIndex < 0
                    ? slots.Count
                    : rule.MaximumStartIndex;
                int maximum = Mathf.Min(slots.Count, configuredMaximum);
                if (maximum < minimum)
                    continue;

                List<int> insertionIndices = new List<int>();
                for (int oneBasedIndex = minimum;
                     oneBasedIndex <= maximum;
                     oneBasedIndex++)
                {
                    int zeroBasedIndex = oneBasedIndex - 1;
                    if (slots[zeroBasedIndex].ClusterOccurrenceId >= 0)
                        continue;

                    if (zeroBasedIndex > 0 &&
                        slots[zeroBasedIndex - 1].ClusterOccurrenceId >= 0)
                    {
                        continue;
                    }

                    insertionIndices.Add(zeroBasedIndex);
                }

                if (insertionIndices.Count == 0)
                    continue;

                int insertAt = insertionIndices[random.Next(insertionIndices.Count)];
                int spineCount = NextInclusive(
                    random,
                    Mathf.Max(1, rule.MinimumSpineIslands),
                    Mathf.Max(1, rule.MaximumSpineIslands));
                int phaseIdentity = nextPhaseIdentity++;
                int clusterOccurrenceId = nextClusterOccurrenceId++;

                List<PlannedIslandSlot> inserted = new List<PlannedIslandSlot>(spineCount);
                for (int spineIndex = 0; spineIndex < spineCount; spineIndex++)
                {
                    inserted.Add(new PlannedIslandSlot
                    {
                        Biome = rule.Biome,
                        PhaseIndex = phaseIdentity,
                        PhaseUsage = IslandPhaseUsage.Cluster,
                        CountsAsMainIsland = true,
                        ClusterRuleIndex = ruleIndex,
                        ClusterOccurrenceId = clusterOccurrenceId,
                        ForcedPrefab = rule.CenterpiecePrefab != null &&
                            spineIndex == spineCount / 2
                                ? rule.CenterpiecePrefab
                                : null
                    });
                }

                slots.InsertRange(insertAt, inserted);
            }
        }
    }

    private bool TryResolveSpecialIslands(
        Random random,
        List<PlannedIslandSlot> slots,
        out string error)
    {
        error = string.Empty;
        HashSet<int> reservedIndices = new HashSet<int>();
        List<SpecialIslandRule> remainingRules = new List<SpecialIslandRule>(specialIslands);

        while (remainingRules.Count > 0)
        {
            SpecialIslandRule mostConstrainedRule = null;
            List<int> mostConstrainedCandidates = null;

            for (int ruleIndex = 0; ruleIndex < remainingRules.Count; ruleIndex++)
            {
                SpecialIslandRule rule = remainingRules[ruleIndex];
                List<int> candidates = new List<int>();

                int minimum = Mathf.Max(1, rule.MinimumIndex);
                int maximum = Mathf.Min(slots.Count, rule.MaximumIndex);
                for (int oneBasedIndex = minimum; oneBasedIndex <= maximum; oneBasedIndex++)
                {
                    if (reservedIndices.Contains(oneBasedIndex))
                        continue;

                    PlannedIslandSlot slot = slots[oneBasedIndex - 1];
                    if (slot.ForcedPrefab == null &&
                        rule.IslandPrefab.SupportsBiome(slot.Biome))
                        candidates.Add(oneBasedIndex);
                }

                if (mostConstrainedCandidates == null ||
                    candidates.Count < mostConstrainedCandidates.Count)
                {
                    mostConstrainedRule = rule;
                    mostConstrainedCandidates = candidates;
                }
            }

            if (mostConstrainedCandidates == null || mostConstrainedCandidates.Count == 0)
            {
                error =
                    $"Special Island '{mostConstrainedRule?.IslandPrefab.name}' has no free, " +
                    $"biome-compatible index inside " +
                    $"{mostConstrainedRule?.MinimumIndex}-{mostConstrainedRule?.MaximumIndex}.";
                return false;
            }

            int chosenIndex = mostConstrainedCandidates[random.Next(mostConstrainedCandidates.Count)];
            reservedIndices.Add(chosenIndex);
            slots[chosenIndex - 1].ForcedPrefab = mostConstrainedRule.IslandPrefab;
            remainingRules.Remove(mostConstrainedRule);
        }

        return true;
    }

    private int PickSmallRunLength(Random random)
    {
        int minimum = Mathf.Max(0, rhythm.MinimumSmallIslandsAfterMedium);
        int maximum = Mathf.Max(minimum, rhythm.MaximumSmallIslandsAfterMedium);
        int result = minimum;

        for (int i = minimum; i < maximum; i++)
        {
            if (!RollPercent(random, rhythm.AdditionalSmallIslandChance))
                break;

            result++;
        }

        return result;
    }

    private bool TryPlaceFirstIsland(
        RouteBuildState state,
        Random random,
        SocketPose routeStartPose,
        PlannedIslandSlot slot,
        RouteFrame frame,
        PlacementDiagnostics diagnostics,
        out PlannedPiece placedIsland)
    {
        placedIsland = null;
        List<IslandOption> islandOptions = CollectIslandOptions(
            state,
            slot,
            MainIslandSelection,
            diagnostics);
        diagnostics.IslandOptions = islandOptions.Count;

        List<FirstIslandCandidate> candidates = new List<FirstIslandCandidate>();
        List<int> entryIndices = new List<int>();

        for (int optionIndex = 0; optionIndex < islandOptions.Count; optionIndex++)
        {
            IslandOption option = islandOptions[optionIndex];
            option.Prefab.CollectSocketIndices(
                SocketUsage.Entry,
                SocketRouteUsage.MainRoute,
                null,
                entryIndices);
            if (entryIndices.Count == 0)
                diagnostics.IslandsWithoutRouteEntry++;

            FirstIslandCandidate best = null;
            for (int entryListIndex = 0; entryListIndex < entryIndices.Count; entryListIndex++)
            {
                int entryIndex = entryIndices[entryListIndex];
                PiecePlacement placement = AlignSocketToPose(option.Prefab, entryIndex, routeStartPose);
                List<OrientedBox> bounds = BuildBounds(option.Prefab, placement);

                if (!TryEvaluateIslandPlacement(
                        option.Prefab,
                        placement,
                        entryIndex,
                        SocketRouteUsage.MainRoute,
                        frame,
                        true,
                        diagnostics,
                        out float score,
                        out float progress))
                {
                    continue;
                }

                if (best == null || score < best.Score)
                {
                    best = new FirstIslandCandidate
                    {
                        Option = option,
                        Placement = placement,
                        EntrySocketIndex = entryIndex,
                        Bounds = bounds,
                        Score = score,
                        ForwardProgress = progress
                    };
                }
            }

            if (best != null)
                candidates.Add(best);
        }

        diagnostics.PairCandidates = candidates.Count;

        if (candidates.Count == 0)
            return false;

        FilterByScoreTolerance(candidates, candidate => candidate.Score);
        FirstIslandCandidate chosen = SelectWeighted(
            candidates,
            candidate => candidate.Option.ChancePercent,
            random);

        if (chosen == null)
            return false;

        placedIsland = CreatePlannedPiece(
            chosen.Option.Prefab,
            chosen.Placement,
            chosen.Bounds,
            slot.Biome,
            slot.CountsAsMainIsland,
            slot.Index,
            false);

        ApplySlotContext(placedIsland, slot);

        placedIsland.UsedSockets.Add(chosen.EntrySocketIndex);
        state.Pieces.Add(placedIsland);
        state.Usage.RecordIsland(chosen.Option.Prefab);
        frame.CurrentProgress = chosen.ForwardProgress;
        return true;
    }

    private bool TryPlaceConnectedIsland(
        RouteBuildState state,
        Random random,
        PlannedPiece currentIsland,
        int? forcedSourceSocketIndex,
        PlannedIslandSlot targetSlot,
        SocketRouteUsage routeUsage,
        RouteFrame frame,
        bool requireFutureExit,
        ConnectionIsland exactConnection,
        IslandSelection selection,
        PlacementDiagnostics diagnostics,
        out PlannedPiece placedIsland)
    {
        placedIsland = null;

        List<IslandOption> islandOptions = CollectIslandOptions(
            state,
            targetSlot,
            selection,
            diagnostics);
        List<ConnectionOption> connectionOptions = CollectConnectionOptions(
            state,
            targetSlot.Biome,
            exactConnection,
            diagnostics);

        diagnostics.IslandOptions += islandOptions.Count;
        diagnostics.ConnectionOptions += connectionOptions.Count;

        if (islandOptions.Count == 0 || connectionOptions.Count == 0)
            return false;

        List<PairCandidate> candidates = BuildPairCandidates(
            state,
            currentIsland,
            forcedSourceSocketIndex,
            routeUsage,
            frame,
            requireFutureExit,
            connectionOptions,
            islandOptions,
            diagnostics);

        diagnostics.PairCandidates += candidates.Count;

        if (candidates.Count == 0)
            return false;

        FilterByScoreTolerance(candidates, candidate => candidate.Score);

        List<ConnectionChoice> connectionChoices = BuildConnectionChoices(candidates);
        ConnectionChoice selectedConnection = SelectWeighted(
            connectionChoices,
            choice => choice.Option.ChancePercent,
            random);

        if (selectedConnection == null)
            return false;

        List<PairCandidate> connectionCandidates = new List<PairCandidate>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i].ConnectionOption, selectedConnection.Option))
                connectionCandidates.Add(candidates[i]);
        }

        PairCandidate chosen = SelectWeighted(
            connectionCandidates,
            candidate => candidate.IslandOption.ChancePercent,
            random);

        if (chosen == null)
            return false;

        PlannedPiece connectionPiece = CreatePlannedPiece(
            chosen.ConnectionOption.Prefab,
            chosen.ConnectionPlacement,
            chosen.ConnectionBounds,
            targetSlot.Biome,
            false,
            0,
            false);

        ApplySlotContext(connectionPiece, targetSlot);

        connectionPiece.UsedSockets.Add(chosen.ConnectionEntrySocketIndex);
        connectionPiece.UsedSockets.Add(chosen.ConnectionExitSocketIndex);

        placedIsland = CreatePlannedPiece(
            chosen.IslandOption.Prefab,
            chosen.IslandPlacement,
            chosen.IslandBounds,
            targetSlot.Biome,
            targetSlot.CountsAsMainIsland && !targetSlot.IsBeacon,
            targetSlot.Index,
            targetSlot.IsBeacon);

        ApplySlotContext(placedIsland, targetSlot);

        placedIsland.UsedSockets.Add(chosen.IslandEntrySocketIndex);
        currentIsland.UsedSockets.Add(chosen.SourceSocketIndex);

        state.Pieces.Add(connectionPiece);
        state.Pieces.Add(placedIsland);
        state.Usage.RecordConnection(chosen.ConnectionOption.Prefab);
        state.Usage.RecordIsland(chosen.IslandOption.Prefab);
        frame.CurrentProgress = chosen.ForwardProgress;
        return true;
    }

    private List<PairCandidate> BuildPairCandidates(
        RouteBuildState state,
        PlannedPiece currentIsland,
        int? forcedSourceSocketIndex,
        SocketRouteUsage routeUsage,
        RouteFrame frame,
        bool requireFutureExit,
        List<ConnectionOption> connectionOptions,
        List<IslandOption> islandOptions,
        PlacementDiagnostics diagnostics)
    {
        List<PairCandidate> results = new List<PairCandidate>();
        List<int> sourceExitIndices = new List<int>();
        List<int> connectionEntryIndices = new List<int>();
        List<int> connectionExitIndices = new List<int>();
        List<int> islandEntryIndices = new List<int>();

        if (forcedSourceSocketIndex.HasValue)
        {
            int index = forcedSourceSocketIndex.Value;
            IslandSocket socket = currentIsland.Prefab.GetSocket(index);
            if (socket != null &&
                !currentIsland.UsedSockets.Contains(index) &&
                socket.CanBeUsedAs(SocketUsage.Exit) &&
                socket.SupportsRoute(routeUsage))
            {
                sourceExitIndices.Add(index);
            }
        }
        else
        {
            currentIsland.Prefab.CollectSocketIndices(
                SocketUsage.Exit,
                routeUsage,
                currentIsland.UsedSockets,
                sourceExitIndices);
        }

        diagnostics.SourceExitSockets += sourceExitIndices.Count;
        if (sourceExitIndices.Count == 0)
            diagnostics.NoUsableSourceExit++;

        for (int connectionOptionIndex = 0;
             connectionOptionIndex < connectionOptions.Count;
             connectionOptionIndex++)
        {
            ConnectionOption connectionOption = connectionOptions[connectionOptionIndex];
            ConnectionIsland connectionPrefab = connectionOption.Prefab;

            connectionPrefab.CollectSocketIndices(
                SocketUsage.Entry,
                routeUsage,
                null,
                connectionEntryIndices);
            connectionPrefab.CollectSocketIndices(
                SocketUsage.Exit,
                routeUsage,
                null,
                connectionExitIndices);

            if (connectionEntryIndices.Count == 0)
                diagnostics.ConnectionsWithoutRouteEntry++;
            if (connectionExitIndices.Count < 1)
                diagnostics.ConnectionsWithoutRouteExit++;

            for (int islandOptionIndex = 0;
                 islandOptionIndex < islandOptions.Count;
                 islandOptionIndex++)
            {
                IslandOption islandOption = islandOptions[islandOptionIndex];
                AboveIsland islandPrefab = islandOption.Prefab;
                islandPrefab.CollectSocketIndices(
                    SocketUsage.Entry,
                    routeUsage,
                    null,
                    islandEntryIndices);
                if (islandEntryIndices.Count == 0)
                    diagnostics.IslandsWithoutRouteEntry++;

                PairCandidate bestForPair = null;

                for (int sourceListIndex = 0;
                     sourceListIndex < sourceExitIndices.Count;
                     sourceListIndex++)
                {
                    int sourceIndex = sourceExitIndices[sourceListIndex];
                    IslandSocket sourceSocket = currentIsland.Prefab.GetSocket(sourceIndex);
                    if (!sourceSocket.Allows(connectionPrefab.ConnectionType))
                    {
                        diagnostics.SourceConnectionTypeRejected++;
                        continue;
                    }

                    SocketPose sourcePose = GetSocketPose(currentIsland, sourceIndex);

                    for (int connectionEntryListIndex = 0;
                         connectionEntryListIndex < connectionEntryIndices.Count;
                         connectionEntryListIndex++)
                    {
                        int connectionEntryIndex = connectionEntryIndices[connectionEntryListIndex];
                        PiecePlacement connectionPlacement = AlignSocketToPose(
                            connectionPrefab,
                            connectionEntryIndex,
                            sourcePose);

                        List<OrientedBox> connectionBounds = BuildBounds(
                            connectionPrefab,
                            connectionPlacement);

                        if (OverlapsExisting(
                                connectionBounds,
                                state.Pieces,
                                currentIsland))
                        {
                            diagnostics.ConnectionOverlapRejected++;
                            continue;
                        }

                        for (int connectionExitListIndex = 0;
                             connectionExitListIndex < connectionExitIndices.Count;
                             connectionExitListIndex++)
                        {
                            int connectionExitIndex = connectionExitIndices[connectionExitListIndex];
                            if (connectionExitIndex == connectionEntryIndex)
                                continue;

                            SocketPose connectionExitPose = GetSocketPose(
                                connectionPrefab,
                                connectionPlacement,
                                connectionExitIndex);

                            for (int islandEntryListIndex = 0;
                                 islandEntryListIndex < islandEntryIndices.Count;
                                 islandEntryListIndex++)
                            {
                                int islandEntryIndex = islandEntryIndices[islandEntryListIndex];
                                IslandSocket islandEntrySocket = islandPrefab.GetSocket(islandEntryIndex);
                                if (!islandEntrySocket.Allows(connectionPrefab.ConnectionType))
                                {
                                    diagnostics.IslandConnectionTypeRejected++;
                                    continue;
                                }

                                PiecePlacement islandPlacement = AlignSocketToPose(
                                    islandPrefab,
                                    islandEntryIndex,
                                    connectionExitPose);

                                List<OrientedBox> islandBounds = BuildBounds(
                                    islandPrefab,
                                    islandPlacement);

                                if (OverlapsExisting(islandBounds, state.Pieces, null))
                                {
                                    diagnostics.IslandOverlapRejected++;
                                    continue;
                                }

                                if (!TryEvaluateIslandPlacement(
                                        islandPrefab,
                                        islandPlacement,
                                        islandEntryIndex,
                                        routeUsage,
                                        frame,
                                        requireFutureExit,
                                        diagnostics,
                                        out float score,
                                        out float progress))
                                {
                                    continue;
                                }

                                if (bestForPair == null || score < bestForPair.Score)
                                {
                                    bestForPair = new PairCandidate
                                    {
                                        ConnectionOption = connectionOption,
                                        IslandOption = islandOption,
                                        SourceSocketIndex = sourceIndex,
                                        ConnectionEntrySocketIndex = connectionEntryIndex,
                                        ConnectionExitSocketIndex = connectionExitIndex,
                                        IslandEntrySocketIndex = islandEntryIndex,
                                        ConnectionPlacement = connectionPlacement,
                                        IslandPlacement = islandPlacement,
                                        ConnectionBounds = connectionBounds,
                                        IslandBounds = islandBounds,
                                        Score = score,
                                        ForwardProgress = progress
                                    };
                                }
                            }
                        }
                    }
                }

                if (bestForPair != null)
                    results.Add(bestForPair);
            }
        }

        return results;
    }

    private bool TryEvaluateIslandPlacement(
        AboveIsland prefab,
        PiecePlacement placement,
        int usedEntrySocketIndex,
        SocketRouteUsage routeUsage,
        RouteFrame frame,
        bool requireFutureExit,
        PlacementDiagnostics diagnostics,
        out float bestScore,
        out float bestProgress)
    {
        bestScore = float.PositiveInfinity;
        bestProgress = frame.CurrentProgress;

        if (!requireFutureExit)
        {
            SocketPose entryPose = GetSocketPose(prefab, placement, usedEntrySocketIndex);
            return TryScorePose(entryPose, frame, diagnostics, out bestScore, out bestProgress);
        }

        List<int> outputIndices = new List<int>();
        HashSet<int> excluded = new HashSet<int> { usedEntrySocketIndex };
        prefab.CollectSocketIndices(SocketUsage.Exit, routeUsage, excluded, outputIndices);

        if (outputIndices.Count == 0)
        {
            diagnostics.NoFutureExitRejected++;
            return false;
        }

        for (int i = 0; i < outputIndices.Count; i++)
        {
            SocketPose pose = GetSocketPose(prefab, placement, outputIndices[i]);
            if (!TryScorePose(pose, frame, diagnostics, out float score, out float progress))
                continue;

            if (score < bestScore)
            {
                bestScore = score;
                bestProgress = progress;
            }
        }

        return !float.IsPositiveInfinity(bestScore);
    }

    private bool TryScorePose(
        SocketPose pose,
        RouteFrame frame,
        PlacementDiagnostics diagnostics,
        out float score,
        out float progress)
    {
        Vector3 relative = pose.Position - frame.Origin;
        progress = Vector3.Dot(relative, frame.Forward);
        float lateral = Mathf.Abs(Vector3.Dot(relative, frame.Right));
        float relativeHeight = pose.Position.y - frame.HeightOrigin;

        Vector3 outgoingForward = FlattenDirection(pose.Rotation * Vector3.forward, frame.Forward);
        float headingAngle = Vector3.Angle(frame.Forward, outgoingForward);

        if (headingAngle > frame.MaximumHeadingAngle)
        {
            diagnostics.HeadingRejected++;
            score = 0f;
            return false;
        }

        if (lateral > frame.MaximumLateralDrift)
        {
            diagnostics.LateralRejected++;
            score = 0f;
            return false;
        }

        if (progress < frame.CurrentProgress + routeShape.MinimumForwardProgressPerIsland)
        {
            diagnostics.ForwardProgressRejected++;
            score = 0f;
            return false;
        }

        if (relativeHeight < routeShape.MinimumRelativeHeight ||
            relativeHeight > routeShape.MaximumRelativeHeight)
        {
            diagnostics.HeightRejected++;
            score = 0f;
            return false;
        }

        float headingNormalized = headingAngle / Mathf.Max(1f, frame.MaximumHeadingAngle);
        float lateralNormalized = lateral / Mathf.Max(1f, frame.MaximumLateralDrift);

        float heightMidpoint =
            (routeShape.MinimumRelativeHeight + routeShape.MaximumRelativeHeight) * 0.5f;
        float heightHalfRange =
            Mathf.Max(1f, (routeShape.MaximumRelativeHeight - routeShape.MinimumRelativeHeight) * 0.5f);
        float heightNormalized = Mathf.Abs(relativeHeight - heightMidpoint) / heightHalfRange;

        score =
            headingNormalized * routeShape.HeadingScoreWeight +
            lateralNormalized * routeShape.LateralScoreWeight +
            heightNormalized * routeShape.HeightScoreWeight;

        return true;
    }

    private List<IslandOption> CollectIslandOptions(
        RouteBuildState state,
        PlannedIslandSlot slot,
        IslandSelection selection,
        PlacementDiagnostics diagnostics)
    {
        List<IslandOption> options = new List<IslandOption>();

        if (slot.ForcedPrefab != null)
        {
            if (slot.ForcedPrefab.SupportsBiome(slot.Biome))
            {
                options.Add(new IslandOption(slot.ForcedPrefab, 100f));
                diagnostics.AddIslandName(slot.ForcedPrefab.name);
            }
            else
                diagnostics.IslandBiomeRejected++;

            return options;
        }

        for (int i = 0; i < islandPrefabs.Count; i++)
        {
            IslandPoolEntry entry = islandPrefabs[i];
            float chance = entry == null ? 0f : entry.GetChancePercent(slot.Biome);
            if (entry == null || entry.Prefab == null)
                continue;
            if (chance <= 0f)
            {
                diagnostics.IslandChanceRejected++;
                continue;
            }

            AboveIsland prefab = entry.Prefab;
            if (!prefab.SupportsBiome(slot.Biome))
            {
                diagnostics.IslandBiomeRejected++;
                continue;
            }

            if (!prefab.SupportsPhase(slot.PhaseUsage))
            {
                diagnostics.IslandPhaseRejected++;
                continue;
            }

            IslandSelectionRejection selectionRejection = selection.GetRejection(prefab, slot.Size);
            if (selectionRejection != IslandSelectionRejection.None)
            {
                if (selectionRejection == IslandSelectionRejection.Size)
                    diagnostics.IslandSizeRejected++;
                else
                    diagnostics.IslandRoleRejected++;
                continue;
            }

            UsageBlockReason usageReason = state.Usage.GetIslandBlockReason(entry);
            if (usageReason != UsageBlockReason.None)
            {
                if (usageReason == UsageBlockReason.RepeatGap)
                    diagnostics.IslandRepeatGapRejected++;
                else
                    diagnostics.IslandMaximumRejected++;
                continue;
            }

            options.Add(new IslandOption(prefab, chance));
            diagnostics.AddIslandName(prefab.name);
        }

        return options;
    }

    private List<ConnectionOption> CollectConnectionOptions(
        RouteBuildState state,
        IslandBiome biome,
        ConnectionIsland exactConnection,
        PlacementDiagnostics diagnostics)
    {
        List<ConnectionOption> options = new List<ConnectionOption>();

        if (exactConnection != null)
        {
            if (exactConnection.SupportsBiome(biome))
            {
                options.Add(new ConnectionOption(exactConnection, 100f));
                diagnostics.AddConnectionName(exactConnection.name);
            }
            else
                diagnostics.ConnectionBiomeRejected++;

            return options;
        }

        for (int i = 0; i < connectionPrefabs.Count; i++)
        {
            ConnectionPoolEntry entry = connectionPrefabs[i];
            if (entry == null || entry.Prefab == null || entry.ChancePercent <= 0f)
            {
                diagnostics.ConnectionChanceRejected++;
                continue;
            }

            if (!entry.Prefab.SupportsBiome(biome))
            {
                diagnostics.ConnectionBiomeRejected++;
                continue;
            }

            UsageBlockReason usageReason = state.Usage.GetConnectionBlockReason(entry);
            if (usageReason != UsageBlockReason.None)
            {
                if (usageReason == UsageBlockReason.RepeatGap)
                    diagnostics.ConnectionRepeatGapRejected++;
                else
                    diagnostics.ConnectionMaximumRejected++;
                continue;
            }

            options.Add(new ConnectionOption(entry.Prefab, entry.ChancePercent));
            diagnostics.AddConnectionName(entry.Prefab.name);
        }

        return options;
    }

    private bool GenerateClusterPhases(
        RouteBuildState state,
        Random random,
        out string error)
    {
        error = string.Empty;
        if (clusterPhases == null || clusterPhases.Count == 0)
            return true;

        Dictionary<int, List<PlannedPiece>> spinesByOccurrence =
            new Dictionary<int, List<PlannedPiece>>();

        for (int i = 0; i < state.Pieces.Count; i++)
        {
            PlannedPiece piece = state.Pieces[i];
            if (!piece.IsMainIsland || piece.ClusterOccurrenceId < 0)
                continue;

            if (!spinesByOccurrence.TryGetValue(
                    piece.ClusterOccurrenceId,
                    out List<PlannedPiece> spine))
            {
                spine = new List<PlannedPiece>();
                spinesByOccurrence.Add(piece.ClusterOccurrenceId, spine);
            }

            spine.Add(piece);
        }

        List<int> occurrenceIds = new List<int>(spinesByOccurrence.Keys);
        occurrenceIds.Sort();

        for (int occurrenceIndex = 0;
             occurrenceIndex < occurrenceIds.Count;
             occurrenceIndex++)
        {
            int occurrenceId = occurrenceIds[occurrenceIndex];
            List<PlannedPiece> spine = spinesByOccurrence[occurrenceId];
            spine.Sort((a, b) => a.MainIndex.CompareTo(b.MainIndex));

            int ruleIndex = spine[0].ClusterRuleIndex;
            if (ruleIndex < 0 || ruleIndex >= clusterPhases.Count ||
                clusterPhases[ruleIndex] == null)
            {
                error = $"Cluster occurrence {occurrenceId + 1} lost its phase rule.";
                return false;
            }

            ClusterPhaseRule rule = clusterPhases[ruleIndex];
            ClusterSnapshot baseline = CaptureClusterSnapshot(state);
            bool generated = false;
            Dictionary<ClusterTopology, int> attemptsByTopology =
                new Dictionary<ClusterTopology, int>();
            Dictionary<ClusterTopology, string> lastFailureByTopology =
                new Dictionary<ClusterTopology, string>();

            for (int attempt = 0;
                 attempt < Mathf.Max(1, rule.MaximumClusterAttempts);
                 attempt++)
            {
                RestoreClusterSnapshot(state, baseline);
                ClusterTopology topology = SelectClusterTopology(rule.TopologyWeights, random);
                if (!attemptsByTopology.ContainsKey(topology))
                    attemptsByTopology.Add(topology, 0);
                attemptsByTopology[topology]++;
                if (TryGenerateClusterAttempt(
                        state,
                        random,
                        spine,
                        rule,
                        topology,
                        out string attemptFailure))
                {
                    generated = true;
                    break;
                }

                lastFailureByTopology[topology] = attemptFailure;
            }

            if (!generated)
            {
                RestoreClusterSnapshot(state, baseline);
                error =
                    $"Could not build island group {occurrenceId + 1} ({rule.Biome}) " +
                    $"after {Mathf.Max(1, rule.MaximumClusterAttempts)} cluster-only attempts. " +
                    BuildClusterFailureReport(attemptsByTopology, lastFailureByTopology);
                return false;
            }
        }

        return true;
    }

    private bool TryGenerateClusterAttempt(
        RouteBuildState state,
        Random random,
        List<PlannedPiece> spine,
        ClusterPhaseRule rule,
        ClusterTopology topology,
        out string failure)
    {
        failure = string.Empty;
        int additionalCount = NextInclusive(
            random,
            Mathf.Max(0, rule.MinimumAdditionalIslands),
            Mathf.Max(0, rule.MaximumAdditionalIslands));
        bool requestRewardEndpoint = additionalCount > 0 &&
            RollPercent(random, rule.RewardEndpointChance);

        List<PlannedPiece> additionalIslands = new List<PlannedPiece>();
        for (int additionalIndex = 0;
             additionalIndex < additionalCount;
             additionalIndex++)
        {
            bool isLast = additionalIndex == additionalCount - 1;
            bool wantsEndpoint = requestRewardEndpoint && isLast;
            IslandSize size = RollPercent(random, rule.MediumAdditionalIslandChance)
                ? IslandSize.Medium
                : IslandSize.Small;

            IslandSelection selection = wantsEndpoint
                ? ClusterEndpointSelection
                : ClusterIslandSelection;

            bool requireFutureExit = !isLast;
            PlacementDiagnostics diagnostics = new PlacementDiagnostics();

            bool placedSuccessfully = TryPlaceClusterExtra(
                    state,
                    random,
                    spine,
                    additionalIslands,
                    rule,
                    topology,
                    size,
                    selection,
                    requireFutureExit,
                    diagnostics,
                    out PlannedPiece placed);

            if (!placedSuccessfully && wantsEndpoint)
            {
                placedSuccessfully = TryPlaceClusterExtra(
                        state,
                        random,
                        spine,
                        additionalIslands,
                        rule,
                        topology,
                        size,
                        ClusterIslandSelection,
                        requireFutureExit,
                        diagnostics,
                        out placed);
            }

            if (!placedSuccessfully && rule.AllowAdditionalSizeFallback && !wantsEndpoint)
            {
                IslandSize fallbackSize = size == IslandSize.Small
                    ? IslandSize.Medium
                    : IslandSize.Small;
                placedSuccessfully = TryPlaceClusterExtra(
                    state,
                    random,
                    spine,
                    additionalIslands,
                    rule,
                    topology,
                    fallbackSize,
                    ClusterIslandSelection,
                    requireFutureExit,
                    diagnostics,
                    out placed);
            }

            if (!placedSuccessfully)
            {
                failure =
                    $"{topology}: side island {additionalIndex + 1}/{additionalCount} " +
                    $"({size}{(wantsEndpoint ? ", reward endpoint preferred" : string.Empty)}) failed. " +
                    diagnostics.BuildSummary();
                return false;
            }

            additionalIslands.Add(placed);
        }

        int maximumLinks = Mathf.Max(0, rule.MaximumExtraLinks);
        int linksCreated = 0;

        List<PlannedPiece> linkSources = new List<PlannedPiece>(additionalIslands);
        Shuffle(linkSources, random);
        for (int i = 0; i < linkSources.Count && linksCreated < maximumLinks; i++)
        {
            if (!RollPercent(random, rule.ExtraLinkChance))
                continue;

            if (TryPlaceClusterExtraLink(
                    state,
                    random,
                    linkSources[i],
                    spine,
                    additionalIslands,
                    rule,
                    topology))
            {
                linksCreated++;
            }
        }

        return true;
    }

    private bool TryPlaceClusterExtra(
        RouteBuildState state,
        Random random,
        List<PlannedPiece> spine,
        List<PlannedPiece> additionalIslands,
        ClusterPhaseRule rule,
        ClusterTopology topology,
        IslandSize size,
        IslandSelection selection,
        bool requireFutureExit,
        PlacementDiagnostics diagnostics,
        out PlannedPiece placed)
    {
        placed = null;
        List<PlannedPiece> anchors = BuildClusterAnchorOrder(
            spine,
            additionalIslands,
            topology,
            random);

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            PlannedPiece anchor = anchors[anchorIndex];
            List<int> exitIndices = new List<int>();
            anchor.Prefab.CollectSocketIndices(
                SocketUsage.Exit,
                SocketRouteUsage.Detour,
                anchor.UsedSockets,
                exitIndices);
            if (exitIndices.Count == 0)
                diagnostics.NoUsableSourceExit++;
            Shuffle(exitIndices, random);

            for (int exitListIndex = 0;
                 exitListIndex < exitIndices.Count;
                 exitListIndex++)
            {
                int sourceSocketIndex = exitIndices[exitListIndex];
                SocketPose sourcePose = GetSocketPose(anchor, sourceSocketIndex);
                Vector3 branchForward = FlattenDirection(
                    sourcePose.Rotation * Vector3.forward,
                    generation.RouteStart.forward);
                RouteFrame branchFrame = new RouteFrame(
                    sourcePose.Position,
                    branchForward,
                    generation.RouteStart.position.y,
                    rule.MaximumBranchHeadingAngle,
                    Mathf.Max(1f, rule.MaximumWidth),
                    0f);

                PlannedIslandSlot slot = new PlannedIslandSlot
                {
                    Index = 0,
                    Biome = rule.Biome,
                    Size = size,
                    PhaseUsage = IslandPhaseUsage.Cluster,
                    CountsAsMainIsland = false,
                    ClusterRuleIndex = anchor.ClusterRuleIndex,
                    ClusterOccurrenceId = anchor.ClusterOccurrenceId
                };

                ClusterSnapshot beforePlacement = CaptureClusterSnapshot(state);
                if (!TryPlaceConnectedIsland(
                        state,
                        random,
                        anchor,
                        sourceSocketIndex,
                        slot,
                        SocketRouteUsage.Detour,
                        branchFrame,
                        requireFutureExit,
                        null,
                        selection,
                        diagnostics,
                        out PlannedPiece candidate))
                {
                    RestoreClusterSnapshot(state, beforePlacement);
                    continue;
                }

                if (!IsInsideClusterEnvelope(candidate, spine, rule))
                {
                    diagnostics.ClusterEnvelopeRejected++;
                    RestoreClusterSnapshot(state, beforePlacement);
                    continue;
                }

                candidate.ClusterParent = anchor;
                placed = candidate;
                return true;
            }
        }

        return false;
    }

    private List<PlannedPiece> BuildClusterAnchorOrder(
        List<PlannedPiece> spine,
        List<PlannedPiece> additionalIslands,
        ClusterTopology topology,
        Random random)
    {
        List<PlannedPiece> anchors = new List<PlannedPiece>();

        if (topology == ClusterTopology.Hub)
        {
            AddUnique(anchors, spine[spine.Count / 2]);
            for (int i = 0; i < additionalIslands.Count; i++)
                AddUnique(anchors, additionalIslands[i]);

            for (int offset = 1; offset < spine.Count; offset++)
            {
                int left = spine.Count / 2 - offset;
                int right = spine.Count / 2 + offset;
                if (left >= 0) AddUnique(anchors, spine[left]);
                if (right < spine.Count) AddUnique(anchors, spine[right]);
            }
        }
        else if (topology == ClusterTopology.Diamond ||
                 topology == ClusterTopology.Ring)
        {
            if (additionalIslands.Count > 0)
                AddUnique(anchors, additionalIslands[additionalIslands.Count - 1]);

            int preferredSpineIndex = topology == ClusterTopology.Ring
                ? 0
                : Mathf.Max(0, spine.Count / 3);
            AddUnique(anchors, spine[preferredSpineIndex]);
            for (int i = 0; i < spine.Count; i++)
                AddUnique(anchors, spine[i]);
        }
        else
        {
            anchors.AddRange(spine);
            anchors.AddRange(additionalIslands);
            Shuffle(anchors, random);
        }

        return anchors;
    }

    private bool TryPlaceClusterExtraLink(
        RouteBuildState state,
        Random random,
        PlannedPiece preferredSource,
        List<PlannedPiece> spine,
        List<PlannedPiece> additionalIslands,
        ClusterPhaseRule rule,
        ClusterTopology topology)
    {
        List<PlannedPiece> sources = new List<PlannedPiece> { preferredSource };
        for (int i = 0; i < additionalIslands.Count; i++)
            AddUnique(sources, additionalIslands[i]);

        List<PlannedPiece> targets = new List<PlannedPiece>();
        if (topology == ClusterTopology.Ring || topology == ClusterTopology.Diamond)
        {
            for (int i = spine.Count - 1; i >= 0; i--)
                targets.Add(spine[i]);
        }
        else if (topology == ClusterTopology.Hub)
        {
            targets.AddRange(additionalIslands);
            targets.AddRange(spine);
        }
        else
        {
            targets.AddRange(spine);
            targets.AddRange(additionalIslands);
            Shuffle(targets, random);
        }

        List<ConnectionOption> connectionOptions = CollectConnectionOptions(
            state,
            rule.Biome,
            null,
            new PlacementDiagnostics());
        if (connectionOptions.Count == 0)
            return false;

        List<ExtraLinkCandidate> candidates = new List<ExtraLinkCandidate>();
        List<int> sourceIndices = new List<int>();
        List<int> targetIndices = new List<int>();
        List<int> connectionEntryIndices = new List<int>();
        List<int> connectionExitIndices = new List<int>();

        for (int sourceListIndex = 0;
             sourceListIndex < sources.Count;
             sourceListIndex++)
        {
            PlannedPiece source = sources[sourceListIndex];
            source.Prefab.CollectSocketIndices(
                SocketUsage.Exit,
                SocketRouteUsage.Detour,
                source.UsedSockets,
                sourceIndices);

            for (int targetListIndex = 0;
                 targetListIndex < targets.Count;
                 targetListIndex++)
            {
                PlannedPiece target = targets[targetListIndex];
                if (ReferenceEquals(source, target) ||
                    ReferenceEquals(source.ClusterParent, target) ||
                    ReferenceEquals(target.ClusterParent, source))
                {
                    continue;
                }

                target.Prefab.CollectSocketIndices(
                    SocketUsage.Entry,
                    SocketRouteUsage.Detour,
                    target.UsedSockets,
                    targetIndices);

                for (int optionIndex = 0;
                     optionIndex < connectionOptions.Count;
                     optionIndex++)
                {
                    ConnectionOption option = connectionOptions[optionIndex];
                    ConnectionIsland connection = option.Prefab;
                    connection.CollectSocketIndices(
                        SocketUsage.Entry,
                        SocketRouteUsage.Detour,
                        null,
                        connectionEntryIndices);
                    connection.CollectSocketIndices(
                        SocketUsage.Exit,
                        SocketRouteUsage.Detour,
                        null,
                        connectionExitIndices);

                    for (int sourceIndexList = 0;
                         sourceIndexList < sourceIndices.Count;
                         sourceIndexList++)
                    {
                        int sourceIndex = sourceIndices[sourceIndexList];
                        IslandSocket sourceSocket = source.Prefab.GetSocket(sourceIndex);
                        if (!sourceSocket.Allows(connection.ConnectionType))
                            continue;

                        SocketPose sourcePose = GetSocketPose(source, sourceIndex);
                        for (int connectionEntryList = 0;
                             connectionEntryList < connectionEntryIndices.Count;
                             connectionEntryList++)
                        {
                            int connectionEntryIndex =
                                connectionEntryIndices[connectionEntryList];
                            PiecePlacement placement = AlignSocketToPose(
                                connection,
                                connectionEntryIndex,
                                sourcePose);
                            List<OrientedBox> bounds = BuildBounds(connection, placement);

                            if (OverlapsExistingExcept(
                                    bounds,
                                    state.Pieces,
                                    source,
                                    target))
                            {
                                continue;
                            }

                            for (int connectionExitList = 0;
                                 connectionExitList < connectionExitIndices.Count;
                                 connectionExitList++)
                            {
                                int connectionExitIndex =
                                    connectionExitIndices[connectionExitList];
                                if (connectionExitIndex == connectionEntryIndex)
                                    continue;

                                SocketPose exitPose = GetSocketPose(
                                    connection,
                                    placement,
                                    connectionExitIndex);

                                for (int targetIndexList = 0;
                                     targetIndexList < targetIndices.Count;
                                     targetIndexList++)
                                {
                                    int targetIndex = targetIndices[targetIndexList];
                                    IslandSocket targetSocket = target.Prefab.GetSocket(targetIndex);
                                    if (!targetSocket.Allows(connection.ConnectionType))
                                        continue;

                                    SocketPose targetPose = GetSocketPose(target, targetIndex);
                                    float distance = Vector3.Distance(
                                        exitPose.Position,
                                        targetPose.Position);
                                    if (distance > rule.ExtraLinkPositionTolerance)
                                        continue;

                                    float angle = Quaternion.Angle(
                                        exitPose.Rotation,
                                        targetPose.Rotation);
                                    if (angle > rule.ExtraLinkAngleTolerance)
                                        continue;

                                    candidates.Add(new ExtraLinkCandidate
                                    {
                                        Option = option,
                                        Source = source,
                                        Target = target,
                                        SourceSocketIndex = sourceIndex,
                                        TargetSocketIndex = targetIndex,
                                        ConnectionEntrySocketIndex = connectionEntryIndex,
                                        ConnectionExitSocketIndex = connectionExitIndex,
                                        Placement = placement,
                                        Bounds = bounds,
                                        Score =
                                            distance / Mathf.Max(
                                                0.01f,
                                                rule.ExtraLinkPositionTolerance) +
                                            angle / Mathf.Max(
                                                0.1f,
                                                rule.ExtraLinkAngleTolerance)
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        FilterByScoreTolerance(candidates, candidate => candidate.Score);
        List<ConnectionChoice> choices = new List<ConnectionChoice>();
        for (int i = 0; i < candidates.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < choices.Count; j++)
            {
                if (ReferenceEquals(choices[j].Option, candidates[i].Option))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                choices.Add(new ConnectionChoice(candidates[i].Option));
        }

        ConnectionChoice selectedChoice = SelectWeighted(
            choices,
            choice => choice.Option.ChancePercent,
            random);
        if (selectedChoice == null)
            return false;

        List<ExtraLinkCandidate> selectedCandidates = new List<ExtraLinkCandidate>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i].Option, selectedChoice.Option))
                selectedCandidates.Add(candidates[i]);
        }

        ExtraLinkCandidate chosen = selectedCandidates[random.Next(selectedCandidates.Count)];
        PlannedPiece connectionPiece = CreatePlannedPiece(
            chosen.Option.Prefab,
            chosen.Placement,
            chosen.Bounds,
            rule.Biome,
            false,
            0,
            false);
        connectionPiece.IsClusterPiece = true;
        connectionPiece.ClusterRuleIndex = chosen.Source.ClusterRuleIndex;
        connectionPiece.ClusterOccurrenceId = chosen.Source.ClusterOccurrenceId;
        connectionPiece.UsedSockets.Add(chosen.ConnectionEntrySocketIndex);
        connectionPiece.UsedSockets.Add(chosen.ConnectionExitSocketIndex);

        chosen.Source.UsedSockets.Add(chosen.SourceSocketIndex);
        chosen.Target.UsedSockets.Add(chosen.TargetSocketIndex);
        state.Pieces.Add(connectionPiece);
        state.Usage.RecordConnection(chosen.Option.Prefab);
        return true;
    }

    private bool IsInsideClusterEnvelope(
        PlannedPiece candidate,
        List<PlannedPiece> spine,
        ClusterPhaseRule rule)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < spine.Count; i++)
            center += spine[i].Placement.Position;
        center /= Mathf.Max(1, spine.Count);

        Vector3 mainForward = FlattenDirection(
            generation.RouteStart.forward,
            Vector3.forward);
        Vector3 right = Vector3.Cross(Vector3.up, mainForward).normalized;
        Vector3 delta = candidate.Placement.Position - center;

        float halfWidth = Mathf.Max(0.5f, rule.MaximumWidth * 0.5f);
        float halfHeight = Mathf.Max(0.5f, rule.MaximumHeightRange * 0.5f);
        return Mathf.Abs(Vector3.Dot(delta, right)) <= halfWidth &&
               Mathf.Abs(delta.y) <= halfHeight;
    }

    private static ClusterTopology SelectClusterTopology(
        ClusterTopologyWeights weights,
        Random random)
    {
        if (weights == null)
            return ClusterTopology.Hub;

        double hub = Math.Max(0d, weights.Hub);
        double diamond = Math.Max(0d, weights.Diamond);
        double ring = Math.Max(0d, weights.Ring);
        double braided = Math.Max(0d, weights.Braided);
        double total = hub + diamond + ring + braided;
        if (total <= 0d)
            return ClusterTopology.Hub;

        double roll = random.NextDouble() * total;
        if ((roll -= hub) <= 0d) return ClusterTopology.Hub;
        if ((roll -= diamond) <= 0d) return ClusterTopology.Diamond;
        if ((roll -= ring) <= 0d) return ClusterTopology.Ring;
        return ClusterTopology.Braided;
    }

    private static ClusterSnapshot CaptureClusterSnapshot(RouteBuildState state)
    {
        ClusterSnapshot snapshot = new ClusterSnapshot
        {
            PieceCount = state.Pieces.Count,
            Usage = state.Usage.Clone()
        };

        for (int i = 0; i < state.Pieces.Count; i++)
        {
            PlannedPiece piece = state.Pieces[i];
            snapshot.UsedSockets.Add(
                piece,
                new HashSet<int>(piece.UsedSockets));
        }

        return snapshot;
    }

    private static void RestoreClusterSnapshot(
        RouteBuildState state,
        ClusterSnapshot snapshot)
    {
        if (state.Pieces.Count > snapshot.PieceCount)
        {
            state.Pieces.RemoveRange(
                snapshot.PieceCount,
                state.Pieces.Count - snapshot.PieceCount);
        }

        foreach (KeyValuePair<PlannedPiece, HashSet<int>> pair in snapshot.UsedSockets)
        {
            pair.Key.UsedSockets.Clear();
            foreach (int socketIndex in pair.Value)
                pair.Key.UsedSockets.Add(socketIndex);
        }

        state.Usage = snapshot.Usage.Clone();
    }

    private static void AddUnique(List<PlannedPiece> pieces, PlannedPiece piece)
    {
        if (piece != null && !pieces.Contains(piece))
            pieces.Add(piece);
    }

    private void GenerateDetours(RouteBuildState state, Random random)
    {
        int generatedDetours = 0;
        List<PlannedPiece> mainJunctions = new List<PlannedPiece>();

        for (int i = 0; i < state.Pieces.Count; i++)
        {
            PlannedPiece piece = state.Pieces[i];
            if (!piece.IsMainIsland || piece.IsBeacon)
                continue;

            if (piece.Prefab is AboveIsland island && island.Role == IslandRole.Junction)
                mainJunctions.Add(piece);
        }

        Shuffle(mainJunctions, random);

        for (int junctionIndex = 0;
             junctionIndex < mainJunctions.Count && generatedDetours < detours.MaximumDetoursPerRun;
             junctionIndex++)
        {
            if (!RollPercent(random, detours.JunctionDetourChance))
                continue;

            state.DetoursAttempted++;
            PlannedPiece junction = mainJunctions[junctionIndex];
            List<int> exitIndices = new List<int>();
            junction.Prefab.CollectSocketIndices(
                SocketUsage.Exit,
                SocketRouteUsage.Detour,
                junction.UsedSockets,
                exitIndices);

            Shuffle(exitIndices, random);
            bool generated = false;
            PlacementDiagnostics detourDiagnostics = new PlacementDiagnostics();
            for (int exitListIndex = 0; exitListIndex < exitIndices.Count; exitListIndex++)
            {
                if (TryGenerateSingleDetour(
                        state,
                        random,
                        junction,
                        exitIndices[exitListIndex],
                        detourDiagnostics))
                {
                    generated = true;
                    break;
                }
            }

            if (generated)
            {
                generatedDetours++;
                state.DetoursGenerated++;
            }
            else
            {
                if (exitIndices.Count == 0)
                    detourDiagnostics.NoUsableSourceExit++;
                state.DetourFailureSummaries.Add(
                    $"{junction.Prefab.name}: {detourDiagnostics.BuildSummary()}");
            }
        }
    }

    private bool TryGenerateSingleDetour(
        RouteBuildState state,
        Random random,
        PlannedPiece junction,
        int sourceSocketIndex,
        PlacementDiagnostics diagnostics)
    {
        int originalPieceCount = state.Pieces.Count;
        HashSet<int> originalUsedSockets = new HashSet<int>(junction.UsedSockets);
        RunUsageState originalUsage = state.Usage;
        state.Usage = state.Usage.Clone();

        SocketPose branchStart = GetSocketPose(junction, sourceSocketIndex);
        Vector3 branchForward = FlattenDirection(
            branchStart.Rotation * Vector3.forward,
            Vector3.forward);
        RouteFrame frame = new RouteFrame(
            branchStart.Position,
            branchForward,
            generation.RouteStart.position.y,
            detours.MaximumHeadingAngle,
            detours.MaximumLateralDrift,
            0f);

        int islandCount = NextInclusive(random, detours.MinimumIslands, detours.MaximumIslands);
        PlannedPiece current = junction;

        for (int detourIndex = 0; detourIndex < islandCount; detourIndex++)
        {
            bool isEndpoint = detourIndex == islandCount - 1;
            PlannedIslandSlot slot = new PlannedIslandSlot
            {
                Index = 0,
                Biome = junction.Biome,
                Size = IslandSize.Small,
                PhaseUsage = IslandPhaseUsage.Linear,
                CountsAsMainIsland = false
            };

            IslandSelection selection = isEndpoint
                ? DetourEndpointSelection
                : DetourIntermediateSelection;

            int? forcedSource = detourIndex == 0 ? sourceSocketIndex : (int?)null;
            bool placed = TryPlaceConnectedIsland(
                    state,
                    random,
                    current,
                    forcedSource,
                    slot,
                    SocketRouteUsage.Detour,
                    frame,
                    !isEndpoint,
                    null,
                    selection,
                    diagnostics,
                    out PlannedPiece next);

            // A dedicated endpoint is optional. If none can be placed, finish the
            // branch with a normal Small island that does not need another exit.
            if (!placed && isEndpoint)
            {
                placed = TryPlaceConnectedIsland(
                    state,
                    random,
                    current,
                    forcedSource,
                    slot,
                    SocketRouteUsage.Detour,
                    frame,
                    false,
                    null,
                    DetourIntermediateSelection,
                    diagnostics,
                    out next);
            }

            if (!placed)
            {
                RollBackDetour(
                    state,
                    junction,
                    originalPieceCount,
                    originalUsedSockets,
                    originalUsage);
                return false;
            }

            current = next;
        }

        return true;
    }

    private static void RollBackDetour(
        RouteBuildState state,
        PlannedPiece junction,
        int originalPieceCount,
        HashSet<int> originalUsedSockets,
        RunUsageState originalUsage)
    {
        if (state.Pieces.Count > originalPieceCount)
        {
            state.Pieces.RemoveRange(
                originalPieceCount,
                state.Pieces.Count - originalPieceCount);
        }

        junction.UsedSockets.Clear();
        foreach (int index in originalUsedSockets)
            junction.UsedSockets.Add(index);

        state.Usage = originalUsage;
    }

    private void InstantiatePlan(List<PlannedPiece> pieces)
    {
        Transform parent = generation.GeneratedParent != null
            ? generation.GeneratedParent
            : transform;

        generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.transform.SetParent(parent, false);

        for (int i = 0; i < pieces.Count; i++)
        {
            PlannedPiece planned = pieces[i];
            AboveRoutePiece instance = Instantiate(
                planned.Prefab,
                planned.Placement.Position,
                planned.Placement.Rotation,
                generatedRoot.transform);

            instance.name = planned.IsBeacon
                ? $"Beacon - {planned.Prefab.name}"
                : planned.IsMainIsland
                    ? $"Island {planned.MainIndex:00} - {planned.Prefab.name}"
                    : planned.IsClusterPiece
                        ? $"Cluster - {planned.Prefab.name}"
                    : planned.Prefab.name;

            instance.InitializeGeneratedContext(
                planned.Biome,
                planned.IsMainIsland,
                planned.MainIndex,
                planned.IsBeacon,
                planned.IsClusterPiece);

            DisablePlacementBounds(instance);
            generatedInstances.Add(instance);
            PieceInstantiated?.Invoke(instance);
        }
    }

    private static void DisablePlacementBounds(AboveRoutePiece instance)
    {
        for (int i = 0; i < instance.PlacementBounds.Count; i++)
        {
            BoxCollider bounds = instance.PlacementBounds[i];
            if (bounds != null)
                bounds.enabled = false;
        }
    }

    private static PlannedPiece CreatePlannedPiece(
        AboveRoutePiece prefab,
        PiecePlacement placement,
        List<OrientedBox> bounds,
        IslandBiome biome,
        bool isMainIsland,
        int mainIndex,
        bool isBeacon)
    {
        return new PlannedPiece
        {
            Prefab = prefab,
            Placement = placement,
            Bounds = bounds,
            Biome = biome,
            IsMainIsland = isMainIsland,
            MainIndex = mainIndex,
            IsBeacon = isBeacon
        };
    }

    private static void ApplySlotContext(PlannedPiece piece, PlannedIslandSlot slot)
    {
        piece.ClusterRuleIndex = slot.ClusterRuleIndex;
        piece.ClusterOccurrenceId = slot.ClusterOccurrenceId;
        piece.IsClusterPiece = slot.ClusterOccurrenceId >= 0;
    }

    private List<ConnectionChoice> BuildConnectionChoices(List<PairCandidate> candidates)
    {
        List<ConnectionChoice> choices = new List<ConnectionChoice>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ConnectionOption option = candidates[i].ConnectionOption;
            bool alreadyAdded = false;
            for (int j = 0; j < choices.Count; j++)
            {
                if (ReferenceEquals(choices[j].Option, option))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                choices.Add(new ConnectionChoice(option));
        }

        return choices;
    }

    private void FilterByScoreTolerance<T>(List<T> candidates, Func<T, float> scoreSelector)
    {
        if (candidates.Count <= 1)
            return;

        float best = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
            best = Mathf.Min(best, scoreSelector(candidates[i]));

        float maximum = best + routeShape.CandidateScoreTolerance;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (scoreSelector(candidates[i]) > maximum)
                candidates.RemoveAt(i);
        }
    }

    private static T SelectWeighted<T>(
        List<T> items,
        Func<T, float> weightSelector,
        Random random) where T : class
    {
        if (items == null || items.Count == 0)
            return null;

        double total = 0d;
        for (int i = 0; i < items.Count; i++)
            total += Math.Max(0d, weightSelector(items[i]));

        if (total <= 0d)
            return null;

        double roll = random.NextDouble() * total;
        double cumulative = 0d;
        for (int i = 0; i < items.Count; i++)
        {
            cumulative += Math.Max(0d, weightSelector(items[i]));
            if (roll <= cumulative)
                return items[i];
        }

        return items[items.Count - 1];
    }

    private PiecePlacement AlignSocketToPose(
        AboveRoutePiece prefab,
        int socketIndex,
        SocketPose targetPose)
    {
        IslandSocket socket = prefab.GetSocket(socketIndex);
        Matrix4x4 relative = GetRelativeMatrix(prefab.transform, socket.transform);
        Quaternion localRotation = relative.rotation;
        Vector3 localPosition = relative.GetColumn(3);

        Quaternion rootRotation = targetPose.Rotation * Quaternion.Inverse(localRotation);
        Vector3 scaledLocalPosition = Vector3.Scale(prefab.transform.localScale, localPosition);
        Vector3 rootPosition = targetPose.Position - rootRotation * scaledLocalPosition;

        return new PiecePlacement(rootPosition, rootRotation);
    }

    private static SocketPose GetSocketPose(PlannedPiece piece, int socketIndex)
    {
        return GetSocketPose(piece.Prefab, piece.Placement, socketIndex);
    }

    private static SocketPose GetSocketPose(
        AboveRoutePiece prefab,
        PiecePlacement placement,
        int socketIndex)
    {
        IslandSocket socket = prefab.GetSocket(socketIndex);
        Matrix4x4 relative = GetRelativeMatrix(prefab.transform, socket.transform);
        Matrix4x4 rootWorld = Matrix4x4.TRS(
            placement.Position,
            placement.Rotation,
            prefab.transform.localScale);
        Matrix4x4 world = rootWorld * relative;
        return new SocketPose(world.GetColumn(3), world.rotation);
    }

    private List<OrientedBox> BuildBounds(
        AboveRoutePiece prefab,
        PiecePlacement placement)
    {
        List<OrientedBox> results = new List<OrientedBox>();
        Matrix4x4 rootWorld = Matrix4x4.TRS(
            placement.Position,
            placement.Rotation,
            prefab.transform.localScale);

        for (int i = 0; i < prefab.PlacementBounds.Count; i++)
        {
            BoxCollider collider = prefab.PlacementBounds[i];
            if (collider == null)
                continue;

            Matrix4x4 relative = GetRelativeMatrix(prefab.transform, collider.transform);
            Matrix4x4 boxWorld =
                rootWorld *
                relative *
                Matrix4x4.TRS(collider.center, Quaternion.identity, collider.size);

            Vector3 x = boxWorld.GetColumn(0);
            Vector3 y = boxWorld.GetColumn(1);
            Vector3 z = boxWorld.GetColumn(2);

            float xMagnitude = x.magnitude;
            float yMagnitude = y.magnitude;
            float zMagnitude = z.magnitude;
            if (xMagnitude < 0.0001f || yMagnitude < 0.0001f || zMagnitude < 0.0001f)
                continue;

            results.Add(new OrientedBox(
                boxWorld.GetColumn(3),
                x / xMagnitude,
                y / yMagnitude,
                z / zMagnitude,
                new Vector3(
                    xMagnitude * 0.5f + routeShape.HorizontalPlacementClearance,
                    yMagnitude * 0.5f + routeShape.VerticalPlacementClearance,
                    zMagnitude * 0.5f + routeShape.HorizontalPlacementClearance)));
        }

        return results;
    }

    private static Matrix4x4 GetRelativeMatrix(Transform root, Transform child)
    {
        return root.worldToLocalMatrix * child.localToWorldMatrix;
    }

    private static bool OverlapsExisting(
        List<OrientedBox> candidateBounds,
        List<PlannedPiece> pieces,
        PlannedPiece skippedPiece)
    {
        for (int pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
        {
            PlannedPiece piece = pieces[pieceIndex];
            if (ReferenceEquals(piece, skippedPiece))
                continue;

            for (int candidateIndex = 0; candidateIndex < candidateBounds.Count; candidateIndex++)
            {
                for (int existingIndex = 0; existingIndex < piece.Bounds.Count; existingIndex++)
                {
                    if (OrientedBoxesOverlap(
                            candidateBounds[candidateIndex],
                            piece.Bounds[existingIndex]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool OverlapsExistingExcept(
        List<OrientedBox> candidateBounds,
        List<PlannedPiece> pieces,
        PlannedPiece skippedA,
        PlannedPiece skippedB)
    {
        for (int pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
        {
            PlannedPiece piece = pieces[pieceIndex];
            if (ReferenceEquals(piece, skippedA) || ReferenceEquals(piece, skippedB))
                continue;

            for (int candidateIndex = 0; candidateIndex < candidateBounds.Count; candidateIndex++)
            {
                for (int existingIndex = 0; existingIndex < piece.Bounds.Count; existingIndex++)
                {
                    if (OrientedBoxesOverlap(
                            candidateBounds[candidateIndex],
                            piece.Bounds[existingIndex]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool OrientedBoxesOverlap(OrientedBox a, OrientedBox b)
    {
        Vector3 delta = b.Center - a.Center;

        if (!OverlapsOnAxis(a, b, delta, a.AxisX)) return false;
        if (!OverlapsOnAxis(a, b, delta, a.AxisY)) return false;
        if (!OverlapsOnAxis(a, b, delta, a.AxisZ)) return false;
        if (!OverlapsOnAxis(a, b, delta, b.AxisX)) return false;
        if (!OverlapsOnAxis(a, b, delta, b.AxisY)) return false;
        if (!OverlapsOnAxis(a, b, delta, b.AxisZ)) return false;

        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisX, b.AxisX))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisX, b.AxisY))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisX, b.AxisZ))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisY, b.AxisX))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisY, b.AxisY))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisY, b.AxisZ))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisZ, b.AxisX))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisZ, b.AxisY))) return false;
        if (!OverlapsOnAxis(a, b, delta, Vector3.Cross(a.AxisZ, b.AxisZ))) return false;

        return true;
    }

    private static bool OverlapsOnAxis(
        OrientedBox a,
        OrientedBox b,
        Vector3 centerDelta,
        Vector3 axis)
    {
        float sqrMagnitude = axis.sqrMagnitude;
        if (sqrMagnitude < 0.000001f)
            return true;

        axis /= Mathf.Sqrt(sqrMagnitude);
        float distance = Mathf.Abs(Vector3.Dot(centerDelta, axis));
        float radiusA =
            a.Extents.x * Mathf.Abs(Vector3.Dot(a.AxisX, axis)) +
            a.Extents.y * Mathf.Abs(Vector3.Dot(a.AxisY, axis)) +
            a.Extents.z * Mathf.Abs(Vector3.Dot(a.AxisZ, axis));
        float radiusB =
            b.Extents.x * Mathf.Abs(Vector3.Dot(b.AxisX, axis)) +
            b.Extents.y * Mathf.Abs(Vector3.Dot(b.AxisY, axis)) +
            b.Extents.z * Mathf.Abs(Vector3.Dot(b.AxisZ, axis));

        return distance <= radiusA + radiusB;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            fallback.y = 0f;
            return fallback.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : fallback.normalized;
        }

        return direction.normalized;
    }

    private static bool RollPercent(Random random, float percent)
    {
        return random.NextDouble() * 100d < Mathf.Clamp(percent, 0f, 100f);
    }

    private static int NextInclusive(Random random, int minimum, int maximum)
    {
        minimum = Mathf.Min(minimum, maximum);
        maximum = Mathf.Max(minimum, maximum);
        return random.Next(minimum, maximum + 1);
    }

    private static int CreateRandomSeed()
    {
        unchecked
        {
            return Environment.TickCount * 397 ^ DateTime.UtcNow.Ticks.GetHashCode();
        }
    }

    private static void Shuffle<T>(List<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            T value = list[i];
            list[i] = list[other];
            list[other] = value;
        }
    }

    private static readonly IslandSelection MainIslandSelection =
        new IslandSelection(false, true, false);

    private static readonly IslandSelection DetourIntermediateSelection =
        new IslandSelection(false, false, false);

    private static readonly IslandSelection DetourEndpointSelection =
        new IslandSelection(true, false, true);

    private static readonly IslandSelection ClusterIslandSelection =
        new IslandSelection(false, true, false);

    private static readonly IslandSelection ClusterEndpointSelection =
        new IslandSelection(true, false, true);

    private static readonly IslandSelection ExactIslandSelection =
        new IslandSelection(false, true, false);

    private sealed class PlacementDiagnostics
    {
        private readonly List<string> islandNames = new List<string>();
        private readonly List<string> connectionNames = new List<string>();
        public int IslandOptions;
        public int ConnectionOptions;
        public int PairCandidates;
        public int IslandChanceRejected;
        public int IslandBiomeRejected;
        public int IslandPhaseRejected;
        public int IslandSizeRejected;
        public int IslandRoleRejected;
        public int IslandRepeatGapRejected;
        public int IslandMaximumRejected;
        public int ConnectionChanceRejected;
        public int ConnectionBiomeRejected;
        public int ConnectionRepeatGapRejected;
        public int ConnectionMaximumRejected;
        public int SourceExitSockets;
        public int NoUsableSourceExit;
        public int IslandsWithoutRouteEntry;
        public int ConnectionsWithoutRouteEntry;
        public int ConnectionsWithoutRouteExit;
        public int SourceConnectionTypeRejected;
        public int IslandConnectionTypeRejected;
        public int ConnectionOverlapRejected;
        public int IslandOverlapRejected;
        public int NoFutureExitRejected;
        public int HeadingRejected;
        public int LateralRejected;
        public int ForwardProgressRejected;
        public int HeightRejected;
        public int ClusterEnvelopeRejected;

        public string BuildSummary()
        {
            List<string> parts = new List<string>();
            string islandList = islandNames.Count == 0
                ? string.Empty
                : $" [{string.Join(", ", islandNames.ToArray())}]";
            string connectionList = connectionNames.Count == 0
                ? string.Empty
                : $" [{string.Join(", ", connectionNames.ToArray())}]";
            parts.Add(
                $"eligible islands {IslandOptions}{islandList}, connections {ConnectionOptions}{connectionList}, valid pairs {PairCandidates}");

            Add(parts, IslandRepeatGapRejected, "islands blocked by Repeat Gap");
            Add(parts, IslandMaximumRejected, "islands blocked by Max/Run");
            Add(parts, IslandBiomeRejected, "islands rejected by biome");
            Add(parts, IslandPhaseRejected, "islands rejected by Linear/Cluster phase usage");
            Add(parts, IslandSizeRejected, "islands rejected by size");
            Add(parts, IslandRoleRejected, "islands rejected by role");
            Add(parts, IslandChanceRejected, "islands at 0% chance");
            Add(parts, ConnectionRepeatGapRejected, "connections blocked by Repeat Gap");
            Add(parts, ConnectionMaximumRejected, "connections blocked by Max/Run");
            Add(parts, ConnectionBiomeRejected, "connections rejected by biome");
            Add(parts, ConnectionChanceRejected, "connections at 0%/missing");
            Add(parts, NoUsableSourceExit, "anchors with no free route-compatible exit");
            Add(parts, IslandsWithoutRouteEntry, "islands with no compatible entry");
            Add(parts, ConnectionsWithoutRouteEntry, "connections with no compatible entry");
            Add(parts, ConnectionsWithoutRouteExit, "connections with no compatible exit");
            Add(parts, SourceConnectionTypeRejected, "source sockets rejecting connection type");
            Add(parts, IslandConnectionTypeRejected, "island entries rejecting connection type");
            Add(parts, ConnectionOverlapRejected, "connection placements overlapping existing bounds");
            Add(parts, IslandOverlapRejected, "island placements overlapping existing bounds");
            Add(parts, NoFutureExitRejected, "island placements lacking a future exit");
            Add(parts, HeadingRejected, "placements outside heading limit");
            Add(parts, LateralRejected, "placements outside lateral limit");
            Add(parts, ForwardProgressRejected, "placements lacking forward progress");
            Add(parts, HeightRejected, "placements outside height limits");
            Add(parts, ClusterEnvelopeRejected, "placements outside cluster width/height envelope");

            return "Diagnostics: " + string.Join("; ", parts.ToArray()) + ".";
        }

        public void AddIslandName(string value)
        {
            AddUniqueName(islandNames, value);
        }

        public void AddConnectionName(string value)
        {
            AddUniqueName(connectionNames, value);
        }

        private static void AddUniqueName(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value) || values.Contains(value))
                return;
            if (values.Count < 8)
                values.Add(value);
        }

        private static void Add(List<string> parts, int count, string label)
        {
            if (count > 0)
                parts.Add($"{count} {label}");
        }
    }

    private sealed class RouteBuildState
    {
        public readonly List<PlannedPiece> Pieces = new List<PlannedPiece>();
        public RunUsageState Usage = new RunUsageState();
        public int DetoursAttempted;
        public int DetoursGenerated;
        public readonly List<string> DetourFailureSummaries = new List<string>();
    }

    private enum UsageBlockReason
    {
        None,
        RepeatGap,
        MaximumPerRun
    }

    private sealed class RunUsageState
    {
        private readonly Dictionary<AboveIsland, int> islandCounts =
            new Dictionary<AboveIsland, int>();
        private readonly Dictionary<AboveIsland, int> islandLastOrdinal =
            new Dictionary<AboveIsland, int>();
        private readonly Dictionary<ConnectionIsland, int> connectionCounts =
            new Dictionary<ConnectionIsland, int>();
        private readonly Dictionary<ConnectionIsland, int> connectionLastOrdinal =
            new Dictionary<ConnectionIsland, int>();

        private int islandOrdinal;
        private int connectionOrdinal;

        public UsageBlockReason GetIslandBlockReason(IslandPoolEntry entry)
        {
            islandCounts.TryGetValue(entry.Prefab, out int count);
            if (entry.MaximumPerRun >= 0 && count >= entry.MaximumPerRun)
                return UsageBlockReason.MaximumPerRun;

            if (entry.MinimumRepeatGap > 0 &&
                islandLastOrdinal.TryGetValue(entry.Prefab, out int lastOrdinal) &&
                islandOrdinal - lastOrdinal - 1 < entry.MinimumRepeatGap)
            {
                return UsageBlockReason.RepeatGap;
            }

            return UsageBlockReason.None;
        }

        public UsageBlockReason GetConnectionBlockReason(ConnectionPoolEntry entry)
        {
            connectionCounts.TryGetValue(entry.Prefab, out int count);
            if (entry.MaximumPerRun >= 0 && count >= entry.MaximumPerRun)
                return UsageBlockReason.MaximumPerRun;

            if (entry.MinimumRepeatGap > 0 &&
                connectionLastOrdinal.TryGetValue(entry.Prefab, out int lastOrdinal) &&
                connectionOrdinal - lastOrdinal - 1 < entry.MinimumRepeatGap)
            {
                return UsageBlockReason.RepeatGap;
            }

            return UsageBlockReason.None;
        }

        public void RecordIsland(AboveIsland prefab)
        {
            islandCounts.TryGetValue(prefab, out int count);
            islandCounts[prefab] = count + 1;
            islandLastOrdinal[prefab] = islandOrdinal;
            islandOrdinal++;
        }

        public void RecordConnection(ConnectionIsland prefab)
        {
            connectionCounts.TryGetValue(prefab, out int count);
            connectionCounts[prefab] = count + 1;
            connectionLastOrdinal[prefab] = connectionOrdinal;
            connectionOrdinal++;
        }

        public RunUsageState Clone()
        {
            RunUsageState clone = new RunUsageState
            {
                islandOrdinal = islandOrdinal,
                connectionOrdinal = connectionOrdinal
            };

            CopyDictionary(islandCounts, clone.islandCounts);
            CopyDictionary(islandLastOrdinal, clone.islandLastOrdinal);
            CopyDictionary(connectionCounts, clone.connectionCounts);
            CopyDictionary(connectionLastOrdinal, clone.connectionLastOrdinal);
            return clone;
        }

        private static void CopyDictionary<TKey>(
            Dictionary<TKey, int> source,
            Dictionary<TKey, int> destination)
        {
            foreach (KeyValuePair<TKey, int> pair in source)
                destination.Add(pair.Key, pair.Value);
        }
    }

    private sealed class PlannedPiece
    {
        public AboveRoutePiece Prefab;
        public PiecePlacement Placement;
        public List<OrientedBox> Bounds;
        public readonly HashSet<int> UsedSockets = new HashSet<int>();
        public IslandBiome Biome;
        public bool IsMainIsland;
        public int MainIndex;
        public bool IsBeacon;
        public bool IsClusterPiece;
        public int ClusterRuleIndex = -1;
        public int ClusterOccurrenceId = -1;
        public PlannedPiece ClusterParent;
    }

    private sealed class PlannedIslandSlot
    {
        public int Index;
        public IslandBiome Biome;
        public int PhaseIndex;
        public IslandSize Size;
        public IslandPhaseUsage PhaseUsage = IslandPhaseUsage.Linear;
        public AboveIsland ForcedPrefab;
        public bool IsBeacon;
        public bool CountsAsMainIsland = true;
        public int ClusterRuleIndex = -1;
        public int ClusterOccurrenceId = -1;
    }

    private sealed class IslandOption
    {
        public readonly AboveIsland Prefab;
        public readonly float ChancePercent;

        public IslandOption(AboveIsland prefab, float chancePercent)
        {
            Prefab = prefab;
            ChancePercent = chancePercent;
        }
    }

    private sealed class ConnectionOption
    {
        public readonly ConnectionIsland Prefab;
        public readonly float ChancePercent;

        public ConnectionOption(ConnectionIsland prefab, float chancePercent)
        {
            Prefab = prefab;
            ChancePercent = chancePercent;
        }
    }

    private enum IslandSelectionRejection
    {
        None,
        Size,
        Role
    }

    private sealed class IslandSelection
    {
        private readonly bool endpointsOnly;
        private readonly bool allowJunctions;
        private readonly bool ignoreSize;

        public IslandSelection(bool endpointsOnly, bool allowJunctions, bool ignoreSize)
        {
            this.endpointsOnly = endpointsOnly;
            this.allowJunctions = allowJunctions;
            this.ignoreSize = ignoreSize;
        }

        public IslandSelectionRejection GetRejection(AboveIsland prefab, IslandSize desiredSize)
        {
            if (!ignoreSize && prefab.Size != desiredSize)
                return IslandSelectionRejection.Size;

            if (endpointsOnly)
            {
                return prefab.Role == IslandRole.DetourEndpoint
                    ? IslandSelectionRejection.None
                    : IslandSelectionRejection.Role;
            }

            if (prefab.Role == IslandRole.DetourEndpoint)
                return IslandSelectionRejection.Role;

            return allowJunctions || prefab.Role == IslandRole.Regular
                ? IslandSelectionRejection.None
                : IslandSelectionRejection.Role;
        }
    }

    private sealed class PairCandidate
    {
        public ConnectionOption ConnectionOption;
        public IslandOption IslandOption;
        public int SourceSocketIndex;
        public int ConnectionEntrySocketIndex;
        public int ConnectionExitSocketIndex;
        public int IslandEntrySocketIndex;
        public PiecePlacement ConnectionPlacement;
        public PiecePlacement IslandPlacement;
        public List<OrientedBox> ConnectionBounds;
        public List<OrientedBox> IslandBounds;
        public float Score;
        public float ForwardProgress;
    }

    private sealed class FirstIslandCandidate
    {
        public IslandOption Option;
        public PiecePlacement Placement;
        public int EntrySocketIndex;
        public List<OrientedBox> Bounds;
        public float Score;
        public float ForwardProgress;
    }

    private sealed class ConnectionChoice
    {
        public readonly ConnectionOption Option;

        public ConnectionChoice(ConnectionOption option)
        {
            Option = option;
        }
    }

    private sealed class ExtraLinkCandidate
    {
        public ConnectionOption Option;
        public PlannedPiece Source;
        public PlannedPiece Target;
        public int SourceSocketIndex;
        public int TargetSocketIndex;
        public int ConnectionEntrySocketIndex;
        public int ConnectionExitSocketIndex;
        public PiecePlacement Placement;
        public List<OrientedBox> Bounds;
        public float Score;
    }

    private sealed class ClusterSnapshot
    {
        public int PieceCount;
        public RunUsageState Usage;
        public readonly Dictionary<PlannedPiece, HashSet<int>> UsedSockets =
            new Dictionary<PlannedPiece, HashSet<int>>();
    }

    private sealed class RouteFrame
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;
        public readonly float HeightOrigin;
        public readonly float MaximumHeadingAngle;
        public readonly float MaximumLateralDrift;
        public float CurrentProgress;

        public RouteFrame(
            Vector3 origin,
            Vector3 forward,
            float heightOrigin,
            float maximumHeadingAngle,
            float maximumLateralDrift,
            float currentProgress)
        {
            Origin = origin;
            Forward = forward.normalized;
            Right = Vector3.Cross(Vector3.up, Forward).normalized;
            HeightOrigin = heightOrigin;
            MaximumHeadingAngle = maximumHeadingAngle;
            MaximumLateralDrift = maximumLateralDrift;
            CurrentProgress = currentProgress;
        }
    }

    private readonly struct PiecePlacement
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public PiecePlacement(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    private readonly struct SocketPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public SocketPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    private readonly struct OrientedBox
    {
        public readonly Vector3 Center;
        public readonly Vector3 AxisX;
        public readonly Vector3 AxisY;
        public readonly Vector3 AxisZ;
        public readonly Vector3 Extents;

        public OrientedBox(
            Vector3 center,
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ,
            Vector3 extents)
        {
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            Extents = extents;
        }
    }
}
