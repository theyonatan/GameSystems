using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

public sealed partial class IslandRouteGenerator
{
    [SerializeField] private ConnectedRouteSettings connectedRoute = new ConnectedRouteSettings();
    public ConnectedRouteSettings ConnectedRoute => connectedRoute;
    public IReadOnlyList<ConnectedRouteEdge> ConnectedEdges => connectedEdges;
    private readonly List<ConnectedRouteEdge> connectedEdges = new List<ConnectedRouteEdge>();
    private readonly Dictionary<AboveRoutePiece, MeshCollider> connectedGroundMeshes = new Dictionary<AboveRoutePiece, MeshCollider>();
    private readonly Dictionary<AboveRoutePiece, Vector3[]> connectedGroundOutlines = new Dictionary<AboveRoutePiece, Vector3[]>();

    private void AddConnectedGroundBounds(AboveRoutePiece prefab, PiecePlacement placement, List<OrientedBox> bounds)
    {
        if (!connectedRoute.Enabled || !(prefab is AboveIsland)) return;
        if (!connectedGroundMeshes.TryGetValue(prefab, out var ground))
        {
            // Some authored reservation boxes sit below the walking plane. Also reserve the real ground footprint.
            ground = prefab.GetComponentsInChildren<MeshCollider>(true)
                .Where(c => c.sharedMesh != null && c.enabled && !c.isTrigger)
                .OrderByDescending(c => c.sharedMesh.bounds.size.x * c.sharedMesh.bounds.size.z).FirstOrDefault();
            connectedGroundMeshes[prefab] = ground;
        }
        if (ground == null) return;
        if (!connectedGroundOutlines.TryGetValue(prefab,out var localOutline))
        {
            var local = GetRelativeMatrix(prefab.transform,ground.transform);
            localOutline = GroundHull(ground.sharedMesh.vertices.Select(v=>local.MultiplyPoint3x4(v)));
            connectedGroundOutlines[prefab]=localOutline;
        }
        var pieceScale = placement.Scale == Vector3.zero ? prefab.transform.localScale : placement.Scale;
        var rootWorld=Matrix4x4.TRS(placement.Position,placement.Rotation,pieceScale);
        var outline=localOutline.Select(v=>rootWorld.MultiplyPoint3x4(v)).ToArray();
        var meshBounds = ground.sharedMesh.bounds;
        var world = Matrix4x4.TRS(placement.Position, placement.Rotation, pieceScale) *
            GetRelativeMatrix(prefab.transform, ground.transform) * Matrix4x4.TRS(meshBounds.center, Quaternion.identity, meshBounds.size);
        Vector3 x = world.GetColumn(0), y = world.GetColumn(1), z = world.GetColumn(2);
        bounds.Add(new OrientedBox(world.GetColumn(3), x.normalized,
            y.sqrMagnitude > .0001f ? y.normalized : Vector3.up, z.normalized,
            new Vector3(x.magnitude*.5f + routeShape.HorizontalPlacementClearance,
                Mathf.Max(.35f,y.magnitude*.5f), z.magnitude*.5f + routeShape.HorizontalPlacementClearance), outline, routeShape.HorizontalPlacementClearance));
    }


    private static Vector3[] GroundHull(IEnumerable<Vector3> vertices)
    {
        var points=vertices.Select(v=>new Vector3(v.x,0,v.z)).Distinct().OrderBy(v=>v.x).ThenBy(v=>v.z).ToArray();
        var hull=new List<Vector3>();
        float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=points.Length-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        if(hull.Count>1)hull.RemoveAt(hull.Count-1);
        return hull.ToArray();
    }

