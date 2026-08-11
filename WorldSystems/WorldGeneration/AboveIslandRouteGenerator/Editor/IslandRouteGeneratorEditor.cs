#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(IslandRouteGenerator))]
public sealed class IslandRouteGeneratorEditor : Editor
{
    private SerializedProperty generation;
    private SerializedProperty rhythm;
    private SerializedProperty routeShape;
    private SerializedProperty detours;
    private SerializedProperty autoEvenChances;
    private SerializedProperty biomePhases;
    private SerializedProperty clusterPhases;
    private SerializedProperty islandPrefabs;
    private SerializedProperty connectionPrefabs;
    private SerializedProperty specialIslands;
    private SerializedProperty onRouteGenerated;
    private SerializedProperty onRouteGenerationFailed;

    private ReorderableList biomeList;
    private ReorderableList clusterList;
    private ReorderableList islandList;
    private ReorderableList connectionList;
    private ReorderableList specialList;

    private bool showGeneration = true;
    private bool showRhythm = true;
    private bool showRouteShape = true;
    private bool showDetours = true;
    private bool showBiomes = true;
    private bool showClusters = true;
    private bool showIslands = true;
    private bool showConnections = true;
    private bool showSpecials = true;
    private bool showEvents;

    private void OnEnable()
    {
        generation = serializedObject.FindProperty("generation");
        rhythm = serializedObject.FindProperty("rhythm");
        routeShape = serializedObject.FindProperty("routeShape");
        detours = serializedObject.FindProperty("detours");
        autoEvenChances = serializedObject.FindProperty("autoEvenChances");
        biomePhases = serializedObject.FindProperty("biomePhases");
        clusterPhases = serializedObject.FindProperty("clusterPhases");
        islandPrefabs = serializedObject.FindProperty("islandPrefabs");
        connectionPrefabs = serializedObject.FindProperty("connectionPrefabs");
        specialIslands = serializedObject.FindProperty("specialIslands");
        onRouteGenerated = serializedObject.FindProperty("onRouteGenerated");
        onRouteGenerationFailed = serializedObject.FindProperty("onRouteGenerationFailed");

        biomeList = CreateBiomeList();
        clusterList = CreateClusterList();
        islandList = CreateIslandList();
        connectionList = CreateConnectionList();
        specialList = CreateSpecialList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureBiomeChanceOverrides();

        EditorGUILayout.HelpBox(
            "The generator plans the complete route before instantiating anything. " +
            "1.5 organizes island chances by Biome → Medium/Small. Percentages are normalized only among prefabs that are valid for the requested biome, size, phase, sockets, spacing and placement.",
            MessageType.Info);

        DrawPropertyFoldout("Generation", generation, ref showGeneration);
        DrawPropertyFoldout("Island Rhythm", rhythm, ref showRhythm);
        DrawPropertyFoldout("Route Shape & Height", routeShape, ref showRouteShape);
        DrawPropertyFoldout("Detours", detours, ref showDetours);

        EditorGUILayout.PropertyField(
            autoEvenChances,
            new GUIContent(
                "Auto Even Chances",
                "Inside the current biome/size pool, keeps rows above the edited chance fixed, then redistributes the remainder below it. Connection chances still use their own table."));

        if (autoEvenChances.boolValue)
        {
            EditorGUILayout.HelpBox(
                "Editing an island Chance % affects only that biome/size pool, so Grass Medium and Golden Trees Medium can each total 100% independently.",
                MessageType.None);
        }

        DrawListFoldout("Biome Phases", biomeList, ref showBiomes);
        DrawListFoldout("Island Group Phases (1.2)", clusterList, ref showClusters);
        if (showClusters)
        {
            EditorGUILayout.HelpBox(
                "Each enabled rule may insert a numbered cluster spine into the base route. " +
                "Additional lateral islands and extra links are playable but do not advance " +
                "special-island indexes, rhythm, or Beacon timing. Main Route sockets build " +
                "the spine; Detour or Both sockets build lateral cluster paths.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Topology Weight is a relative preference, not an island spawn chance: 1/1/1/1 means 25% each, and 0 disables that topology. " +
                "Island frequency is controlled by Chance % in the biome/size pools below. The last side island no longer requires an unused exit, and 1.5 can fall back between Small and Medium.",
                MessageType.None);
        }
        DrawGroupedIslandPools();
        DrawListFoldout(
            "Connection Prefabs",
            connectionList,
            ref showConnections,
            rect => HandlePrefabDrop(rect, connectionPrefabs, false));
        DrawListFoldout("Special Islands", specialList, ref showSpecials);

        showEvents = EditorGUILayout.Foldout(showEvents, "Events", true);
        if (showEvents)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(onRouteGenerated);
            EditorGUILayout.PropertyField(onRouteGenerationFailed);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        DrawActions();
    }

