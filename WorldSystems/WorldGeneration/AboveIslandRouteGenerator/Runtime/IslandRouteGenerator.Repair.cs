using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

public sealed partial class IslandRouteGenerator
{
    private ConnectedSearchWork connectedWork;
    private readonly List<MainCheckpoint> mainCheckpoints = new List<MainCheckpoint>();
    private RouteBuildState lastConnectedPlan;
    public double LastPlanningSeconds { get; private set; }
    public double LastInstantiationSeconds { get; private set; }
    public bool UsedConnectedFallback { get; private set; }

    private sealed class ConnectedSearchWork
    {
        public readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        public int Remaining, Evaluated, Repairs;
        public int PhaseEnd = int.MaxValue;
        public double PhaseSeconds = double.PositiveInfinity;
        public float Seconds;
        public string LastFailure = "No compatible reconnecting placement was found.";
        public BranchRepairHint RepairHint;
        public PlannedPiece BranchTarget;
        public List<ConnectionOption> BranchConnections;
        public bool Exhausted => Remaining <= 0 || Clock.Elapsed.TotalSeconds >= Seconds;
        public bool PhaseExhausted => Exhausted || Evaluated >= PhaseEnd || Clock.Elapsed.TotalSeconds >= PhaseSeconds;
        public void StartBranchPhase(bool canRepair)
        {
            RepairHint = null;
            double fraction = canRepair ? .8 : 1;
            PhaseEnd = Evaluated + (int)(Remaining * fraction);
            PhaseSeconds = Clock.Elapsed.TotalSeconds + (Seconds - Clock.Elapsed.TotalSeconds) * fraction;
        }
        public void EndPhase() { PhaseEnd = int.MaxValue; PhaseSeconds = double.PositiveInfinity; }
        public bool Consume()
        {
            if (PhaseExhausted) return false;
            Remaining--; Evaluated++; return true;
        }
        public string Summary => $"{LastFailure} Examined {Evaluated} placement combinations; " +
            $"repaired {Repairs} main sections; {Clock.Elapsed.TotalSeconds:F2}s. " +
            (Exhausted ? "Search work/time limit reached." : "Available branch choices exhausted.");
    }

    private bool ConsumeConnectedWork() => connectedWork == null || connectedWork.Consume();

    private sealed class MainCheckpoint
    {
        public ClusterSnapshot Snapshot;
        public List<PlannedIslandSlot> Slots;
        public PlannedPiece Previous;
        public RouteFrame Frame;
        public float Progress;
        public int SlotIndex;
        public readonly HashSet<string> Tried = new HashSet<string>();
    }

    private sealed class BranchRepairHint
    {
        public int BlockerMainIndex;
        public PlannedPiece Anchor, Blocker, Source, Target;
        public AboveIsland Island;
        public ConnectionIsland Connection;
        public List<OrientedBox> IslandBounds, ConnectionBounds;
        public float ClosureScore;
    }

    private void ConsiderBranchRepair(RouteBuildState state, PlannedPiece source, PlannedPiece blocker,
        AboveIsland island, PiecePlacement placement, int entry, ConnectionIsland connection,
        List<OrientedBox> islandBounds, List<OrientedBox> connectionBounds)
    {
        if (!connectedRoute.Enabled || connectedWork == null || connectedWork.BranchTarget == null ||
            connectedWork.BranchConnections == null || !blocker.IsMainIsland || blocker.MainIndex < 3) return;
        var anchor = source;
        while (!anchor.IsMainIsland && anchor.ClusterParent != null) anchor = anchor.ClusterParent;
        if (!anchor.IsMainIsland) return;
        var checkpoint = mainCheckpoints.FirstOrDefault(c => c.SlotIndex >= 2 && c.SlotIndex < c.Slots.Count - 1 &&
            c.Slots[c.SlotIndex].Index == blocker.MainIndex && c.Snapshot.UsedSockets.ContainsKey(anchor));
        if (checkpoint == null) return;
        float score = NativeClosureScore(island, placement, entry, connectedWork.BranchTarget, connectedWork.BranchConnections);
        if (connectedWork.PhaseExhausted || float.IsInfinity(score) || float.IsNaN(score)) return;
        bool preferred = connectedWork.RepairHint == null || score < connectedWork.RepairHint.ClosureScore;
        if (!preferred && !PlacementDiagnosticsEnabled) return;
        var hint = new BranchRepairHint {
            BlockerMainIndex = blocker.MainIndex, Anchor = anchor, Blocker = blocker, Source = source,
            Target = connectedWork.BranchTarget, Island = island, Connection = connection,
            IslandBounds = islandBounds, ConnectionBounds = connectionBounds, ClosureScore = score
        };
        RecordAlternativeRepairHint(hint, placement);
        if (preferred) connectedWork.RepairHint = hint;
    }