    private static bool ConnectedFootprintsOverlap(OrientedBox a,OrientedBox b)
    {
        if(a.GroundOutline==null&&b.GroundOutline==null)return true;
        Vector3[] Polygon(OrientedBox box)=>box.GroundOutline??new[]{
            box.Center-box.AxisX*box.Extents.x-box.AxisZ*box.Extents.z,
            box.Center+box.AxisX*box.Extents.x-box.AxisZ*box.Extents.z,
            box.Center+box.AxisX*box.Extents.x+box.AxisZ*box.Extents.z,
            box.Center-box.AxisX*box.Extents.x+box.AxisZ*box.Extents.z};
        var pa=Polygon(a);var pb=Polygon(b);float clearance=a.OutlineClearance+b.OutlineClearance;
        foreach(var polygon in new[]{pa,pb})for(int i=0;i<polygon.Length;i++)
        {
            var d=polygon[(i+1)%polygon.Length]-polygon[i];var axis=new Vector3(-d.z,0,d.x).normalized;
            if(axis.sqrMagnitude<.1f)continue;
            float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=float.PositiveInfinity,bmax=float.NegativeInfinity;
            foreach(var p in pa){float value=Vector3.Dot(p,axis);amin=Mathf.Min(amin,value);amax=Mathf.Max(amax,value);}
            foreach(var p in pb){float value=Vector3.Dot(p,axis);bmin=Mathf.Min(bmin,value);bmax=Mathf.Max(bmax,value);}
            if(amax+clearance<bmin||bmax+clearance<amin)return false;
        }
        return true;
    }

    [Serializable]
    public sealed class ConnectedRouteEdge
    {
        public string From, To, FromSocket, ToSocket, ConnectionPrefab;
        public int FromPiece, ToPiece, ConnectionPiece;
        public bool IsMainRoute;
        public float ClosingError;
        public Vector3[] Centerline;
    }

    private bool ValidateConnectedRoute(out string report)
    {
        var s = connectedRoute;
        bool valid = s.MinimumCycles > 0 && s.MaximumCycles >= s.MinimumCycles &&
            s.MinimumIslandsPerCycle > 0 && s.MaximumIslandsPerCycle >= s.MinimumIslandsPerCycle &&
            s.SearchBudget > 0 && s.CandidatesPerStep > 0 && s.MaximumConnectionSpan > 0 &&
            s.BranchSizes != null && s.BranchSizes.Length > 0 && s.MaximumMainRepairs >= 0 &&
            s.RepairBacktrackIslands > 0 && s.MaximumPlacementEvaluations > 0 &&
            s.MaximumSearchSeconds > 0 && s.MaximumGenerationSeconds > 0 && s.MaximumReconnectionDistance > 0 &&
            s.ClosurePositionTolerance > 0 && s.ClosurePositionTolerance <= 1f &&
            s.ClosureAngleTolerance > 0 && s.ClosureAngleTolerance <= 30f;
        report = valid ? string.Empty : "Invalid connected-route search limits or closure tolerance.";
        return valid;
    }

    private bool TryBuildConnectedRoute(Random random, out RouteBuildState state, out string error)
    {
        LastClosureDiagnostics = connectedRoute.Diagnostics ? new ClosureDiagnostics() : null;
        diagnosticRepairHints.Clear(); diagnosticRepairCandidates = null; diagnosticRepairProbeRules = null;
        mainCheckpoints.Clear();
        connectedWork = new ConnectedSearchWork { Remaining = connectedRoute.MaximumPlacementEvaluations,
            Seconds = connectedRoute.MaximumSearchSeconds };
        try
        {
            if (!TryBuildCompleteRoute(random, out state, out error))
            {
                connectedWork.LastFailure = error;
                bool repaired = false;
                while (!connectedWork.Exhausted && connectedWork.Repairs < connectedRoute.MaximumMainRepairs)
                {
                    if (TryRepairMainSection(state, random, out error)) { repaired = true; break; }
                    connectedWork.LastFailure = error;
                }
                if (!repaired) { error = connectedWork.Summary; return false; }
            }
            while (true)
            {
                diagnosticRepairHints.Clear();
                connectedWork.StartBranchPhase(connectedWork.Repairs < connectedRoute.MaximumMainRepairs);
                var beforeBranches = CaptureClusterSnapshot(state);
                if (TryAddConnectedAlternatives(state, random, out error)) return true;
                connectedWork.LastFailure = error;
                RestoreClusterSnapshot(state, beforeBranches);
                if (!TryRepairMainSection(state, random, out string repairError))
                {
                    error = connectedWork.Summary + " " + repairError;
                    return false;
                }
            }
        }
        finally { LogClosureDiagnosticSummary(); connectedWork = null; }
    }