    private void DrawActions()
    {
        IslandRouteGenerator generator = (IslandRouteGenerator)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Configuration"))
            {
                bool valid = generator.ValidateConfiguration(out string report);
                if (valid)
                    Debug.Log(report, generator);
                else
                    Debug.LogError(report, generator);
            }

            if (GUILayout.Button("Generate Route"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Above Island Route");
                if (generator.GenerateRoute())
                    EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            if (GUILayout.Button("Clear Generated"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Above Island Route");
                generator.ClearGeneratedRoute();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }

        BackgroundIslandGenerator background = generator.GetComponentInChildren<BackgroundIslandGenerator>(true);
        if (background == null)
        {
            if (GUILayout.Button("Create Background Islands Child (1.3)"))
            {
                GameObject child = new GameObject("Background Islands");
                Undo.RegisterCreatedObjectUndo(child, "Create Background Island Generator");
                child.transform.SetParent(generator.transform, false);
                background = Undo.AddComponent<BackgroundIslandGenerator>(child);
                Selection.activeGameObject = child;
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
        else
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField("Background Generator", background, typeof(BackgroundIslandGenerator), true);
                if (GUILayout.Button("Select", GUILayout.Width(60f)))
                    Selection.activeGameObject = background.gameObject;
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Generate Route creates a scene preview in Edit Mode. Clear it before entering Play Mode if Generate On Start is enabled.",
                MessageType.None);
        }

        if (GUILayout.Button("Copy Route Configuration & Last Diagnostics"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildRouteConfigurationReport(generator);
            Debug.Log("Copied route pools, cluster rules, detour settings and the last detailed generation diagnostics.", generator);
        }
    }

    private static string BuildRouteConfigurationReport(IslandRouteGenerator generator)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Above Route Generator — 1.5.3 Configuration");
        text.AppendLine($"Last seed: {generator.LastUsedSeed}");
        text.AppendLine($"Last failure: {generator.LastFailureReason}");
        text.AppendLine($"Last diagnostics: {generator.LastGenerationDiagnostics}");
        text.AppendLine();
        text.AppendLine("## Rhythm / Shape / Detours");
        text.AppendLine(
            $"Small after Medium: {generator.Rhythm.MinimumSmallIslandsAfterMedium}..{generator.Rhythm.MaximumSmallIslandsAfterMedium}; " +
            $"optional chance: {generator.Rhythm.AdditionalSmallIslandChance}%");
        text.AppendLine(
            $"Heading: {generator.RouteShape.MaximumHeadingAngle}; lateral drift: {generator.RouteShape.MaximumLateralDrift}; " +
            $"height: {generator.RouteShape.MinimumRelativeHeight}..{generator.RouteShape.MaximumRelativeHeight}; " +
            $"forward progress: {generator.RouteShape.MinimumForwardProgressPerIsland}");
        text.AppendLine(
            $"Detours enabled: {generator.Detours.EnableDetours}; chance: {generator.Detours.JunctionDetourChance}%; " +
            $"max/run: {generator.Detours.MaximumDetoursPerRun}; islands: {generator.Detours.MinimumIslands}..{generator.Detours.MaximumIslands}; " +
            $"heading: {generator.Detours.MaximumHeadingAngle}; drift: {generator.Detours.MaximumLateralDrift}");

        text.AppendLine();
        text.AppendLine("## Biome Phases");
        for (int i = 0; i < generator.BiomePhases.Count; i++)
        {
            BiomePhase phase = generator.BiomePhases[i];
            if (phase != null)
                text.AppendLine($"- {i + 1}. {phase.Biome}: {phase.MinimumIslands}..{phase.MaximumIslands} islands");
        }

        text.AppendLine();
        text.AppendLine("## Island Pools by Biome / Size");
        foreach (IslandBiome biome in Enum.GetValues(typeof(IslandBiome)))
        {
            text.AppendLine($"### {biome}");
            AppendRoutePool(text, generator, biome, IslandSize.Medium);
            AppendRoutePool(text, generator, biome, IslandSize.Small);
        }

        text.AppendLine();
        text.AppendLine("## Cluster Rules");
        for (int i = 0; i < generator.ClusterPhases.Count; i++)
        {
            ClusterPhaseRule rule = generator.ClusterPhases[i];
            if (rule == null)
                continue;
            ClusterTopologyWeights weights = rule.TopologyWeights;
            text.AppendLine(
                $"- Rule {i + 1}: enabled={rule.Enabled}; biome={rule.Biome}; chance={rule.ChancePercent}%; " +
                $"start={rule.MinimumStartIndex}..{rule.MaximumStartIndex}; occurrences={rule.MaximumOccurrencesPerRun}; " +
                $"spine={rule.MinimumSpineIslands}..{rule.MaximumSpineIslands}; extras={rule.MinimumAdditionalIslands}..{rule.MaximumAdditionalIslands}; " +
                $"medium extra={rule.MediumAdditionalIslandChance}%; size fallback={rule.AllowAdditionalSizeFallback}; " +
                $"reward endpoint={rule.RewardEndpointChance}%; links={rule.ExtraLinkChance}%/{rule.MaximumExtraLinks}; " +
                $"envelope={rule.MaximumWidth}x{rule.MaximumHeightRange}; attempts={rule.MaximumClusterAttempts}; " +
                $"topology weights H/D/R/B={weights?.Hub ?? 0}/{weights?.Diamond ?? 0}/{weights?.Ring ?? 0}/{weights?.Braided ?? 0}");
        }

        text.AppendLine();
        text.AppendLine("## Connections");
        for (int i = 0; i < generator.ConnectionPrefabs.Count; i++)
        {
            ConnectionPoolEntry entry = generator.ConnectionPrefabs[i];
            if (entry == null || entry.Prefab == null)
            {
                text.AppendLine($"- Row {i + 1}: MISSING");
                continue;
            }
            text.AppendLine(
                $"- {entry.Prefab.name}: type={entry.Prefab.ConnectionType}; chance={entry.ChancePercent}%; " +
                $"repeat gap={entry.MinimumRepeatGap}; max/run={entry.MaximumPerRun}; " +
                $"Main E{entry.Prefab.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.MainRoute)}/X{entry.Prefab.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.MainRoute)}; " +
                $"Detour E{entry.Prefab.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.Detour)}/X{entry.Prefab.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.Detour)}");
        }

        return text.ToString().TrimEnd();
    }

    private static void AppendRoutePool(
        StringBuilder text,
        IslandRouteGenerator generator,
        IslandBiome biome,
        IslandSize size)
    {
        List<IslandPoolEntry> entries = generator.IslandPrefabs
            .Where(entry => entry != null && entry.Prefab != null &&
                entry.Prefab.SupportsBiome(biome) && entry.Prefab.Size == size)
            .ToList();
        float regularTotal = entries
            .Where(entry => entry.Prefab.Role != IslandRole.DetourEndpoint)
            .Sum(entry => entry.GetChancePercent(biome));
        text.AppendLine($"#### {size} — {entries.Count} entries; regular total {regularTotal:0.##}%");
        foreach (IslandPoolEntry entry in entries)
        {
            AboveIsland prefab = entry.Prefab;
            text.AppendLine(
                $"- #{(prefab.HasCatalogId ? prefab.IslandId.ToString("000") : "---")} " +
                $"{(prefab.HasCatalogName ? prefab.IslandName : prefab.name)}: chance={entry.GetChancePercent(biome)}%; " +
                $"role={prefab.Role}; phase={prefab.PhaseUsage}; repeat gap={entry.MinimumRepeatGap}; max/run={entry.MaximumPerRun}; " +
                $"Main E{prefab.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.MainRoute)}/X{prefab.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.MainRoute)}; " +
                $"Detour E{prefab.GetSocketCount(SocketUsage.Entry, SocketRouteUsage.Detour)}/X{prefab.GetSocketCount(SocketUsage.Exit, SocketRouteUsage.Detour)}");
        }
    }

    private static void DrawPropertyFoldout(
        string label,
        SerializedProperty property,
        ref bool expanded)
    {
        expanded = EditorGUILayout.Foldout(expanded, label, true);
        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        SerializedProperty child = property.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            EditorGUILayout.PropertyField(child, true);
            enterChildren = false;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(3f);
    }

    private static void DrawListFoldout(
        string label,
        ReorderableList list,
        ref bool expanded,
        Action<Rect> dropHandler = null)
    {
        expanded = EditorGUILayout.Foldout(expanded, label, true);
        dropHandler?.Invoke(GUILayoutUtility.GetLastRect());
        if (!expanded)
            return;

        list.DoLayoutList();
        EditorGUILayout.Space(3f);
    }

    private ReorderableList CreateBiomeList()
    {
        ReorderableList list = new ReorderableList(
            serializedObject,
            biomePhases,
            true,
            true,
            true,
            true);

        list.drawHeaderCallback = rect =>
        {
            DrawColumnLabel(rect, 0f, 0.48f, "Biome");
            DrawColumnLabel(rect, 0.50f, 0.23f, "Minimum");
            DrawColumnLabel(rect, 0.75f, 0.23f, "Maximum");
        };

        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = biomePhases.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            DrawPropertyColumn(rect, 0f, 0.48f, element.FindPropertyRelative("Biome"));
            DrawPropertyColumn(rect, 0.50f, 0.23f, element.FindPropertyRelative("MinimumIslands"));
            DrawPropertyColumn(rect, 0.75f, 0.23f, element.FindPropertyRelative("MaximumIslands"));
        };

        list.elementHeight = EditorGUIUtility.singleLineHeight + 5f;
        return list;
    }

    private ReorderableList CreateClusterList()
    {
        ReorderableList list = new ReorderableList(
            serializedObject,
            clusterPhases,
            true,
            true,
            true,
            true);

        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(
                rect,
                "Optional playable cluster rules (expand a row to configure)",
                EditorStyles.miniBoldLabel);
        };

        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = clusterPhases.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUI.GetPropertyHeight(element, true);
            EditorGUI.PropertyField(
                rect,
                element,
                new GUIContent($"Group Rule {index + 1}"),
                true);
        };

        list.elementHeightCallback = index =>
        {
            if (index < 0 || index >= clusterPhases.arraySize)
                return EditorGUIUtility.singleLineHeight + 6f;

            SerializedProperty element = clusterPhases.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 6f;
        };

        list.onAddCallback = _ => AddClusterRule();
        return list;
    }