    private static MainCheckpoint BlockerCheckpoint(List<MainCheckpoint> possible, BranchRepairHint hint)
    {
        return hint == null ? null : possible.FirstOrDefault(c =>
            c.Slots[c.SlotIndex].Index == hint.BlockerMainIndex && c.Snapshot.UsedSockets.ContainsKey(hint.Anchor));
    }

    private static bool RepairCandidateClearsTurn(PairCandidate candidate, BranchRepairHint hint)
    {
        if (hint == null) return true;
        foreach (var bounds in new[] { candidate.IslandBounds, candidate.ConnectionBounds })
        foreach (var turn in new[] { hint.IslandBounds, hint.ConnectionBounds })
        foreach (var a in bounds)
        foreach (var b in turn)
            if (OrientedBoxesOverlap(a, b)) return false;
        return true;
    }

    private List<PairCandidate> ProbeMainRepair(RouteBuildState state, MainCheckpoint checkpoint,
        PlacementDiagnostics diagnostics)
    {
        // Probe the saved prefix without destroying the live tail or its later checkpoints.
        var probe = new RouteBuildState { Usage = checkpoint.Snapshot.Usage.Clone() };
        PlannedPiece previous = null;
        for (int i = 0; i < checkpoint.Snapshot.PieceCount; i++)
        {
            var original = state.Pieces[i];
            var copy = CreatePlannedPiece(original.Prefab, original.Placement, original.Bounds,
                original.Biome, original.IsMainIsland, original.MainIndex, original.IsBeacon);
            copy.Label = original.Label;
            copy.UsedSockets.UnionWith(checkpoint.Snapshot.UsedSockets[original]);
            copy.ReservedSockets.UnionWith(checkpoint.Snapshot.ReservedSockets[original]);
            probe.Pieces.Add(copy);
            if (ReferenceEquals(original, checkpoint.Previous)) previous = copy;
        }
        var savedFrame = checkpoint.Frame;
        var frame = new RouteFrame(savedFrame.Origin, savedFrame.Forward, savedFrame.HeightOrigin,
            savedFrame.MaximumHeadingAngle, savedFrame.MaximumLateralDrift, checkpoint.Progress);
        var slot = checkpoint.Slots[checkpoint.SlotIndex];
        if (PlacementDiagnosticsEnabled) diagnostics.DiagnosticStage = "repair_probe";
        var result = BuildPairCandidates(probe, previous, null, SocketRouteUsage.MainRoute, frame, true,
            CollectConnectionOptions(probe, slot.Biome, null, diagnostics),
            CollectIslandOptions(probe, slot, MainIslandSelection, diagnostics), diagnostics, true)
            .Where(c => !checkpoint.Tried.Contains(CandidateKey(c))).ToList();
        if (PlacementDiagnosticsEnabled) { diagnosticRepairCandidates = result; diagnosticRepairProbeRules = diagnostics; }
        return result;
    }

    private static string CandidateKey(PairCandidate c) =>
        $"{c.IslandOption.Prefab.GetInstanceID()}:{c.ConnectionOption.Prefab.GetInstanceID()}:" +
        $"{c.SourceSocketIndex}:{c.ConnectionEntrySocketIndex}:{c.ConnectionExitSocketIndex}:{c.IslandEntrySocketIndex}";

    private void ReserveConnectedSockets(PlannedPiece piece)
    {
        if (!connectedRoute.Enabled || !piece.IsMainIsland || piece.MainIndex < 2) return;
        var exits = FreeSockets(piece, SocketUsage.Exit);
        var mainExits = new List<int>();
        piece.Prefab.CollectSocketIndices(SocketUsage.Exit, SocketRouteUsage.MainRoute, piece.UsedSockets, mainExits);
        // Retain a side exit for a branch, while leaving at least one continuation exit.
        if (exits.Count > 1)
        {
            var heading = FlattenDirection(generation.RouteStart.forward, Vector3.forward);
            var reservable = exits.Where(i => !mainExits.Contains(i) || mainExits.Count > 1).ToList();
            if (reservable.Count > 0)
            {
                int side = reservable.OrderByDescending(i => Vector3.Angle(heading,
                    GetSocketPose(piece, i).Rotation * Vector3.forward)).First();
                piece.ReservedSockets.Add(side);
            }
        }
    }

