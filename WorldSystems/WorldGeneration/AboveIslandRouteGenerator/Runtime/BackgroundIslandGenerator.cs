using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Random = System.Random;

[ExecuteAlways]
public sealed class BackgroundIslandGenerator : MonoBehaviour
{
    [SerializeField]
    private BackgroundIntegrationSettings integration = new BackgroundIntegrationSettings();

    [SerializeField]
    private BackgroundDistributionSettings distribution = new BackgroundDistributionSettings();

    [SerializeField]
    private BackgroundScenicClusterSettings scenicClusters = new BackgroundScenicClusterSettings();

    [SerializeField]
    private BackgroundPerformanceSettings performance = new BackgroundPerformanceSettings();

    [Tooltip("Editor convenience: changing a chance keeps rows above fixed and redistributes the remainder across rows below.")]
    [SerializeField]
    private bool autoEvenChances;

    [SerializeField]
    private List<BackgroundLayerSettings> layers = new List<BackgroundLayerSettings>
    {
        BackgroundLayerSettings.NearDefaults(),
        BackgroundLayerSettings.MiddleDefaults(),
        BackgroundLayerSettings.FarDefaults()
    };

    [SerializeField]
    private List<BackgroundIslandPoolEntry> backgroundPrefabs = new List<BackgroundIslandPoolEntry>();

    [SerializeField]
    private List<BackgroundLandmarkRule> landmarkRules = new List<BackgroundLandmarkRule>();

    [Header("Events")]
    [SerializeField]
    private UnityEvent onBackgroundGenerated = new UnityEvent();

    [SerializeField]
    private UnityEvent onBackgroundGenerationFailed = new UnityEvent();

    private const string GeneratedRootName = "[Generated Background Islands]";

    private IslandRouteGenerator subscribedRouteGenerator;
    private GameObject generatedRoot;
    private readonly List<BackgroundIsland> generatedInstances = new List<BackgroundIsland>();

    public BackgroundIntegrationSettings Integration => integration;
    public BackgroundDistributionSettings Distribution => distribution;
    public BackgroundScenicClusterSettings ScenicClusters => scenicClusters;
    public BackgroundPerformanceSettings Performance => performance;
    public bool AutoEvenChances => autoEvenChances;
    public IReadOnlyList<BackgroundLayerSettings> Layers => layers;
    public IReadOnlyList<BackgroundIslandPoolEntry> BackgroundPrefabs => backgroundPrefabs;
    public IReadOnlyList<BackgroundLandmarkRule> LandmarkRules => landmarkRules;
    public IReadOnlyList<BackgroundIsland> GeneratedInstances => generatedInstances;
    public int LastUsedSeed { get; private set; }
    public string LastGenerationReport { get; private set; }

    public event Action BackgroundGenerated;
    public event Action BackgroundCleared;

    private void OnEnable()
    {
        RefreshRouteSubscription();
    }

    private void OnDisable()
    {
        RemoveRouteSubscription();
    }

    private void OnValidate()
    {
        if (integration == null)
            integration = new BackgroundIntegrationSettings();
        if (distribution == null)
            distribution = new BackgroundDistributionSettings();
        if (scenicClusters == null)
            scenicClusters = new BackgroundScenicClusterSettings();
        if (performance == null)
            performance = new BackgroundPerformanceSettings();
        if (layers == null)
            layers = new List<BackgroundLayerSettings>();
        if (backgroundPrefabs == null)
            backgroundPrefabs = new List<BackgroundIslandPoolEntry>();
        if (landmarkRules == null)
            landmarkRules = new List<BackgroundLandmarkRule>();

        if (isActiveAndEnabled)
            RefreshRouteSubscription();
    }

    [ContextMenu("Generate Background Islands")]
    public bool GenerateBackground()
    {
        RefreshRouteSubscription();
        if (!ValidateConfiguration(out string validationReport))
        {
            LastGenerationReport = validationReport;
            Debug.LogError($"Background island configuration is invalid:\n{validationReport}", this);
            onBackgroundGenerationFailed.Invoke();
            return false;
        }

        List<RouteAnchor> anchors = CollectRouteAnchors(subscribedRouteGenerator);
        if (anchors.Count < 2)
        {
            LastGenerationReport = "Generate the playable route first. At least two numbered route islands are required.";
            Debug.LogWarning(LastGenerationReport, this);
            onBackgroundGenerationFailed.Invoke();
            return false;
        }

        RoutePath path = new RoutePath(anchors, distribution.ExtendBeforeRoute, distribution.ExtendBeyondRoute);
        int seed = integration.UseRouteSeed
            ? CombineSeeds(subscribedRouteGenerator.LastUsedSeed, integration.SeedOffset)
            : integration.Seed;
        LastUsedSeed = seed;
        Random random = new Random(seed);

        PlanningState state = new PlanningState();
        state.PlayableBounds.AddRange(CollectPlayableBounds(subscribedRouteGenerator));
        state.ExclusionVolumes.AddRange(FindActiveExclusionVolumes());
        state.DensityVolumes.AddRange(FindActiveDensityVolumes());

        int landmarksPlaced = PlanLandmarks(path, random, state);
        int regularPlaced = PlanLayers(path, random, state);
        int satellitesPlaced = PlanScenicClusters(path, random, state, out int scenicGroupsPlaced);
        int standaloneSmallBoosts = ApplyStandaloneSmallScaleBoosts(state);

        ClearGeneratedBackground();
        InstantiatePlan(state.Planned);

        LastGenerationReport =
            $"Generated {state.Planned.Count} background islands " +
            $"({landmarksPlaced} landmarks, {regularPlaced} layer islands, " +
            $"{scenicGroupsPlaced} scenic groups / {satellitesPlaced} satellites, " +
            $"{standaloneSmallBoosts} standalone Small boosts) " +
            $"with seed {seed}. Visual cost: {state.VisualCost}/{FormatBudget(performance.MaximumVisualCost)}. " +
            BuildClusterReport(state);

        if (performance.WarnWhenRequestedCountCannotFit && state.UnfilledRequests > 0)
            Debug.LogWarning($"{LastGenerationReport} {state.UnfilledRequests} requested placements could not fit safely.", this);

        onBackgroundGenerated.Invoke();
        BackgroundGenerated?.Invoke();
        return true;
    }

