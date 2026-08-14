using System;
using System.Collections.Generic;
using FishNet;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds one NavMesh for the complete generated route. The NavMeshSurface is
/// created on the generator's transient route root, so Children collection
/// includes every generated island, connection and route-prefab blocker while
/// excluding the decorative background-island hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeRouteNavMesh : MonoBehaviour
{
    [Header("Route")]
    [SerializeField]
    private IslandRouteGenerator routeGenerator;

    [Header("Runtime Bake")]
    [Tooltip("Must match the Agent Type used by the Sentinel NavMeshAgent.")]
    [SerializeField]
    private int agentTypeId;

    [Tooltip("Only geometry on these layers contributes to the route NavMesh.")]
    [SerializeField]
    private LayerMask includedLayers = ~0;

    [Tooltip("Physics Colliders include generated props that block navigation.")]
    [SerializeField]
    private NavMeshCollectGeometry useGeometry =
        NavMeshCollectGeometry.PhysicsColliders;

    [SerializeField]
    private bool overrideVoxelSize;

    [SerializeField, Min(0.01f)]
    private float voxelSize = 0.1f;

    [SerializeField]
    private bool overrideTileSize;

    [SerializeField, Min(16)]
    private int tileSize = 256;

    [Header("Diagnostics")]
    [SerializeField]
    private bool logSuccessfulBuild = true;

    private NavMeshSurface _surface;
    private bool _subscribed;
    private int _builtSeed;
    private int _builtPieceCount;
    private AboveRoutePiece _builtFirstPiece;
    private AboveRoutePiece _builtLastPiece;

    public IslandRouteGenerator RouteGenerator => routeGenerator;
    public int AgentTypeId => agentTypeId;
    public bool IsBuilt =>
        _surface != null && _surface.navMeshData != null;
    public string LastBuildReport { get; private set; } = string.Empty;

    private void Awake()
    {
        ResolveRouteGenerator();
    }

    private void OnEnable()
    {
        ResolveRouteGenerator();
        SubscribeToRouteGenerator();
    }

    private void OnDisable()
    {
        UnsubscribeFromRouteGenerator();
        ClearBuiltNavMesh();
    }

    private void OnValidate()
    {
        voxelSize = Mathf.Max(0.01f, voxelSize);
        tileSize = Mathf.Max(16, tileSize);
    }

    public bool ValidateConfiguration(out string report)
    {
        ResolveRouteGenerator();

        List<string> errors = new List<string>();

        if (routeGenerator == null)
            errors.Add("Route Generator is not assigned.");

        NavMeshBuildSettings settings =
            NavMesh.GetSettingsByID(agentTypeId);

        if (settings.agentTypeID < 0)
        {
            errors.Add(
                $"Agent Type ID {agentTypeId} does not exist. Match this " +
                "to the Sentinel prefab's NavMeshAgent Agent Type.");
        }

        if (includedLayers.value == 0)
            errors.Add("Included Layers is empty; the bake would collect no geometry.");

        if (overrideVoxelSize && voxelSize <= 0f)
            errors.Add("Voxel Size must be greater than zero.");

        if (overrideTileSize && tileSize < 16)
            errors.Add("Tile Size must be at least 16 voxels.");

        report = errors.Count == 0
            ? "Runtime route NavMesh configuration is valid."
            : string.Join("\n", errors.ToArray());

        return errors.Count == 0;
    }

    /// <summary>
    /// Idempotently builds the NavMesh for the generator's current concrete
    /// route. In play mode only the FishNet server is allowed to build it.
    /// </summary>
    public bool EnsureBuiltForCurrentRoute(out string report)
    {
        ResolveRouteGenerator();

        if (IsReadyForCurrentRoute())
        {
            report = LastBuildReport;
            return true;
        }

        if (Application.isPlaying && !InstanceFinder.IsServerStarted)
        {
            report =
                "Runtime route NavMesh baking is server-only, but no FishNet " +
                "server is running in this process.";
            LastBuildReport = report;
            return false;
        }

        if (!ValidateConfiguration(out string validationReport))
        {
            report =
                "Runtime route NavMesh configuration is invalid:\n" +
                validationReport;
            LastBuildReport = report;
            return false;
        }

        IReadOnlyList<AboveRoutePiece> pieces =
            routeGenerator.GeneratedInstances;

        if (pieces == null || pieces.Count == 0)
        {
            report =
                "No generated route exists. Generate the complete route " +
                "before building its runtime NavMesh.";
            LastBuildReport = report;
            return false;
        }

        Transform generatedRoot = pieces[0] != null
            ? pieces[0].transform.parent
            : null;

        if (generatedRoot == null)
        {
            report =
                "The first generated route piece has no generated route root.";
            LastBuildReport = report;
            return false;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            AboveRoutePiece piece = pieces[i];

            if (piece == null || !piece.transform.IsChildOf(generatedRoot))
            {
                report =
                    "Generated route pieces do not share one route root, so " +
                    "a Children-only route NavMesh cannot be built safely.";
                LastBuildReport = report;
                return false;
            }
        }

        ClearBuiltNavMesh();

        try
        {
            _surface = generatedRoot.GetComponent<NavMeshSurface>();

            if (_surface == null)
                _surface = generatedRoot.gameObject.AddComponent<NavMeshSurface>();

            _surface.agentTypeID = agentTypeId;
            _surface.collectObjects = CollectObjects.Children;
            _surface.layerMask = includedLayers;
            _surface.useGeometry = useGeometry;
            _surface.overrideVoxelSize = overrideVoxelSize;
            _surface.voxelSize = voxelSize;
            _surface.overrideTileSize = overrideTileSize;
            _surface.tileSize = tileSize;
            _surface.BuildNavMesh();

            if (_surface.navMeshData == null)
            {
                report =
                    "NavMeshSurface.BuildNavMesh completed without producing " +
                    "NavMesh data. Check Included Layers, colliders and Agent Type.";
                LastBuildReport = report;
                ClearBuiltNavMesh();
                return false;
            }
        }
        catch (Exception exception)
        {
            report =
                $"Runtime route NavMesh build threw " +
                $"{exception.GetType().Name}: {exception.Message}";
            LastBuildReport = report;
            ClearBuiltNavMesh();
            return false;
        }

        RememberBuiltRoute(pieces);

        LastBuildReport =
            $"Built one runtime route NavMesh for {pieces.Count} generated " +
            $"piece(s), seed {routeGenerator.LastUsedSeed}, using " +
            $"{useGeometry} and Children collection.";
        report = LastBuildReport;

        if (logSuccessfulBuild)
            Debug.Log("[Runtime Route NavMesh] " + report, this);

        return true;
    }

    public bool IsReadyForCurrentRoute()
    {
        if (!IsBuilt || routeGenerator == null)
            return false;

        IReadOnlyList<AboveRoutePiece> pieces =
            routeGenerator.GeneratedInstances;

        if (pieces == null || pieces.Count == 0)
            return false;

        AboveRoutePiece first = pieces[0];
        AboveRoutePiece last = pieces[pieces.Count - 1];

        return _builtSeed == routeGenerator.LastUsedSeed &&
               _builtPieceCount == pieces.Count &&
               _builtFirstPiece == first &&
               _builtLastPiece == last;
    }

    public void ClearBuiltNavMesh()
    {
        if (_surface != null)
        {
            try
            {
                _surface.RemoveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Runtime Route NavMesh] Removing the previous runtime " +
                    $"NavMesh failed: {exception.Message}",
                    this);
            }
        }

        _surface = null;
        _builtSeed = 0;
        _builtPieceCount = 0;
        _builtFirstPiece = null;
        _builtLastPiece = null;
        LastBuildReport = string.Empty;
    }

    private void RememberBuiltRoute(
        IReadOnlyList<AboveRoutePiece> pieces)
    {
        _builtSeed = routeGenerator.LastUsedSeed;
        _builtPieceCount = pieces.Count;
        _builtFirstPiece = pieces[0];
        _builtLastPiece = pieces[pieces.Count - 1];
    }

    private void HandleRouteCleared()
    {
        ClearBuiltNavMesh();
    }

    private void ResolveRouteGenerator()
    {
        if (routeGenerator == null)
            routeGenerator = FindFirstObjectByType<IslandRouteGenerator>();
    }

    private void SubscribeToRouteGenerator()
    {
        if (_subscribed || routeGenerator == null)
            return;

        routeGenerator.RouteCleared += HandleRouteCleared;
        _subscribed = true;
    }

    private void UnsubscribeFromRouteGenerator()
    {
        if (!_subscribed || routeGenerator == null)
            return;

        routeGenerator.RouteCleared -= HandleRouteCleared;
        _subscribed = false;
    }
}
