using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class IslandRouteGenerator
{
    /// <summary>Authored layouts share the saved-layout loader, without requiring web cycles.</summary>
    public static bool ValidateAuthoredSocketLayout(ConnectedRouteLayout layout, out string report, bool allowOpenConnectionEnds = false)
    {
        report = "Authored layout is empty.";
        if (!layout || layout.Pieces == null || layout.Pieces.Count == 0) return false;
        if (layout.Pieces.Any(p => p == null || !p.Prefab) || layout.SocketLinks == null)
        { report = "Authored layout contains a missing piece or socket-link list."; return false; }
        var occupied = new HashSet<string>();
        var neighbours = Enumerable.Range(0, layout.Pieces.Count).Select(_ => new List<int>()).ToArray();
        foreach (var link in layout.SocketLinks)
        {
            if (link == null || link.FromPiece < 0 || link.ToPiece < 0 || link.FromPiece >= neighbours.Length ||
                link.ToPiece >= neighbours.Length || link.FromPiece == link.ToPiece)
            { report = "Authored layout has an invalid socket link."; return false; }
            var a = layout.Pieces[link.FromPiece]; var b = layout.Pieces[link.ToPiece];
            if (!a.Prefab || !b.Prefab || (a.Prefab is ConnectionIsland) == (b.Prefab is ConnectionIsland))
            { report = "Socket links must join an island and a connection prefab."; return false; }
            int ai = a.Prefab.Sockets.ToList().FindIndex(s => s && s.name == link.FromSocket);
            int bi = b.Prefab.Sockets.ToList().FindIndex(s => s && s.name == link.ToSocket);
            if (ai < 0 || bi < 0 || !occupied.Add(link.FromPiece + ":" + link.FromSocket) ||
                !occupied.Add(link.ToPiece + ":" + link.ToSocket))
            { report = "A socket is missing or used more than once."; return false; }
            var sa = a.Prefab.GetSocket(ai); var sb = b.Prefab.GetSocket(bi);
            var connector = (a.Prefab as ConnectionIsland) ?? (b.Prefab as ConnectionIsland);
            if (!sa.CanBeUsedAs(SocketUsage.Exit) || !sb.CanBeUsedAs(SocketUsage.Entry) ||
                (sa.RouteUsage & sb.RouteUsage) == 0 || !sa.Allows(connector.ConnectionType) || !sb.Allows(connector.ConnectionType))
            { report = "Socket direction, route usage or connection type is incompatible."; return false; }
            var pa = GetSocketPose(a.Prefab, new PiecePlacement(a.Position, a.Rotation, a.Scale), ai);
            var pb = GetSocketPose(b.Prefab, new PiecePlacement(b.Position, b.Rotation, b.Scale), bi);
            float positionTolerance = Mathf.Clamp(link.PositionTolerance, .02f, 1f);
            float angleTolerance = Mathf.Clamp(link.AngleTolerance, .2f, 30f);
            if (Vector3.Distance(pa.Position, pb.Position) > positionTolerance || IslandSocket.AlignmentAngle(sa, pa.Rotation, sb, pb.Rotation) > angleTolerance)
            { report = "An authored socket link has moved apart. Reconnect it before runtime use."; return false; }
            neighbours[link.FromPiece].Add(link.ToPiece); neighbours[link.ToPiece].Add(link.FromPiece);
        }
        var reached = new HashSet<int> { 0 }; var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.Count > 0)
            foreach (int index in neighbours[pending.Dequeue()]) if (reached.Add(index)) pending.Enqueue(index);
        if (reached.Count != neighbours.Length)
        { report = $"Authored layout has {neighbours.Length - reached.Count} disconnected pieces. Draft can be saved; reconnect before runtime use."; return false; }
        foreach (var piece in layout.Pieces)
            if (!piece.Prefab || piece.Prefab is ConnectionIsland &&
                (neighbours[layout.Pieces.IndexOf(piece)].Count != 2 && !(allowOpenConnectionEnds && neighbours[layout.Pieces.IndexOf(piece)].Count == 1)))
            { report = "Every runtime connection needs an island at both ends."; return false; }
        report = "Authored socket layout is connected.";
        return true;
    }

#if UNITY_EDITOR
    /// <summary>One dry-plan attempt. Uses the regular generator's pools, sockets, rhythm and bounds.</summary>
    public bool TryPlanAuthoringLinear(int seed, out ConnectedRouteLayout layout, out string report)
    {
        layout = null;
        if (biomePhases.Sum(p => p == null ? 0 : p.MaximumIslands) > 64 ||
            islandPrefabs.Count > 512 || connectionPrefabs.Count > 512 ||
            islandPrefabs.Any(p => p?.Prefab && p.Prefab.Sockets.Count > 32) ||
            connectionPrefabs.Any(p => p?.Prefab && p.Prefab.Sockets.Count > 32))
        { report = "Authoring preview limit: 64 islands, 512 pool entries and 32 sockets per prefab."; return false; }
        bool wasConnected = connectedRoute.Enabled, wasApproved = connectedRoute.UseApprovedLayout;
        bool wasDetours = detours.EnableDetours;
        var savedClusters = clusterPhases;
        connectedRoute.Enabled = false; connectedRoute.UseApprovedLayout = false;
        detours.EnableDetours = false; clusterPhases = new List<ClusterPhaseRule>();
        connectedWork = new ConnectedSearchWork { Remaining = 20000, Seconds = 2f };
        try
        {
            if (!ValidateConfiguration(out report)) return false;
            if (!TryBuildCompleteRoute(new System.Random(seed), out var state, out report))
            { report = "One bounded linear attempt failed: " + report; return false; }
            layout = ScriptableObject.CreateInstance<ConnectedRouteLayout>();
            layout.IsAuthoredLayout = true; layout.SourceSeed = seed;
            var origin = generation.RouteStart;
            for (int i = 0; i < state.Pieces.Count; i++)
            {
                var piece = state.Pieces[i];
                var saved = new ConnectedRouteLayout.Piece { Prefab = piece.Prefab,
                    Position = Quaternion.Inverse(origin.rotation) * (piece.Placement.Position - origin.position),
                    Rotation = Quaternion.Inverse(origin.rotation) * piece.Placement.Rotation,
                    Scale = piece.Placement.Scale == Vector3.zero ? piece.Prefab.transform.localScale : piece.Placement.Scale,
                    Label = string.IsNullOrEmpty(piece.Label) ? $"Piece {i + 1:000}" : piece.Label,
                    Biome = piece.Biome, IsMainIsland = piece.IsMainIsland, MainIndex = piece.MainIndex,
                    IsBeacon = piece.IsBeacon, IsClusterPiece = piece.IsClusterPiece };
                layout.Pieces.Add(saved);
            }
            report = $"Planned {layout.Pieces.Count} pieces, seed {seed}. Linear spine only; settings assets unchanged.";
            return true;
        }
        finally
        {
            connectedWork = null; connectedRoute.Enabled = wasConnected; connectedRoute.UseApprovedLayout = wasApproved;
            detours.EnableDetours = wasDetours; clusterPhases = savedClusters;
        }
    }
#endif
}
