using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class IslandRouteGenerator
{
    public bool UsesApprovedConnectedLayout => connectedRoute.Enabled && connectedRoute.UseApprovedLayout;
    public bool UsedApprovedLayout { get; private set; }

    /// <summary>Capture only after this exact instantiated route passes its NavMesh/gate checks.</summary>
    public bool CaptureConnectedLayout(ConnectedRouteLayout layout, out string report)
    {
        if (layout == null || lastConnectedPlan == null || generatedInstances.Count != lastConnectedPlan.Pieces.Count)
        { report = "No current connected plan can be captured."; return false; }
        if (!ValidateConnectedNavigation(out report)) return false;
        layout.Pieces.Clear(); layout.Edges.Clear(); layout.SourceSeed = LastUsedSeed;
        var origin = generation.RouteStart;
        foreach (var piece in lastConnectedPlan.Pieces)
        {
            var saved = new ConnectedRouteLayout.Piece { Prefab = piece.Prefab,
                Position = Quaternion.Inverse(origin.rotation) * (piece.Placement.Position - origin.position),
                Rotation = Quaternion.Inverse(origin.rotation) * piece.Placement.Rotation,
                Label = piece.Label, Biome = piece.Biome, IsMainIsland = piece.IsMainIsland,
                IsBeacon = piece.IsBeacon, IsClusterPiece = piece.IsClusterPiece, MainIndex = piece.MainIndex };
            foreach (int index in piece.UsedSockets)
            {
                var pose = GetSocketPose(piece.Prefab, new PiecePlacement(Vector3.zero, Quaternion.identity), index);
                saved.UsedSockets.Add(new ConnectedRouteLayout.Socket {
                    Name = piece.Prefab.GetSocket(index).name, Position = pose.Position, Rotation = pose.Rotation });
            }
            layout.Pieces.Add(saved);
        }
        foreach (var edge in connectedEdges)
            layout.Edges.Add(CopyLayoutEdge(edge, p => Quaternion.Inverse(origin.rotation) * (p - origin.position)));
        report = $"Captured validated connected layout: {layout.Pieces.Count} pieces, seed {layout.SourceSeed}.";
        return true;
    }

    public bool GenerateConnectedFallback(out string report)
        => GenerateSavedConnectedLayout(connectedRoute.FallbackLayout, false, out report);

    public bool GenerateApprovedConnectedLayout(out string report)
        => GenerateSavedConnectedLayout(connectedRoute.ApprovedLayout, true, out report);

    private bool GenerateSavedConnectedLayout(ConnectedRouteLayout layout, bool approved, out string report)
    {
        UsedConnectedFallback = false;
        UsedApprovedLayout = false;
        LastPlanningSeconds = 0;
        LastInstantiationSeconds = 0;
        connectedGroundMeshes.Clear();
        connectedGroundOutlines.Clear();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (!connectedRoute.Enabled || layout == null || generation.RouteStart == null)
        { report = "Connected mode, a saved layout and Route Start are required."; return false; }
        var state = new RouteBuildState();
        foreach (var saved in layout.Pieces)
        {
            if (saved.Prefab == null) { report = "Saved layout contains a missing prefab."; return false; }
            var placement = new PiecePlacement(generation.RouteStart.position + generation.RouteStart.rotation * saved.Position,
                generation.RouteStart.rotation * saved.Rotation, saved.Scale);
            var piece = CreatePlannedPiece(saved.Prefab, placement, BuildBounds(saved.Prefab, placement),
                saved.Biome, saved.IsMainIsland, saved.MainIndex, saved.IsBeacon);
            piece.Label = saved.Label; piece.IsClusterPiece = saved.IsClusterPiece;
            foreach (var socket in saved.UsedSockets)
            {
                int index = saved.Prefab.Sockets.ToList().FindIndex(s => s != null && s.name == socket.Name);
                if (index < 0) { report = $"Saved layout socket removed: {saved.Prefab.name}/{socket.Name}."; return false; }
                var pose = GetSocketPose(saved.Prefab, new PiecePlacement(Vector3.zero, Quaternion.identity), index);
                if (Vector3.Distance(pose.Position, socket.Position) > .005f ||
                    Quaternion.Angle(pose.Rotation, socket.Rotation) > .1f)
                { report = $"Saved layout socket moved: {saved.Prefab.name}/{socket.Name}. Revalidate its layout."; return false; }
                piece.UsedSockets.Add(index);
            }
            state.Pieces.Add(piece);
        }
        foreach (var edge in layout.Edges)
        {
            if (edge.FromPiece < 0 || edge.ToPiece < 0 || edge.ConnectionPiece < 0 ||
                edge.FromPiece >= state.Pieces.Count || edge.ToPiece >= state.Pieces.Count ||
                edge.ConnectionPiece >= state.Pieces.Count ||
                !(state.Pieces[edge.FromPiece].Prefab is AboveIsland) ||
                !(state.Pieces[edge.ToPiece].Prefab is AboveIsland) ||
                !(state.Pieces[edge.ConnectionPiece].Prefab is ConnectionIsland))
            { report = "Saved layout has invalid connection indices or piece types."; return false; }
            state.ConnectedEdges.Add(CopyLayoutEdge(edge,
                p => generation.RouteStart.position + generation.RouteStart.rotation * p));
        }
        var main = state.Pieces.Where(p => p.IsMainIsland).OrderBy(p => p.MainIndex).ToList();
        int islands = state.Pieces.Count(p => p.Prefab is AboveIsland);
        if (!layout.IsAuthoredLayout && (main.Count < 3 || state.Pieces.Count(p => p.IsBeacon) != 1 ||
            state.ConnectedEdges.Count - islands + 1 < connectedRoute.MinimumCycles ||
            HasMandatoryMiddleIsland(state, main)))
        { report = "Saved layout does not meet the required connected-web structure."; return false; }
        if (layout.IsAuthoredLayout && !ValidateAuthoredSocketLayout(layout, out report)) return false;
        LastPlanningSeconds = clock.Elapsed.TotalSeconds;
        clock.Restart();
        ClearGeneratedRoute();
        InstantiatePlan(state.Pieces);
        connectedEdges.AddRange(state.ConnectedEdges);
        lastConnectedPlan = state;
        LastUsedSeed = layout.SourceSeed;
        LastInstantiationSeconds = clock.Elapsed.TotalSeconds;
        UsedConnectedFallback = !approved;
        UsedApprovedLayout = approved;
        LastFailureReason = string.Empty;
        LastGenerationDiagnostics = (approved ? "Loaded approved layout (no generation search). " : "Saved fallback. ") +
            BuildSuccessfulGenerationReport(state);
        onRouteGenerated.Invoke(); RouteGenerated?.Invoke();
        report = LastGenerationDiagnostics;
        return true;
    }

    private static ConnectedRouteEdge CopyLayoutEdge(ConnectedRouteEdge edge, Func<Vector3, Vector3> transform)
        => new ConnectedRouteEdge { From = edge.From, To = edge.To,
            FromSocket = edge.FromSocket, ToSocket = edge.ToSocket, ConnectionPrefab = edge.ConnectionPrefab,
            FromPiece = edge.FromPiece, ToPiece = edge.ToPiece, ConnectionPiece = edge.ConnectionPiece,
            IsMainRoute = edge.IsMainRoute, ClosingError = edge.ClosingError,
            Centerline = edge.Centerline.Select(transform).ToArray() };
}
