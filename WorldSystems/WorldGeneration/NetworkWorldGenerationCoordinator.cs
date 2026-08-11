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
    [Header("Generators")]
    [SerializeField] private IslandRouteGenerator routeGenerator;
    [SerializeField] private BackgroundIslandGenerator backgroundGenerator;

    [Header("Grass")]
    [SerializeField] private IslandGrassRouteBridge grassBridge;
    [SerializeField] private WorldGrassManager worldGrassManager;

    [Header("Generation")]
    [SerializeField, Min(1)] private int maximumSeedAttempts = 12;

    [Header("Client Readiness")]
    [SerializeField, Min(5f)] private float clientReadyTimeout = 45f;
    [SerializeField] private bool disconnectClientOnFailure = true;
    
    [Header("Diagnostics")]
    [SerializeField] private bool logWorldHashDiagnostics = true;
    
    private readonly Dictionary<int, NetworkConnection>
        _registeredClients = new();

    private readonly HashSet<int> _seedSentClients = new();
    private readonly HashSet<int> _readyClients = new();
    private readonly List<NetworkObject> _spawnedWorldRoots = new();

    private Coroutine _clientReadyRoutine;

    public bool ServerWorldGenerated { get; private set; }
    public bool ServerWorldGenerationFailed { get; private set; }
    public string ServerFailureReason { get; private set; }

    public int WorldSeed { get; private set; }
    public int WorldHash { get; private set; }
    public int SpawnedPieceCount { get; private set; }

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

        // The coordinator is now the only system allowed to start generation.
        if (routeGenerator != null)
            routeGenerator.Generation.GenerateOnStart = false;

        if (backgroundGenerator != null)
            backgroundGenerator.Integration.GenerateWithRoute = false;
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

        bool generated = false;

        for (int attempt = 1;
             attempt <= maximumSeedAttempts;
             attempt++)
        {
            int candidateSeed = CreateSeed();

            backgroundGenerator.ClearGeneratedBackground();
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
        if (logWorldHashDiagnostics)
        {
            LogWorldHashDiagnostics(
                $"SERVER seed={WorldSeed}",
                pieces,
                WorldHash);
        }
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
            
            root.transform.SetParent(null, true);
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
            root.transform.SetParent(null, true);
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
            if (logWorldHashDiagnostics)
            {
                LogWorldHashDiagnostics(
                    $"CLIENT seed={seed}",
                    pieces,
                    localHash);
            }

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

            AddHash(ref hash, piece.GenerationOrder);
            AddHash(ref hash, (int)piece.Kind);
            AddHash(ref hash, (int)piece.Biome);
            AddHash(ref hash, piece.IsMainIsland ? 1 : 0);
            AddHash(ref hash, piece.MainIslandIndex);
            AddHash(ref hash, piece.IsBeacon ? 1 : 0);
            AddHash(ref hash, piece.IsClusterPiece ? 1 : 0);
            AddHash(ref hash, (int)piece.BackgroundLayer);

            Transform root = piece.transform;

            AddVector(ref hash, root.position, 100f);
            AddRotation(ref hash, root.rotation);
            AddVector(ref hash, root.lossyScale, 1000f);
        }

        return unchecked((int)hash);
    }

    private static void AddVector(
        ref uint hash,
        Vector3 value,
        float precision)
    {
        AddHash(ref hash, Mathf.RoundToInt(value.x * precision));
        AddHash(ref hash, Mathf.RoundToInt(value.y * precision));
        AddHash(ref hash, Mathf.RoundToInt(value.z * precision));
    }

    private static void AddRotation(
        ref uint hash,
        Quaternion rotation)
    {
        // q and -q represent the same rotation.
        if (rotation.w < 0f)
        {
            rotation.x = -rotation.x;
            rotation.y = -rotation.y;
            rotation.z = -rotation.z;
            rotation.w = -rotation.w;
        }

        AddHash(ref hash, Mathf.RoundToInt(rotation.x * 1000f));
        AddHash(ref hash, Mathf.RoundToInt(rotation.y * 1000f));
        AddHash(ref hash, Mathf.RoundToInt(rotation.z * 1000f));
        AddHash(ref hash, Mathf.RoundToInt(rotation.w * 1000f));
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

    private static void LogWorldHashDiagnostics(
        string side,
        List<GeneratedWorldNetworkPiece> pieces,
        int finalHash)
    {
        uint rollingHash = 2166136261;

        AddHash(ref rollingHash, pieces.Count);

        Debug.Log(
            $"[World Hash][{side}] BEGIN " +
            $"count={pieces.Count}, " +
            $"hashAfterCount={unchecked((int)rollingHash)}, " +
            $"finalHash={finalHash}");

        for (int i = 0; i < pieces.Count; i++)
        {
            GeneratedWorldNetworkPiece piece = pieces[i];
            Transform root = piece.transform;

            Vector3 position = root.position;
            Vector3 scale = root.lossyScale;
            Quaternion rotation = root.rotation;

            // q and -q represent the same rotation.
            if (rotation.w < 0f)
            {
                rotation.x = -rotation.x;
                rotation.y = -rotation.y;
                rotation.z = -rotation.z;
                rotation.w = -rotation.w;
            }

            int px = Mathf.RoundToInt(position.x * 100f);
            int py = Mathf.RoundToInt(position.y * 100f);
            int pz = Mathf.RoundToInt(position.z * 100f);

            int rx = Mathf.RoundToInt(rotation.x * 10000f);
            int ry = Mathf.RoundToInt(rotation.y * 10000f);
            int rz = Mathf.RoundToInt(rotation.z * 10000f);
            int rw = Mathf.RoundToInt(rotation.w * 10000f);

            int sx = Mathf.RoundToInt(scale.x * 1000f);
            int sy = Mathf.RoundToInt(scale.y * 1000f);
            int sz = Mathf.RoundToInt(scale.z * 1000f);

            // Keep this in exactly the same order as CalculateWorldHash().
            AddHash(ref rollingHash, piece.GenerationOrder);
            AddHash(ref rollingHash, (int)piece.Kind);
            AddHash(ref rollingHash, (int)piece.Biome);
            AddHash(ref rollingHash, piece.IsMainIsland ? 1 : 0);
            AddHash(ref rollingHash, piece.MainIslandIndex);
            AddHash(ref rollingHash, piece.IsBeacon ? 1 : 0);
            AddHash(ref rollingHash, piece.IsClusterPiece ? 1 : 0);
            AddHash(ref rollingHash, (int)piece.BackgroundLayer);

            AddHash(ref rollingHash, px);
            AddHash(ref rollingHash, py);
            AddHash(ref rollingHash, pz);

            AddHash(ref rollingHash, rx);
            AddHash(ref rollingHash, ry);
            AddHash(ref rollingHash, rz);
            AddHash(ref rollingHash, rw);

            AddHash(ref rollingHash, sx);
            AddHash(ref rollingHash, sy);
            AddHash(ref rollingHash, sz);

            Debug.Log(
                $"[World Hash][{side}] " +
                $"row={i:000} " +
                $"rolling={unchecked((int)rollingHash)} " +
                $"name='{piece.name}' " +
                $"order={piece.GenerationOrder} " +
                $"kind={(int)piece.Kind} " +
                $"biome={(int)piece.Biome} " +
                $"main={(piece.IsMainIsland ? 1 : 0)} " +
                $"mainIndex={piece.MainIslandIndex} " +
                $"beacon={(piece.IsBeacon ? 1 : 0)} " +
                $"cluster={(piece.IsClusterPiece ? 1 : 0)} " +
                $"layer={(int)piece.BackgroundLayer} " +
                $"posQ=({px},{py},{pz}) " +
                $"rotQ=({rx},{ry},{rz},{rw}) " +
                $"scaleQ=({sx},{sy},{sz}) " +
                $"posRaw=({position.x:F6},{position.y:F6},{position.z:F6}) " +
                $"rotRaw=({rotation.x:F6},{rotation.y:F6}," +
                $"{rotation.z:F6},{rotation.w:F6}) " +
                $"scaleRaw=({scale.x:F6},{scale.y:F6},{scale.z:F6})");
        }

        Debug.Log(
            $"[World Hash][{side}] END " +
            $"calculated={unchecked((int)rollingHash)}, " +
            $"expectedCalculation={finalHash}");
    }
}