    [ContextMenu("Clear Generated Background Islands")]
    public void ClearGeneratedBackground()
    {
        generatedInstances.Clear();
        BackgroundCleared?.Invoke();

        if (generatedRoot == null)
        {
            Transform parent = integration != null && integration.GeneratedParent != null
                ? integration.GeneratedParent
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
        IslandRouteGenerator route = ResolveRouteGenerator();

        if (route == null)
            errors.Add("Integration / Route Generator is missing and none was found on a parent.");
        if (layers == null || layers.Count == 0)
            errors.Add("Add at least one Background Layer.");
        if (backgroundPrefabs == null || backgroundPrefabs.Count == 0)
            errors.Add("Add at least one Background Prefab.");

        if (distribution.MaximumPlacementAttempts < 1)
            errors.Add("Distribution / Maximum Placement Attempts must be at least 1.");
        if (distribution.CandidatesPerIsland < 1)
            errors.Add("Distribution / Candidates Per Island must be at least 1.");
        if (distribution.EndIslandIndex != -1 && distribution.EndIslandIndex < distribution.StartIslandIndex)
            errors.Add("Distribution / End Island Index must be -1 or at least Start Island Index.");
        if (Mathf.Approximately(distribution.LeftSideWeight + distribution.RightSideWeight, 0f))
            errors.Add("At least one side weight must be greater than zero.");
        if (distribution.BoostStandaloneSmallIslands &&
            distribution.MaximumStandaloneSmallScaleMultiplier < distribution.MinimumStandaloneSmallScaleMultiplier)
        {
            errors.Add("Distribution / Maximum Standalone Small Scale Multiplier must be at least its minimum.");
        }

        HashSet<BackgroundIsland> seen = new HashSet<BackgroundIsland>();
        float chanceTotal = 0f;
        if (backgroundPrefabs != null)
        {
            for (int i = 0; i < backgroundPrefabs.Count; i++)
            {
                BackgroundIslandPoolEntry entry = backgroundPrefabs[i];
                string label = $"Background Prefab row {i + 1}";
                if (entry == null || entry.Prefab == null)
                {
                    errors.Add($"{label} has no prefab.");
                    continue;
                }

                if (!seen.Add(entry.Prefab))
                    warnings.Add($"{label} repeats prefab '{entry.Prefab.name}'.");
                if (!entry.Prefab.HasUsablePlacementBounds())
                    errors.Add($"{label} ({entry.Prefab.name}) needs at least one Placement Bounds box.");
                if (entry.Prefab.CalculateLocalPlacementRadius() <= 0.01f)
                    errors.Add($"{label} ({entry.Prefab.name}) has unusable Placement Bounds.");
                if (entry.ChancePercent <= 0f)
                    warnings.Add($"{label} has 0% chance and cannot be selected.");
                chanceTotal += Mathf.Max(0f, entry.ChancePercent);
            }
        }

        if (chanceTotal <= 0f)
            errors.Add("Background prefab chances must total more than 0%.");
        else if (!Mathf.Approximately(chanceTotal, 100f))
            warnings.Add($"Background prefab percentages total {chanceTotal:0.##}% instead of 100%. They will be normalized while generating.");

        if (layers != null)
        {
            HashSet<BackgroundIslandLayer> layerKinds = new HashSet<BackgroundIslandLayer>();
            for (int i = 0; i < layers.Count; i++)
            {
                BackgroundLayerSettings layer = layers[i];
                if (layer == null)
                {
                    errors.Add($"Background Layer row {i + 1} is empty.");
                    continue;
                }

                if (!layerKinds.Add(layer.Layer))
                    warnings.Add($"More than one enabled/configured row uses the {layer.Layer} layer.");
                if (layer.MaximumCount < layer.MinimumCount)
                    errors.Add($"{layer.Name} layer Maximum Count must be at least Minimum Count.");
                if (layer.MaximumLateralDistance < layer.MinimumLateralDistance)
                    errors.Add($"{layer.Name} layer Maximum Lateral Distance must be at least its minimum.");
                if (layer.MaximumHeightOffset < layer.MinimumHeightOffset)
                    errors.Add($"{layer.Name} layer Maximum Height Offset must be at least its minimum.");
                if (layer.MaximumScaleMultiplier < layer.MinimumScaleMultiplier)
                    errors.Add($"{layer.Name} layer Maximum Scale Multiplier must be at least its minimum.");
            }
        }

        if (scenicClusters != null && scenicClusters.Enabled)
        {
            if (scenicClusters.MaximumSatelliteIslands < scenicClusters.MinimumSatelliteIslands)
                errors.Add("Scenic Clusters / Maximum Satellite Islands must be at least its minimum.");
            if (scenicClusters.MaximumSpreadRadius < scenicClusters.MinimumSpreadRadius)
                errors.Add("Scenic Clusters / Maximum Spread Radius must be at least its minimum.");
            if (scenicClusters.MaximumHeightOffset < scenicClusters.MinimumHeightOffset)
                errors.Add("Scenic Clusters / Maximum Height Offset must be at least its minimum.");
            if (scenicClusters.MaximumScaleMultiplier < scenicClusters.MinimumScaleMultiplier)
                errors.Add("Scenic Clusters / Maximum Scale Multiplier must be at least its minimum.");
            if (performance.MaximumBackgroundIslands > 0 &&
                scenicClusters.ReservedSatelliteSlots >= performance.MaximumBackgroundIslands)
            {
                warnings.Add(
                    "Scenic Clusters / Reserved Satellite Slots uses the complete island-count budget. " +
                    "1.5 will still allow one center island, but a smaller reservation is recommended.");
            }
            if (scenicClusters.ClusterChance >= 100f && scenicClusters.MaximumClustersPerRun > 0)
            {
                warnings.Add(
                    $"Scenic Clusters is 100%, but Maximum Clusters Per Run is " +
                    $"{scenicClusters.MaximumClustersPerRun}. Set the maximum to 0 if every " +
                    "eligible center should be attempted.");
            }
        }

        if (landmarkRules != null)
        {
            for (int i = 0; i < landmarkRules.Count; i++)
            {
                BackgroundLandmarkRule rule = landmarkRules[i];
                if (rule == null || !rule.Enabled)
                    continue;
                if (rule.Prefab == null)
                    errors.Add($"Landmark Rule {i + 1} has no prefab.");
                else if (!rule.Prefab.HasUsablePlacementBounds())
                    errors.Add($"Landmark Rule {i + 1} prefab ({rule.Prefab.name}) needs Placement Bounds.");
                if (rule.MaximumRouteIndex != -1 && rule.MaximumRouteIndex < rule.MinimumRouteIndex)
                    errors.Add($"Landmark Rule {i + 1} maximum route index must be -1 or at least its minimum.");
            }
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
        if (builder.Length == 0)
            builder.Append("Configuration is valid.");

        report = builder.ToString().TrimEnd();
        return errors.Count == 0;
    }

    private int PlanLandmarks(RoutePath path, Random random, PlanningState state)
    {
        int placed = 0;
        if (landmarkRules == null)
            return placed;

        for (int ruleIndex = 0; ruleIndex < landmarkRules.Count; ruleIndex++)
        {
            BackgroundLandmarkRule rule = landmarkRules[ruleIndex];
            if (rule == null || !rule.Enabled || rule.Prefab == null)
                continue;

            int maximum = Mathf.Max(1, rule.MaximumPerRun);
            for (int occurrence = 0; occurrence < maximum; occurrence++)
            {
                if (!RollPercent(random, rule.ChancePercent) || IsBudgetFull(state, rule.Prefab))
                    continue;

                BackgroundLayerSettings layer = FindLayer(rule.Layer);
                if (layer == null || !layer.Enabled || !rule.Prefab.SupportsLayer(rule.Layer))
                    continue;

                int maxIndex = rule.MaximumRouteIndex == -1 ? path.MaximumMainIndex : rule.MaximumRouteIndex;
                int minIndex = Mathf.Clamp(rule.MinimumRouteIndex, path.MinimumMainIndex, path.MaximumMainIndex);
                maxIndex = Mathf.Clamp(maxIndex, minIndex, path.MaximumMainIndex);
                float distance = path.RandomDistanceForIndexRange(random, minIndex, maxIndex);

                if (TryPlanSpecific(path, random, state, rule.Prefab, layer, distance, rule.Side,
                    rule.UseLayerDistance ? layer.MinimumLateralDistance : rule.MinimumLateralDistance,
                    rule.UseLayerDistance ? layer.MaximumLateralDistance : rule.MaximumLateralDistance,
                    rule.MinimumHeightOffset, rule.MaximumHeightOffset,
                    rule.MinimumScaleMultiplier, rule.MaximumScaleMultiplier,
                    true, out PlannedBackgroundIsland planned))
                {
                    state.Planned.Add(planned);
                    RegisterPlan(state, planned, -1);
                    placed++;
                }
                else
                {
                    state.UnfilledRequests++;
                }
            }
        }

        return placed;
    }

    private int PlanLayers(RoutePath path, Random random, PlanningState state)
    {
        int placed = 0;
        if (layers == null)
            return placed;

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            BackgroundLayerSettings layer = layers[layerIndex];
            if (layer == null || !layer.Enabled)
                continue;

            int requested = Mathf.Clamp(
                Mathf.RoundToInt(path.ExtendedLength / 100f * layer.DensityPer100Units),
                layer.MinimumCount,
                layer.MaximumCount);

            int cellCount = Mathf.Max(1, Mathf.CeilToInt(path.ExtendedLength / Mathf.Max(1f, distribution.CellSize)));
            int[] perCell = new int[cellCount];
            List<int> availableCells = new List<int>();
            for (int cell = 0; cell < cellCount; cell++)
            {
                if (!RollPercent(random, layer.EmptyCellChance))
                    availableCells.Add(cell);
            }
            if (availableCells.Count == 0)
                availableCells.Add(random.Next(0, cellCount));

            for (int request = 0; request < requested; request++)
            {
                if (IsRegularLayerBudgetFull(state))
                    break;

                int cell = ChooseCell(random, availableCells, perCell, layer.MaximumIslandsPerCell);
                if (cell < 0)
                {
                    state.UnfilledRequests += requested - request;
                    break;
                }

                float start = -distribution.ExtendBeforeRoute + cell * distribution.CellSize;
                float end = Mathf.Min(start + distribution.CellSize, path.Length + distribution.ExtendBeyondRoute);
                float distance = RandomRange(random, start, Mathf.Max(start, end));
                IslandBiome biome = path.Sample(distance, random, distribution.BiomeTransitionBlendDistance).Biome;
                int entryIndex = ChoosePoolEntry(random, state, biome, layer.Layer, layer.MaximumSize);
                if (entryIndex < 0)
                {
                    state.UnfilledRequests++;
                    continue;
                }

                BackgroundIsland prefab = backgroundPrefabs[entryIndex].Prefab;
                if (TryPlanSpecific(path, random, state, prefab, layer, distance, BackgroundSidePreference.Either,
                    layer.MinimumLateralDistance, layer.MaximumLateralDistance,
                    layer.MinimumHeightOffset, layer.MaximumHeightOffset,
                    layer.MinimumScaleMultiplier, layer.MaximumScaleMultiplier,
                    false, out PlannedBackgroundIsland planned))
                {
                    planned.PoolEntryIndex = entryIndex;
                    planned.CellIndex = cell;
                    state.Planned.Add(planned);
                    RegisterPlan(state, planned, entryIndex);
                    perCell[cell]++;
                    placed++;
                }
                else
                {
                    state.UnfilledRequests++;
                }
            }
        }

        return placed;
    }

    private int PlanScenicClusters(
        RoutePath path,
        Random random,
        PlanningState state,
        out int clusters)
    {
        clusters = 0;
        if (!scenicClusters.Enabled)
            return 0;

        List<PlannedBackgroundIsland> centers = new List<PlannedBackgroundIsland>(state.Planned);
        Shuffle(random, centers);
        int satellites = 0;
        bool unlimitedClusters = scenicClusters.MaximumClustersPerRun <= 0;

        for (int i = 0; i < centers.Count; i++)
        {
            if (!unlimitedClusters && clusters >= scenicClusters.MaximumClustersPerRun)
            {
                state.ClusterCapSkipped += centers.Count - i;
                break;
            }

            PlannedBackgroundIsland center = centers[i];
            state.ClusterCentersConsidered++;
            if (!RollPercent(random, scenicClusters.ClusterChance))
            {
                state.ClusterChanceSkipped++;
                continue;
            }

            BackgroundLayerSettings layer = FindLayer(center.Layer);
            if (layer == null)
            {
                state.ClusterMissingLayer++;
                continue;
            }

            int requested = RandomRangeInclusive(random,
                scenicClusters.MinimumSatelliteIslands,
                Mathf.Max(scenicClusters.MinimumSatelliteIslands, scenicClusters.MaximumSatelliteIslands));
            int placedHere = 0;
            int centerPlanIndex = FindMatchingPlanIndex(state.Planned, center);
            float reservedCenterRadius = center.Radius;
            if (centerPlanIndex >= 0 && center.StandaloneSmallScaleMultiplier > 1f)
            {
                center.Radius = center.Prefab.CalculateLocalPlacementRadius() * center.Scale;
                PlannedBackgroundIsland unboostedCenter = state.Planned[centerPlanIndex];
                unboostedCenter.Radius = center.Radius;
                state.Planned[centerPlanIndex] = unboostedCenter;
            }

            for (int satellite = 0; satellite < requested; satellite++)
            {
                if (IsGlobalBudgetFull(state))
                {
                    state.ClusterBudgetBlocked++;
                    break;
                }

                IslandBiome biome = scenicClusters.KeepCenterBiome
                    ? center.Biome
                    : path.Sample(center.RouteDistance, random, distribution.BiomeTransitionBlendDistance).Biome;
                int entryIndex = ChoosePoolEntry(
                    random,
                    state,
                    biome,
                    center.Layer,
                    scenicClusters.MaximumSatelliteSize,
                    scenicClusters.IgnoreRepeatGapForSatellites);
                if (entryIndex < 0)
                {
                    state.ClusterNoEligiblePrefab++;
                    continue;
                }

                BackgroundIsland prefab = backgroundPrefabs[entryIndex].Prefab;
                float prefabScale = RandomRange(random, prefab.MinimumScaleMultiplier, prefab.MaximumScaleMultiplier);
                float scale = center.Scale * RandomRange(random,
                    scenicClusters.MinimumScaleMultiplier,
                    Mathf.Max(scenicClusters.MinimumScaleMultiplier, scenicClusters.MaximumScaleMultiplier)) * prefabScale;
                float radius = prefab.CalculateLocalPlacementRadius() * scale;

                bool found = false;
                for (int attempt = 0; attempt < distribution.MaximumPlacementAttempts; attempt++)
                {
                    float angle = RandomRange(random, 0f, 360f) * Mathf.Deg2Rad;
                    float requestedSpread = RandomRange(
                        random,
                        scenicClusters.MinimumSpreadRadius,
                        scenicClusters.MaximumSpreadRadius);
                    float minimumNonOverlappingSpread =
                        center.Radius + radius + scenicClusters.MinimumSurfaceGap;
                    float spread = Mathf.Max(requestedSpread, minimumNonOverlappingSpread);
                    Vector3 position = center.Position + new Vector3(Mathf.Cos(angle) * spread,
                        RandomRange(random, scenicClusters.MinimumHeightOffset, scenicClusters.MaximumHeightOffset),
                        Mathf.Sin(angle) * spread);

                    if (!IsSatelliteCandidateValid(path, state, center, position, radius))
                        continue;

                    PlannedBackgroundIsland planned = CreatePlan(prefab, center.Layer, biome, position, scale,
                        center.RouteDistance, entryIndex, -1, false, true, random);
                    state.Planned.Add(planned);
                    RegisterPlan(state, planned, entryIndex);
                    placedHere++;
                    satellites++;
                    found = true;
                    break;
                }

                if (!found)
                {
                    state.UnfilledRequests++;
                    state.ClusterPlacementFailed++;
                }
            }

            if (placedHere > 0)
            {
                clusters++;
                if (centerPlanIndex >= 0)
                {
                    PlannedBackgroundIsland clusteredCenter = state.Planned[centerPlanIndex];
                    clusteredCenter.IsClusterCenter = true;
                    clusteredCenter.StandaloneSmallScaleMultiplier = 1f;
                    state.Planned[centerPlanIndex] = clusteredCenter;
                }
            }
            else if (centerPlanIndex >= 0 && reservedCenterRadius > center.Radius)
            {
                PlannedBackgroundIsland restoredCenter = state.Planned[centerPlanIndex];
                restoredCenter.Radius = reservedCenterRadius;
                state.Planned[centerPlanIndex] = restoredCenter;
            }
        }

        return satellites;
    }

    private static int FindMatchingPlanIndex(List<PlannedBackgroundIsland> plans, PlannedBackgroundIsland target)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (SamePlan(plans[i], target))
                return i;
        }

