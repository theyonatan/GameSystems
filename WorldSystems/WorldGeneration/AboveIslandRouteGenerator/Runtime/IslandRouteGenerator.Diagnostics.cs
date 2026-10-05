using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class IslandRouteGenerator
{
    public ClosureDiagnostics LastClosureDiagnostics { get; private set; }

    public sealed class ClosureDiagnostics
    {
        public int TotalCalls, DroppedCalls;
        public ClosureDiagnosticCall FirstCall;
        public readonly List<ClosureDiagnosticCall> Calls = new List<ClosureDiagnosticCall>();
        public readonly List<object> Repairs = new List<object>();
        public readonly Dictionary<string, PlacementRuleDiagnostics> PlacementRules = new Dictionary<string, PlacementRuleDiagnostics>();
    }

    public sealed class PlacementRuleDiagnostics
    {
        public int CandidateTests, CandidatesAccepted, CandidatesRejected, SocketTests;
        public int IndependentlyOutsideHeading, IndependentlyOutsideProgress;
        public readonly Dictionary<string, int> FirstResults = new Dictionary<string, int>();
        public readonly List<PlacementRuleExample> Examples = new List<PlacementRuleExample>();
    }

    public sealed class PlacementRuleExample
    {
        public string Stage, FirstResult, Source, SourcePrefab, SourceSocket, Connector, Island, Entry, EvaluatedSocket, Target, OverlappingPiece;
        public bool CandidateAccepted, HeadingWouldPass, ProgressWouldPass, LateralWouldPass, HeightWouldPass;
        public float Heading, HeadingLimit, Progress, RequiredProgress, Lateral, LateralLimit, Height, MinimumHeight, MaximumHeight;
        public float[] SourcePosition, IslandPosition, SocketPosition, FrameOrigin, FrameForward, TargetPosition;
    }

    private sealed class DiagnosticRepairHint
    {
        public BranchRepairHint Hint;
        public PiecePlacement IslandPlacement;
    }

    private readonly Dictionary<int, DiagnosticRepairHint> diagnosticRepairHints = new Dictionary<int, DiagnosticRepairHint>();
    private List<PairCandidate> diagnosticRepairCandidates;
    private PlacementDiagnostics diagnosticRepairProbeRules;
    private bool PlacementDiagnosticsEnabled => connectedRoute.Enabled && connectedRoute.Diagnostics &&
        connectedWork != null && LastClosureDiagnostics != null;

    private void BeginPlacementRuleDiagnostics(PlacementDiagnostics diagnostics, PlannedPiece source, string stage)
    {
        if (!PlacementDiagnosticsEnabled) return;
        diagnostics.DiagnosticSource = source;
        diagnostics.DiagnosticStage = stage;
    }

    private void PreparePlacementRuleCandidate(PlacementDiagnostics diagnostics, AboveIsland island, PiecePlacement placement, int entry)
    {
        if (!PlacementDiagnosticsEnabled || diagnostics.DiagnosticStage == null) return;
        diagnostics.DiagnosticIsland = island;
        diagnostics.DiagnosticPlacement = placement;
        diagnostics.DiagnosticEntry = entry;
        diagnostics.DiagnosticSocket = entry;
        diagnostics.DiagnosticCurrentExamples?.Clear();
    }

    private PlacementRuleDiagnostics PlacementRuleBucket(string stage)
    {
        if (!LastClosureDiagnostics.PlacementRules.TryGetValue(stage, out var bucket))
        {
            bucket = new PlacementRuleDiagnostics();
            LastClosureDiagnostics.PlacementRules.Add(stage, bucket);
        }
        return bucket;
    }

    private void RecordPlacementRuleSocket(PlacementDiagnostics diagnostics, SocketPose pose, RouteFrame frame,
        string result, float heading, float progress, float lateral, float height)
    {
        if (!PlacementDiagnosticsEnabled || diagnostics.DiagnosticStage == null) return;
        var bucket = PlacementRuleBucket(diagnostics.DiagnosticStage);
        bucket.SocketTests++;
        bucket.FirstResults.TryGetValue(result, out int count); bucket.FirstResults[result] = count + 1;
        float required = frame.CurrentProgress + routeShape.MinimumForwardProgressPerIsland;
        if (heading > frame.MaximumHeadingAngle) bucket.IndependentlyOutsideHeading++;
        if (progress < required) bucket.IndependentlyOutsideProgress++;
        if ((result != "heading" && result != "progress") || bucket.Examples.Count(e => e.FirstResult == result) >= 8) return;
        var source = diagnostics.DiagnosticSource;
        var island = diagnostics.DiagnosticIsland;
        string evaluated = island.GetSocket(diagnostics.DiagnosticSocket).name;
        if (bucket.Examples.Any(e => e.FirstResult == result && e.Source == source?.Label &&
            e.Connector == diagnostics.DiagnosticConnection?.name && e.Island == island.name && e.EvaluatedSocket == evaluated)) return;
        var example = new PlacementRuleExample {
            Stage = diagnostics.DiagnosticStage, FirstResult = result, Source = source?.Label, SourcePrefab = source?.Prefab.name,
            SourceSocket = source == null ? null : source.Prefab.GetSocket(diagnostics.DiagnosticSourceSocket).name,
            Connector = diagnostics.DiagnosticConnection?.name, Island = island.name,
            Entry = island.GetSocket(diagnostics.DiagnosticEntry).name, EvaluatedSocket = evaluated,
            Target = connectedWork.BranchTarget?.Label, OverlappingPiece = diagnostics.DiagnosticOverlap,
            Heading = heading, HeadingLimit = frame.MaximumHeadingAngle, Progress = progress, RequiredProgress = required,
            Lateral = lateral, LateralLimit = frame.MaximumLateralDrift, Height = height,
            MinimumHeight = routeShape.MinimumRelativeHeight, MaximumHeight = routeShape.MaximumRelativeHeight,
            HeadingWouldPass = heading <= frame.MaximumHeadingAngle, ProgressWouldPass = progress >= required,
            LateralWouldPass = lateral <= frame.MaximumLateralDrift || (connectedRoute.Enabled && connectedRoute.IgnoreLateralLayoutLimit),
            HeightWouldPass = height >= routeShape.MinimumRelativeHeight && height <= routeShape.MaximumRelativeHeight,
            SourcePosition = source == null ? null : DiagnosticVector(source.Placement.Position),
            IslandPosition = DiagnosticVector(diagnostics.DiagnosticPlacement.Position), SocketPosition = DiagnosticVector(pose.Position),
            FrameOrigin = DiagnosticVector(frame.Origin), FrameForward = DiagnosticVector(frame.Forward),
            TargetPosition = connectedWork.BranchTarget == null ? null : DiagnosticVector(connectedWork.BranchTarget.Placement.Position)
        };
        bucket.Examples.Add(example);
        if (diagnostics.DiagnosticCurrentExamples == null) diagnostics.DiagnosticCurrentExamples = new List<PlacementRuleExample>();
        diagnostics.DiagnosticCurrentExamples.Add(example);
    }

    private void FinishPlacementRuleCandidate(PlacementDiagnostics diagnostics, bool accepted)
    {
        if (!PlacementDiagnosticsEnabled || diagnostics.DiagnosticStage == null) return;
        var bucket = PlacementRuleBucket(diagnostics.DiagnosticStage);
        bucket.CandidateTests++;
        if (accepted) bucket.CandidatesAccepted++; else bucket.CandidatesRejected++;
        if (diagnostics.DiagnosticCurrentExamples != null)
            foreach (var sample in diagnostics.DiagnosticCurrentExamples) sample.CandidateAccepted = accepted;
    }

    private void RecordAlternativeRepairHint(BranchRepairHint hint, PiecePlacement islandPlacement)
    {
        if (!PlacementDiagnosticsEnabled) return;
        if (!diagnosticRepairHints.TryGetValue(hint.BlockerMainIndex, out var old) || hint.ClosureScore < old.Hint.ClosureScore)
            diagnosticRepairHints[hint.BlockerMainIndex] = new DiagnosticRepairHint { Hint = hint, IslandPlacement = islandPlacement };
    }

    private object DiagnosticBounds(List<OrientedBox> bounds) => bounds.Select(b => new {
        center = DiagnosticVector(b.Center), x = DiagnosticVector(b.AxisX), y = DiagnosticVector(b.AxisY), z = DiagnosticVector(b.AxisZ),
        extents = DiagnosticVector(b.Extents), outline = b.GroundOutline?.Select(DiagnosticVector).ToArray(), clearance = b.OutlineClearance
    }).ToArray();

    private object DiagnosticRepairHintInfo(DiagnosticRepairHint saved) => new {
        blockerMainIndex = saved.Hint.BlockerMainIndex, blocker = saved.Hint.Blocker.Label, blockerPrefab = saved.Hint.Blocker.Prefab.name,
        source = saved.Hint.Source.Label, sourcePrefab = saved.Hint.Source.Prefab.name,
        sourcePosition = DiagnosticVector(saved.Hint.Source.Placement.Position), target = saved.Hint.Target.Label,
        targetPosition = DiagnosticVector(saved.Hint.Target.Placement.Position), anchor = saved.Hint.Anchor.Label,
        turnIsland = saved.Hint.Island.name, turnConnection = saved.Hint.Connection.name, closureScore = saved.Hint.ClosureScore,
        islandPosition = DiagnosticVector(saved.IslandPlacement.Position), islandYaw = saved.IslandPlacement.Rotation.eulerAngles.y,
        islandBounds = DiagnosticBounds(saved.Hint.IslandBounds), connectionBounds = DiagnosticBounds(saved.Hint.ConnectionBounds)
    };

    public sealed class ClosureDiagnosticCall
    {
        public int Id, Repairs, StartEvaluations, EndEvaluations, RemainingWork;
        public double StartSeconds, EndSeconds, PhaseDeadline;
        public float PositionLimit, AngleLimit;
        public string Outcome, PoolDiagnostics;
        public object[] Sources, Targets, Pool;
        public readonly List<object> Filters = new List<object>();
        public readonly List<ClosureDiagnosticPair> Pairs = new List<ClosureDiagnosticPair>();
        public object Budget;
    }

    public sealed class ClosureDiagnosticPair
    {
        public string Source, SourcePrefab, SourceSocket, Target, TargetPrefab, TargetSocket;
        public string Connector, ConnectorEntry, ConnectorExit, FirstRejection;
        public float Distance, Angle, PositionLimit, AngleLimit;
        public float[] SourcePosition, TargetPosition, ConnectorFarEndPosition;
        public float SourceYaw, TargetYaw, ConnectorFarEndYaw;
        public bool AngleChecked, OverlapChecked, Overlap;
    }

    private static float[] DiagnosticVector(Vector3 p) => new[] { p.x, p.y, p.z };

    private object DiagnosticSocketInfo(PlannedPiece piece, int index)
    {
        var socket = piece.Prefab.GetSocket(index);
        var pose = GetSocketPose(piece, index);
        return new { index, socket = socket.name, usage = socket.Usage.ToString(),
            route = socket.RouteUsage.ToString(), allowedConnections = socket.AllowedConnections.ToString(),
            used = piece.UsedSockets.Contains(index), position = DiagnosticVector(pose.Position),
            yaw = pose.Rotation.eulerAngles.y };
    }

    private object DiagnosticPieceInfo(PlannedPiece piece, SocketUsage usage)
    {
        var eligible = new List<int>();
        piece.Prefab.CollectSocketIndices(usage, SocketRouteUsage.Detour, piece.UsedSockets, eligible);
        return new { label = piece.Label, prefab = piece.Prefab.name,
            position = DiagnosticVector(piece.Placement.Position), yaw = piece.Placement.Rotation.eulerAngles.y,
            eligibleSocketIndices = eligible.ToArray(),
            sockets = Enumerable.Range(0, piece.Prefab.Sockets.Count).Select(i => DiagnosticSocketInfo(piece, i)).ToArray() };
    }

    private ClosureDiagnosticCall BeginClosureDiagnostics(RouteBuildState state,
        List<PlannedPiece> sources, List<PlannedPiece> targets, ClusterPhaseRule rule,
        PlacementDiagnostics diagnostics, List<ConnectionOption> options)
    {
        if (!connectedRoute.Enabled || !connectedRoute.Diagnostics || LastClosureDiagnostics == null)
            return null;
        if (!string.IsNullOrWhiteSpace(connectedRoute.DiagnosticsTargetLabel) &&
            !targets.Any(t => t.Label == connectedRoute.DiagnosticsTargetLabel)) return null;
        var call = new ClosureDiagnosticCall {
            Id = ++LastClosureDiagnostics.TotalCalls, Repairs = connectedWork?.Repairs ?? 0,
            StartEvaluations = connectedWork?.Evaluated ?? 0,
            StartSeconds = connectedWork?.Clock.Elapsed.TotalSeconds ?? 0,
            PhaseDeadline = connectedWork?.PhaseSeconds ?? double.PositiveInfinity,
            PositionLimit = rule.ExtraLinkPositionTolerance, AngleLimit = rule.ExtraLinkAngleTolerance,
            PoolDiagnostics = diagnostics.BuildSummary(),
            Sources = sources.Select(s => DiagnosticPieceInfo(s, SocketUsage.Exit)).ToArray(),
            Targets = targets.Select(t => DiagnosticPieceInfo(t, SocketUsage.Entry)).ToArray(),
            Pool = connectionPrefabs.Select((entry, index) => {
                string reason = entry == null || entry.Prefab == null ? "missing prefab" :
                    entry.ChancePercent <= 0 ? "chance <= 0" :
                    !entry.Prefab.SupportsBiome(rule.Biome) ? "biome" :
                    state.Usage.GetConnectionBlockReason(entry) != UsageBlockReason.None ?
                    state.Usage.GetConnectionBlockReason(entry).ToString() : "eligible";
                return (object)new { index, prefab = entry?.Prefab?.name, chance = entry?.ChancePercent,
                    reason, included = entry?.Prefab != null && options.Any(o => o.Prefab == entry.Prefab) };
            }).ToArray()
        };
        if (LastClosureDiagnostics.FirstCall == null) LastClosureDiagnostics.FirstCall = call;
        if (LastClosureDiagnostics.Calls.Count == 256) {
            LastClosureDiagnostics.Calls.RemoveAt(0); LastClosureDiagnostics.DroppedCalls++;
        }
        LastClosureDiagnostics.Calls.Add(call);
        return call;
    }

    private ClosureDiagnosticPair RecordClosureDiagnosticPair(ClosureDiagnosticCall call,
        PlannedPiece source, int sourceIndex, PlannedPiece target, int targetIndex,
        ConnectionIsland connector, int connectorEntry, int connectorExit,
        SocketPose sourcePose, SocketPose targetPose, SocketPose exitPose, float distance)
    {
        if (call == null) return null;
        var pair = new ClosureDiagnosticPair {
            Source = source.Label, SourcePrefab = source.Prefab.name, SourceSocket = source.Prefab.GetSocket(sourceIndex).name,
            Target = target.Label, TargetPrefab = target.Prefab.name, TargetSocket = target.Prefab.GetSocket(targetIndex).name,
            Connector = connector.name, ConnectorEntry = connector.GetSocket(connectorEntry).name,
            ConnectorExit = connector.GetSocket(connectorExit).name,
            Distance = distance, Angle = Quaternion.Angle(exitPose.Rotation, targetPose.Rotation),
            PositionLimit = call.PositionLimit, AngleLimit = call.AngleLimit,
            SourcePosition = DiagnosticVector(sourcePose.Position), TargetPosition = DiagnosticVector(targetPose.Position),
            ConnectorFarEndPosition = DiagnosticVector(exitPose.Position),
            SourceYaw = sourcePose.Rotation.eulerAngles.y, TargetYaw = targetPose.Rotation.eulerAngles.y,
            ConnectorFarEndYaw = exitPose.Rotation.eulerAngles.y
        };
        call.Pairs.Add(pair);
        return pair;
    }

    private void FinishClosureDiagnostics(ClosureDiagnosticCall call, string outcome)
    {
        if (call == null) return;
        call.Outcome = outcome;
        call.EndEvaluations = connectedWork?.Evaluated ?? 0;
        call.RemainingWork = connectedWork?.Remaining ?? 0;
        call.EndSeconds = connectedWork?.Clock.Elapsed.TotalSeconds ?? 0;
    }

    private void RecordClosureBudgetExhaustion(ClosureDiagnosticCall call)
    {
        if (call == null) return;
        call.Budget = new { remaining = connectedWork.Remaining, evaluations = connectedWork.Evaluated,
            elapsedSeconds = connectedWork.Clock.Elapsed.TotalSeconds, globalSecondsLimit = connectedWork.Seconds,
            phaseEvaluationEnd = connectedWork.PhaseEnd, phaseSecondsEnd = connectedWork.PhaseSeconds,
            globalExhausted = connectedWork.Exhausted, phaseExhausted = connectedWork.PhaseExhausted };
        FinishClosureDiagnostics(call, "budget exhausted before socket-pair evaluation");
    }

    private void LogClosureDiagnosticSummary()
    {
        if (!connectedRoute.Diagnostics || LastClosureDiagnostics == null) return;
        var pairs = LastClosureDiagnostics.Calls.SelectMany(c => c.Pairs).ToList();
        string counts = string.Join(", ", pairs.GroupBy(p => p.FirstRejection)
            .Select(g => $"{g.Key}: {g.Count()}"));
        var first = LastClosureDiagnostics.FirstCall?.Pairs.FirstOrDefault();
        string example = first == null ? string.Empty :
            $" First checked pair: {first.SourcePrefab}/{first.SourceSocket} -> " +
            $"{first.TargetPrefab}/{first.TargetSocket}, {first.Connector}: " +
            $"{first.Distance:F3}/{first.PositionLimit:F3} units, " +
            $"{first.Angle:F2}/{first.AngleLimit:F2} degrees; {first.FirstRejection}.";
        Debug.Log($"Connected closure diagnostics: {LastClosureDiagnostics.TotalCalls} calls, " +
            $"{LastClosureDiagnostics.DroppedCalls} dropped; retained pair results: {counts}.{example} " +
            "Export Closure Diagnostics in the generator Inspector for every retained measured pair.", this);
    }

    private void RecordMainRepairDiagnostics(MainCheckpoint checkpoint, BranchRepairHint hint, bool targeted,
        int candidateCount, int clearCount, PairCandidate choice)
    {
        if (!connectedRoute.Enabled || !connectedRoute.Diagnostics || LastClosureDiagnostics == null) return;
        LastClosureDiagnostics.Repairs.Add(new {
            repair = connectedWork.Repairs, targeted, checkpointSlot = checkpoint.SlotIndex,
            mainIndex = checkpoint.Slots[checkpoint.SlotIndex].Index,
            blocker = hint?.Blocker.Label, blockerPrefab = hint?.Blocker.Prefab.name,
            blockerMainIndex = hint?.BlockerMainIndex, source = hint?.Source.Label, target = hint?.Target.Label,
            rejectedTurnIsland = hint?.Island.name, rejectedTurnConnection = hint?.Connection.name,
            closureScore = hint?.ClosureScore, candidateCount, clearCount,
            chosenIsland = choice?.IslandOption.Prefab.name, chosenConnection = choice?.ConnectionOption.Prefab.name,
            clearsRecordedTurn = choice == null ? (bool?)null : RepairCandidateClearsTurn(choice, hint),
            probeRuleSummary = diagnosticRepairProbeRules?.BuildSummary(),
            otherBlockerHints = diagnosticRepairHints.Values.Where(h => h.Hint.BlockerMainIndex != hint?.BlockerMainIndex)
                .OrderBy(h => h.Hint.ClosureScore).Select(DiagnosticRepairHintInfo).ToArray(),
            rejectedClearanceExamples = hint == null || diagnosticRepairCandidates == null ? null : diagnosticRepairCandidates
                .Where(c => !RepairCandidateClearsTurn(c, hint)).Take(6).Select(c => new {
                    island = c.IslandOption.Prefab.name, connector = c.ConnectionOption.Prefab.name,
                    sourceSocket = checkpoint.Previous.Prefab.GetSocket(c.SourceSocketIndex).name,
                    islandPosition = DiagnosticVector(c.IslandPlacement.Position), islandYaw = c.IslandPlacement.Rotation.eulerAngles.y,
                    islandBounds = DiagnosticBounds(c.IslandBounds), connectionBounds = DiagnosticBounds(c.ConnectionBounds)
                }).ToArray(),
            evaluations = connectedWork.Evaluated, elapsedSeconds = connectedWork.Clock.Elapsed.TotalSeconds
        });
    }
}
