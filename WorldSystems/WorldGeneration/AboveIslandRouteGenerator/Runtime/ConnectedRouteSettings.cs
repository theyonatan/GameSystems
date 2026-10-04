using System;
using UnityEngine;

/// <summary>Closed, nearby alternatives around the existing Main Route socket chain.</summary>
[Serializable]
public sealed class ConnectedRouteSettings
{
    public bool Enabled;
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
}
