#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(IslandForgeOrganizer))]
public sealed class IslandForgeOrganizerEditor : Editor
{
    [MenuItem("GameObject/Above/Create Island Forge", false, 10)]
    private static void CreateIslandForge(MenuCommand command)
    {
        GameObject forge = new GameObject("Forge");
        GameObjectUtility.SetParentAndAlign(forge, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(forge, "Create Island Forge");
        Undo.AddComponent<IslandForgeOrganizer>(forge);
        Selection.activeGameObject = forge;
    }

    private enum BrowseKind
    {
        Everything,
        Islands,
        Connections,
        Unrecognized
    }

    private enum BrowseSize
    {
        Any,
        Small,
        Medium
    }

    private enum BrowseSort
    {
        HierarchyOrder,
        Name,
        Type,
        Biome,
        Size
    }

    private enum BiomeMembership
    {
        Any,
        Exclusive,
        Shared,
        AllBiomes
    }

    private sealed class ForgeItem
    {
        public Transform Transform;
        public AboveIsland Island;
        public ConnectionIsland Connection;
        public BackgroundIsland Background;
        public int SiblingIndex;
        public IslandBiome[] Biomes = Array.Empty<IslandBiome>();
        public bool SupportsEveryBiome;

        public bool IsIsland => Island != null;
        public bool IsConnection => Connection != null;
        public bool IsRecognized => IsIsland || IsConnection;
        public AboveRoutePiece RoutePiece => Island != null ? Island : Connection;
        public string CatalogName => Island != null && Island.HasCatalogName
            ? Island.IslandName.Trim()
            : Transform.name;
        public int CatalogId => Island != null ? Island.IslandId : -1;
    }

    private sealed class LayoutGroup
    {
        public string Label;
        public Color Color;
        public bool BeginsConnectionSection;
        public readonly List<ForgeItem> Items = new List<ForgeItem>();
    }

    private struct Footprint
    {
        public float Width;
        public float Depth;
        public Vector3 CenterOffset;
    }

    private struct PlannedRow
    {
        public string Label;
        public Color Color;
        public float CenterZ;
        public float Width;
        public float Depth;
        public int Count;
    }

    private SerializedProperty includeInactive;
    private SerializedProperty multiBiomeLayout;
    private SerializedProperty groupConnectionsByType;
    private SerializedProperty localOrigin;
    private SerializedProperty columnsPerRow;
    private SerializedProperty minimumCellWidth;
    private SerializedProperty minimumCellDepth;
    private SerializedProperty horizontalGap;
    private SerializedProperty rowGap;
    private SerializedProperty categoryGap;
    private SerializedProperty connectionSectionGap;
    private SerializedProperty centerRows;
    private SerializedProperty preserveLocalHeight;
    private SerializedProperty usePlacementBounds;
    private SerializedProperty fallBackToRendererBounds;
    private SerializedProperty showSceneGuides;
    private SerializedProperty showItemLabels;

    private bool showCollection = true;
    private bool showLayout = true;
    private bool showStats = true;
    private bool showBrowser = true;
    private bool showCatalog = true;
    private bool showSharedCatalog = true;
    private bool showAllBiomeCatalog = true;
    private bool showConnectionCatalog = true;
    private bool showSceneOptions;
    private string catalogSearch = string.Empty;
    private string search = string.Empty;
    private BrowseKind browseKind;
    private BrowseSize browseSize;
    private BrowseSort browseSort;
    private BiomeMembership biomeMembership;
    private int selectedBiome = -1;
    private IslandConnectionType? selectedConnectionType;
    private Vector2 browserScroll;
    private bool showAllBrowserResults;
    private readonly Dictionary<IslandBiome, bool> biomeCatalogFoldouts = new Dictionary<IslandBiome, bool>();

    private GUIStyle headerStyle;
    private GUIStyle metricNumberStyle;
    private GUIStyle metricLabelStyle;
    private GUIStyle rowLabelStyle;
    private GUIStyle badgeStyle;

    private IslandForgeOrganizer Organizer => (IslandForgeOrganizer)target;

    private void OnEnable()
    {
        includeInactive = serializedObject.FindProperty("includeInactive");
        multiBiomeLayout = serializedObject.FindProperty("multiBiomeLayout");
        groupConnectionsByType = serializedObject.FindProperty("groupConnectionsByType");
        localOrigin = serializedObject.FindProperty("localOrigin");
        columnsPerRow = serializedObject.FindProperty("columnsPerRow");
        minimumCellWidth = serializedObject.FindProperty("minimumCellWidth");
        minimumCellDepth = serializedObject.FindProperty("minimumCellDepth");
        horizontalGap = serializedObject.FindProperty("horizontalGap");
        rowGap = serializedObject.FindProperty("rowGap");
        categoryGap = serializedObject.FindProperty("categoryGap");
        connectionSectionGap = serializedObject.FindProperty("connectionSectionGap");
        centerRows = serializedObject.FindProperty("centerRows");
        preserveLocalHeight = serializedObject.FindProperty("preserveLocalHeight");
        usePlacementBounds = serializedObject.FindProperty("usePlacementBounds");
        fallBackToRendererBounds = serializedObject.FindProperty("fallBackToRendererBounds");
        showSceneGuides = serializedObject.FindProperty("showSceneGuides");
        showItemLabels = serializedObject.FindProperty("showItemLabels");

        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void HandleHierarchyChanged()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    private void HandleUndoRedo()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        List<ForgeItem> items = CollectItems();
        DrawHeader(items);
        DrawCatalog(items);
        DrawCollectionSettings();
        DrawStatistics(items);
        DrawBrowser(items);
        DrawLayoutSettings(items);

        serializedObject.ApplyModifiedProperties();
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 8, 7, 7)
        };

        metricNumberStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };

        metricLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        rowLabelStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold
        };

        badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(5, 5, 1, 1)
        };
    }

    private void DrawHeader(List<ForgeItem> items)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 40f);
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.26f, 0.34f, 1f));
        GUI.Label(rect, "  Island Forge Organizer  •  1.5.0", headerStyle);

        int recognized = items.Count(item => item.IsRecognized);
        int unrecognized = items.Count - recognized;
        string subtitle = $"{recognized} recognized direct children";
        if (unrecognized > 0)
            subtitle += $"  •  {unrecognized} unrecognized";

        EditorGUILayout.LabelField(subtitle, EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.HelpBox(
            "Live catalog only: sorting and browsing never change hierarchy order. " +
            "Arrange Grid moves direct children in local X/Z with Undo, while preserving sibling indices, rotation and scale.",
            MessageType.Info);
    }

    private void DrawCatalog(List<ForgeItem> items)
    {
        showCatalog = EditorGUILayout.BeginFoldoutHeaderGroup(showCatalog, "Island Catalog");
        if (!showCatalog)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        List<ForgeItem> islands = items.Where(item => item.IsIsland).ToList();
        List<ForgeItem> connections = items.Where(item => item.IsConnection).ToList();
        Dictionary<int, int> idCounts = islands
            .Where(item => item.Island.HasCatalogId)
            .GroupBy(item => item.CatalogId)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<string, int> nameCounts = islands
            .Where(item => item.Island.HasCatalogName)
            .GroupBy(item => item.Island.IslandName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        catalogSearch = EditorGUILayout.TextField("Search name or ID", catalogSearch);
        if (GUILayout.Button("Copy Complete Forge Report"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildForgeReport(items);
            Debug.Log(
                "Copied island names, IDs, biomes, sizes, roles, phase usage, socket capabilities, connections, background settings and catalog totals.",
                Organizer);
        }
        EditorGUILayout.LabelField(
            "Biome totals are eligibility totals. Shared and All-Biome islands intentionally appear in every biome where they can generate.",
            EditorStyles.wordWrappedMiniLabel);

        foreach (IslandBiome biome in GetAllBiomes())
            DrawBiomeCatalog(islands, biome, idCounts, nameCounts);

        List<ForgeItem> shared = islands
            .Where(item => !item.SupportsEveryBiome && item.Biomes.Length > 1)
            .ToList();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showSharedCatalog = EditorGUILayout.Foldout(
                showSharedCatalog,
                $"Shared / Multi-Biome Islands — Total: {shared.Count}",
                true);
            if (showSharedCatalog)
                DrawCatalogEntries(shared, null, idCounts, nameCounts);
        }

        List<ForgeItem> allBiome = islands.Where(item => item.SupportsEveryBiome).ToList();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showAllBiomeCatalog = EditorGUILayout.Foldout(
                showAllBiomeCatalog,
                $"All-Biome Islands — Total: {allBiome.Count}",
                true);
            if (showAllBiomeCatalog)
                DrawCatalogEntries(allBiome, null, idCounts, nameCounts);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showConnectionCatalog = EditorGUILayout.Foldout(
                showConnectionCatalog,
                $"Connections — Total: {connections.Count}",
                true);
            if (showConnectionCatalog)
                DrawConnectionCatalog(connections);
        }

        DrawIdentityHealth(islands, idCounts, nameCounts);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBiomeCatalog(
        List<ForgeItem> islands,
        IslandBiome biome,
        Dictionary<int, int> idCounts,
        Dictionary<string, int> nameCounts)
    {
        List<ForgeItem> eligible = islands.Where(item => SupportsBiome(item, biome)).ToList();
        if (!biomeCatalogFoldouts.TryGetValue(biome, out bool open))
            open = true;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Color previousColor = GUI.color;
            GUI.color = Color.Lerp(Color.white, GetBiomeColor(biome), 0.2f);
            open = EditorGUILayout.Foldout(
                open,
                $"{Nicify(biome.ToString())} — Total Eligible: {eligible.Count}",
                true);
            GUI.color = previousColor;
            biomeCatalogFoldouts[biome] = open;

            if (!open)
                return;

            DrawCatalogSize(eligible, IslandSize.Medium, biome, idCounts, nameCounts);
            EditorGUILayout.Space(2f);
            DrawCatalogSize(eligible, IslandSize.Small, biome, idCounts, nameCounts);
        }
    }

    private void DrawCatalogSize(
        List<ForgeItem> eligible,
        IslandSize size,
        IslandBiome biome,
        Dictionary<int, int> idCounts,
        Dictionary<string, int> nameCounts)
    {
        List<ForgeItem> matching = eligible.Where(item => item.Island.Size == size).ToList();
        int exclusive = matching.Count(item => IsExclusiveToBiome(item, biome));
        int shared = matching.Count - exclusive;
        EditorGUILayout.LabelField(
            $"{Nicify(size.ToString())} — Total: {matching.Count}   (Exclusive: {exclusive}  •  Shared: {shared})",
            EditorStyles.boldLabel);
        DrawCatalogEntries(matching, biome, idCounts, nameCounts);
    }

    private void DrawCatalogEntries(
        IEnumerable<ForgeItem> source,
        IslandBiome? currentBiome,
        Dictionary<int, int> idCounts,
        Dictionary<string, int> nameCounts)
    {
        List<ForgeItem> visible = source
            .Where(MatchesCatalogSearch)
            .OrderBy(item => item.CatalogId < 0 ? int.MaxValue : item.CatalogId)
            .ThenBy(item => item.CatalogName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visible.Count == 0)
        {
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(catalogSearch) ? "None" : "No matching entries",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        foreach (ForgeItem item in visible)
            DrawCatalogEntry(item, currentBiome, idCounts, nameCounts);
    }

    private void DrawCatalogEntry(
        ForgeItem item,
        IslandBiome? currentBiome,
        Dictionary<int, int> idCounts,
        Dictionary<string, int> nameCounts)
    {
        bool duplicateId = item.Island.HasCatalogId && idCounts.TryGetValue(item.CatalogId, out int idCount) && idCount > 1;
        bool duplicateName = item.Island.HasCatalogName &&
            nameCounts.TryGetValue(item.Island.IslandName.Trim(), out int nameCount) && nameCount > 1;

        List<string> badges = new List<string>();
        string membership = GetMembershipBadge(item, currentBiome);
        if (!string.IsNullOrEmpty(membership))
            badges.Add(membership);
        if (item.Island.Role != IslandRole.Regular)
            badges.Add(Nicify(item.Island.Role.ToString()));
        if (item.Background != null)
            badges.Add("Background");
        if (!item.Island.HasCatalogName)
            badges.Add("Missing Name");
        if (!item.Island.HasCatalogId)
            badges.Add("Missing ID");
        if (duplicateId)
            badges.Add("Duplicate ID");
        if (duplicateName)
            badges.Add("Duplicate Name");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(12f);
            GUIContent content = new GUIContent(
                FormatIslandIdentity(item),
                BuildIslandTooltip(item));
            if (GUILayout.Button(
                    content,
                    EditorStyles.linkLabel,
                    GUILayout.MinWidth(100f),
                    GUILayout.ExpandWidth(false)))
            {
                Selection.activeGameObject = item.Transform.gameObject;
                EditorGUIUtility.PingObject(item.Transform.gameObject);
                if (Event.current.clickCount > 1)
                    SceneView.lastActiveSceneView?.FrameSelected();
            }

            if (badges.Count > 0)
                GUILayout.Label(
                    string.Join("  ", badges.Select(value => $"[{value}]")),
                    badgeStyle,
                    GUILayout.ExpandWidth(false));

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Frame", GUILayout.Width(48f)))
            {
                Selection.activeGameObject = item.Transform.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
    }

    private void DrawConnectionCatalog(List<ForgeItem> connections)
    {
        foreach (IslandConnectionType type in Enum.GetValues(typeof(IslandConnectionType)))
        {
            List<ForgeItem> matching = connections
                .Where(item => item.Connection.ConnectionType == type && MatchesCatalogSearch(item))
                .OrderBy(item => item.Transform.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int total = connections.Count(item => item.Connection.ConnectionType == type);
            if (total == 0)
                continue;

            EditorGUILayout.LabelField($"{Nicify(type.ToString())} — Total: {total}", EditorStyles.boldLabel);
            if (matching.Count == 0)
            {
                EditorGUILayout.LabelField("No matching entries", EditorStyles.centeredGreyMiniLabel);
                continue;
            }

            foreach (ForgeItem item in matching)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    if (GUILayout.Button(item.Transform.name, EditorStyles.linkLabel))
                    {
                        Selection.activeGameObject = item.Transform.gameObject;
                        EditorGUIUtility.PingObject(item.Transform.gameObject);
                        if (Event.current.clickCount > 1)
                            SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(GetBiomeLabel(item), EditorStyles.miniLabel);
                }
            }
        }
    }

    private void DrawIdentityHealth(
        List<ForgeItem> islands,
        Dictionary<int, int> idCounts,
        Dictionary<string, int> nameCounts)
    {
        int missingIds = islands.Count(item => !item.Island.HasCatalogId);
        int missingNames = islands.Count(item => !item.Island.HasCatalogName);
        int duplicateIds = idCounts.Count(pair => pair.Value > 1);
        int duplicateNames = nameCounts.Count(pair => pair.Value > 1);
        if (missingIds == 0 && missingNames == 0 && duplicateIds == 0 && duplicateNames == 0)
            return;

        List<string> details = new List<string>
        {
            $"{missingIds} missing ID(s)",
            $"{missingNames} missing name(s)",
            $"{duplicateIds} duplicated ID value(s)",
            $"{duplicateNames} duplicated name value(s)"
        };
        EditorGUILayout.HelpBox("Catalog identity: " + string.Join("  •  ", details), MessageType.Warning);
    }

    private static string BuildForgeReport(List<ForgeItem> items)
    {
        List<ForgeItem> islands = items.Where(item => item.IsIsland).ToList();
        List<ForgeItem> connections = items.Where(item => item.IsConnection).ToList();
        List<ForgeItem> background = islands.Where(item => item.Background != null).ToList();
        StringBuilder text = new StringBuilder();

        text.AppendLine("# Above Island Forge — 1.5.0 Report");
        text.AppendLine(
            $"Unique islands: {islands.Count}; Small: {islands.Count(item => item.Island.Size == IslandSize.Small)}; " +
            $"Medium: {islands.Count(item => item.Island.Size == IslandSize.Medium)}; " +
            $"Junctions: {islands.Count(item => item.Island.Role == IslandRole.Junction)}; " +
            $"Detour endpoints: {islands.Count(item => item.Island.Role == IslandRole.DetourEndpoint)}; " +
            $"Background-enabled: {background.Count}; Connections: {connections.Count}");

        foreach (IslandBiome biome in GetAllBiomes())
        {
            text.AppendLine();
            text.AppendLine($"## {Nicify(biome.ToString())}");
            AppendBiomeSizeReport(text, islands, biome, IslandSize.Medium);
            AppendBiomeSizeReport(text, islands, biome, IslandSize.Small);
        }

        text.AppendLine();
        text.AppendLine("## Unique Island Details");
        foreach (ForgeItem item in islands
                     .OrderBy(value => value.CatalogId < 0 ? int.MaxValue : value.CatalogId)
                     .ThenBy(value => value.CatalogName, StringComparer.OrdinalIgnoreCase))
        {
            AboveIsland island = item.Island;
            string socketSummary = FormatSocketSummary(island);
            string clusterAnchor = HasClusterAnchorLayout(island) ? "yes" : "no";
            text.Append(
                $"- {FormatIslandIdentity(item)} | object={item.Transform.name} | biomes={GetBiomeLabel(item)} | " +
                $"size={island.Size} | role={island.Role} | phase={island.PhaseUsage} | " +
                $"sockets={socketSummary} | cluster anchor={clusterAnchor} | bounds={island.PlacementBounds.Count}");

            if (item.Background != null)
            {
                BackgroundIsland scenery = item.Background;
                text.Append(
                    $" | background: biomes={GetBackgroundBiomeLabel(scenery)}, layers={scenery.AllowedLayers}, " +
                    $"size={scenery.Size}, visual cost={scenery.VisualCost}, " +
                    $"radius={scenery.CalculateLocalPlacementRadius():0.##}, yaw={scenery.AllowRandomYaw}, " +
                    $"scale={scenery.MinimumScaleMultiplier:0.##}..{scenery.MaximumScaleMultiplier:0.##}");
            }

            text.AppendLine();
        }

        text.AppendLine();
        text.AppendLine("## Connections");
        foreach (ForgeItem item in connections
                     .OrderBy(value => value.Connection.ConnectionType)
                     .ThenBy(value => value.Transform.name, StringComparer.OrdinalIgnoreCase))
        {
            text.AppendLine(
                $"- {item.Transform.name} | type={item.Connection.ConnectionType} | biomes={GetBiomeLabel(item)} | " +
                $"sockets={FormatSocketSummary(item.Connection)} | bounds={item.Connection.PlacementBounds.Count}");
        }

        List<ForgeItem> unknown = items.Where(item => !item.IsRecognized).ToList();
        if (unknown.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Unrecognized Direct Children");
            foreach (ForgeItem item in unknown)
                text.AppendLine($"- {item.Transform.name}");
        }

        return text.ToString().TrimEnd();
    }

    private static void AppendBiomeSizeReport(
        StringBuilder text,
        List<ForgeItem> islands,
        IslandBiome biome,
        IslandSize size)
    {
        List<ForgeItem> eligible = islands
            .Where(item => item.Island.Size == size && SupportsBiome(item, biome))
            .OrderBy(item => item.CatalogId < 0 ? int.MaxValue : item.CatalogId)
            .ThenBy(item => item.CatalogName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int exclusive = eligible.Count(item => IsExclusiveToBiome(item, biome));
        int shared = eligible.Count - exclusive;
        text.AppendLine($"### {size} — Total: {eligible.Count} (Exclusive: {exclusive}; Shared/All: {shared})");
        foreach (ForgeItem item in eligible)
        {
            string membership = GetMembershipBadge(item, biome);
            text.AppendLine(
                $"- {FormatIslandIdentity(item)}" +
                (string.IsNullOrEmpty(membership) ? string.Empty : $" [{membership}]") +
                (item.Island.Role == IslandRole.Regular ? string.Empty : $" [{item.Island.Role}]") +
                $" [Phase: {item.Island.PhaseUsage}]");
        }
    }

    private static string FormatSocketSummary(AboveRoutePiece piece)
    {
        return
            $"total {piece.Sockets.Count}; " +
            $"Main E{piece.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.MainRoute)}/X{piece.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.MainRoute)}; " +
            $"Detour E{piece.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.Detour)}/X{piece.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.Detour)}";
    }

    private static bool HasClusterAnchorLayout(AboveIsland island)
    {
        if (island == null || island.Sockets.Count < 3)
            return false;

        for (int entryIndex = 0; entryIndex < island.Sockets.Count; entryIndex++)
        {
            IslandSocket entry = island.GetSocket(entryIndex);
            if (entry == null || !entry.CanBeUsedAs(SocketUsage.Entry) ||
                !entry.SupportsRoute(SocketRouteUsage.MainRoute))
            {
                continue;
            }

            for (int continuationIndex = 0; continuationIndex < island.Sockets.Count; continuationIndex++)
            {
                if (continuationIndex == entryIndex)
                    continue;
                IslandSocket continuation = island.GetSocket(continuationIndex);
                if (continuation == null || !continuation.CanBeUsedAs(SocketUsage.Exit) ||
                    !continuation.SupportsRoute(SocketRouteUsage.MainRoute))
                {
                    continue;
                }

                for (int branchIndex = 0; branchIndex < island.Sockets.Count; branchIndex++)
                {
                    if (branchIndex == entryIndex || branchIndex == continuationIndex)
                        continue;
                    IslandSocket branch = island.GetSocket(branchIndex);
                    if (branch != null && branch.CanBeUsedAs(SocketUsage.Exit) &&
                        branch.SupportsRoute(SocketRouteUsage.Detour))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string GetBackgroundBiomeLabel(BackgroundIsland background)
    {
        if (background.AllowedBiomes == null || background.AllowedBiomes.Count == 0)
            return "All Biomes";
        return string.Join(" + ", background.AllowedBiomes.Select(value => Nicify(value.ToString())));
    }

    private bool MatchesCatalogSearch(ForgeItem item)
    {
        if (string.IsNullOrWhiteSpace(catalogSearch))
            return true;

        string query = catalogSearch.Trim();
        return item.Transform.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.CatalogName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (item.IsIsland && FormatIslandId(item.CatalogId).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
            (item.IsIsland && item.CatalogId.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
            GetItemTags(item).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExclusiveToBiome(ForgeItem item, IslandBiome biome)
    {
        return !item.SupportsEveryBiome && item.Biomes.Length == 1 && item.Biomes[0] == biome;
    }

    private static string FormatIslandId(int id)
    {
        return id < 0 ? "#---" : $"#{id:000}";
    }

    private static string FormatIslandIdentity(ForgeItem item)
    {
        return $"{FormatIslandId(item.CatalogId)}  {item.CatalogName}";
    }

    private static string BuildIslandTooltip(ForgeItem item)
    {
        string background = item.Background == null
            ? "No"
            : $"Yes ({item.Background.Size}, {item.Background.AllowedLayers}, cost {item.Background.VisualCost})";
        return
            $"Scene object: {item.Transform.name}\n" +
            $"Biomes: {GetBiomeLabel(item)}\n" +
            $"Size: {Nicify(item.Island.Size.ToString())}\n" +
            $"Role: {Nicify(item.Island.Role.ToString())}\n" +
            $"Phase usage: {Nicify(item.Island.PhaseUsage.ToString())}\n" +
            $"Background use: {background}\n" +
            $"Cached sockets: {item.Island.Sockets.Count}\n" +
            "Click to select; double-click to frame in Scene View.";
    }

    private static string GetMembershipBadge(ForgeItem item, IslandBiome? currentBiome)
    {
        if (item.SupportsEveryBiome)
            return "All Biomes";
        if (item.Biomes.Length <= 1)
            return string.Empty;

        IEnumerable<IslandBiome> shown = currentBiome.HasValue
            ? item.Biomes.Where(biome => biome != currentBiome.Value)
            : item.Biomes;
        string biomes = string.Join(" + ", shown.Select(biome => Nicify(biome.ToString())));
        return string.IsNullOrEmpty(biomes) ? "Shared" : "Shared: " + biomes;
    }

    private void DrawCollectionSettings()
    {
        showCollection = EditorGUILayout.BeginFoldoutHeaderGroup(showCollection, "Collection & Multi-Biome Rules");
        if (showCollection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(includeInactive);
            EditorGUILayout.PropertyField(multiBiomeLayout);
            EditorGUILayout.PropertyField(groupConnectionsByType);
            EditorGUI.indentLevel--;

            if ((ForgeMultiBiomeLayoutMode)multiBiomeLayout.enumValueIndex == ForgeMultiBiomeLayoutMode.SharedRows)
            {
                EditorGUILayout.HelpBox(
                    "Recommended: a prefab supporting multiple explicit biomes appears once in Shared / Multi-Biome. " +
                    "An empty Allowed Biomes list appears once in All Biomes. Statistics still count each prefab as eligible for every biome it supports.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Multi-biome and all-biome prefabs are placed in their first supported biome row. " +
                    "This is compact, but the grid no longer shows their shared eligibility at a glance.",
                    MessageType.Warning);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawStatistics(List<ForgeItem> items)
    {
        showStats = EditorGUILayout.BeginFoldoutHeaderGroup(showStats, "Live Statistics");
        if (!showStats)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        int islands = items.Count(item => item.IsIsland);
        int small = items.Count(item => item.IsIsland && item.Island.Size == IslandSize.Small);
        int medium = items.Count(item => item.IsIsland && item.Island.Size == IslandSize.Medium);
        int connections = items.Count(item => item.IsConnection);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawMetric(items.Count, "Direct children");
            DrawMetric(islands, "Islands");
            DrawMetric(connections, "Connections");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawMetric(small, "Small islands");
            DrawMetric(medium, "Medium islands");
            DrawMetric(items.Count(item => !item.IsRecognized), "Unrecognized");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawMetric(items.Count(item => item.IsIsland && item.Island.Role == IslandRole.Junction), "Junctions");
            DrawMetric(items.Count(item => item.IsIsland && !item.SupportsEveryBiome && item.Biomes.Length > 1), "Shared islands");
            DrawMetric(items.Count(item => item.IsIsland && !item.Island.HasCatalogId), "Missing IDs");
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Biome eligibility", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Shared islands are deliberately included in every biome they support; use Unique islands above for a non-overlapping total.",
            EditorStyles.wordWrappedMiniLabel);

        DrawStatsHeader("Biome", "Small", "Medium", "Total", "Exclusive / Shared");
        foreach (IslandBiome biome in GetAllBiomes())
        {
            List<ForgeItem> matching = items
                .Where(item => item.IsIsland && SupportsBiome(item, biome))
                .ToList();
            int biomeSmall = matching.Count(item => item.Island.Size == IslandSize.Small);
            int biomeMedium = matching.Count(item => item.Island.Size == IslandSize.Medium);
            int exclusive = matching.Count(item => !item.SupportsEveryBiome && item.Biomes.Length == 1);
            int shared = matching.Count - exclusive;
            DrawStatsRow(Nicify(biome.ToString()), biomeSmall, biomeMedium, matching.Count, $"{exclusive} / {shared}");
        }

        if (connections > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
            DrawStatsHeader("Type", "Count", "%", "", "");
            foreach (IslandConnectionType type in Enum.GetValues(typeof(IslandConnectionType)))
            {
                int count = items.Count(item => item.IsConnection && item.Connection.ConnectionType == type);
                float percent = connections > 0 ? count * 100f / connections : 0f;
                DrawStatsRow(Nicify(type.ToString()), count, $"{percent:0.#}%", string.Empty, string.Empty);
            }
        }

        int missingBounds = items.Count(item => item.IsRecognized && !item.RoutePiece.HasUsablePlacementBounds());
        int missingSockets = items.Count(item => item.IsRecognized && item.RoutePiece.Sockets.Count == 0);
        if (missingBounds > 0 || missingSockets > 0)
        {
            EditorGUILayout.HelpBox(
                $"Catalog health: {missingBounds} recognized prefab instance(s) have no Placement Bounds and " +
                $"{missingSockets} have no cached sockets. Renderer bounds can lay out missing-bound items, but generation still needs authored bounds.",
                MessageType.Warning);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawMetric(int value, string label)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(54f)))
        {
            GUILayout.Label(value.ToString(), metricNumberStyle);
            GUILayout.Label(label, metricLabelStyle);
        }
    }

    private static void DrawStatsHeader(string a, string b, string c, string d, string e)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(a, EditorStyles.miniBoldLabel, GUILayout.MinWidth(95f));
            GUILayout.Label(b, EditorStyles.miniBoldLabel, GUILayout.Width(46f));
            GUILayout.Label(c, EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            GUILayout.Label(d, EditorStyles.miniBoldLabel, GUILayout.Width(46f));
            GUILayout.Label(e, EditorStyles.miniBoldLabel, GUILayout.MinWidth(88f));
        }
    }

    private static void DrawStatsRow(string a, int b, int c, int d, string e)
    {
        DrawStatsRow(a, b, c.ToString(), d.ToString(), e);
    }

    private static void DrawStatsRow(string a, int b, string c, string d, string e)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(a, GUILayout.MinWidth(95f));
            GUILayout.Label(b.ToString(), GUILayout.Width(46f));
            GUILayout.Label(c, GUILayout.Width(52f));
            GUILayout.Label(d, GUILayout.Width(46f));
            GUILayout.Label(e, EditorStyles.miniLabel, GUILayout.MinWidth(88f));
        }
    }

    private void DrawBrowser(List<ForgeItem> items)
    {
        showBrowser = EditorGUILayout.BeginFoldoutHeaderGroup(showBrowser, "Browse, Sort & Filter");
        if (!showBrowser)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        search = EditorGUILayout.TextField("Search", search);
        browseKind = (BrowseKind)EditorGUILayout.EnumPopup("Kind", browseKind);
        browseSize = (BrowseSize)EditorGUILayout.EnumPopup("Island Size", browseSize);

        string[] biomeNames = new[] { "Any biome" }
            .Concat(GetAllBiomes().Select(value => Nicify(value.ToString())))
            .ToArray();
        selectedBiome = EditorGUILayout.Popup("Biome", selectedBiome + 1, biomeNames) - 1;
        biomeMembership = (BiomeMembership)EditorGUILayout.EnumPopup("Biome Membership", biomeMembership);

        if (browseKind == BrowseKind.Connections || browseKind == BrowseKind.Everything)
        {
            string[] connectionNames = new[] { "Any connection type" }
                .Concat(Enum.GetValues(typeof(IslandConnectionType))
                    .Cast<IslandConnectionType>()
                    .Select(value => Nicify(value.ToString())))
                .ToArray();
            int connectionIndex = selectedConnectionType.HasValue ? (int)selectedConnectionType.Value + 1 : 0;
            connectionIndex = EditorGUILayout.Popup("Connection Type", connectionIndex, connectionNames);
            selectedConnectionType = connectionIndex == 0
                ? (IslandConnectionType?)null
                : (IslandConnectionType)(connectionIndex - 1);
        }

        browseSort = (BrowseSort)EditorGUILayout.EnumPopup("Display Sort", browseSort);

        List<ForgeItem> matching = SortItems(items.Where(MatchesBrowser).ToList());
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField($"{matching.Count} matching direct child object(s)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = matching.Count > 0;
            if (GUILayout.Button("Show Only Matching In Scene"))
                ApplySceneFilter(items, matching);
            GUI.enabled = true;

            if (GUILayout.Button("Clear Scene Filter"))
                ClearSceneFilter(false);
        }

        EditorGUILayout.LabelField(
            "Scene filtering uses editor visibility only. It does not disable objects or alter builds.",
            EditorStyles.wordWrappedMiniLabel);

        int visibleCount = showAllBrowserResults ? matching.Count : Mathf.Min(20, matching.Count);
        float desiredHeight = Mathf.Clamp(visibleCount * 24f + 4f, 28f, 260f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            browserScroll = EditorGUILayout.BeginScrollView(browserScroll, GUILayout.Height(desiredHeight));
            for (int i = 0; i < visibleCount; i++)
                DrawBrowserRow(matching[i]);
            EditorGUILayout.EndScrollView();
        }

        if (matching.Count > 20)
        {
            if (GUILayout.Button(showAllBrowserResults ? "Show First 20" : $"Show All {matching.Count}"))
                showAllBrowserResults = !showAllBrowserResults;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBrowserRow(ForgeItem item)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            string kind = item.IsIsland ? item.Island.Size.ToString() : item.IsConnection ? "Link" : "Other";
            GUILayout.Label(kind, badgeStyle, GUILayout.Width(52f));

            string displayName = item.IsIsland ? FormatIslandIdentity(item) : item.Transform.name;
            if (GUILayout.Button(displayName, EditorStyles.linkLabel, GUILayout.MinWidth(100f)))
            {
                Selection.activeGameObject = item.Transform.gameObject;
                EditorGUIUtility.PingObject(item.Transform.gameObject);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(GetItemTags(item), EditorStyles.miniLabel, GUILayout.MaxWidth(150f));

            if (GUILayout.Button("Frame", GUILayout.Width(48f)))
            {
                Selection.activeGameObject = item.Transform.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
    }

    private void DrawLayoutSettings(List<ForgeItem> items)
    {
        showLayout = EditorGUILayout.BeginFoldoutHeaderGroup(showLayout, "Scene Grid Layout");
        if (!showLayout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.PropertyField(localOrigin);
        EditorGUILayout.PropertyField(columnsPerRow);
        EditorGUILayout.PropertyField(minimumCellWidth);
        EditorGUILayout.PropertyField(minimumCellDepth);
        EditorGUILayout.PropertyField(horizontalGap);
        EditorGUILayout.PropertyField(rowGap);
        EditorGUILayout.PropertyField(categoryGap);
        EditorGUILayout.PropertyField(connectionSectionGap);
        EditorGUILayout.PropertyField(centerRows);
        EditorGUILayout.PropertyField(preserveLocalHeight);
        EditorGUILayout.PropertyField(usePlacementBounds);
        EditorGUILayout.PropertyField(fallBackToRendererBounds);

        showSceneOptions = EditorGUILayout.Foldout(showSceneOptions, "Scene View Labels & Guides", true);
        if (showSceneOptions)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(showSceneGuides);
            EditorGUILayout.PropertyField(showItemLabels);
            EditorGUI.indentLevel--;
        }

        int recognized = items.Count(item => item.IsRecognized);
        int unrecognized = items.Count - recognized;
        if (unrecognized > 0)
        {
            EditorGUILayout.HelpBox(
                $"{unrecognized} unrecognized direct child object(s) will stay exactly where they are. " +
                "Only roots containing AboveIsland or ConnectionIsland are arranged.",
                MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = recognized > 0;
            if (GUILayout.Button($"Arrange {recognized} Recognized Children", GUILayout.Height(30f)))
                ArrangeGrid(items);
            GUI.enabled = true;

            if (GUILayout.Button("Frame Forge", GUILayout.Height(30f), GUILayout.Width(90f)))
            {
                Selection.activeGameObject = Organizer.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        EditorGUILayout.LabelField(
            "Arrangement is one Undo step. It writes only localPosition on recognized direct children and never calls SetSiblingIndex.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private List<ForgeItem> CollectItems()
    {
        List<ForgeItem> results = new List<ForgeItem>();
        Transform root = Organizer.transform;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!Organizer.IncludeInactive && !child.gameObject.activeSelf)
                continue;

            AboveIsland island = child.GetComponent<AboveIsland>();
            ConnectionIsland connection = child.GetComponent<ConnectionIsland>();
            BackgroundIsland background = child.GetComponent<BackgroundIsland>();
            AboveRoutePiece routePiece = island != null ? island : connection;
            IslandBiome[] biomes = routePiece == null
                ? Array.Empty<IslandBiome>()
                : routePiece.AllowedBiomes.Distinct().ToArray();

            results.Add(new ForgeItem
            {
                Transform = child,
                Island = island,
                Connection = connection,
                Background = background,
                SiblingIndex = i,
                Biomes = biomes,
                SupportsEveryBiome = routePiece != null && biomes.Length == 0
            });
        }

        return results;
    }

    private bool MatchesBrowser(ForgeItem item)
    {
        if (!string.IsNullOrWhiteSpace(search) &&
            item.Transform.name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0 &&
            item.CatalogName.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0 &&
            (!item.IsIsland || (FormatIslandId(item.CatalogId).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0 &&
                item.CatalogId.ToString().IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0)) &&
            GetItemTags(item).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (browseKind == BrowseKind.Islands && !item.IsIsland)
            return false;
        if (browseKind == BrowseKind.Connections && !item.IsConnection)
            return false;
        if (browseKind == BrowseKind.Unrecognized && item.IsRecognized)
            return false;

        if (browseSize != BrowseSize.Any)
        {
            if (!item.IsIsland)
                return false;
            IslandSize required = browseSize == BrowseSize.Small ? IslandSize.Small : IslandSize.Medium;
            if (item.Island.Size != required)
                return false;
        }

        if (selectedBiome >= 0)
        {
            if (!item.IsRecognized)
                return false;
            IslandBiome biome = GetAllBiomes()[selectedBiome];
            if (!SupportsBiome(item, biome))
                return false;
        }

        if (biomeMembership != BiomeMembership.Any)
        {
            if (!item.IsRecognized)
                return false;

            bool matches = biomeMembership == BiomeMembership.AllBiomes
                ? item.SupportsEveryBiome
                : biomeMembership == BiomeMembership.Shared
                    ? !item.SupportsEveryBiome && item.Biomes.Length > 1
                    : !item.SupportsEveryBiome && item.Biomes.Length == 1;
            if (!matches)
                return false;
        }

        if (selectedConnectionType.HasValue &&
            (!item.IsConnection || item.Connection.ConnectionType != selectedConnectionType.Value))
        {
            return false;
        }

        return true;
    }

    private List<ForgeItem> SortItems(List<ForgeItem> items)
    {
        switch (browseSort)
        {
            case BrowseSort.Name:
                return items.OrderBy(item => item.CatalogName, StringComparer.OrdinalIgnoreCase).ToList();
            case BrowseSort.Type:
                return items.OrderBy(GetKindOrder).ThenBy(item => item.Transform.name, StringComparer.OrdinalIgnoreCase).ToList();
            case BrowseSort.Biome:
                return items.OrderBy(GetBiomeSortKey).ThenBy(item => item.Transform.name, StringComparer.OrdinalIgnoreCase).ToList();
            case BrowseSort.Size:
                return items.OrderBy(item => item.IsIsland ? (int)item.Island.Size : 100)
                    .ThenBy(item => item.Transform.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            default:
                return items.OrderBy(item => item.SiblingIndex).ToList();
        }
    }

    private static int GetKindOrder(ForgeItem item)
    {
        return item.IsIsland ? 0 : item.IsConnection ? 1 : 2;
    }

    private static int GetBiomeSortKey(ForgeItem item)
    {
        if (!item.IsRecognized)
            return int.MaxValue;
        if (item.SupportsEveryBiome)
            return int.MaxValue - 1;
        if (item.Biomes.Length == 0)
            return int.MaxValue - 2;
        return (int)item.Biomes[0];
    }

    private void ApplySceneFilter(List<ForgeItem> allItems, List<ForgeItem> matchingItems)
    {
        ClearSceneFilter(false);
        HashSet<GameObject> matching = new HashSet<GameObject>(matchingItems.Select(item => item.Transform.gameObject));
        List<string> hiddenIds = new List<string>();

        foreach (ForgeItem item in allItems)
        {
            GameObject gameObject = item.Transform.gameObject;
            if (matching.Contains(gameObject) || SceneVisibilityManager.instance.IsHidden(gameObject, false))
                continue;

            SceneVisibilityManager.instance.Hide(gameObject, true);
            hiddenIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString());
        }

        SessionState.SetString(GetFilterSessionKey(), string.Join("\n", hiddenIds));
        SceneView.RepaintAll();
    }

    private void ClearSceneFilter(bool revealEveryDirectChild)
    {
        if (revealEveryDirectChild)
        {
            for (int i = 0; i < Organizer.transform.childCount; i++)
                SceneVisibilityManager.instance.Show(Organizer.transform.GetChild(i).gameObject, true);
        }
        else
        {
            string stored = SessionState.GetString(GetFilterSessionKey(), string.Empty);
            string[] ids = stored.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string idText in ids)
            {
                if (!GlobalObjectId.TryParse(idText, out GlobalObjectId id))
                    continue;

                GameObject gameObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
                if (gameObject != null)
                    SceneVisibilityManager.instance.Show(gameObject, true);
            }
        }

        SessionState.EraseString(GetFilterSessionKey());
        SceneView.RepaintAll();
    }

    private string GetFilterSessionKey()
    {
        return "Above.IslandForge.Hidden." + GlobalObjectId.GetGlobalObjectIdSlow(Organizer.gameObject);
    }

    private void ArrangeGrid(List<ForgeItem> allItems)
    {
        List<LayoutGroup> groups = BuildLayoutGroups(allItems);
        List<Transform> transforms = groups.SelectMany(group => group.Items)
            .Select(item => item.Transform)
            .Distinct()
            .ToList();

        if (transforms.Count == 0)
            return;

        Undo.RecordObjects(transforms.Cast<UnityEngine.Object>().ToArray(), "Arrange Island Forge Grid");
        PlanAndOptionallyApplyRows(groups, true);

        foreach (Transform child in transforms)
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);

        EditorSceneManager.MarkSceneDirty(Organizer.gameObject.scene);
        SceneView.RepaintAll();
        Repaint();
        Debug.Log(
            $"Island Forge arranged {transforms.Count} recognized direct children without changing hierarchy order.",
            Organizer);
    }

    private List<LayoutGroup> BuildLayoutGroups(List<ForgeItem> allItems)
    {
        List<ForgeItem> islands = allItems.Where(item => item.IsIsland).ToList();
        List<ForgeItem> connections = allItems.Where(item => item.IsConnection).ToList();
        List<LayoutGroup> groups = new List<LayoutGroup>();
        IslandBiome[] biomes = GetAllBiomes();

        foreach (IslandBiome biome in biomes)
        {
            AddIslandGroup(groups, $"{Nicify(biome.ToString())} • Medium", GetBiomeColor(biome),
                islands.Where(item => item.Island.Size == IslandSize.Medium && BelongsToBiomeLayoutRow(item, biome)));
            AddIslandGroup(groups, $"{Nicify(biome.ToString())} • Small", GetBiomeColor(biome),
                islands.Where(item => item.Island.Size == IslandSize.Small && BelongsToBiomeLayoutRow(item, biome)));
        }

        if (Organizer.MultiBiomeLayout == ForgeMultiBiomeLayoutMode.SharedRows)
        {
            Color sharedColor = new Color(0.62f, 0.42f, 0.82f, 1f);
            AddIslandGroup(groups, "Shared / Multi-Biome • Medium", sharedColor,
                islands.Where(item => !item.SupportsEveryBiome && item.Biomes.Length > 1 && item.Island.Size == IslandSize.Medium));
            AddIslandGroup(groups, "Shared / Multi-Biome • Small", sharedColor,
                islands.Where(item => !item.SupportsEveryBiome && item.Biomes.Length > 1 && item.Island.Size == IslandSize.Small));

            Color allColor = new Color(0.35f, 0.72f, 0.78f, 1f);
            AddIslandGroup(groups, "All Biomes • Medium", allColor,
                islands.Where(item => item.SupportsEveryBiome && item.Island.Size == IslandSize.Medium));
            AddIslandGroup(groups, "All Biomes • Small", allColor,
                islands.Where(item => item.SupportsEveryBiome && item.Island.Size == IslandSize.Small));
        }

        if (connections.Count > 0)
        {
            bool firstConnectionGroup = true;
            if (Organizer.GroupConnectionsByType)
            {
                foreach (IslandConnectionType type in Enum.GetValues(typeof(IslandConnectionType)))
                {
                    LayoutGroup group = CreateGroup(
                        $"Connections • {Nicify(type.ToString())}",
                        new Color(0.72f, 0.55f, 0.27f, 1f),
                        connections.Where(item => item.Connection.ConnectionType == type));
                    if (group.Items.Count == 0)
                        continue;
                    group.BeginsConnectionSection = firstConnectionGroup;
                    firstConnectionGroup = false;
                    groups.Add(group);
                }
            }
            else
            {
                LayoutGroup group = CreateGroup(
                    "Connections",
                    new Color(0.72f, 0.55f, 0.27f, 1f),
                    connections);
                group.BeginsConnectionSection = true;
                groups.Add(group);
            }
        }

        return groups;
    }

    private bool BelongsToBiomeLayoutRow(ForgeItem item, IslandBiome biome)
    {
        if (Organizer.MultiBiomeLayout == ForgeMultiBiomeLayoutMode.SharedRows)
            return !item.SupportsEveryBiome && item.Biomes.Length == 1 && item.Biomes[0] == biome;

        IslandBiome[] allBiomes = GetAllBiomes();
        IslandBiome chosen = item.SupportsEveryBiome
            ? allBiomes[0]
            : item.Biomes.Length > 0 ? item.Biomes[0] : allBiomes[0];
        return chosen == biome;
    }

    private static void AddIslandGroup(
        List<LayoutGroup> groups,
        string label,
        Color color,
        IEnumerable<ForgeItem> items)
    {
        LayoutGroup group = CreateGroup(label, color, items);
        if (group.Items.Count > 0)
            groups.Add(group);
    }

    private static LayoutGroup CreateGroup(string label, Color color, IEnumerable<ForgeItem> items)
    {
        LayoutGroup group = new LayoutGroup { Label = label, Color = color };
        group.Items.AddRange(items.OrderBy(item => item.Transform.name, StringComparer.OrdinalIgnoreCase));
        return group;
    }

    private List<PlannedRow> PlanAndOptionallyApplyRows(List<LayoutGroup> groups, bool apply)
    {
        List<PlannedRow> rows = new List<PlannedRow>();
        float cursorZ = Organizer.LocalOrigin.z;
        int columns = Mathf.Max(1, Organizer.ColumnsPerRow);

        foreach (LayoutGroup group in groups)
        {
            if (group.BeginsConnectionSection && rows.Count > 0)
                cursorZ -= Organizer.ConnectionSectionGap;

            Dictionary<ForgeItem, Footprint> footprints = group.Items.ToDictionary(item => item, CalculateFootprint);
            float cellWidth = Mathf.Max(
                Organizer.MinimumCellWidth,
                group.Items.Max(item => footprints[item].Width));
            float cellDepth = Mathf.Max(
                Organizer.MinimumCellDepth,
                group.Items.Max(item => footprints[item].Depth));
            cellWidth += Organizer.HorizontalGap;
            cellDepth += Organizer.RowGap;

            int physicalRowCount = Mathf.CeilToInt(group.Items.Count / (float)columns);
            for (int physicalRow = 0; physicalRow < physicalRowCount; physicalRow++)
            {
                int start = physicalRow * columns;
                int count = Mathf.Min(columns, group.Items.Count - start);
                float rowWidth = count * cellWidth;
                float startX = Organizer.CenterRows
                    ? Organizer.LocalOrigin.x - rowWidth * 0.5f + cellWidth * 0.5f
                    : Organizer.LocalOrigin.x + cellWidth * 0.5f;

                string label = physicalRowCount > 1
                    ? $"{group.Label}  ({physicalRow + 1}/{physicalRowCount})"
                    : group.Label;
                rows.Add(new PlannedRow
                {
                    Label = label,
                    Color = group.Color,
                    CenterZ = cursorZ,
                    Width = rowWidth,
                    Depth = cellDepth,
                    Count = count
                });

                if (apply)
                {
                    for (int column = 0; column < count; column++)
                    {
                        ForgeItem item = group.Items[start + column];
                        Footprint footprint = footprints[item];
                        Vector3 current = item.Transform.localPosition;
                        Vector3 target = new Vector3(
                            startX + column * cellWidth - footprint.CenterOffset.x,
                            Organizer.PreserveLocalHeight ? current.y : Organizer.LocalOrigin.y,
                            cursorZ - footprint.CenterOffset.z);
                        item.Transform.localPosition = target;
                    }
                }

                cursorZ -= cellDepth;
            }

            cursorZ -= Organizer.CategoryGap;
        }

        return rows;
    }

    private Footprint CalculateFootprint(ForgeItem item)
    {
        if (Organizer.UsePlacementBounds && TryCalculatePlacementBounds(item, out Bounds localBounds))
            return ToFootprint(item, localBounds);

        if (Organizer.FallBackToRendererBounds && TryCalculateRendererBounds(item, out localBounds))
            return ToFootprint(item, localBounds);

        return new Footprint
        {
            Width = Organizer.MinimumCellWidth,
            Depth = Organizer.MinimumCellDepth,
            CenterOffset = Vector3.zero
        };
    }

    private Footprint ToFootprint(ForgeItem item, Bounds localBounds)
    {
        return new Footprint
        {
            Width = Mathf.Max(0.1f, localBounds.size.x),
            Depth = Mathf.Max(0.1f, localBounds.size.z),
            CenterOffset = localBounds.center - item.Transform.localPosition
        };
    }

    private bool TryCalculatePlacementBounds(ForgeItem item, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        IReadOnlyList<BoxCollider> placementBounds = item.RoutePiece.PlacementBounds;
        for (int i = 0; i < placementBounds.Count; i++)
        {
            BoxCollider box = placementBounds[i];
            if (box == null)
                continue;

            EncapsulateBoxCollider(box, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private void EncapsulateBoxCollider(BoxCollider box, ref Bounds bounds, ref bool hasBounds)
    {
        Vector3 half = box.size * 0.5f;
        Matrix4x4 toForge = Organizer.transform.worldToLocalMatrix * box.transform.localToWorldMatrix;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = box.center + Vector3.Scale(half, new Vector3(x, y, z));
                    Vector3 forgeCorner = toForge.MultiplyPoint3x4(localCorner);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(forgeCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(forgeCorner);
                    }
                }
            }
        }
    }

    private bool TryCalculateRendererBounds(ForgeItem item, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = item.Transform.GetComponentsInChildren<Renderer>(Organizer.IncludeInactive);
        foreach (Renderer renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 worldCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 forgeCorner = Organizer.transform.InverseTransformPoint(worldCorner);
                        if (!hasBounds)
                        {
                            bounds = new Bounds(forgeCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(forgeCorner);
                        }
                    }
                }
            }
        }

        return hasBounds;
    }

    private void OnSceneGUI()
    {
        EnsureStyles();
        IslandForgeOrganizer organizer = Organizer;
        List<ForgeItem> items = CollectItems();

        if (organizer.ShowSceneGuides)
        {
            List<PlannedRow> rows = PlanAndOptionallyApplyRows(BuildLayoutGroups(items), false);
            Matrix4x4 previous = Handles.matrix;
            Handles.matrix = organizer.transform.localToWorldMatrix;
            foreach (PlannedRow row in rows)
            {
                Handles.color = new Color(row.Color.r, row.Color.g, row.Color.b, 0.75f);
                float halfWidth = row.Width * 0.5f;
                float startX = organizer.CenterRows ? organizer.LocalOrigin.x - halfWidth : organizer.LocalOrigin.x;
                float endX = organizer.CenterRows ? organizer.LocalOrigin.x + halfWidth : organizer.LocalOrigin.x + row.Width;
                Handles.DrawDottedLine(
                    new Vector3(startX, organizer.LocalOrigin.y, row.CenterZ),
                    new Vector3(endX, organizer.LocalOrigin.y, row.CenterZ),
                    5f);
                Handles.Label(
                    new Vector3(startX, organizer.LocalOrigin.y, row.CenterZ + row.Depth * 0.38f),
                    $"{row.Label}  •  {row.Count}",
                    rowLabelStyle);
            }
            Handles.matrix = previous;
        }

        if (organizer.ShowItemLabels)
        {
            foreach (ForgeItem item in items.Where(value => value.IsRecognized))
            {
                string label = item.IsIsland ? FormatIslandIdentity(item) : item.Transform.name;
                Handles.Label(item.Transform.position, label, EditorStyles.miniBoldLabel);
            }
        }
    }

    private static bool SupportsBiome(ForgeItem item, IslandBiome biome)
    {
        return item.IsRecognized && (item.SupportsEveryBiome || item.Biomes.Contains(biome));
    }

    private static IslandBiome[] GetAllBiomes()
    {
        return Enum.GetValues(typeof(IslandBiome)).Cast<IslandBiome>().ToArray();
    }

    private static Color GetBiomeColor(IslandBiome biome)
    {
        switch (biome)
        {
            case IslandBiome.Grass:
                return new Color(0.35f, 0.72f, 0.38f, 1f);
            case IslandBiome.GoldenTrees:
                return new Color(0.93f, 0.66f, 0.20f, 1f);
            default:
                float hue = Mathf.Repeat((int)biome * 0.19f + 0.12f, 1f);
                return Color.HSVToRGB(hue, 0.55f, 0.9f);
        }
    }

    private static string GetItemTags(ForgeItem item)
    {
        if (item.IsIsland)
            return $"{GetBiomeLabel(item)} • {Nicify(item.Island.Role.ToString())} • {Nicify(item.Island.PhaseUsage.ToString())}";
        if (item.IsConnection)
            return $"{GetBiomeLabel(item)} • {Nicify(item.Connection.ConnectionType.ToString())}";
        return "No AboveIsland / ConnectionIsland";
    }

    private static string GetBiomeLabel(ForgeItem item)
    {
        if (item.SupportsEveryBiome)
            return "All Biomes";
        if (item.Biomes.Length == 0)
            return "No Biome";
        return string.Join(" + ", item.Biomes.Select(biome => Nicify(biome.ToString())));
    }

    private static string Nicify(string value)
    {
        return ObjectNames.NicifyVariableName(value);
    }
}
#endif
