using System;
using UnityEngine;

/// <summary>Closed, nearby alternatives around the existing Main Route socket chain.</summary>
[Serializable]
public sealed class ConnectedRouteSettings
{
    public bool Enabled;
    [Tooltip("Load Approved Layout directly, without procedural search or fallback retries. Only applies when connected mode is enabled.")]
    public bool UseApprovedLayout;
    public ConnectedRouteLayout ApprovedLayout;
    [Tooltip("In connected mode, sideways distance affects ranking but does not reject a placement. Socket fit, overlap and heading checks remain enforced.")]
    public bool IgnoreLateralLayoutLimit;
    [Tooltip("Record detailed socket/bridge closure diagnostics. Disabled during normal generation.")]
    public bool Diagnostics;
    [Tooltip("Only diagnose closures targeting this planner label. Leave empty to capture all targets. Captured calls are bounded.")]
    public string DiagnosticsTargetLabel = "Cluster 17";
    [Min(1)] public int MinimumCycles = 2;
    [Min(1)] public int MaximumCycles = 3;
    [Min(1)] public int MinimumIslandsPerCycle = 2;
    [Min(1)] public int MaximumIslandsPerCycle = 4;
    [Tooltip("Bounded native island/connection search; failed branches are removed from the plan.")]
    [Min(20)] public int SearchBudget = 1200;
    [Min(1)] public int CandidatesPerStep = 8;
    [Min(1)] public float MaximumConnectionSpan = 14f;
    [Min(.01f)] public float ClosurePositionTolerance = .7f;
    [Range(.1f,30f)] public float ClosureAngleTolerance = 12f;
    [Tooltip("Zero uses a fresh seed. A nonzero value reproduces the development example.")]
    public int PreviewSeed;
    [Tooltip("Island sizes allowed in reconnecting branches; normal pool/biome/phase rules still apply.")]
    public IslandSize[] BranchSizes = { IslandSize.Small, IslandSize.Medium };
    [Min(0)] public int MaximumMainRepairs = 3;
    [Min(1)] public int RepairBacktrackIslands = 2;
    [Tooltip("Total placement/closure combinations examined across one plan, including repairs.")]
    [Min(100)] public int MaximumPlacementEvaluations = 60000;
    [Tooltip("Emergency time limit for one connected plan. Work limits normally stop it first.")]
    [Min(.1f)] public float MaximumSearchSeconds = 2f;
    [Tooltip("Random startup switches to the saved fallback after this total search time.")]
    [Min(.1f)] public float MaximumGenerationSeconds = 8f;
    [Min(1)] public float MaximumReconnectionDistance = 110f;
    public ConnectedRouteLayout FallbackLayout;
}