    private void AddClusterRule()
    {
        int index = clusterPhases.arraySize;
        clusterPhases.InsertArrayElementAtIndex(index);
        SerializedProperty element = clusterPhases.GetArrayElementAtIndex(index);

        element.FindPropertyRelative("Enabled").boolValue = true;
        element.FindPropertyRelative("Biome").enumValueIndex = 0;
        element.FindPropertyRelative("ChancePercent").floatValue = 35f;
        element.FindPropertyRelative("MinimumStartIndex").intValue = 4;
        element.FindPropertyRelative("MaximumStartIndex").intValue = -1;
        element.FindPropertyRelative("MaximumOccurrencesPerRun").intValue = 1;
        element.FindPropertyRelative("MinimumSpineIslands").intValue = 3;
        element.FindPropertyRelative("MaximumSpineIslands").intValue = 5;
        element.FindPropertyRelative("CenterpiecePrefab").objectReferenceValue = null;
        element.FindPropertyRelative("MinimumAdditionalIslands").intValue = 1;
        element.FindPropertyRelative("MaximumAdditionalIslands").intValue = 3;
        element.FindPropertyRelative("MediumAdditionalIslandChance").floatValue = 15f;
        element.FindPropertyRelative("AllowAdditionalSizeFallback").boolValue = true;
        element.FindPropertyRelative("RewardEndpointChance").floatValue = 25f;
        element.FindPropertyRelative("ExtraLinkChance").floatValue = 35f;
        element.FindPropertyRelative("MaximumExtraLinks").intValue = 2;
        element.FindPropertyRelative("MaximumWidth").floatValue = 80f;
        element.FindPropertyRelative("MaximumHeightRange").floatValue = 30f;
        element.FindPropertyRelative("MaximumBranchHeadingAngle").floatValue = 105f;
        element.FindPropertyRelative("MaximumClusterAttempts").intValue = 12;
        element.FindPropertyRelative("ExtraLinkPositionTolerance").floatValue = 2.5f;
        element.FindPropertyRelative("ExtraLinkAngleTolerance").floatValue = 15f;

        SerializedProperty weights = element.FindPropertyRelative("TopologyWeights");
        weights.FindPropertyRelative("Hub").floatValue = 35f;
        weights.FindPropertyRelative("Diamond").floatValue = 25f;
        weights.FindPropertyRelative("Ring").floatValue = 15f;
        weights.FindPropertyRelative("Braided").floatValue = 25f;
        element.isExpanded = true;
    }

