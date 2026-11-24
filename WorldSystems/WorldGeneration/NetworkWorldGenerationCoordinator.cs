using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkWorldGenerationCoordinator : NetworkBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private LevelConfiguration assignedLevelConfiguration;

    [Tooltip(
        "When Wind exists, resolve its selected Level ID before server world " +
        "generation. This is the normal Lobby-to-SkyKingdom path.")]
    [SerializeField]
    private bool applyWindSelectedLevel = true;

    [Tooltip("Apply the assigned level before server validation and generation. Leave off to use the Route Generator and Event Director exactly as configured in the scene.")]
    [SerializeField] private bool applyAssignedLevelOnStart;
    [Tooltip("Generate scenery around an authored arena without a gameplay route or encounters.")]
    [SerializeField] private bool backgroundOnly;
    [SerializeField] private Bounds authoredArenaBounds = new Bounds(Vector3.zero, new Vector3(50, 30, 50));

    [Header("Generators")]
    [SerializeField] private IslandRouteGenerator routeGenerator;
    [SerializeField] private BackgroundIslandGenerator backgroundGenerator;

    [Header("Grass")]
    [SerializeField] private IslandGrassRouteBridge grassBridge;
    [SerializeField] private WorldGrassManager worldGrassManager;

    [Header("Events")]
    [SerializeField] private RunEventDirector eventDirector;

    [Tooltip("Cosmetic camp marker for islands assigned a Boko Spawns encounter.")]
    [SerializeField] private GameObject bokoCampfirePrefab;
    private readonly List<GameObject> _campfireMarkers = new();

    [Header("Runtime Navigation")]
    [SerializeField] private RuntimeRouteNavMesh runtimeRouteNavMesh;

    [Header("Generation")]
    [Tooltip("Legacy route seed limit. Connected routes with Preview Seed = 0 use at most 30 total route attempts, without nested route retries.")]
    [SerializeField, Min(1)] private int maximumSeedAttempts = 12;

    [Header("Client Readiness")]
    [SerializeField, Min(5f)] private float clientReadyTimeout = 45f;
    [SerializeField] private bool disconnectClientOnFailure = true;

    private readonly Dictionary<int, NetworkConnection>
        _registeredClients = new();

    private readonly HashSet<int> _seedSentClients = new();
    private readonly HashSet<int> _readyClients = new();
    private readonly List<NetworkObject> _spawnedWorldRoots = new();

    private Coroutine _clientReadyRoutine;
    private string _assignedLevelApplyError = string.Empty;

    public bool ServerWorldGenerated { get; private set; }
    public bool ServerWorldGenerationFailed { get; private set; }
    public string ServerFailureReason { get; private set; }

    public int WorldSeed { get; private set; }
    public int WorldHash { get; private set; }
    public int SpawnedPieceCount { get; private set; }
    public LevelConfiguration AssignedLevelConfiguration =>
        assignedLevelConfiguration;
    public int SelectedSavedRunIndex { get; private set; } = -1;
    public int SelectedSavedLayoutIndex { get; private set; } = -1;
    public LevelConfiguration ActiveRunConfiguration { get; private set; }

    public event Action<NetworkConnection> ServerClientWorldReady;
    public event Action<NetworkConnection, string> ServerClientWorldFailed;

    // Called by the direct-scene bootstrap before this coordinator's Awake.
    // Menu sessions keep resolving their server-selected Wind configuration.
    public void PrepareDirectSceneLevel(LevelConfiguration level)
    {
        assignedLevelConfiguration = level;
        applyWindSelectedLevel = false;
        applyAssignedLevelOnStart = true;
    }

    private bool preserveAuthoredPreview;

    // Explicit direct-scene opt-in. Keep authored placements, then run the same
    // navigation, event validation, network spawning and client-ready handshake.
    public bool PreserveAuthoredPreviewForDirectScene()
    {
        if (V3Backend) return false;
        if (!(Application.isEditor || Debug.isDebugBuild)) return false;
        if (!routeGenerator)
            routeGenerator = FindObjectsByType<IslandRouteGenerator>(FindObjectsSortMode.None)
                .FirstOrDefault(route => route.gameObject.scene == gameObject.scene);
        preserveAuthoredPreview = routeGenerator && routeGenerator.GeneratedInstances.Count > 0;
        if (preserveAuthoredPreview)
        {
            applyWindSelectedLevel = false;
            applyAssignedLevelOnStart = false;
        }
        return preserveAuthoredPreview;
    }

    public Above.GridV3.V3NetworkBackend V3Backend => GetComponent<Above.GridV3.V3NetworkBackend>();

    private void Awake()
    {
        if (V3Backend) return;
        if (routeGenerator == null)
            routeGenerator = FindFirstObjectByType<IslandRouteGenerator>();

        if (backgroundGenerator == null)
            backgroundGenerator =
                FindFirstObjectByType<BackgroundIslandGenerator>();

        if (grassBridge == null)
            grassBridge = FindFirstObjectByType<IslandGrassRouteBridge>();

        if (worldGrassManager == null)
            worldGrassManager =
                FindFirstObjectByType<WorldGrassManager>();

        if (eventDirector == null)
        {
            RunEventDirector[] directors =
                FindObjectsByType<RunEventDirector>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < directors.Length; i++)
            {
                if (directors[i].gameObject.scene != gameObject.scene)
                    continue;

                eventDirector = directors[i];
                break;
            }
        }

        if (runtimeRouteNavMesh == null)
        {
            RuntimeRouteNavMesh[] builders =
                FindObjectsByType<RuntimeRouteNavMesh>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < builders.Length; i++)
            {
                if (builders[i].gameObject.scene != gameObject.scene)
                    continue;

                runtimeRouteNavMesh = builders[i];
                break;
            }
        }

        bool attemptedWindSelection =
            !backgroundOnly && applyWindSelectedLevel && Wind.Instance != null;

        if (attemptedWindSelection)
        {
            if (!Wind.Instance.TryGetSelectedLevelConfiguration(
                    out LevelConfiguration selectedLevel,
                    out string selectionError))
            {
                _assignedLevelApplyError =
                    "Could not resolve Wind's selected level: " +
                    selectionError;
            }
            else if (!ApplyLevelConfiguration(
                         selectedLevel,
                         out _assignedLevelApplyError))
            {
                _assignedLevelApplyError =
                    "Could not apply Wind's selected level: " +
                    _assignedLevelApplyError;
            }
            else
            {
                Debug.Log(
                    $"[World Generation] Applied Wind level " +
                    $"'{selectedLevel.LevelId}' ({selectedLevel.name}).",
                    this);
            }
        }
        else if (!backgroundOnly && applyAssignedLevelOnStart)
        {
            if (assignedLevelConfiguration == null)
            {
                _assignedLevelApplyError =
                    "Apply Assigned Level On Start is enabled, but no " +
                    "Level Configuration is assigned.";
            }
            else if (!ApplyLevelConfiguration(
                         assignedLevelConfiguration,
                         out _assignedLevelApplyError))
            {
                _assignedLevelApplyError =
                    "Could not apply assigned level: " +
                    _assignedLevelApplyError;
            }

        }

        if (!string.IsNullOrEmpty(_assignedLevelApplyError))
        {
            Debug.LogError(
                "[World Generation] " + _assignedLevelApplyError,
                this);
        }

        // The coordinator is now the only system allowed to start generation.
        if (routeGenerator != null)
            routeGenerator.Generation.GenerateOnStart = false;

        if (backgroundGenerator != null)
            backgroundGenerator.Integration.GenerateWithRoute = false;
    }

    /// <summary>
    /// Applies the route and event halves of one level before world generation.
    /// Direct-scene testing and Wind selection will both use this entry point.
    /// </summary>
    public bool ApplyLevelConfiguration(
        LevelConfiguration level,
        out string error)
    {
        if (level == null)
        {
            error = "Level Configuration is null.";
            return false;
        }

        if (!level.ValidateRuntimeLayoutSource(out error)) return false;

        if (level.RouteConfiguration == null)
        {
            error = $"Level '{level.name}' has no Route Configuration.";
            return false;
        }

        if (level.EventConfiguration == null)
        {
            error = $"Level '{level.name}' has no Event Configuration.";
            return false;
        }

        if (!ApplyConfigurations(
                level.RouteConfiguration,
                level.EventConfiguration,
                out error))
        {
            error =
                $"Could not apply level '{level.name}': " + error;
            return false;
        }

        assignedLevelConfiguration = level;
        ActiveRunConfiguration = level;
        error = string.Empty;
        return true;
    }

    private bool ApplySavedRun(int index, out string error)
    {
        LevelConfiguration level = assignedLevelConfiguration;
        if (level == null)
        {
            error = "Cannot select a saved run without a level configuration.";
            return false;
        }
        if (!level.TryGetSavedRun(index, out var run, out error))
            return false;
        if (!ApplyConfigurations(run.RouteConfiguration, run.EventConfiguration, out error))
            return false;

        // Keep the telescope destination and progression identity on the parent level.
        assignedLevelConfiguration = level;
        ActiveRunConfiguration = run;
        SelectedSavedRunIndex = index;
        return true;
    }

    private bool ApplySavedLayout(int index, out string error, int energySeed = 0)
    {
        var level = assignedLevelConfiguration;
        if (!level)
        { error = "Cannot select a saved layout without a level configuration."; return false; }
        if (!level.TryGetSavedLayout(index, out var layout, out var settings, out error)) return false;
        if (!ApplyConfigurations(settings.RouteConfiguration, settings.EventConfiguration, out error)) return false;
        routeGenerator.ConnectedRoute.Enabled = true;
        routeGenerator.ConnectedRoute.UseApprovedLayout = true;
        routeGenerator.ConnectedRoute.ApprovedLayout = layout;
        if (level.IsEnergyCollection && !EnergyCollectionWorld.TryConfigure(routeGenerator, layout, level.EnergyCollection,
                energySeed == 0 ? CreateSeed() : energySeed, out error)) return false;
        assignedLevelConfiguration = level;
        ActiveRunConfiguration = settings;
        SelectedSavedLayoutIndex = index;
        return true;
    }

    private bool SelectSavedLayoutOrRun(out string error)
    {
        error = string.Empty;
        var level = assignedLevelConfiguration;
        if (!level || backgroundOnly) return true;
        if (!level.ValidateRuntimeLayoutSource(out error)) return false;
        if (level.SavedLayoutCount > 0)
        {
            int index = UnityEngine.Random.Range(0, level.SavedLayoutCount);
            if (level.IsEnergyCollection)
            {
                var ready = new List<int>(); var invalid = new List<string>();
                for (int i = 0; i < level.SavedLayoutCount; i++)
                    if (level.TryGetSavedLayout(i, out _, out _, out string issue)) ready.Add(i);
                    else invalid.Add($"{level.SavedLayouts[i]?.name}: {issue}");
                if (invalid.Count > 0) Debug.LogWarning("[Energy Collection] Assigned layouts requiring author fixes (excluded from this run's choice):\n" + string.Join("\n", invalid), this);
                if (ready.Count == 0) { error = "No Energy Collection layout is ready. " + string.Join("\n", invalid); return false; }
                index = ready[UnityEngine.Random.Range(0, ready.Count)];
            }
            if (!ApplySavedLayout(index, out error)) return false;
            Debug.Log($"[World Generation] Selected layout {index + 1}/{level.SavedLayoutCount}: '{level.SavedLayouts[index].name}'. Loading the approved layout without search.", this);
        }
        else if (level.SavedRunCount > 0)
        {
            int index = UnityEngine.Random.Range(0, level.SavedRunCount);
            if (!ApplySavedRun(index, out error)) return false;
            Debug.Log($"[World Generation] Selected saved run {index + 1}/{level.SavedRunCount}: '{ActiveRunConfiguration.name}'. Loading its approved layout and authored events without search.", this);
        }
        return true;
    }

    /// <summary>
    /// Applies a route/event pair before the server starts generation. This is
    /// used by direct-scene development tests that do not need to create a
    /// complete LevelConfiguration asset first.
    /// </summary>
    public bool ApplyConfigurations(
        RouteConfiguration routeConfiguration,
        RunEventConfiguration eventConfiguration,
        out string error)
    {
        if (routeGenerator == null)
        {
            error = "Route Generator is missing.";
            return false;
        }

        if (eventDirector == null)
        {
            error = "Event Director is missing.";
            return false;
        }

        if (routeConfiguration == null)
        {
            error = "Route Configuration is null.";
            return false;
        }

        if (eventConfiguration == null)
        {
            error = "Run Event Configuration is null.";
            return false;
        }

        EnergyCollectionWorld.Clear(routeGenerator);
        if (!routeGenerator.ApplyConfiguration(routeConfiguration))
        {
            error =
                $"Could not apply route configuration " +
                $"'{routeConfiguration.name}'.";
            return false;
        }

        if (!eventDirector.ApplyConfiguration(eventConfiguration))
        {
            error =
                $"Could not apply event configuration " +
                $"'{eventConfiguration.name}'.";
            return false;
        }

        assignedLevelConfiguration = null;
        ActiveRunConfiguration = null;
        SelectedSavedRunIndex = -1;
        SelectedSavedLayoutIndex = -1;
        _assignedLevelApplyError = string.Empty;
        error = string.Empty;
        return true;
    }

    private readonly HashSet<Action> _v3StartListeners = new();
    private bool _v3Started;
    private Coroutine _v3StartDispatch;

    // The generator owns replay for listeners that subscribe after server spawn.
    public void SubscribeV3ServerStarted(Action listener)
    {
        _v3StartListeners.Add(listener);
        ScheduleV3StartSignal();
    }

    public void UnsubscribeV3ServerStarted(Action listener) => _v3StartListeners.Remove(listener);

    private void ScheduleV3StartSignal()
    {
        if (_v3Started && _v3StartListeners.Count != 0 && _v3StartDispatch == null)
            _v3StartDispatch = StartCoroutine(DispatchV3StartSignal());
    }

    private IEnumerator DispatchV3StartSignal()
    {
        // Complete FishNet's scene activation first, but do not poll.
        yield return null;
        _v3StartDispatch = null;
        foreach (var listener in _v3StartListeners.ToArray())
        {
            if (!_v3Started) yield break;
            if (_v3StartListeners.Remove(listener)) listener();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (V3Backend)
        {
            _v3Started = true;
            ScheduleV3StartSignal();
            StartCoroutine(GenerateV3World());
            return;
        }
        if (preserveAuthoredPreview) StartCoroutine(GeneratePreservedPreviewAfterSceneSpawn());
        else GenerateAndSpawnServerWorld();
    }

    private System.Collections.IEnumerator GeneratePreservedPreviewAfterSceneSpawn()
    {
        // FishNet activates authored scene islands after their OnStartServer callbacks.
        // Bake only once that pass has completed, so scene colliders enter the NavMesh.
        yield return null;
        if (IsServerStarted) GenerateAndSpawnServerWorld();
    }

    public override void OnStopServer()
    {
        _v3Started = false;
        if (_v3StartDispatch != null) StopCoroutine(_v3StartDispatch);
        _v3StartDispatch = null;
        _v3StartListeners.Clear();
        if (V3Backend) V3Backend.Clear();
        ClearCampMarkers();
        EnergyCollectionWorld.Clear(routeGenerator);
        if (eventDirector != null) eventDirector.ClearServerEncounters();
        base.OnStopServer();

        _registeredClients.Clear();
        _seedSentClients.Clear();
        _readyClients.Clear();
        _spawnedWorldRoots.Clear();
    }

    [Server]
    public void ServerRegisterClient(NetworkConnection connection)
    {
        if (connection == null)
            return;

        _registeredClients[connection.ClientId] = connection;

        if (!ServerWorldGenerated)
        {
            if (ServerWorldGenerationFailed)
            {
                ServerRejectClient(
                    connection,
                    ServerFailureReason);
            }

            return;
        }

        SendWorldCompletionToClient(connection);
    }

    [Server]
    public void ServerUnregisterClient(NetworkConnection connection)
    {
        if (connection == null)
            return;

        int clientId = connection.ClientId;

        _registeredClients.Remove(clientId);
        _seedSentClients.Remove(clientId);
        _readyClients.Remove(clientId);
    }

    [Server]
    private void GenerateAndSpawnServerWorld()
    {
        ServerWorldGenerated = false;
        ServerWorldGenerationFailed = false;
        ServerFailureReason = string.Empty;

        if (backgroundOnly)
        {
            WorldSeed = CreateSeed();
            if (!backgroundGenerator || !backgroundGenerator.GenerateBackgroundForArena(authoredArenaBounds, WorldSeed))
            {
                FailServerGeneration("Arena background generation failed.");
                return;
            }
            CompleteServerWorld();
            return;
        }

        if (!preserveAuthoredPreview && string.IsNullOrEmpty(_assignedLevelApplyError) && !SelectSavedLayoutOrRun(out _assignedLevelApplyError))
            _assignedLevelApplyError = "Could not load the selected saved layout/run: " + _assignedLevelApplyError;

        if (!string.IsNullOrEmpty(_assignedLevelApplyError))
        {
            FailServerGeneration(_assignedLevelApplyError);
            return;
        }

        if (routeGenerator == null || backgroundGenerator == null)
        {
            FailServerGeneration(
                "Route Generator or Background Generator is missing.");
            return;
        }

        if (!routeGenerator.ValidateConfiguration(
                out string routeValidation))
        {
            FailServerGeneration(
                $"Route configuration is invalid:\n{routeValidation}");
            return;
        }

        if (!backgroundGenerator.ValidateConfiguration(
                out string backgroundValidation))
        {
            FailServerGeneration(
                $"Background configuration is invalid:\n" +
                backgroundValidation);
            return;
        }

        if (eventDirector != null)
        {
            if (!eventDirector.ValidateConfiguration(
                    out string eventValidation))
            {
                FailServerGeneration(
                    $"Event configuration is invalid:\n" +
                    eventValidation);
                return;
            }

            if (eventDirector.RouteGenerator != routeGenerator)
            {
                FailServerGeneration(
                    "The Event Director and World Generation Coordinator " +
                    "must reference the same Route Generator.");
                return;
            }

            if (eventDirector.RuntimeRouteNavMesh != runtimeRouteNavMesh)
            {
                FailServerGeneration(
                    "The Event Director and World Generation Coordinator " +
                    "must reference the same Runtime Route NavMesh.");
                return;
            }
        }

        if (runtimeRouteNavMesh != null)
        {
            if (!runtimeRouteNavMesh.ValidateConfiguration(
                    out string navMeshValidation))
            {
                FailServerGeneration(
                    "Runtime route NavMesh configuration is invalid:\n" +
                    navMeshValidation);
                return;
            }

            if (runtimeRouteNavMesh.RouteGenerator != routeGenerator)
            {
                FailServerGeneration(
                    "Runtime Route NavMesh and World Generation Coordinator " +
                    "must reference the same Route Generator.");
                return;
            }
        }

        bool generated = false;
        bool approvedLayout = preserveAuthoredPreview || routeGenerator.UsesApprovedConnectedLayout;
        int previewSeed = routeGenerator.ConnectedRoute.Enabled ? routeGenerator.ConnectedRoute.PreviewSeed : 0;
        bool randomConnectedRoute = !approvedLayout && routeGenerator.ConnectedRoute.Enabled && previewSeed == 0;
        int attemptLimit = approvedLayout ? 1 : randomConnectedRoute
            ? IslandRouteGenerator.MaximumRandomRouteAttempts
            : maximumSeedAttempts;
        string lastAttemptFailure = "No generation attempt completed.";
        var generationClock = System.Diagnostics.Stopwatch.StartNew();
        bool triedFallback = false;

        for (int attempt = 1;
             attempt <= attemptLimit;
             attempt++)
        {
            bool fallback = randomConnectedRoute && (attempt > attemptLimit ||
                generationClock.Elapsed.TotalSeconds >= routeGenerator.ConnectedRoute.MaximumGenerationSeconds);
            if (fallback) triedFallback = true;
            int candidateSeed = preserveAuthoredPreview ? routeGenerator.LastUsedSeed : previewSeed != 0 ? previewSeed : CreateSeed();

            backgroundGenerator.ClearGeneratedBackground();
            if (runtimeRouteNavMesh != null)
                runtimeRouteNavMesh.ClearBuiltNavMesh();
            if (!preserveAuthoredPreview) routeGenerator.ClearGeneratedRoute();

            routeGenerator.Generation.UseRandomSeed = false;
            routeGenerator.Generation.Seed = candidateSeed;

            backgroundGenerator.Integration.UseRouteSeed = true;
            backgroundGenerator.Integration.GenerateWithRoute = false;

            // One outer attempt is exactly one full route plan in random
            // connected mode. Failed joins/bakes/backgrounds consume that same
            // attempt; no inner 12x retry multiplier can restart the budget.
            if (randomConnectedRoute)
                Debug.Log(fallback ? "[World Generation] Search limit reached; loading saved connected fallback." :
                    $"[World Generation] Route attempt {attempt}/{attemptLimit}, seed {candidateSeed}.", this);

            string fallbackReport = string.Empty;
            bool routeGenerated = preserveAuthoredPreview || (fallback
                ? routeGenerator.GenerateConnectedFallback(out fallbackReport)
                : randomConnectedRoute ? routeGenerator.GenerateRouteWithAttemptLimit(1) : routeGenerator.GenerateRoute());
            if ((approvedLayout || fallback) && routeGenerated) candidateSeed = routeGenerator.LastUsedSeed;
            if (routeGenerator.ConnectedRoute.Enabled)
                Debug.Log($"[World Generation] Timings: planning {routeGenerator.LastPlanningSeconds:F3}s, " +
                    $"island creation {routeGenerator.LastInstantiationSeconds:F3}s.", this);
            if (!routeGenerated)
            {
                lastAttemptFailure = fallback ? fallbackReport : $"Seed {candidateSeed}: {routeGenerator.LastFailureReason}";
                if (approvedLayout || fallback) break;
                Debug.LogWarning(
                    $"[World Generation] Seed attempt {attempt}/" +
                    $"{attemptLimit} failed: " +
                    routeGenerator.LastFailureReason,
                    this);
                if (previewSeed != 0)
                {
                    FailServerGeneration("Preview seed could not be generated: " + routeGenerator.LastFailureReason);
                    return;
                }

                continue;
            }

            var navigationClock = System.Diagnostics.Stopwatch.StartNew();
            // Build once from the complete generated route hierarchy. Event
            // planning is intentionally blocked until this exact bake exists.
            if (runtimeRouteNavMesh != null &&
                !runtimeRouteNavMesh.EnsureBuiltForCurrentRoute(
                    out string navMeshBuildError))
            {
                FailServerGeneration(
                    "The generated route runtime NavMesh could not be built:\n" +
                    navMeshBuildError);
                return;
            }

            double bakeSeconds = navigationClock.Elapsed.TotalSeconds;
            if (!EnergyCollectionWorld.ValidateNavigation(routeGenerator, out string energyNavigationError))
            {
                FailServerGeneration("Energy Collection navigation failed: " + energyNavigationError);
                return;
            }
            navigationClock.Restart();
            string connectivityError = "Runtime route NavMesh is missing.";
            if (routeGenerator.ConnectedRoute.Enabled &&
                (runtimeRouteNavMesh == null || !routeGenerator.ValidateConnectedNavigation(out connectivityError)))
            {
                lastAttemptFailure = $"Seed {candidateSeed}: {connectivityError}";
                Debug.LogWarning("[World Generation] Connected route rejected: " + connectivityError, this);
                Debug.Log($"[World Generation] Timings: NavMesh {bakeSeconds:F3}s, " +
                    $"validation {navigationClock.Elapsed.TotalSeconds:F3}s (rejected).", this);
                if (approvedLayout || fallback) break;
                if (previewSeed != 0)
                {
                    FailServerGeneration("Preview seed failed its walkability/gate check: " + connectivityError);
                    return;
                }
                continue;
            }

            if (routeGenerator.ConnectedRoute.Enabled)
                Debug.Log($"[World Generation] Timings: NavMesh {bakeSeconds:F3}s, " +
                    $"validation {navigationClock.Elapsed.TotalSeconds:F3}s (passed).", this);
            // Event planning is deterministic for this exact route. A plan
            // failure is a configuration problem, so do not hide it by trying
            // another seed or rerolling an invalid event.
            if (eventDirector != null &&
                !eventDirector.EnsurePlanForCurrentRoute(
                    out string eventPlanError))
            {
                FailServerGeneration(
                    $"The generated route has no valid event plan:\n" +
                    eventPlanError);
                return;
            }

            if (!backgroundGenerator.GenerateBackground())
            {
                lastAttemptFailure = $"Seed {candidateSeed}: background generation failed: " +
                    backgroundGenerator.LastGenerationReport;
                Debug.LogWarning(
                    $"[World Generation] Background generation failed " +
                    $"for seed {candidateSeed}: " +
                    backgroundGenerator.LastGenerationReport,
                    this);

                if (approvedLayout || fallback) break;
                continue;
            }

            WorldSeed = candidateSeed;
            generated = true;
            break;
        }

        if (!generated)
        {
            FailServerGeneration(approvedLayout
                ? "Approved connected layout failed; no generation search or retries will run. " + lastAttemptFailure
                : randomConnectedRoute
                ? $"Stopped within the {attemptLimit}-attempt/search-time limit without a valid world. " +
                  $"Saved fallback {(triedFallback ? "also failed" : "was not tried")}. " +
                  $"No further retries will run. Last rejection: {lastAttemptFailure}"
                : $"No world could be generated after {attemptLimit} seed attempts.");
            return;
        }

        CompleteServerWorld();
    }

    [Server]
    private void CompleteServerWorld()
    {
        if (!ValidateGeneratedNetworkRoots(out string networkError))
        {
            FailServerGeneration(networkError);
            return;
        }

        try
        {
            SpawnedPieceCount = SpawnGeneratedWorld();
            EnergyCollectionWorld.SpawnStations(routeGenerator, root => _spawnedWorldRoots.Add(root));
        }
        catch (Exception exception)
        {
            DespawnPartiallySpawnedWorld();

            FailServerGeneration(
                $"Network-spawning the generated world failed:\n" +
                exception.Message);

            return;
        }

        ApplyGrassAndRebuild();

        if (!TryCollectReadyPieces(
                SpawnedPieceCount,
                out List<GeneratedWorldNetworkPiece> pieces,
                out string collectionError))
        {
            FailServerGeneration(collectionError);
            return;
        }

        WorldHash = CalculateWorldHash(pieces);
        ServerWorldGenerated = true;
        if (!backgroundOnly) ServerCreateBokoCampMarkers();

        Debug.Log(
            $"[World Generation] Server generated and spawned " +
            $"{SpawnedPieceCount} pieces with seed {WorldSeed}. " +
            $"Hash: {WorldHash}.",
            this);

        foreach (NetworkConnection connection
                 in _registeredClients.Values)
        {
            SendWorldCompletionToClient(connection);
        }
    }

    [Server]
    private void ServerCreateBokoCampMarkers()
    {
        if (!bokoCampfirePrefab) return;
        var positions = new List<Vector3>();
        foreach (var piece in routeGenerator.GeneratedInstances)
        {
            var trigger = piece.GetComponent<IslandEventTrigger>();
            if (!trigger || !trigger.HasAssignedEvent || trigger.AssignedEvent != IslandEventId.BokoSpawns) continue;
            if (TryCampMarkerPosition(trigger.Island, out var position)) positions.Add(position);
        }
        ApplyCampMarkers(positions.ToArray());
        ObserversCreateCampMarkers(positions.ToArray());
    }

    private static bool TryCampMarkerPosition(AboveIsland island, out Vector3 position)
    {
        foreach (var point in island.GetComponentsInChildren<ItemSpawnPoint>())
            if (point.isActiveAndEnabled && !point.HasSpawnedItem &&
                TryClearCampGround(point.transform.position, .75f, out position)) return true;

        // Some eligible island families have no general item markers; retain the
        // proven encounter anchor instead of silently removing their campfire.
        Vector3 center = island.transform.position;
        var bounds = island.PlacementBounds;
        if (bounds != null && bounds.Count > 0 && bounds[0]) center = bounds[0].bounds.center;
        var spawn = island.GetComponentInChildren<KoboldSpawnPoint>();
        if (spawn) center = spawn.SpawnTransform.position;
        if (TryClearCampGround(center, 12f, out position)) return true;
        // A pickup can also occupy that fallback. Search nearby on the same island
        // without moving items, changing their seeded selection, or blocking paths.
        for (int ring = 1; ring <= 3; ring++)
        for (int direction = 0; direction < 8; direction++)
        {
            float angle = direction * Mathf.PI / 4f;
            var candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 1.5f);
            if (!TryClearCampGround(candidate, .75f, out position)) continue;
            var sampled = position;
            if (bounds == null || bounds.Count == 0 || bounds.Any(b => b && b.bounds.Contains(sampled))) return true;
        }
        position = default;
        Debug.LogWarning("[World Generation] No walkable pickup-clear campfire location on " + island.name);
        return false;
    }

    private static bool TryClearCampGround(Vector3 candidate, float radius, out Vector3 position)
    {
        position = default;
        if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var ground, radius, UnityEngine.AI.NavMesh.AllAreas)) return false;
        foreach (var collider in Physics.OverlapSphere(ground.position + Vector3.up * .4f, 1.1f,
                     Physics.AllLayers, QueryTriggerInteraction.Collide))
            if (collider.GetComponentInParent<BreakableCrystal>() || collider.GetComponentInParent<BreakableSkull>() ||
                collider.GetComponentInParent<DroppedPowerPickup>() || collider.GetComponentInParent<EnergyStationCollector>()) return false;
        position = ground.position;
        return true;
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversCreateCampMarkers(Vector3[] positions)
    {
        if (IsServerInitialized) return;
        ApplyCampMarkers(positions);
    }

    private void ApplyCampMarkers(Vector3[] positions)
    {
        ClearCampMarkers();
        if (!bokoCampfirePrefab) return;
        foreach (var position in positions)
        {
            var marker = Instantiate(bokoCampfirePrefab, position, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, gameObject.scene);
            // These mark planned encounters on the already baked route. The
            // source campfire has a solid collider at the enemy spawn position.
            foreach (var collider in marker.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            _campfireMarkers.Add(marker);
        }
    }

    private void ClearCampMarkers()
    {
        foreach (var marker in _campfireMarkers) if (marker) Destroy(marker);
        _campfireMarkers.Clear();
    }

    public override void OnStopClient()
    {
        if (V3Backend && !IsServerStarted) V3Backend.Clear();
        ClearCampMarkers();
        EnergyCollectionWorld.Clear(routeGenerator);
        base.OnStopClient();
    }

    [Server]
    private int SpawnGeneratedWorld()
    {
        int generationOrder = 0;

        IReadOnlyList<AboveRoutePiece> routePieces =
            routeGenerator.GeneratedInstances;

        for (int i = 0; i < routePieces.Count; i++)
        {
            AboveRoutePiece routePiece = routePieces[i];

            GeneratedWorldNetworkPiece marker =
                routePiece.GetComponentInParent<
                    GeneratedWorldNetworkPiece>();

            NetworkObject root = marker.GetComponent<NetworkObject>();

            if (!root.IsSpawned) ServerManager.Spawn(root);

            if (!root.IsSpawned)
            {
                throw new InvalidOperationException(
                    $"FishNet did not spawn route piece " +
                    $"'{routePiece.name}'. Confirm its root prefab is in " +
                    $"the Spawnable Prefabs collection.");
            }

            _spawnedWorldRoots.Add(root);

            marker.ServerInitializeRoute(
                generationOrder++,
                routePiece.GeneratedBiome,
                routePiece.IsGeneratedMainIsland,
                routePiece.GeneratedMainIndex,
                routePiece.IsGeneratedBeacon,
                routePiece.IsGeneratedClusterPiece);
        }

        IReadOnlyList<BackgroundIsland> backgroundPieces =
            backgroundGenerator.GeneratedInstances;

        for (int i = 0; i < backgroundPieces.Count; i++)
        {
            BackgroundIsland backgroundPiece = backgroundPieces[i];

            GeneratedWorldNetworkPiece marker =
                backgroundPiece.GetComponentInParent<
                    GeneratedWorldNetworkPiece>();

            NetworkObject root = marker.GetComponent<NetworkObject>();

            if (!root.IsSpawned) ServerManager.Spawn(root);

            if (!root.IsSpawned)
            {
                throw new InvalidOperationException(
                    $"FishNet did not spawn background piece " +
                    $"'{backgroundPiece.name}'. Confirm its root prefab " +
                    $"is in the Spawnable Prefabs collection.");
            }

            _spawnedWorldRoots.Add(root);

            marker.ServerInitializeBackground(
                generationOrder++,
                backgroundPiece.GeneratedBiome,
                backgroundPiece.GeneratedLayer);
        }

        return generationOrder;
    }

    private bool ValidateGeneratedNetworkRoots(out string error)
    {
        HashSet<NetworkObject> roots = new();

        IReadOnlyList<AboveRoutePiece> routePieces =
            routeGenerator.GeneratedInstances;

        for (int i = 0; i < routePieces.Count; i++)
        {
            if (!ValidatePieceRoot(
                    routePieces[i],
                    roots,
                    out error))
            {
                return false;
            }
        }

        IReadOnlyList<BackgroundIsland> backgroundPieces =
            backgroundGenerator.GeneratedInstances;

        for (int i = 0; i < backgroundPieces.Count; i++)
        {
            if (!ValidatePieceRoot(
                    backgroundPieces[i],
                    roots,
                    out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidatePieceRoot(
        Component generatedPiece,
        HashSet<NetworkObject> usedRoots,
        out string error)
    {
        GeneratedWorldNetworkPiece marker =
            generatedPiece.GetComponentInParent<
                GeneratedWorldNetworkPiece>();

        if (marker == null)
        {
            error =
                $"Generated prefab '{generatedPiece.name}' has no " +
                $"{nameof(GeneratedWorldNetworkPiece)} on its root.";
            return false;
        }

        NetworkObject root = marker.GetComponent<NetworkObject>();

        if (root == null)
        {
            error =
                $"Generated prefab '{generatedPiece.name}' has no " +
                $"NetworkObject beside its " +
                $"{nameof(GeneratedWorldNetworkPiece)}.";
            return false;
        }

        if (!usedRoots.Add(root))
        {
            error =
                $"Generated pieces share the same network root " +
                $"'{root.name}'. Every generated island/connection needs " +
                $"its own root NetworkObject.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    [Server]
    private void SendWorldCompletionToClient(
        NetworkConnection connection)
    {
        if (connection == null ||
            _seedSentClients.Contains(connection.ClientId))
        {
            return;
        }

        // The host uses the same world objects as the server.
        if (IsHostConnection(connection))
        {
            ServerAcceptClient(connection);
            return;
        }

        _seedSentClients.Add(connection.ClientId);

        if (V3Backend)
            TargetV3World(connection, WorldSeed, WorldHash, V3Backend.DescriptorJson);
        else TargetWorldSpawnComplete(
            connection,
            WorldSeed,
            SpawnedPieceCount,
            WorldHash,
            SelectedSavedRunIndex,
            SelectedSavedLayoutIndex,
            assignedLevelConfiguration ? assignedLevelConfiguration.LevelId : string.Empty);

        StartCoroutine(
            ServerClientTimeoutRoutine(
                connection,
                WorldSeed));
    }

    private static bool IsHostConnection(
        NetworkConnection connection)
    {
        if (!InstanceFinder.IsHostStarted ||
            InstanceFinder.ClientManager.Connection == null)
        {
            return false;
        }

        return InstanceFinder.ClientManager.Connection.ClientId ==
               connection.ClientId;
    }

    [TargetRpc]
    private void TargetWorldSpawnComplete(
        NetworkConnection connection,
        int seed,
        int expectedPieceCount,
        int expectedHash,
        int savedRunIndex,
        int savedLayoutIndex,
        string levelId)
    {
        if (_clientReadyRoutine != null)
            StopCoroutine(_clientReadyRoutine);

        string localLevelId = assignedLevelConfiguration ? assignedLevelConfiguration.LevelId : string.Empty;
        if (!string.Equals(localLevelId, levelId, StringComparison.Ordinal))
        {
            ServerReportWorldReady(seed, 0, false,
                $"Level mismatch: server selected '{levelId}', client selected '{localLevelId}'. Return to Lobby and select the same expedition.");
            _clientReadyRoutine = null;
            return;
        }
        if (assignedLevelConfiguration &&
            (!assignedLevelConfiguration.ValidateRuntimeLayoutSource(out string sourceError) ||
             assignedLevelConfiguration.IsEnergyCollection && (savedLayoutIndex < 0 || savedRunIndex >= 0) ||
             assignedLevelConfiguration.LevelId == Wind.ChargedCellLevelId && (savedRunIndex < 0 || savedLayoutIndex >= 0)))
        {
            ServerReportWorldReady(seed, 0, false,
                "The server's layout selection does not match the selected expedition's assigned pool. " + sourceError);
            _clientReadyRoutine = null;
            return;
        }
        // Clients receive the server's choice; they never roll or generate a layout.
        if (savedLayoutIndex >= 0 && !(IsServerStarted && assignedLevelConfiguration && assignedLevelConfiguration.IsEnergyCollection) &&
            !ApplySavedLayout(savedLayoutIndex, out string layoutError, seed))
        {
            ServerReportWorldReady(seed, 0, false, "Could not apply the server's saved layout: " + layoutError);
            _clientReadyRoutine = null;
            return;
        }
        if (savedRunIndex >= 0 && !ApplySavedRun(savedRunIndex, out string error))
        {
            ServerReportWorldReady(seed, 0, false, "Could not apply the server's saved run: " + error);
            _clientReadyRoutine = null;
            return;
        }

        _clientReadyRoutine = StartCoroutine(
            ClientVerifyWorldRoutine(
                seed,
                expectedPieceCount,
                expectedHash));
    }

    private IEnumerator ClientVerifyWorldRoutine(
        int seed,
        int expectedPieceCount,
        int expectedHash)
    {
        float deadline =
            Time.realtimeSinceStartup + clientReadyTimeout;

        List<GeneratedWorldNetworkPiece> pieces = null;
        string failure = string.Empty;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (TryCollectReadyPieces(
                    expectedPieceCount,
                    out pieces,
                    out failure))
            {
                break;
            }

            yield return null;
        }

        if (pieces == null ||
            pieces.Count != expectedPieceCount)
        {
            ServerReportWorldReady(
                seed,
                0,
                false,
                $"Timed out waiting for generated pieces. {failure}");

            _clientReadyRoutine = null;
            yield break;
        }

        ApplyGrassAndRebuild();

        // Allow all freshly enabled renderers/colliders to settle.
        yield return null;

        int localHash = CalculateWorldHash(pieces);

        if (localHash != expectedHash)
        {
            ServerReportWorldReady(
                seed,
                localHash,
                false,
                $"World hash mismatch. Server={expectedHash}, " +
                $"Client={localHash}.");

            _clientReadyRoutine = null;
            yield break;
        }

        ServerReportWorldReady(
            seed,
            localHash,
            true,
            string.Empty);

        _clientReadyRoutine = null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerReportWorldReady(
        int seed,
        int clientHash,
        bool success,
        string failureReason,
        NetworkConnection sender = null)
    {
        if (sender == null ||
            !_registeredClients.ContainsKey(sender.ClientId))
        {
            return;
        }

        if (!success)
        {
            ServerRejectClient(
                sender,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "Client failed to initialize the generated world."
                    : failureReason);

            return;
        }

        if (seed != WorldSeed || clientHash != WorldHash)
        {
            ServerRejectClient(
                sender,
                $"Generated world verification mismatch. " +
                $"Expected seed/hash {WorldSeed}/{WorldHash}, " +
                $"received {seed}/{clientHash}.");

            return;
        }

        ServerAcceptClient(sender);
    }

    [Server]
    private void ServerAcceptClient(NetworkConnection connection)
    {
        if (!_readyClients.Add(connection.ClientId))
            return;

        Debug.Log(
            $"[World Generation] Client {connection.ClientId} is ready.",
            this);

        ServerClientWorldReady?.Invoke(connection);
    }

    [Server]
    private void ServerRejectClient(
        NetworkConnection connection,
        string reason)
    {
        Debug.LogError(
            $"[World Generation] Client {connection.ClientId} failed: " +
            reason,
            this);

        ServerClientWorldFailed?.Invoke(connection, reason);

        if (disconnectClientOnFailure)
            connection.Disconnect(true);
    }

    private IEnumerator ServerClientTimeoutRoutine(
        NetworkConnection connection,
        int expectedSeed)
    {
        float deadline =
            Time.realtimeSinceStartup + clientReadyTimeout;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (connection == null ||
                !_registeredClients.ContainsKey(connection.ClientId) ||
                _readyClients.Contains(connection.ClientId) ||
                WorldSeed != expectedSeed)
            {
                yield break;
            }

            yield return null;
        }

        if (connection != null &&
            _registeredClients.ContainsKey(connection.ClientId) &&
            !_readyClients.Contains(connection.ClientId))
        {
            ServerRejectClient(
                connection,
                $"Timed out after {clientReadyTimeout:0} seconds.");
        }
    }

    private bool TryCollectReadyPieces(
        int expectedCount,
        out List<GeneratedWorldNetworkPiece> pieces,
        out string error)
    {
        GeneratedWorldNetworkPiece[] found =
            FindObjectsByType<GeneratedWorldNetworkPiece>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        pieces = new List<GeneratedWorldNetworkPiece>();

        for (int i = 0; i < found.Length; i++)
        {
            GeneratedWorldNetworkPiece piece = found[i];

            if (piece == null ||
                piece.gameObject.scene != gameObject.scene ||
                !piece.IsSpawned)
            {
                continue;
            }

            if (!piece.IsInitialized)
            {
                error =
                    $"Piece '{piece.name}' is spawned but has not " +
                    $"received its generated context.";
                pieces = null;
                return false;
            }

            pieces.Add(piece);
        }

        if (pieces.Count != expectedCount)
        {
            error =
                $"Received {pieces.Count}/{expectedCount} world pieces.";
            pieces = null;
            return false;
        }

        pieces.Sort(
            (a, b) =>
                a.GenerationOrder.CompareTo(b.GenerationOrder));

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].GenerationOrder == i)
                continue;

            error =
                $"Expected generation order {i}, but found " +
                $"{pieces[i].GenerationOrder} on '{pieces[i].name}'.";
            pieces = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ApplyGrassAndRebuild()
    {
        if (grassBridge == null)
            grassBridge = FindFirstObjectByType<IslandGrassRouteBridge>();

        if (grassBridge != null)
        {
            AboveRoutePiece[] routePieces =
                FindObjectsByType<AboveRoutePiece>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < routePieces.Length; i++)
            {
                if (routePieces[i].gameObject.scene == gameObject.scene &&
                    routePieces[i].HasGeneratedContext)
                {
                    grassBridge.ApplyPresetToPiece(routePieces[i]);
                }
            }
        }

        if (worldGrassManager == null)
        {
            worldGrassManager =
                FindFirstObjectByType<WorldGrassManager>();
        }

        if (worldGrassManager != null)
            worldGrassManager.RebuildNow();
        else
            WorldGrassManager.NotifySourcesChanged();
    }

    private static int CalculateWorldHash(
        List<GeneratedWorldNetworkPiece> pieces)
    {
        uint hash = 2166136261;

        AddHash(ref hash, pieces.Count);

        for (int i = 0; i < pieces.Count; i++)
        {
            GeneratedWorldNetworkPiece piece = pieces[i];

            // Hash only context explicitly synchronized by
            // GeneratedWorldNetworkPiece. Spawn transforms are replicated by
            // FishNet and may differ slightly after client-side parenting,
            // interpolation or animation, so they are not readiness data.
            AddHash(ref hash, piece.GenerationOrder);
            AddHash(ref hash, (int)piece.Kind);
            AddHash(ref hash, (int)piece.Biome);
            AddHash(ref hash, piece.IsMainIsland ? 1 : 0);
            AddHash(ref hash, piece.MainIslandIndex);
            AddHash(ref hash, piece.IsBeacon ? 1 : 0);
            AddHash(ref hash, piece.IsClusterPiece ? 1 : 0);
            AddHash(ref hash, (int)piece.BackgroundLayer);
        }

        return unchecked((int)hash);
    }

    private static void AddHash(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (byte)value;
            hash *= 16777619;

            hash ^= (byte)(value >> 8);
            hash *= 16777619;

            hash ^= (byte)(value >> 16);
            hash *= 16777619;

            hash ^= (byte)(value >> 24);
            hash *= 16777619;
        }
    }

    private void FailV3Generation(string reason)
    {
        // Registered clients may still be waiting for the server to finish. Their
        // per-client timeout starts only after descriptor delivery, so reject now.
        FailServerGeneration(reason);
        if (V3Backend) V3Backend.Clear();
        foreach (var connection in _registeredClients.Values.ToArray())
            if (connection != null && connection.IsActive) ServerRejectClient(connection, reason);
    }

    private IEnumerator GenerateV3World()
    {
        WorldSeed = CreateSeed();
        try {
            V3Backend.BuildServer(WorldSeed, root => _spawnedWorldRoots.Add(root));
            WorldHash = unchecked((int)Above.GridV3.V3WorldBuilder.StableHash(V3Backend.WorldHash));
            SpawnedPieceCount = V3Backend.Result.timings.islands + V3Backend.Result.timings.connectors;
        } catch (Exception e) { DespawnPartiallySpawnedWorld(); V3Backend.Clear(); FailV3Generation(e.ToString()); yield break; }
        yield return V3Backend.WaitForPresentation();
        if (!string.IsNullOrEmpty(V3Backend.PresentationError)) { DespawnPartiallySpawnedWorld(); FailV3Generation(V3Backend.PresentationError); yield break; }
        ServerWorldGenerated = true;
        foreach (var connection in _registeredClients.Values.ToArray()) SendWorldCompletionToClient(connection);
    }

    [TargetRpc]
    private void TargetV3World(NetworkConnection connection, int seed, int expectedHash, string descriptor)
    {
        if (_clientReadyRoutine != null) StopCoroutine(_clientReadyRoutine);
        _clientReadyRoutine = StartCoroutine(ClientBuildV3(seed, expectedHash, descriptor));
    }
    private IEnumerator ClientBuildV3(int seed, int expectedHash, string descriptor)
    {
        string error = null;
        try { if (!V3Backend) throw new InvalidOperationException("V3 backend missing from client scene."); V3Backend.BuildClient(descriptor, seed); }
        catch (Exception e) { error = e.Message; }
        if (error != null) { V3Backend?.Clear(); ServerReportWorldReady(seed, 0, false, error); yield break; }
        yield return V3Backend.WaitForPresentation();
        if (!string.IsNullOrEmpty(V3Backend.PresentationError)) { ServerReportWorldReady(seed, 0, false, V3Backend.PresentationError); yield break; }
        int hash = unchecked((int)Above.GridV3.V3WorldBuilder.StableHash(V3Backend.WorldHash));
        ServerReportWorldReady(seed, hash, hash == expectedHash, hash == expectedHash ? "" : "V3 hash mismatch");
        _clientReadyRoutine = null;
    }
    private static int CreateSeed()
    {
        return BitConverter.ToInt32(
            Guid.NewGuid().ToByteArray(),
            0);
    }

    [Server]
    private void FailServerGeneration(string reason)
    {
        if (eventDirector != null) eventDirector.ClearServerEncounters();
        ServerWorldGenerated = false;
        ServerWorldGenerationFailed = true;
        ServerFailureReason = reason;

        Debug.LogError(
            $"[World Generation] {reason}",
            this);
    }   

    [Server]
    private void DespawnPartiallySpawnedWorld()
    {
        for (int i = _spawnedWorldRoots.Count - 1; i >= 0; i--)
        {
            NetworkObject root = _spawnedWorldRoots[i];

            if (root != null && root.IsSpawned)
                ServerManager.Despawn(root);
            else if (root != null && V3Backend) Destroy(root.gameObject);
        }

        _spawnedWorldRoots.Clear();
    }
}