    private bool TryAddConnectedAlternatives(RouteBuildState state, Random random, out string error)
    {
        var main = state.Pieces.Where(p => p.IsMainIsland).OrderBy(p => p.MainIndex).ToList();
        var gate = main.Last();
        int wanted = random.Next(connectedRoute.MinimumCycles, connectedRoute.MaximumCycles + 1);
        int cycles = 0;
        int budget = connectedRoute.SearchBudget;
        while (cycles < wanted && budget > 0 && !connectedWork.PhaseExhausted)
        {
            var islands = state.Pieces.Where(p => p.Prefab is AboveIsland && !p.IsBeacon && p.MainIndex != 1).ToList();
            var sources = islands.Where(p => p != gate &&
                FreeSockets(p, SocketUsage.Exit).Count > 0)
                .OrderBy(p => cycles == 0 && p.MainIndex == 2 ? 0 : 1).ThenBy(_ => random.Next()).ToList();
            bool added = false;
            foreach (var source in sources)
            {
                float sourceProgress = MainProgress(source, main);
                var targets = islands.Where(p => p != source && p.MainIndex != 2 &&
                    MainProgress(p, main) > sourceProgress + 1.25f && FreeSockets(p, SocketUsage.Entry).Count > 0 &&
                    Vector3.Distance(source.Placement.Position, p.Placement.Position) < connectedRoute.MaximumReconnectionDistance)
                    .OrderBy(p => Vector3.Distance(source.Placement.Position, p.Placement.Position) + random.NextDouble() * 12).ToList();
                foreach (var target in targets)
                {
                    int pairBudget = Math.Min(320, budget);
                    int before = pairBudget;
                    if (TryCloseAlternative(state, random, source, target, ref pairBudget))
                    {
                        budget -= before - pairBudget;
                        cycles++; added = true; break;
                    }
                    budget -= before - pairBudget;
                    if (budget <= 0 || connectedWork.PhaseExhausted) break;
                }
                if (added || budget <= 0 || connectedWork.PhaseExhausted) break;
            }
            if (!added) break;
        }
        if (cycles < connectedRoute.MinimumCycles)
        {
            error = $"Closed {cycles}/{connectedRoute.MinimumCycles} required alternatives. " + connectedWork.LastFailure;
            return false;
        }
        if (HasMandatoryMiddleIsland(state, main))
        {
            error = "The web still requires every player to cross the same middle island.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool HasMandatoryMiddleIsland(RouteBuildState state, List<PlannedPiece> main)
    {
        if (main.Count < 4) return false;
        int pickup = state.Pieces.IndexOf(main[1]);
        int gate = state.Pieces.IndexOf(main[main.Count - 1]);
        foreach (var middle in main.Skip(2).Take(main.Count - 3))
        {
            int blocked = state.Pieces.IndexOf(middle);
            var visited = new HashSet<int> { pickup };
            var pending = new Queue<int>();
            pending.Enqueue(pickup);
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                foreach (var edge in state.ConnectedEdges)
                {
                    int next = edge.FromPiece == current ? edge.ToPiece :
                        edge.ToPiece == current ? edge.FromPiece : -1;
                    if (next < 0 || next == blocked || !visited.Add(next)) continue;
                    pending.Enqueue(next);
                }
            }
            if (!visited.Contains(gate)) return true;
        }
        return false;
    }

    private bool TryCloseAlternative(RouteBuildState state, Random random, PlannedPiece source,
        PlannedPiece target, ref int budget)
    {
        // Also grow backward from an available return socket. Its geometry determines the meeting place.
        var diagnostics = new PlacementDiagnostics();
        var slot = new PlannedIslandSlot { Biome=source.Biome, Size=IslandSize.Small,
            PhaseUsage=IslandPhaseUsage.Cluster, CountsAsMainIsland=false, Index=0 };
        var islands = CollectBranchIslands(state, source.Biome, diagnostics);
        var connections = CollectConnectionOptions(state, source.Biome, null, diagnostics)
            .Where(c=>IsCompactConnection(c.Prefab)).ToList();
        diagnostics.ConnectionOptions += connections.Count;
        var choices = new List<ReversePlacement>();
        foreach (int targetEntry in FreeSockets(target,SocketUsage.Entry))
        foreach (var connection in connections)
        {
            if (!target.Prefab.GetSocket(targetEntry).Allows(connection.Prefab.ConnectionType)) continue;
            var inputs=new List<int>();var outputs=new List<int>();
            connection.Prefab.CollectSocketIndices(SocketUsage.Entry,SocketRouteUsage.Detour,null,inputs);
            connection.Prefab.CollectSocketIndices(SocketUsage.Exit,SocketRouteUsage.Detour,null,outputs);
            foreach (int output in outputs)
            {
                var cp=AlignSocketToPose(connection.Prefab,output,GetSocketPose(target,targetEntry));
                var cb=BuildBounds(connection.Prefab,cp);
                if(OverlapsExisting(cb,state.Pieces,target))continue;
                foreach (int input in inputs)
                foreach (var island in islands)
                {
                    var exits=new List<int>();var entries=new List<int>();
                    island.Prefab.CollectSocketIndices(SocketUsage.Exit,SocketRouteUsage.Detour,null,exits);
                    island.Prefab.CollectSocketIndices(SocketUsage.Entry,SocketRouteUsage.Detour,null,entries);
                    if(entries.Count==0)continue;
                    foreach(int exit in exits)
                    {
                        if (!ConsumeConnectedWork()) return false;
                        if (input == output || !island.Prefab.GetSocket(exit).Allows(connection.Prefab.ConnectionType)) continue;
                        var ip=AlignSocketToPose(island.Prefab,exit,GetSocketPose(connection.Prefab,cp,input));
                        var ib=BuildBounds(island.Prefab,ip);
                        if(OverlapsExisting(ib,state.Pieces,null))continue;
                        float score=entries.Min(e=>Vector3.Distance(GetSocketPose(island.Prefab,ip,e).Position,source.Placement.Position));
                        choices.Add(new ReversePlacement{Island=island.Prefab,Connection=connection.Prefab,
                            IslandPlacement=ip,ConnectionPlacement=cp,IslandBounds=ib,ConnectionBounds=cb,
                            IslandExit=exit,ConnectionEntry=input,ConnectionExit=output,TargetEntry=targetEntry,Score=score+(float)random.NextDouble()*8});
                    }
                }
            }
        }
        if (choices.Count == 0)
            connectedWork.LastFailure = $"No open return placement from {source.Label} to {target.Label}: " +
                "return sockets/types are unavailable or the island/bridge overlaps existing pieces.";
        foreach(var choice in choices.OrderBy(c=>c.Score).Take(Math.Max(12, connectedRoute.CandidatesPerStep)))
        {
            var snapshot=CaptureClusterSnapshot(state);
            var island=CreatePlannedPiece(choice.Island,choice.IslandPlacement,choice.IslandBounds,source.Biome,false,0,false);
            var connection=CreatePlannedPiece(choice.Connection,choice.ConnectionPlacement,choice.ConnectionBounds,source.Biome,false,0,false);
            island.IsClusterPiece=true;connection.IsClusterPiece=true;
            island.UsedSockets.Add(choice.IslandExit);target.UsedSockets.Add(choice.TargetEntry);
            connection.UsedSockets.Add(choice.ConnectionEntry);connection.UsedSockets.Add(choice.ConnectionExit);
            state.Pieces.Add(island);state.Pieces.Add(connection);
            state.Usage.RecordIsland(choice.Island);state.Usage.RecordConnection(choice.Connection);
            RecordConnectedEdge(state,island,target,connection,choice.IslandExit,choice.TargetEntry,false);
            int localBudget=Math.Min(65,budget);int before=localBudget;
            bool closed=SearchClosedBranch(state,random,source,island,1,ref localBudget);
            budget-=before-localBudget;
            if(closed)return true;
            RestoreClusterSnapshot(state,snapshot);
            if(budget<=0)break;
        }
        return false;
    }

    private sealed class ReversePlacement
    {
        public AboveIsland Island;
        public ConnectionIsland Connection;
        public PiecePlacement IslandPlacement,ConnectionPlacement;
        public List<OrientedBox> IslandBounds,ConnectionBounds;
        public int IslandExit,ConnectionEntry,ConnectionExit,TargetEntry;
        public float Score;
    }

    private bool SearchClosedBranch(RouteBuildState state, Random random, PlannedPiece source,
        PlannedPiece target, int depth, ref int budget)
    {
        if (--budget < 0 || connectedWork.PhaseExhausted) return false;
        if (depth >= connectedRoute.MinimumIslandsPerCycle)
        {
            var rule = new ClusterPhaseRule
            {
                Biome = source.Biome,
                ExtraLinkPositionTolerance = connectedRoute.ClosurePositionTolerance,
                ExtraLinkAngleTolerance = connectedRoute.ClosureAngleTolerance
            };
            // Existing code aligns ONE authored closing piece; never a path-to-path chain.
            if (TryPlaceClusterExtraLink(state, random, source, new List<PlannedPiece>{target},
                    new List<PlannedPiece>(), rule, ClusterTopology.Diamond)) return true;
            connectedWork.LastFailure = $"No closing bridge fits the position/angle of a free entry on {target.Label}.";
        }
        if (depth >= connectedRoute.MaximumIslandsPerCycle || budget <= 0) return false;

        Vector3 toward = FlattenDirection(target.Placement.Position - source.Placement.Position, Vector3.forward);
        var frame = new RouteFrame(source.Placement.Position, toward, generation.RouteStart.position.y, 179f, 35f, -8f);
        var slot = new PlannedIslandSlot { Biome = source.Biome, Size = IslandSize.Small,
            PhaseUsage = IslandPhaseUsage.Cluster, CountsAsMainIsland = false, Index = 0 };
        var diagnostics = new PlacementDiagnostics();
        var islandOptions = CollectBranchIslands(state, source.Biome, diagnostics);
        var connections = CollectConnectionOptions(state, source.Biome, null, diagnostics)
            .Where(c => IsCompactConnection(c.Prefab)).ToList();
        diagnostics.ConnectionOptions += connections.Count;
        List<PairCandidate> candidates;
        connectedWork.BranchTarget = target;
        connectedWork.BranchConnections = connections;
        try
        {
            candidates = BuildPairCandidates(state, source, null, SocketRouteUsage.Detour,
                frame, true, connections, islandOptions, diagnostics, true);
        }
        finally { connectedWork.BranchTarget = null; connectedWork.BranchConnections = null; }
        diagnostics.PairCandidates += candidates.Count;
        if (candidates.Count == 0)
            connectedWork.LastFailure = $"Cannot extend {source.Label}: " + diagnostics.BuildSummary();
        var ranked = candidates.Select(c => new { Candidate = c,
            Score = NativeClosureScore(c.IslandOption.Prefab, c.IslandPlacement, c.IslandEntrySocketIndex, target, connections)
                    + (float)random.NextDouble()*3f })
            .OrderBy(c => c.Score)
            .GroupBy(c => new { c.Candidate.SourceSocketIndex, c.Candidate.ConnectionOption.Prefab,
                Island = c.Candidate.IslandOption.Prefab, c.Candidate.IslandEntrySocketIndex })
            .Select(g => g.First()).Take(connectedRoute.CandidatesPerStep).ToList();
        foreach (var choice in ranked)
        {
            var snapshot = CaptureClusterSnapshot(state);
            var island = CommitPairCandidate(state, source, slot, choice.Candidate);
            island.IsClusterPiece = true;
            island.ClusterParent = source;
            if (SearchClosedBranch(state, random, island, target, depth+1, ref budget)) return true;
            RestoreClusterSnapshot(state, snapshot);
            if (budget <= 0) break;
        }
        return false;
    }

    private float NativeClosureScore(AboveIsland island, PiecePlacement placement, int usedEntry,
        PlannedPiece target, List<ConnectionOption> connections)
    {
        float best = float.PositiveInfinity;
        var exits = new List<int>();
        island.CollectSocketIndices(SocketUsage.Exit, SocketRouteUsage.Detour, new HashSet<int>{usedEntry}, exits);
        foreach (int exit in exits)
        foreach (var option in connections)
        {
            var connection = option.Prefab;
            var inputs = new List<int>(); var outputs = new List<int>();
            connection.CollectSocketIndices(SocketUsage.Entry, SocketRouteUsage.Detour, null, inputs);
            connection.CollectSocketIndices(SocketUsage.Exit, SocketRouteUsage.Detour, null, outputs);
            foreach (int input in inputs)
            {
                var cp = AlignSocketToPose(connection, input, GetSocketPose(island, placement, exit));
                foreach (int output in outputs)
                foreach (int entry in FreeSockets(target, SocketUsage.Entry))
                {
                    if (!ConsumeConnectedWork()) return best;
                    var a = GetSocketPose(connection, cp, output); var b = GetSocketPose(target, entry);
                    float score = Vector3.Distance(a.Position,b.Position) + Quaternion.Angle(a.Rotation,b.Rotation)*.12f;
                    best = Mathf.Min(best, score);
                }
            }
        }
        return best;
    }

    private bool IsCompactConnection(ConnectionIsland prefab)
    {
        if (prefab.ConnectionType != IslandConnectionType.Normal) return false;
        var entries = new List<int>(); var exits = new List<int>();
        prefab.CollectSocketIndices(SocketUsage.Entry, SocketRouteUsage.Both, null, entries);
        prefab.CollectSocketIndices(SocketUsage.Exit, SocketRouteUsage.Both, null, exits);
        return entries.Any(a => exits.Any(b => a != b &&
            Vector3.Distance(prefab.GetSocket(a).transform.position, prefab.GetSocket(b).transform.position) <= connectedRoute.MaximumConnectionSpan));
    }

    private static List<int> FreeSockets(PlannedPiece piece, SocketUsage usage)
    {
        var result = new List<int>();
        piece.Prefab.CollectSocketIndices(usage, SocketRouteUsage.Detour, piece.UsedSockets, result);
        return result;
    }

    private static float MainProgress(PlannedPiece piece, List<PlannedPiece> main)
    {
        if (piece.IsMainIsland) return piece.MainIndex;
        return main.OrderBy(p => Vector3.SqrMagnitude(p.Placement.Position-piece.Placement.Position)).First().MainIndex;
    }

    private void RecordConnectedEdge(RouteBuildState state, PlannedPiece source, PlannedPiece target,
        PlannedPiece connection, int sourceSocket, int targetSocket, bool mainRoute)
    {
        if (string.IsNullOrEmpty(source.Label)) source.Label = source.IsMainIsland ? $"Main {source.MainIndex:00}" : $"Cluster {state.Pieces.IndexOf(source)}";
        if (string.IsNullOrEmpty(target.Label)) target.Label = target.IsBeacon ? "Beacon" : target.IsMainIsland ? $"Main {target.MainIndex:00}" : $"Cluster {state.Pieces.IndexOf(target)}";
        connection.Label = source.Label + " → " + target.Label;
        var start = GetSocketPose(source, sourceSocket).Position;
        var end = GetSocketPose(target, targetSocket).Position;
        var exit = connection.UsedSockets.First(i => connection.Prefab.GetSocket(i).CanBeUsedAs(SocketUsage.Exit));
        var actualEnd = GetSocketPose(connection, exit).Position;
        state.ConnectedEdges.Add(new ConnectedRouteEdge {
            From = source.Label, To = target.Label,
            FromSocket = source.Prefab.GetSocket(sourceSocket).name, ToSocket = target.Prefab.GetSocket(targetSocket).name,
            FromPiece = state.Pieces.IndexOf(source), ToPiece = state.Pieces.IndexOf(target), ConnectionPiece = state.Pieces.IndexOf(connection),
            ConnectionPrefab = connection.Prefab.name, IsMainRoute = mainRoute,
            ClosingError = Vector3.Distance(end,actualEnd), Centerline = new[]{start,actualEnd,end}
        });
    }

    public bool ValidateConnectedNavigation(out string report)
    {
        report = string.Empty;
        if (!connectedRoute.Enabled) return true;
        var loadedLayout = UsedApprovedLayout ? connectedRoute.ApprovedLayout : UsedConnectedFallback ? connectedRoute.FallbackLayout : null;
        if (loadedLayout && loadedLayout.IsAuthoredLayout && !loadedLayout.ValidateBatteryNavigation)
        {
            report = "Authored socket graph loaded. Mode-specific gameplay navigation checks are not configured for this generic layout.";
            return true;
        }
        var islands = generatedInstances.OfType<AboveIsland>().ToArray();
        var pickup = islands.FirstOrDefault(p => p.GeneratedMainIndex == 2);
        if (pickup == null || !UnityEngine.AI.NavMesh.SamplePosition(pickup.transform.position,
                out var start, 5f, UnityEngine.AI.NavMesh.AllAreas))
        { report = "Battery pickup has no walkable surface."; return false; }
        foreach (var island in islands)
        {
            var path = new UnityEngine.AI.NavMeshPath();
            bool reachable = UnityEngine.AI.NavMesh.SamplePosition(island.transform.position,
                out var end, 5f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.CalculatePath(start.position,end.position,UnityEngine.AI.NavMesh.AllAreas,path) &&
                path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            // An island pivot can lie in a tiny NavMesh pocket enclosed by its
            // authored props. Used socket approaches represent the traversable
            // route; every local connection is still checked below. Also check
            // beacon approaches so an isolated pivot cannot hide a gate bypass.
            if (!reachable)
            {
                int pieceIndex = generatedInstances.IndexOf(island);
                foreach (var edge in connectedEdges)
                {
                    bool source = edge.FromPiece == pieceIndex;
                    if (!source && edge.ToPiece != pieceIndex) continue;
                    if (source && generatedInstances[edge.ToPiece].IsGeneratedBeacon) continue;
                    var socket = island.Sockets.First(s => s.name == (source ? edge.FromSocket : edge.ToSocket));
                    Vector3 approach = socket.transform.position + socket.transform.forward * (source ? -1.1f : 1.1f);
                    if (UnityEngine.AI.NavMesh.SamplePosition(approach, out end, 2f, UnityEngine.AI.NavMesh.AllAreas) &&
                        UnityEngine.AI.NavMesh.CalculatePath(start.position, end.position, UnityEngine.AI.NavMesh.AllAreas, path) &&
                        path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    { reachable = true; break; }
                }
            }
            if (reachable == island.IsGeneratedBeacon)
            { report = island.IsGeneratedBeacon ? "The closed gate can be bypassed on foot." : "Unreachable island: " + island.name; return false; }
        }
        foreach (var edge in connectedEdges)
        {
            if (generatedInstances[edge.ToPiece] is AboveIsland targetIsland && targetIsland.IsGeneratedBeacon) continue;
            if (!ValidateNativeConnection(edge))
            {
                report = "The local connection is not walkable: " + edge.From + " -> " + edge.To;
                return false;
            }
        }
        report = "Every local connection is walkable; the closed gate isolates the beacon.";
        return true;
    }

    private bool ValidateNativeConnection(ConnectedRouteEdge edge)
    {
        var from = generatedInstances[edge.FromPiece];
        var to = generatedInstances[edge.ToPiece];
        var connection = generatedInstances[edge.ConnectionPiece];
        var exit = from.Sockets.First(s => s.name == edge.FromSocket);
        var entry = to.Sockets.First(s => s.name == edge.ToSocket);
        Vector3 begin = exit.transform.position - exit.transform.forward * 1.1f;
        Vector3 end = entry.transform.position + entry.transform.forward * 1.1f;
        var path = new UnityEngine.AI.NavMeshPath();
        int areas = UnityEngine.AI.NavMesh.AllAreas;
        if (!UnityEngine.AI.NavMesh.SamplePosition(begin, out var a, 2f, areas) ||
            !UnityEngine.AI.NavMesh.SamplePosition(end, out var b, 2f, areas) ||
            !UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, areas, path) ||
            path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) return false;

        // A path around the rest of the web must not disguise a broken local connection.
        for (int i = 1; i < path.corners.Length; i++)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(path.corners[i-1], path.corners[i]) / .5f));
            for (int j = 0; j <= samples; j++)
            {
                Vector3 point = Vector3.Lerp(path.corners[i-1], path.corners[i], (float)j / samples);
                bool supported = Physics.RaycastAll(point + Vector3.up * .4f, Vector3.down, .9f,
                    ~0, QueryTriggerInteraction.Ignore).Any(hit =>
                    {
                        var piece = hit.collider.GetComponentInParent<AboveRoutePiece>();
                        return hit.normal.y > .6f && (piece == from || piece == to || piece == connection);
                    });
                if (!supported) return false;
            }
        }
        return true;
    }

}