    private void DrawGroupedIslandPools()
    {
        showIslands = EditorGUILayout.Foldout(showIslands, "Island Prefabs by Biome & Size (1.5)", true);
        Rect dropArea = GUILayoutUtility.GetLastRect();
        HandlePrefabDrop(dropArea, islandPrefabs, true);
        if (!showIslands)
            return;

        EditorGUILayout.HelpBox(
            "Each shared island has an independent chance in every supported biome. Medium and Small regular pools each target 100%. " +
            "Detour Endpoints are shown separately because they are selected only when a branch ends. Drag prefab assets onto this section to add them.",
            MessageType.Info);

        int emptyRowCount = CountEmptyIslandRows();
        if (emptyRowCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"Found {emptyRowCount} hidden empty island row{(emptyRowCount == 1 ? string.Empty : "s")}. " +
                "Empty rows are not displayed in the grouped pools and make configuration validation fail.",
                MessageType.Warning);
        }

        foreach (IslandBiome biome in Enum.GetValues(typeof(IslandBiome)))
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(biome.ToString()), EditorStyles.boldLabel);
                DrawIslandBucket(biome, IslandSize.Medium, false);
                EditorGUILayout.Space(3f);
                DrawIslandBucket(biome, IslandSize.Small, false);
                EditorGUILayout.Space(3f);
                DrawIslandBucket(biome, IslandSize.Medium, true);
                EditorGUILayout.Space(2f);
                DrawIslandBucket(biome, IslandSize.Small, true);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(emptyRowCount == 0))
            {
                string cleanupLabel = emptyRowCount == 0
                    ? "No Empty Rows"
                    : $"Remove Empty Rows ({emptyRowCount})";

                if (GUILayout.Button(cleanupLabel))
                    RemoveEmptyIslandRows();
            }

            if (GUILayout.Button("Normalize Every Biome/Size Pool"))
                NormalizeAllIslandBuckets();
        }
    }

    private void DrawIslandBucket(IslandBiome biome, IslandSize size, bool endpointsOnly)
    {
        List<SerializedProperty> entries = new List<SerializedProperty>();
        List<int> entryIndices = new List<int>();
        List<SerializedProperty> chances = new List<SerializedProperty>();
        float total = 0f;
        int linearCount = 0;
        int clusterCount = 0;

        for (int i = 0; i < islandPrefabs.arraySize; i++)
        {
            SerializedProperty entry = islandPrefabs.GetArrayElementAtIndex(i);
            AboveIsland prefab = entry.FindPropertyRelative("Prefab").objectReferenceValue as AboveIsland;
            if (prefab == null || !prefab.SupportsBiome(biome) || prefab.Size != size)
                continue;
            if ((prefab.Role == IslandRole.DetourEndpoint) != endpointsOnly)
                continue;

            SerializedProperty chance = FindOrCreateBiomeChance(entry, biome);
            entries.Add(entry);
            entryIndices.Add(i);
            chances.Add(chance);
            total += Mathf.Max(0f, chance.floatValue);
            if (prefab.SupportsPhase(IslandPhaseUsage.Linear))
                linearCount++;
            if (prefab.SupportsPhase(IslandPhaseUsage.Cluster))
                clusterCount++;
        }

        string label = endpointsOnly
            ? $"{size} Detour Endpoints — Total: {entries.Count}"
            : $"{size} — {entries.Count} islands — Total: {total:0.##}%";

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (!endpointsOnly && entries.Count > 0 && GUILayout.Button("Normalize 100%", GUILayout.Width(105f)))
                NormalizeChanceProperties(chances, 100f);
        }

        if (!endpointsOnly)
        {
            EditorGUILayout.LabelField(
                $"Linear eligible: {linearCount}  •  Cluster eligible: {clusterCount}" +
                (Mathf.Abs(total - 100f) <= 0.01f ? string.Empty : "  •  Needs normalization"),
                EditorStyles.miniLabel);
        }

        if (entries.Count == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect header = EditorGUILayout.GetControlRect();
        DrawColumnLabel(header, 0f, 0.39f, "Island Prefab");
        DrawColumnLabel(header, 0.41f, 0.13f, "Chance %");
        DrawColumnLabel(header, 0.56f, 0.12f, "Repeat Gap");
        DrawColumnLabel(header, 0.70f, 0.12f, "Max / Run");
        DrawColumnLabel(header, 0.84f, 0.08f, "Use");

        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty entry = entries[i];
            int entryIndex = entryIndices[i];
            SerializedProperty chance = chances[i];
            AboveIsland prefab = entry.FindPropertyRelative("Prefab").objectReferenceValue as AboveIsland;
            Rect row = EditorGUILayout.GetControlRect();

            using (new EditorGUI.DisabledScope(true))
                DrawPropertyColumn(row, 0f, 0.39f, entry.FindPropertyRelative("Prefab"));

            EditorGUI.BeginChangeCheck();
            DrawPropertyColumn(row, 0.41f, 0.13f, chance);
            bool chanceChanged = EditorGUI.EndChangeCheck();
            DrawPropertyColumn(row, 0.56f, 0.12f, entry.FindPropertyRelative("MinimumRepeatGap"));
            DrawPropertyColumn(row, 0.70f, 0.12f, entry.FindPropertyRelative("MaximumPerRun"));

            string phase = prefab == null
                ? "?"
                : prefab.PhaseUsage == IslandPhaseUsage.Both
                    ? "L+C"
                    : prefab.PhaseUsage == IslandPhaseUsage.Linear ? "L" : "C";
            EditorGUI.LabelField(Column(row, 0.84f, 0.08f), phase, EditorStyles.miniBoldLabel);

            GUIContent removeContent = new GUIContent(
                "×",
                prefab == null
                    ? "Remove this island table row."
                    : $"Remove '{prefab.name}' from every biome/size pool. You can undo this action.");

            if (GUI.Button(Column(row, 0.94f, 0.06f), removeContent, EditorStyles.miniButton))
                RemoveIslandRow(entryIndex, prefab);

            if (chanceChanged && autoEvenChances.boolValue && !endpointsOnly)
                AutoEvenChanceProperties(chances, i);

            if (prefab != null && prefab.Role == IslandRole.Junction)
            {
                Rect badge = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight - 2f);
                badge.xMin += 16f;
                EditorGUI.LabelField(badge, "[Junction]", EditorStyles.miniLabel);
            }
        }
    }

    private int CountEmptyIslandRows()
    {
        int count = 0;
        for (int i = 0; i < islandPrefabs.arraySize; i++)
        {
            SerializedProperty prefab = islandPrefabs
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("Prefab");

            if (prefab.objectReferenceValue == null)
                count++;
        }

        return count;
    }

    private void RemoveEmptyIslandRows()
    {
        Undo.RecordObjects(targets, "Remove Empty Island Rows");

        for (int i = islandPrefabs.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty prefab = islandPrefabs
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("Prefab");

            if (prefab.objectReferenceValue == null)
                DeleteArrayElement(islandPrefabs, i);
        }

        if (autoEvenChances.boolValue)
            NormalizeAllIslandBuckets();

        serializedObject.ApplyModifiedProperties();
        GUIUtility.ExitGUI();
    }

    private void RemoveIslandRow(int index, AboveIsland prefab)
    {
        string undoLabel = prefab == null
            ? "Remove Island Row"
            : $"Remove Island {prefab.name}";

        Undo.RecordObjects(targets, undoLabel);
        DeleteArrayElement(islandPrefabs, index);

        if (autoEvenChances.boolValue)
            NormalizeAllIslandBuckets();

        serializedObject.ApplyModifiedProperties();
        GUIUtility.ExitGUI();
    }

    private static void DeleteArrayElement(SerializedProperty array, int index)
    {
        int previousSize = array.arraySize;
        array.DeleteArrayElementAtIndex(index);

        // Unity clears object-reference array entries before removing them.
        // IslandPoolEntry is currently a serialized class, but this keeps the
        // helper correct if that representation changes later.
        if (array.arraySize == previousSize)
            array.DeleteArrayElementAtIndex(index);
    }

    private void EnsureBiomeChanceOverrides()
    {
        if (islandPrefabs == null)
            return;

        foreach (IslandBiome biome in Enum.GetValues(typeof(IslandBiome)))
        {
            for (int i = 0; i < islandPrefabs.arraySize; i++)
            {
                SerializedProperty entry = islandPrefabs.GetArrayElementAtIndex(i);
                AboveIsland prefab = entry.FindPropertyRelative("Prefab").objectReferenceValue as AboveIsland;
                if (prefab != null && prefab.SupportsBiome(biome))
                    FindOrCreateBiomeChance(entry, biome);
            }
        }
    }

    private static SerializedProperty FindOrCreateBiomeChance(
        SerializedProperty entry,
        IslandBiome biome)
    {
        SerializedProperty values = entry.FindPropertyRelative("BiomeChances");
        for (int i = 0; i < values.arraySize; i++)
        {
            SerializedProperty value = values.GetArrayElementAtIndex(i);
            if (value.FindPropertyRelative("Biome").enumValueIndex == (int)biome)
                return value.FindPropertyRelative("ChancePercent");
        }

        int index = values.arraySize;
        values.InsertArrayElementAtIndex(index);
        SerializedProperty created = values.GetArrayElementAtIndex(index);
        created.FindPropertyRelative("Biome").enumValueIndex = (int)biome;
        created.FindPropertyRelative("ChancePercent").floatValue =
            entry.FindPropertyRelative("ChancePercent").floatValue;
        return created.FindPropertyRelative("ChancePercent");
    }

    private void NormalizeAllIslandBuckets()
    {
        foreach (IslandBiome biome in Enum.GetValues(typeof(IslandBiome)))
        {
            NormalizeIslandBucket(biome, IslandSize.Medium, false);
            NormalizeIslandBucket(biome, IslandSize.Small, false);
            NormalizeIslandBucket(biome, IslandSize.Medium, true);
            NormalizeIslandBucket(biome, IslandSize.Small, true);
        }
    }

    private void NormalizeIslandBucket(IslandBiome biome, IslandSize size, bool endpointsOnly)
    {
        List<SerializedProperty> chances = new List<SerializedProperty>();
        for (int i = 0; i < islandPrefabs.arraySize; i++)
        {
            SerializedProperty entry = islandPrefabs.GetArrayElementAtIndex(i);
            AboveIsland prefab = entry.FindPropertyRelative("Prefab").objectReferenceValue as AboveIsland;
            if (prefab == null || !prefab.SupportsBiome(biome) || prefab.Size != size ||
                (prefab.Role == IslandRole.DetourEndpoint) != endpointsOnly)
            {
                continue;
            }
            chances.Add(FindOrCreateBiomeChance(entry, biome));
        }
        NormalizeChanceProperties(chances, 100f);
    }

    private static void AutoEvenChanceProperties(List<SerializedProperty> chances, int editedIndex)
    {
        if (editedIndex < 0 || editedIndex >= chances.Count)
            return;
        float locked = 0f;
        for (int i = 0; i < editedIndex; i++)
            locked += Mathf.Max(0f, chances[i].floatValue);
        float available = Mathf.Max(0f, 100f - locked);
        chances[editedIndex].floatValue = RoundChance(Mathf.Clamp(chances[editedIndex].floatValue, 0f, available));
        if (editedIndex + 1 < chances.Count)
            NormalizeChanceProperties(chances.GetRange(editedIndex + 1, chances.Count - editedIndex - 1), available - chances[editedIndex].floatValue);
    }

    private static void NormalizeChanceProperties(List<SerializedProperty> chances, float target)
    {
        if (chances == null || chances.Count == 0)
            return;
        float sourceTotal = chances.Sum(value => Mathf.Max(0f, value.floatValue));
        if (sourceTotal <= 0.0001f)
        {
            float even = target / chances.Count;
            for (int i = 0; i < chances.Count; i++)
                chances[i].floatValue = RoundChance(i == chances.Count - 1 ? target - even * i : even);
            return;
        }

        float assigned = 0f;
        for (int i = 0; i < chances.Count; i++)
        {
            float value = i == chances.Count - 1
                ? target - assigned
                : RoundChance(target * Mathf.Max(0f, chances[i].floatValue) / sourceTotal);
            chances[i].floatValue = Mathf.Max(0f, value);
            assigned += chances[i].floatValue;
        }
    }

    private ReorderableList CreateIslandList()
    {
        ReorderableList list = new ReorderableList(
            serializedObject,
            islandPrefabs,
            true,
            true,
            true,
            true);

        list.drawHeaderCallback = rect =>
        {
            DrawChanceTableHeader(rect, "Island Prefab");
            HandlePrefabDrop(rect, islandPrefabs, true);
        };
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = islandPrefabs.GetArrayElementAtIndex(index);
            DrawChanceTableRow(rect, element, islandPrefabs, index);
        };
        list.onAddCallback = _ => AddChanceRow(islandPrefabs, null);
        list.onRemoveCallback = targetList => RemoveChanceRow(targetList, islandPrefabs);
        list.elementHeight = EditorGUIUtility.singleLineHeight + 5f;
        return list;
    }

    private ReorderableList CreateConnectionList()
    {
        ReorderableList list = new ReorderableList(
            serializedObject,
            connectionPrefabs,
            true,
            true,
            true,
            true);

        list.drawHeaderCallback = rect =>
        {
            DrawChanceTableHeader(rect, "Connection Prefab");
            HandlePrefabDrop(rect, connectionPrefabs, false);
        };
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = connectionPrefabs.GetArrayElementAtIndex(index);
            DrawChanceTableRow(rect, element, connectionPrefabs, index);
        };
        list.onAddCallback = _ => AddChanceRow(connectionPrefabs, null);
        list.onRemoveCallback = targetList => RemoveChanceRow(targetList, connectionPrefabs);
        list.elementHeight = EditorGUIUtility.singleLineHeight + 5f;
        return list;
    }

    private ReorderableList CreateSpecialList()
    {
        ReorderableList list = new ReorderableList(
            serializedObject,
            specialIslands,
            true,
            true,
            true,
            true);

        list.drawHeaderCallback = rect =>
        {
            DrawColumnLabel(rect, 0f, 0.58f, "Specific Island Prefab");
            DrawColumnLabel(rect, 0.60f, 0.18f, "Min Index");
            DrawColumnLabel(rect, 0.80f, 0.18f, "Max Index");
        };

        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = specialIslands.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            DrawPropertyColumn(rect, 0f, 0.58f, element.FindPropertyRelative("IslandPrefab"));
            DrawPropertyColumn(rect, 0.60f, 0.18f, element.FindPropertyRelative("MinimumIndex"));
            DrawPropertyColumn(rect, 0.80f, 0.18f, element.FindPropertyRelative("MaximumIndex"));
        };

        list.elementHeight = EditorGUIUtility.singleLineHeight + 5f;
        return list;
    }

    private static void DrawChanceTableHeader(Rect rect, string prefabLabel)
    {
        DrawColumnLabel(rect, 0f, 0.49f, prefabLabel);
        DrawColumnLabel(rect, 0.51f, 0.15f, "Chance %");
        DrawColumnLabel(rect, 0.68f, 0.14f, "Repeat Gap");
        DrawColumnLabel(rect, 0.84f, 0.14f, "Max / Run");
    }

    private void DrawChanceTableRow(
        Rect rect,
        SerializedProperty element,
        SerializedProperty table,
        int index)
    {
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;

        SerializedProperty prefab = element.FindPropertyRelative("Prefab");
        SerializedProperty chance = element.FindPropertyRelative("ChancePercent");

        EditorGUI.BeginChangeCheck();
        DrawPropertyColumn(rect, 0f, 0.49f, prefab);
        bool prefabChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        DrawPropertyColumn(rect, 0.51f, 0.15f, chance);
        bool chanceChanged = EditorGUI.EndChangeCheck();

        DrawPropertyColumn(rect, 0.68f, 0.14f, element.FindPropertyRelative("MinimumRepeatGap"));
        DrawPropertyColumn(rect, 0.84f, 0.14f, element.FindPropertyRelative("MaximumPerRun"));

        if (!autoEvenChances.boolValue)
            return;

        if (chanceChanged)
            AutoEvenBelow(table, index);
        else if (prefabChanged && prefab.objectReferenceValue != null)
            NormalizeAllChances(table);
    }

    private void AddChanceRow(
        SerializedProperty table,
        UnityEngine.Object prefab,
        bool rebalance = true)
    {
        float startingChance = autoEvenChances.boolValue
            ? GetAverageChance(table)
            : 100f;

        int index = table.arraySize;
        table.InsertArrayElementAtIndex(index);

        SerializedProperty element = table.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        element.FindPropertyRelative("ChancePercent").floatValue = startingChance;
        element.FindPropertyRelative("MinimumRepeatGap").intValue = 0;
        element.FindPropertyRelative("MaximumPerRun").intValue = -1;

        if (autoEvenChances.boolValue && rebalance)
            NormalizeAllChances(table);
    }

    private void RemoveChanceRow(ReorderableList list, SerializedProperty table)
    {
        ReorderableList.defaultBehaviours.DoRemoveButton(list);

        if (autoEvenChances.boolValue)
            NormalizeAllChances(table);
    }

    private void HandlePrefabDrop(
        Rect dropArea,
        SerializedProperty table,
        bool islandTable)
    {
        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition) ||
            (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
        {
            return;
        }

        List<UnityEngine.Object> accepted = new List<UnityEngine.Object>();
        UnityEngine.Object[] dragged = DragAndDrop.objectReferences;
        for (int i = 0; i < dragged.Length; i++)
        {
            UnityEngine.Object prefab = GetDraggedPrefab(dragged[i], islandTable);
            if (prefab == null || !EditorUtility.IsPersistent(prefab) ||
                ContainsPrefab(table, prefab) || accepted.Contains(prefab))
            {
                continue;
            }

            accepted.Add(prefab);
        }

        if (accepted.Count == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            for (int i = 0; i < accepted.Count; i++)
                AddChanceRow(table, accepted[i], false);

            if (autoEvenChances.boolValue)
            {
                if (islandTable)
                {
                    EnsureBiomeChanceOverrides();
                    NormalizeAllIslandBuckets();
                }
                else
                {
                    NormalizeAllChances(table);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        current.Use();
    }

    private static UnityEngine.Object GetDraggedPrefab(
        UnityEngine.Object dragged,
        bool islandTable)
    {
        if (islandTable && dragged is AboveIsland island)
            return island;

        if (!islandTable && dragged is ConnectionIsland connection)
            return connection;

        if (!(dragged is GameObject gameObject))
            return null;

        return islandTable
            ? gameObject.GetComponent<AboveIsland>()
            : (UnityEngine.Object)gameObject.GetComponent<ConnectionIsland>();
    }

    private static bool ContainsPrefab(
        SerializedProperty table,
        UnityEngine.Object prefab)
    {
        for (int i = 0; i < table.arraySize; i++)
        {
            SerializedProperty existing = table
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("Prefab");

            if (existing.objectReferenceValue == prefab)
                return true;
        }

        return false;
    }

    private static float GetAverageChance(SerializedProperty table)
    {
        if (table.arraySize == 0)
            return 100f;

        float total = 0f;
        for (int i = 0; i < table.arraySize; i++)
        {
            total += Mathf.Max(
                0f,
                table.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("ChancePercent")
                    .floatValue);
        }

        return total > 0.0001f ? total / table.arraySize : 1f;
    }

    private static void AutoEvenBelow(SerializedProperty table, int editedIndex)
    {
        if (editedIndex < 0 || editedIndex >= table.arraySize)
            return;

        float lockedTotal = 0f;
        for (int i = 0; i < editedIndex; i++)
        {
            lockedTotal += Mathf.Max(
                0f,
                table.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("ChancePercent")
                    .floatValue);
        }

        SerializedProperty editedChance = table
            .GetArrayElementAtIndex(editedIndex)
            .FindPropertyRelative("ChancePercent");

        float availableForEditedAndLower = Mathf.Max(0f, 100f - lockedTotal);
        if (editedIndex == table.arraySize - 1)
        {
            editedChance.floatValue = RoundChance(availableForEditedAndLower);
            return;
        }

        editedChance.floatValue = RoundChance(
            Mathf.Clamp(editedChance.floatValue, 0f, availableForEditedAndLower));

        float remainder = Mathf.Max(
            0f,
            100f - lockedTotal - editedChance.floatValue);

        DistributeChance(table, editedIndex + 1, remainder);
    }

    private static void NormalizeAllChances(SerializedProperty table)
    {
        DistributeChance(table, 0, 100f);
    }

    private static void DistributeChance(
        SerializedProperty table,
        int startIndex,
        float targetTotal)
    {
        int count = table.arraySize - startIndex;
        if (count <= 0)
            return;

        int targetUnits = Mathf.Max(0, Mathf.RoundToInt(targetTotal * 100f));
        double totalWeight = 0d;
        double[] weights = new double[count];

        for (int i = 0; i < count; i++)
        {
            float value = table.GetArrayElementAtIndex(startIndex + i)
                .FindPropertyRelative("ChancePercent")
                .floatValue;

            weights[i] = Math.Max(0d, value);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0.000001d)
        {
            totalWeight = count;
            for (int i = 0; i < count; i++)
                weights[i] = 1d;
        }

        int allocatedUnits = 0;
        int[] units = new int[count];
        List<ChanceRemainder> remainders = new List<ChanceRemainder>(count);

        for (int i = 0; i < count; i++)
        {
            double exactUnits = targetUnits * weights[i] / totalWeight;
            units[i] = (int)Math.Floor(exactUnits);
            allocatedUnits += units[i];
            remainders.Add(new ChanceRemainder(i, exactUnits - units[i]));
        }

        remainders.Sort((a, b) =>
        {
            int remainderComparison = b.Remainder.CompareTo(a.Remainder);
            return remainderComparison != 0
                ? remainderComparison
                : a.Index.CompareTo(b.Index);
        });

        int missingUnits = targetUnits - allocatedUnits;
        for (int i = 0; i < missingUnits; i++)
            units[remainders[i % remainders.Count].Index]++;

        for (int i = 0; i < count; i++)
        {
            table.GetArrayElementAtIndex(startIndex + i)
                .FindPropertyRelative("ChancePercent")
                .floatValue = units[i] / 100f;
        }
    }

    private static float RoundChance(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    private readonly struct ChanceRemainder
    {
        public readonly int Index;
        public readonly double Remainder;

        public ChanceRemainder(int index, double remainder)
        {
            Index = index;
            Remainder = remainder;
        }
    }

    private static void DrawColumnLabel(Rect rect, float start, float width, string label)
    {
        Rect column = Column(rect, start, width);
        EditorGUI.LabelField(column, label, EditorStyles.miniBoldLabel);
    }

    private static void DrawPropertyColumn(
        Rect rect,
        float start,
        float width,
        SerializedProperty property)
    {
        EditorGUI.PropertyField(Column(rect, start, width), property, GUIContent.none);
    }

    private static Rect Column(Rect rect, float start, float width)
    {
        return new Rect(
            rect.x + rect.width * start,
            rect.y,
            rect.width * width,
            rect.height);
    }
}
#endif
