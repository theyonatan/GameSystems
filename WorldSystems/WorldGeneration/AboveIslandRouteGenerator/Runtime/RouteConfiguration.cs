using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RouteConfiguration",
    menuName = "Above/World Generation/Route Configuration")]
public sealed class RouteConfiguration : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable runtime ID. Use lowercase words separated by underscores, such as demo_first_steps.")]
    [SerializeField]
    private string routeId = "new_route";

    [SerializeField]
    private string displayName = "New Route";

    [Header("Route Settings")]
    [SerializeField]
    private IslandGenerationSettings generation =
        new IslandGenerationSettings();

    [SerializeField]
    private IslandRhythmSettings rhythm =
        new IslandRhythmSettings();

    [SerializeField]
    private IslandRouteShapeSettings routeShape =
        new IslandRouteShapeSettings();

    [SerializeField]
    private IslandDetourSettings detours =
        new IslandDetourSettings();

    [SerializeField]
    private List<BiomePhase> biomePhases =
        new List<BiomePhase>();

    [SerializeField]
    private List<ClusterPhaseRule> clusterPhases =
        new List<ClusterPhaseRule>();

    [SerializeField]
    private List<IslandPoolEntry> islandPrefabs =
        new List<IslandPoolEntry>();

    [SerializeField]
    private List<ConnectionPoolEntry> connectionPrefabs =
        new List<ConnectionPoolEntry>();

    [SerializeField]
    private List<SpecialIslandRule> specialIslands =
        new List<SpecialIslandRule>();

    public string RouteId => routeId;
    public string DisplayName => displayName;
    public IslandGenerationSettings Generation => generation;
    public IslandRhythmSettings Rhythm => rhythm;
    public IslandRouteShapeSettings RouteShape => routeShape;
    public IslandDetourSettings Detours => detours;
    public List<BiomePhase> BiomePhases => biomePhases;
    public List<ClusterPhaseRule> ClusterPhases => clusterPhases;
    public List<IslandPoolEntry> IslandPrefabs => islandPrefabs;
    public List<ConnectionPoolEntry> ConnectionPrefabs => connectionPrefabs;
    public List<SpecialIslandRule> SpecialIslands => specialIslands;
}
