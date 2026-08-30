using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Generators")]
    [SerializeField] private IslandRouteGenerator routeGenerator;
    [SerializeField] private BackgroundIslandGenerator backgroundGenerator;

    [Header("Grass")]
    [SerializeField] private IslandGrassRouteBridge grassBridge;
    [SerializeField] private WorldGrassManager worldGrassManager;

    [Header("Events")]
    [SerializeField] private RunEventDirector eventDirector;

    [Header("Runtime Navigation")]
    [SerializeField] private RuntimeRouteNavMesh runtimeRouteNavMesh;

    [Header("Generation")]
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

    public event Action<NetworkConnection> ServerClientWorldReady;
    public event Action<NetworkConnection, string> ServerClientWorldFailed;

    private void Awake()
    {
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
            applyWindSelectedLevel && Wind.Instance != null;

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
        else if (applyAssignedLevelOnStart)
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
        error = string.Empty;
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
        _assignedLevelApplyError = string.Empty;
        error = string.Empty;
        return true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        GenerateAndSpawnServerWorld();
    }

    public override void OnStopServer()
    {
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

        for (int attempt = 1;
             attempt <= maximumSeedAttempts;
             attempt++)
        {
            int candidateSeed = CreateSeed();

            backgroundGenerator.ClearGeneratedBackground();
            if (runtimeRouteNavMesh != null)
                runtimeRouteNavMesh.ClearBuiltNavMesh();
            routeGenerator.ClearGeneratedRoute();

            routeGenerator.Generation.UseRandomSeed = false;
            routeGenerator.Generation.Seed = candidateSeed;

            backgroundGenerator.Integration.UseRouteSeed = true;
            backgroundGenerator.Integration.GenerateWithRoute = false;

            if (!routeGenerator.GenerateRoute())
            {
                Debug.LogWarning(
                    $"[World Generation] Seed attempt {attempt}/" +
                    $"{maximumSeedAttempts} failed: " +
                    routeGenerator.LastFailureReason,
                    this);

                continue;
            }

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
                Debug.LogWarning(
                    $"[World Generation] Background generation failed " +
                    $"for seed {candidateSeed}: " +
                    backgroundGenerator.LastGenerationReport,
                    this);

                continue;
            }

            WorldSeed = candidateSeed;
            generated = true;
            break;
        }

        if (!generated)
        {
            FailServerGeneration(
                $"No world could be generated after " +
                $"{maximumSeedAttempts} seed attempts.");
            return;
        }

        if (!ValidateGeneratedNetworkRoots(out string networkError))
        {
            FailServerGeneration(networkError);
            return;
        }

        try
        {
            SpawnedPieceCount = SpawnGeneratedWorld();
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

            ServerManager.Spawn(root);

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

            ServerManager.Spawn(root);

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

        TargetWorldSpawnComplete(
            connection,
            WorldSeed,
            SpawnedPieceCount,
            WorldHash);

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
        int expectedHash)
    {
        if (_clientReadyRoutine != null)
            StopCoroutine(_clientReadyRoutine);

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

    private static int CreateSeed()
    {
        return BitConverter.ToInt32(
            Guid.NewGuid().ToByteArray(),
            0);
    }

    [Server]
    private void FailServerGeneration(string reason)
    {
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
        }

        _spawnedWorldRoots.Clear();
    }
}