        return -1;
    }

    private static int ApplyStandaloneSmallScaleBoosts(PlanningState state)
    {
        int applied = 0;
        for (int i = 0; i < state.Planned.Count; i++)
        {
            PlannedBackgroundIsland planned = state.Planned[i];
            if (planned.IsLandmark || planned.IsSatellite || planned.IsClusterCenter ||
                planned.StandaloneSmallScaleMultiplier <= 1f)
            {
                continue;
            }

            planned.Scale *= planned.StandaloneSmallScaleMultiplier;
            state.Planned[i] = planned;
            applied++;
        }

        return applied;
    }

    private bool IsSatelliteCandidateValid(
        RoutePath path,
        PlanningState state,
        PlannedBackgroundIsland center,
        Vector3 position,
        float radius)
    {
        if (radius <= 0.01f)
            return false;

        if (path.HorizontalDistanceToPath(position) < distribution.RouteCorridorClearance + radius)
            return false;

        for (int i = 0; i < state.PlayableBounds.Count; i++)
        {
            PlacementSphere playable = state.PlayableBounds[i];
            float required = radius + playable.Radius + distribution.PlayableBoundsClearance;
            if ((position - playable.Center).sqrMagnitude < required * required)
                return false;
        }

        for (int i = 0; i < state.Planned.Count; i++)
        {
            PlannedBackgroundIsland other = state.Planned[i];
            bool isCenter = SamePlan(other, center);
            float gap = isCenter
                ? scenicClusters.MinimumSurfaceGap
                : distribution.BackgroundBoundsClearance;
            float required = radius + other.Radius + gap;
            if ((position - other.Position).sqrMagnitude < required * required)
                return false;
        }

        for (int i = 0; i < state.ExclusionVolumes.Count; i++)
        {
            if (state.ExclusionVolumes[i] != null &&
                state.ExclusionVolumes[i].IntersectsSphere(position, radius))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SamePlan(PlannedBackgroundIsland first, PlannedBackgroundIsland second)
    {
        return first.Prefab == second.Prefab &&
               first.Layer == second.Layer &&
               first.Position == second.Position &&
               Mathf.Approximately(first.Scale, second.Scale) &&
               first.IsLandmark == second.IsLandmark &&
               first.IsSatellite == second.IsSatellite;
    }

    private bool TryPlanSpecific(
        RoutePath path,
        Random random,
        PlanningState state,
        BackgroundIsland prefab,
        BackgroundLayerSettings layer,
        float preferredRouteDistance,
        BackgroundSidePreference sidePreference,
        float minimumLateral,
        float maximumLateral,
        float minimumHeight,
        float maximumHeight,
        float minimumScale,
        float maximumScale,
        bool landmark,
        out PlannedBackgroundIsland planned)
    {
        planned = default(PlannedBackgroundIsland);
        if (prefab == null || IsBudgetFull(state, prefab))
            return false;

        List<PlacementCandidate> candidates = new List<PlacementCandidate>();
        int desiredCandidates = Mathf.Max(1, distribution.CandidatesPerIsland);
        float localRadius = prefab.CalculateLocalPlacementRadius();

        for (int attempt = 0; attempt < distribution.MaximumPlacementAttempts && candidates.Count < desiredCandidates; attempt++)
        {
            float distanceJitter = distribution.CellSize * 0.4f;
            float routeDistance = preferredRouteDistance + RandomRange(random, -distanceJitter, distanceJitter);
            RouteSample sample = path.Sample(routeDistance, random, distribution.BiomeTransitionBlendDistance);
            if (!prefab.SupportsBiome(sample.Biome) || !prefab.SupportsLayer(layer.Layer))
                continue;

            int side = ChooseSide(random, state, sidePreference);
            float lateral = RandomRange(random, minimumLateral, Mathf.Max(minimumLateral, maximumLateral));
            float height = RandomRange(random, minimumHeight, Mathf.Max(minimumHeight, maximumHeight));
            float layerScale = RandomRange(random, minimumScale, Mathf.Max(minimumScale, maximumScale));
            float prefabScale = RandomRange(random, prefab.MinimumScaleMultiplier, prefab.MaximumScaleMultiplier);
            float scale = layerScale * prefabScale;
            float standaloneSmallScaleMultiplier = !landmark &&
                prefab.Size == BackgroundIslandSize.Small &&
                distribution.BoostStandaloneSmallIslands
                ? RandomRange(random,
                    Mathf.Max(1f, distribution.MinimumStandaloneSmallScaleMultiplier),
                    Mathf.Max(distribution.MinimumStandaloneSmallScaleMultiplier,
                        distribution.MaximumStandaloneSmallScaleMultiplier))
                : 1f;
            float radius = localRadius * scale * standaloneSmallScaleMultiplier;
            Vector3 position = sample.Position + sample.Right * (side * lateral) + Vector3.up * height;

            if (!IsCandidateValid(path, state, position, radius, layer.MinimumSpacing))
                continue;

            float weight = GetDensityWeight(state, position);
            if (weight <= 0f)
                continue;

            candidates.Add(new PlacementCandidate
            {
                Position = position,
                RouteDistance = routeDistance,
                Biome = sample.Biome,
                Side = side,
                Scale = scale,
                Radius = radius,
                StandaloneSmallScaleMultiplier = standaloneSmallScaleMultiplier,
                Weight = weight
            });
        }

        if (candidates.Count == 0)
            return false;

        PlacementCandidate selected = WeightedCandidate(random, candidates);
        planned = CreatePlan(prefab, layer.Layer, selected.Biome, selected.Position, selected.Scale,
            selected.RouteDistance, -1, -1, landmark, false, random);
        planned.Radius = selected.Radius;
        planned.StandaloneSmallScaleMultiplier = selected.StandaloneSmallScaleMultiplier;
        planned.Side = selected.Side;
        return true;
    }

    private bool IsCandidateValid(RoutePath path, PlanningState state, Vector3 position, float radius, float spacing)
    {
        if (radius <= 0.01f)
            return false;

        float corridorDistance = path.HorizontalDistanceToPath(position);
        if (corridorDistance < distribution.RouteCorridorClearance + radius)
            return false;

        for (int i = 0; i < state.PlayableBounds.Count; i++)
        {
            PlacementSphere playable = state.PlayableBounds[i];
            float required = radius + playable.Radius + distribution.PlayableBoundsClearance;
            if ((position - playable.Center).sqrMagnitude < required * required)
                return false;
        }

        for (int i = 0; i < state.Planned.Count; i++)
        {
            PlannedBackgroundIsland other = state.Planned[i];
            float required = radius + other.Radius + distribution.BackgroundBoundsClearance + spacing;
            if ((position - other.Position).sqrMagnitude < required * required)
                return false;
        }

        for (int i = 0; i < state.ExclusionVolumes.Count; i++)
        {
            if (state.ExclusionVolumes[i] != null && state.ExclusionVolumes[i].IntersectsSphere(position, radius))
                return false;
        }

        return true;
    }

    private int ChoosePoolEntry(
        Random random,
        PlanningState state,
        IslandBiome biome,
        BackgroundIslandLayer layer,
        BackgroundIslandSize maximumSize,
        bool ignoreRepeatGap = false)
    {
        List<int> eligible = new List<int>();
        float total = 0f;
        for (int i = 0; i < backgroundPrefabs.Count; i++)
        {
            BackgroundIslandPoolEntry entry = backgroundPrefabs[i];
            if (entry == null || entry.Prefab == null || entry.ChancePercent <= 0f)
                continue;
            if (!entry.Prefab.SupportsBiome(biome) || !entry.Prefab.SupportsLayer(layer) ||
                (int)entry.Prefab.Size > (int)maximumSize)
                continue;
            if (entry.MaximumPerRun >= 0 && GetUsageCount(state, i) >= entry.MaximumPerRun)
                continue;
            if (!ignoreRepeatGap && entry.MinimumRepeatGap > 0 && state.SelectionHistory.Count > 0)
            {
                int last = state.SelectionHistory.LastIndexOf(i);
                if (last >= 0 && state.SelectionHistory.Count - last - 1 < entry.MinimumRepeatGap)
                    continue;
            }
            if (IsBudgetFull(state, entry.Prefab))
                continue;

            eligible.Add(i);
            total += entry.ChancePercent;
        }

        if (eligible.Count == 0 || total <= 0f)
            return -1;

        float roll = RandomRange(random, 0f, total);
        for (int i = 0; i < eligible.Count; i++)
        {
            int index = eligible[i];
            roll -= backgroundPrefabs[index].ChancePercent;
            if (roll <= 0f)
                return index;
        }

        return eligible[eligible.Count - 1];
    }

    private void InstantiatePlan(List<PlannedBackgroundIsland> plan)
    {
        Transform parent = integration.GeneratedParent != null ? integration.GeneratedParent : transform;
        generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.transform.SetParent(parent, false);

        for (int i = 0; i < plan.Count; i++)
        {
            PlannedBackgroundIsland item = plan[i];
            BackgroundIsland instance = Instantiate(item.Prefab, item.Position, item.Rotation, generatedRoot.transform);
            instance.transform.localScale = instance.transform.localScale * item.Scale;
            instance.name = item.IsLandmark
                ? $"Landmark - {item.Prefab.name}"
                : item.IsSatellite
                    ? $"{item.Layer} Satellite - {item.Prefab.name}"
                    : $"{item.Layer} - {item.Prefab.name}";
            instance.InitializeGeneratedContext(item.Biome, item.Layer);

            for (int boundsIndex = 0; boundsIndex < instance.PlacementBounds.Count; boundsIndex++)
            {
                BoxCollider bounds = instance.PlacementBounds[boundsIndex];
                if (bounds != null)
                    bounds.enabled = false;
            }

            if (performance.DisableGeneratedColliders)
            {
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    colliders[colliderIndex].enabled = false;
            }

            BackgroundLayerSettings layer = FindLayer(item.Layer);
            bool castShadows = layer == null || layer.CastShadows;
            if ((item.Layer == BackgroundIslandLayer.Far && performance.DisableFarShadows) || !castShadows)
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    renderers[rendererIndex].shadowCastingMode = ShadowCastingMode.Off;
            }

            if (item.Layer == BackgroundIslandLayer.Far && performance.WarnWhenFarPrefabHasNoLODGroup &&
                instance.GetComponentInChildren<LODGroup>(true) == null)
            {
                Debug.LogWarning($"Far background prefab '{item.Prefab.name}' has no LODGroup.", instance);
            }

            generatedInstances.Add(instance);
        }
    }

    private void HandleRouteGenerated()
    {
        if (!integration.GenerateWithRoute)
            return;
        if (!Application.isPlaying && !integration.GenerateEditModePreview)
            return;
        GenerateBackground();
    }

    private void HandleRouteCleared()
    {
        if (integration.ClearWithRoute)
            ClearGeneratedBackground();
    }

    private IslandRouteGenerator ResolveRouteGenerator()
    {
        if (integration != null && integration.RouteGenerator != null)
            return integration.RouteGenerator;
        return GetComponentInParent<IslandRouteGenerator>();
    }

    private void RefreshRouteSubscription()
    {
        IslandRouteGenerator resolved = ResolveRouteGenerator();
        if (resolved == subscribedRouteGenerator)
            return;

        RemoveRouteSubscription();
        subscribedRouteGenerator = resolved;
        if (subscribedRouteGenerator != null)
        {
            subscribedRouteGenerator.RouteGenerated += HandleRouteGenerated;
            subscribedRouteGenerator.RouteCleared += HandleRouteCleared;
        }
    }

    private void RemoveRouteSubscription()
    {
        if (subscribedRouteGenerator == null)
            return;
        subscribedRouteGenerator.RouteGenerated -= HandleRouteGenerated;
        subscribedRouteGenerator.RouteCleared -= HandleRouteCleared;
        subscribedRouteGenerator = null;
    }

    private List<RouteAnchor> CollectRouteAnchors(IslandRouteGenerator route)
    {
        List<RouteAnchor> anchors = new List<RouteAnchor>();
        IReadOnlyList<AboveRoutePiece> pieces = route.GeneratedInstances;
        int start = Mathf.Max(1, distribution.StartIslandIndex);
        int end = distribution.EndIslandIndex;

        for (int i = 0; i < pieces.Count; i++)
        {
            AboveRoutePiece piece = pieces[i];
            if (piece == null || !piece.HasGeneratedContext)
                continue;
            if (!piece.IsGeneratedMainIsland && !piece.IsGeneratedBeacon)
                continue;

            int index = piece.IsGeneratedBeacon ? int.MaxValue : piece.GeneratedMainIndex;
            if (!piece.IsGeneratedBeacon && (index < start || (end != -1 && index > end)))
                continue;
            if (piece.IsGeneratedBeacon && end != -1)
                continue;

            anchors.Add(new RouteAnchor
            {
                Position = piece.transform.position,
                Biome = piece.GeneratedBiome,
                MainIndex = index
            });
        }

        anchors.Sort((a, b) => a.MainIndex.CompareTo(b.MainIndex));
        return anchors;
    }

    private static List<PlacementSphere> CollectPlayableBounds(IslandRouteGenerator route)
    {
        List<PlacementSphere> spheres = new List<PlacementSphere>();
        IReadOnlyList<AboveRoutePiece> pieces = route.GeneratedInstances;
        for (int i = 0; i < pieces.Count; i++)
        {
            AboveRoutePiece piece = pieces[i];
            if (piece == null)
                continue;
            for (int j = 0; j < piece.PlacementBounds.Count; j++)
            {
                BoxCollider box = piece.PlacementBounds[j];
                if (box == null)
                    continue;
                Vector3 half = Vector3.Scale(box.size * 0.5f, Abs(box.transform.lossyScale));
                spheres.Add(new PlacementSphere
                {
                    Center = box.transform.TransformPoint(box.center),
                    Radius = half.magnitude
                });
            }
        }
        return spheres;
    }

    private static List<BackgroundExclusionVolume> FindActiveExclusionVolumes()
    {
        BackgroundExclusionVolume[] found = FindObjectsOfType<BackgroundExclusionVolume>(true);
        return new List<BackgroundExclusionVolume>(found);
    }

    private static List<BackgroundDensityVolume> FindActiveDensityVolumes()
    {
        BackgroundDensityVolume[] found = FindObjectsOfType<BackgroundDensityVolume>(true);
        return new List<BackgroundDensityVolume>(found);
    }

    private BackgroundLayerSettings FindLayer(BackgroundIslandLayer layer)
    {
        if (layers == null)
            return null;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i] != null && layers[i].Layer == layer)
                return layers[i];
        }
        return null;
    }

    private float GetDensityWeight(PlanningState state, Vector3 position)
    {
        float weight = 1f;
        for (int i = 0; i < state.DensityVolumes.Count; i++)
        {
            BackgroundDensityVolume volume = state.DensityVolumes[i];
            if (volume != null && volume.Contains(position))
                weight *= volume.DensityMultiplier;
        }
        return weight;
    }

    private int ChooseSide(Random random, PlanningState state, BackgroundSidePreference preference)
    {
        if (preference == BackgroundSidePreference.Left)
            return -1;
        if (preference == BackgroundSidePreference.Right)
            return 1;

        float left = Mathf.Max(0f, distribution.LeftSideWeight) / (1f + state.LeftCount * 0.35f);
        float right = Mathf.Max(0f, distribution.RightSideWeight) / (1f + state.RightCount * 0.35f);
        return RandomRange(random, 0f, left + right) < left ? -1 : 1;
    }

    private static int ChooseCell(Random random, List<int> cells, int[] perCell, int maximumPerCell)
    {
        List<int> eligible = new List<int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (perCell[cells[i]] < Mathf.Max(1, maximumPerCell))
                eligible.Add(cells[i]);
        }
        return eligible.Count == 0 ? -1 : eligible[random.Next(0, eligible.Count)];
    }

    private static PlacementCandidate WeightedCandidate(Random random, List<PlacementCandidate> candidates)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
            total += candidates[i].Weight;
        float roll = RandomRange(random, 0f, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Weight;
            if (roll <= 0f)
                return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    private static PlannedBackgroundIsland CreatePlan(BackgroundIsland prefab, BackgroundIslandLayer layer,
        IslandBiome biome, Vector3 position, float scale, float routeDistance, int poolEntryIndex,
        int cellIndex, bool landmark, bool satellite, Random random)
    {
        return new PlannedBackgroundIsland
        {
            Prefab = prefab,
            Layer = layer,
            Biome = biome,
            Position = position,
            Rotation = prefab.AllowRandomYaw
                ? Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f)
                : prefab.transform.rotation,
            Scale = scale,
            Radius = prefab.CalculateLocalPlacementRadius() * scale,
            RouteDistance = routeDistance,
            PoolEntryIndex = poolEntryIndex,
            CellIndex = cellIndex,
            IsLandmark = landmark,
            IsSatellite = satellite
        };
    }

    private static void RegisterPlan(PlanningState state, PlannedBackgroundIsland planned, int entryIndex)
    {
        state.VisualCost += planned.Prefab.VisualCost;
        if (planned.Side < 0)
            state.LeftCount++;
        else if (planned.Side > 0)
            state.RightCount++;

        if (entryIndex >= 0)
        {
            state.SelectionHistory.Add(entryIndex);
            if (!state.UsageCounts.ContainsKey(entryIndex))
                state.UsageCounts.Add(entryIndex, 0);
            state.UsageCounts[entryIndex]++;
        }
    }

    private bool IsGlobalBudgetFull(PlanningState state)
    {
        if (performance.MaximumBackgroundIslands > 0 && state.Planned.Count >= performance.MaximumBackgroundIslands)
            return true;
        return performance.MaximumVisualCost > 0 && state.VisualCost >= performance.MaximumVisualCost;
    }

    private bool IsRegularLayerBudgetFull(PlanningState state)
    {
        if (IsGlobalBudgetFull(state))
            return true;

        if (!scenicClusters.Enabled || scenicClusters.ReservedSatelliteSlots <= 0 ||
            performance.MaximumBackgroundIslands <= 0)
        {
            return false;
        }

        int centerBudget = Mathf.Max(
            1,
            performance.MaximumBackgroundIslands - scenicClusters.ReservedSatelliteSlots);
        return state.Planned.Count >= centerBudget;
    }

    private bool IsBudgetFull(PlanningState state, BackgroundIsland prefab)
    {
        if (IsGlobalBudgetFull(state))
            return true;
        return performance.MaximumVisualCost > 0 && state.VisualCost + prefab.VisualCost > performance.MaximumVisualCost;
    }

    private static int GetUsageCount(PlanningState state, int entryIndex)
    {
        return state.UsageCounts.TryGetValue(entryIndex, out int count) ? count : 0;
    }

    private static string FormatBudget(int budget)
    {
        return budget <= 0 ? "unlimited" : budget.ToString();
    }

    private static string BuildClusterReport(PlanningState state)
    {
        return
            $"Cluster attempts: {state.ClusterCentersConsidered} centers; " +
            $"chance skipped {state.ClusterChanceSkipped}, cap skipped {state.ClusterCapSkipped}, " +
            $"no eligible prefab {state.ClusterNoEligiblePrefab}, " +
            $"could not fit {state.ClusterPlacementFailed}, budget blocked {state.ClusterBudgetBlocked}.";
    }

    private static int CombineSeeds(int first, int second)
    {
        unchecked
        {
            return (first * 397) ^ second;
        }
    }

    private static bool RollPercent(Random random, float chance)
    {
        return random.NextDouble() * 100.0 < Mathf.Clamp(chance, 0f, 100f);
    }

    private static float RandomRange(Random random, float minimum, float maximum)
    {
        return minimum + (float)random.NextDouble() * (maximum - minimum);
    }

    private static int RandomRangeInclusive(Random random, int minimum, int maximum)
    {
        return random.Next(minimum, maximum + 1);
    }

    private static void Shuffle<T>(Random random, List<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swap = random.Next(0, i + 1);
            T temporary = values[i];
            values[i] = values[swap];
            values[swap] = temporary;
        }
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private sealed class PlanningState
    {
        public readonly List<PlannedBackgroundIsland> Planned = new List<PlannedBackgroundIsland>();
        public readonly List<PlacementSphere> PlayableBounds = new List<PlacementSphere>();
        public readonly List<BackgroundExclusionVolume> ExclusionVolumes = new List<BackgroundExclusionVolume>();
        public readonly List<BackgroundDensityVolume> DensityVolumes = new List<BackgroundDensityVolume>();
        public readonly Dictionary<int, int> UsageCounts = new Dictionary<int, int>();
        public readonly List<int> SelectionHistory = new List<int>();
        public int VisualCost;
        public int LeftCount;
        public int RightCount;
        public int UnfilledRequests;
        public int ClusterCentersConsidered;
        public int ClusterChanceSkipped;
        public int ClusterCapSkipped;
        public int ClusterMissingLayer;
        public int ClusterNoEligiblePrefab;
        public int ClusterPlacementFailed;
        public int ClusterBudgetBlocked;
    }

    private struct PlacementSphere
    {
        public Vector3 Center;
        public float Radius;
    }

    private struct PlacementCandidate
    {
        public Vector3 Position;
        public float RouteDistance;
        public IslandBiome Biome;
        public int Side;
        public float Scale;
        public float Radius;
        public float StandaloneSmallScaleMultiplier;
        public float Weight;
    }

    private struct PlannedBackgroundIsland
    {
        public BackgroundIsland Prefab;
        public BackgroundIslandLayer Layer;
        public IslandBiome Biome;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
        public float Radius;
        public float RouteDistance;
        public int PoolEntryIndex;
        public int CellIndex;
        public int Side;
        public bool IsLandmark;
        public bool IsSatellite;
        public bool IsClusterCenter;
        public float StandaloneSmallScaleMultiplier;
    }

    private struct RouteAnchor
    {
        public Vector3 Position;
        public IslandBiome Biome;
        public int MainIndex;
    }

    private struct RouteSample
    {
        public Vector3 Position;
        public Vector3 Right;
        public IslandBiome Biome;
    }

    private sealed class RoutePath
    {
        private readonly List<RouteAnchor> anchors;
        private readonly float[] cumulative;

        public float Length { get; }
        public float ExtendedLength { get; }
        public int MinimumMainIndex { get; }
        public int MaximumMainIndex { get; }

        public RoutePath(List<RouteAnchor> source, float extendBefore, float extendBeyond)
        {
            anchors = source;
            cumulative = new float[anchors.Count];
            for (int i = 1; i < anchors.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(anchors[i - 1].Position, anchors[i].Position);
            Length = cumulative[cumulative.Length - 1];
            ExtendedLength = Length + Mathf.Max(0f, extendBefore) + Mathf.Max(0f, extendBeyond);
            MinimumMainIndex = anchors[0].MainIndex;
            MaximumMainIndex = anchors[anchors.Count - 1].MainIndex == int.MaxValue
                ? anchors[Mathf.Max(0, anchors.Count - 2)].MainIndex
                : anchors[anchors.Count - 1].MainIndex;
        }

        public RouteSample Sample(float distance, Random random, float transitionBlend)
        {
            int segment;
            float t;
            if (distance <= 0f)
            {
                segment = 0;
                float segmentLength = Mathf.Max(0.001f, cumulative[1] - cumulative[0]);
                t = distance / segmentLength;
            }
            else if (distance >= Length)
            {
                segment = anchors.Count - 2;
                float segmentLength = Mathf.Max(0.001f, cumulative[segment + 1] - cumulative[segment]);
                t = 1f + (distance - Length) / segmentLength;
            }
            else
            {
                segment = 0;
                while (segment < cumulative.Length - 2 && cumulative[segment + 1] < distance)
                    segment++;
                float segmentLength = Mathf.Max(0.001f, cumulative[segment + 1] - cumulative[segment]);
                t = (distance - cumulative[segment]) / segmentLength;
            }

            Vector3 start = anchors[segment].Position;
            Vector3 end = anchors[segment + 1].Position;
            Vector3 forward = end - start;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;

            IslandBiome biome = anchors[segment].Biome;
            if (transitionBlend > 0f)
            {
                for (int i = 1; i < anchors.Count; i++)
                {
                    if (anchors[i - 1].Biome == anchors[i].Biome ||
                        Mathf.Abs(distance - cumulative[i]) > transitionBlend)
                    {
                        continue;
                    }

                    biome = random.NextDouble() < 0.5 ? anchors[i - 1].Biome : anchors[i].Biome;
                    break;
                }
            }

            return new RouteSample
            {
                Position = Vector3.LerpUnclamped(start, end, t),
                Right = right,
                Biome = biome
            };
        }

        public float RandomDistanceForIndexRange(Random random, int minimumIndex, int maximumIndex)
        {
            int first = 0;
            int last = anchors.Count - 1;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].MainIndex >= minimumIndex)
                {
                    first = i;
                    break;
                }
            }
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i].MainIndex <= maximumIndex)
                {
                    last = i;
                    break;
                }
            }
            float minimum = cumulative[Mathf.Clamp(first, 0, cumulative.Length - 1)];
            float maximum = cumulative[Mathf.Clamp(last, 0, cumulative.Length - 1)];
            return RandomRange(random, minimum, Mathf.Max(minimum, maximum));
        }

        public float HorizontalDistanceToPath(Vector3 position)
        {
            Vector2 point = new Vector2(position.x, position.z);
            float closest = float.MaxValue;
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                Vector2 start = new Vector2(anchors[i].Position.x, anchors[i].Position.z);
                Vector2 end = new Vector2(anchors[i + 1].Position.x, anchors[i + 1].Position.z);
                Vector2 delta = end - start;
                float denominator = delta.sqrMagnitude;
                float t = denominator <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, delta) / denominator);
                closest = Mathf.Min(closest, Vector2.Distance(point, start + delta * t));
            }
            return closest;
        }
    }
}