    private MainCheckpoint RememberMainCheckpoint(RouteBuildState state, List<PlannedIslandSlot> slots,
        int index, PlannedPiece previous, RouteFrame frame)
    {
        var checkpoint = new MainCheckpoint { Snapshot = CaptureClusterSnapshot(state), Slots = slots,
            SlotIndex = index, Previous = previous, Frame = frame, Progress = frame.CurrentProgress };
        mainCheckpoints.Add(checkpoint);
        return checkpoint;
    }

    private bool TryRepairMainSection(RouteBuildState state, Random random, out string error)
    {
        error = "No repairable main section remains.";
        if (connectedWork.Exhausted) { error = "No search work/time remains for another repair."; return false; }
        if (connectedWork.Repairs >= connectedRoute.MaximumMainRepairs)
        { error = "Main-section repair limit reached."; return false; }
        // Prefer the main placement that blocked a useful turn; keep the existing bounded backtrack otherwise.
        var possible = mainCheckpoints.Where(c => c.SlotIndex >= 2 &&
            c.SlotIndex < c.Slots.Count - 1).ToList();
        if (possible.Count == 0) return false;
        int offset = Math.Min(possible.Count - 1, connectedWork.Repairs * connectedRoute.RepairBacktrackIslands);
        var fallback = possible[possible.Count - 1 - offset];
        var hint = connectedWork.RepairHint;
        var targeted = BlockerCheckpoint(possible, hint);
        connectedWork.Repairs++;
        connectedWork.EndPhase();
        connectedWork.RepairHint = null;
        MainCheckpoint checkpoint = null;
        PairCandidate choice = null;
        foreach (var candidateCheckpoint in new[] { targeted, fallback }.Where(c => c != null).Distinct())
        {
            if (connectedWork.Exhausted) { error = "Search work/time limit reached while selecting a main repair."; return false; }
            var diagnostics = new PlacementDiagnostics();
            var candidates = ProbeMainRepair(state, candidateCheckpoint, diagnostics);
            var choices = candidateCheckpoint == targeted
                ? candidates.Where(c => RepairCandidateClearsTurn(c, hint)).ToList() : candidates;
            if (connectedWork.Exhausted) { error = "Search work/time limit reached while probing a main repair."; return false; }
            choice = choices.OrderBy(c => c.Score + (float)random.NextDouble() * routeShape.CandidateScoreTolerance).FirstOrDefault();
            RecordMainRepairDiagnostics(candidateCheckpoint, hint, candidateCheckpoint == targeted,
                candidates.Count, choices.Count, choice);
            if (choice == null)
            {
                error = "Main repair has no unused placement" + (candidateCheckpoint == targeted ? " clearing the blocked turn. " : ". ") + diagnostics.BuildSummary();
                continue;
            }
            checkpoint = candidateCheckpoint;
            break;
        }
        if (checkpoint == null) return false;
        RestoreClusterSnapshot(state, checkpoint.Snapshot);
        int checkpointIndex = mainCheckpoints.IndexOf(checkpoint);
        mainCheckpoints.RemoveRange(checkpointIndex + 1, mainCheckpoints.Count - checkpointIndex - 1);
        checkpoint.Frame.CurrentProgress = checkpoint.Progress;
        var slot = checkpoint.Slots[checkpoint.SlotIndex];
        checkpoint.Tried.Add(CandidateKey(choice));
        var next = CommitPairCandidate(state, checkpoint.Previous, slot, choice);
        ReserveConnectedSockets(next);
        checkpoint.Frame.CurrentProgress = choice.ForwardProgress;
        return TryBuildMainTail(random, state, checkpoint.Slots, checkpoint.SlotIndex + 1,
            next, checkpoint.Frame, out error);
    }

    private List<IslandOption> CollectBranchIslands(RouteBuildState state, IslandBiome biome,
        PlacementDiagnostics diagnostics)
    {
        var options = new List<IslandOption>();
        foreach (var size in connectedRoute.BranchSizes.Distinct())
            options.AddRange(CollectIslandOptions(state, new PlannedIslandSlot {
                Biome = biome, Size = size, PhaseUsage = IslandPhaseUsage.Cluster, CountsAsMainIsland = false },
                ExactIslandSelection, diagnostics));
        diagnostics.IslandOptions += options.Count;
        if (options.Count == 0) connectedWork.LastFailure = "No eligible branch island: " + diagnostics.BuildSummary();
        return options;
    }
}
