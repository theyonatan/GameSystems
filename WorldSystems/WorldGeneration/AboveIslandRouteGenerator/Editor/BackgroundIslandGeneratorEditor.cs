#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(BackgroundIslandGenerator))]
public sealed class BackgroundIslandGeneratorEditor : Editor
{
    private SerializedProperty integration;
    private SerializedProperty distribution;
    private SerializedProperty scenicClusters;
    private SerializedProperty performance;
    private SerializedProperty autoEvenChances;
    private SerializedProperty layers;
    private SerializedProperty backgroundPrefabs;
    private SerializedProperty landmarkRules;
    private SerializedProperty onBackgroundGenerated;
    private SerializedProperty onBackgroundGenerationFailed;

    private ReorderableList prefabList;
    private bool showIntegration = true;
    private bool showDistribution = true;
    private bool showClusters = true;
    private bool showPerformance = true;
    private bool showLayers = true;
    private bool showPrefabs = true;
    private bool showLandmarks = true;
    private bool showEvents;

    private void OnEnable()
    {
        integration = serializedObject.FindProperty("integration");
        distribution = serializedObject.FindProperty("distribution");
        scenicClusters = serializedObject.FindProperty("scenicClusters");
        performance = serializedObject.FindProperty("performance");
        autoEvenChances = serializedObject.FindProperty("autoEvenChances");
        layers = serializedObject.FindProperty("layers");
        backgroundPrefabs = serializedObject.FindProperty("backgroundPrefabs");
        landmarkRules = serializedObject.FindProperty("landmarkRules");
        onBackgroundGenerated = serializedObject.FindProperty("onBackgroundGenerated");
        onBackgroundGenerationFailed = serializedObject.FindProperty("onBackgroundGenerationFailed");
        prefabList = CreatePrefabList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "This component decorates an already-generated playable route. It never changes sockets, route placement, or route success. " +
            "Place it on a child of IslandRouteGenerator for the recommended one-way setup.",
            MessageType.Info);

        DrawPropertyFoldout("Route Integration", integration, ref showIntegration);
        DrawPropertyFoldout("Distribution & Clearance", distribution, ref showDistribution);
        DrawPropertyFoldout("Scenic Clusters", scenicClusters, ref showClusters);
        if (showClusters)
        {
            EditorGUILayout.HelpBox(
                "Cluster Chance is rolled per eligible center. Maximum Clusters still caps it; set the maximum to 0 for unlimited. " +
                "Satellite Repeat Gap can be ignored independently, and satellite spacing is now measured from island edges so large bounds can still form groups.",
                MessageType.None);
        }
        DrawPropertyFoldout("Performance Budget", performance, ref showPerformance);

        EditorGUILayout.PropertyField(autoEvenChances, new GUIContent(
            "Auto Even Chances",
            "Keeps the edited row and rows above fixed, then redistributes the remaining percentage across lower rows."));

        showLayers = EditorGUILayout.Foldout(showLayers, "Near / Middle / Far Layers", true);
        if (showLayers)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(layers, true);
            EditorGUI.indentLevel--;
        }

        showPrefabs = EditorGUILayout.Foldout(showPrefabs, "Background Prefabs", true);
        HandlePrefabDrop(GUILayoutUtility.GetLastRect());
        if (showPrefabs)
        {
            prefabList.DoLayoutList();
            EditorGUILayout.HelpBox(
                "Drag one or many BackgroundIsland prefab assets onto the foldout name or table header. Chances are normalized only among currently eligible prefabs.",
                MessageType.None);
        }

        showLandmarks = EditorGUILayout.Foldout(showLandmarks, "Hero Landmark Rules", true);
        if (showLandmarks)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(landmarkRules, true);
            EditorGUI.indentLevel--;
        }

        showEvents = EditorGUILayout.Foldout(showEvents, "Events", true);
        if (showEvents)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(onBackgroundGenerated);
            EditorGUILayout.PropertyField(onBackgroundGenerationFailed);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space(8f);
        DrawActions();
    }

    private void DrawActions()
    {
        BackgroundIslandGenerator generator = (BackgroundIslandGenerator)target;
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

            if (GUILayout.Button("Generate Background"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Background Islands");
                if (generator.GenerateBackground())
                    EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            if (GUILayout.Button("Clear Background"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Background Islands");
                generator.ClearGeneratedBackground();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }

        EditorGUILayout.HelpBox(
            string.IsNullOrEmpty(generator.LastGenerationReport)
                ? "Generate the playable route first, then use Generate Background. Automatic generation runs after the route succeeds."
                : generator.LastGenerationReport,
            MessageType.None);

        if (GUILayout.Button("Copy Background Configuration & Diagnostics"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildConfigurationReport(generator);
            Debug.Log("Copied the complete background-island configuration and last diagnostic report.", generator);
        }
    }

    private static string BuildConfigurationReport(BackgroundIslandGenerator generator)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Above Background Islands — 1.5.3 Configuration");
        text.AppendLine($"Last seed: {generator.LastUsedSeed}");
        text.AppendLine($"Last report: {generator.LastGenerationReport}");

        BackgroundDistributionSettings distribution = generator.Distribution;
        text.AppendLine();
        text.AppendLine("## Distribution");
        text.AppendLine(
            $"Route range: {distribution.StartIslandIndex}..{distribution.EndIslandIndex}; " +
            $"corridor: {distribution.RouteCorridorClearance}; playable clearance: {distribution.PlayableBoundsClearance}; " +
            $"background clearance: {distribution.BackgroundBoundsClearance}; cell size: {distribution.CellSize}; " +
            $"placement attempts: {distribution.MaximumPlacementAttempts}; candidates: {distribution.CandidatesPerIsland}");
        text.AppendLine(
            $"Standalone Small boost: enabled={distribution.BoostStandaloneSmallIslands}; " +
            $"scale={distribution.MinimumStandaloneSmallScaleMultiplier}..{distribution.MaximumStandaloneSmallScaleMultiplier}; " +
            "cluster centers/satellites/landmarks excluded=True");

        BackgroundScenicClusterSettings clusters = generator.ScenicClusters;
        text.AppendLine();
        text.AppendLine("## Scenic Clusters");
        text.AppendLine(
            $"Enabled: {clusters.Enabled}; chance: {clusters.ClusterChance}%; maximum groups: " +
            $"{(clusters.MaximumClustersPerRun <= 0 ? "unlimited" : clusters.MaximumClustersPerRun.ToString())}; " +
            $"satellites: {clusters.MinimumSatelliteIslands}..{clusters.MaximumSatelliteIslands}; " +
            $"spread: {clusters.MinimumSpreadRadius}..{clusters.MaximumSpreadRadius}; surface gap: {clusters.MinimumSurfaceGap}; " +
            $"height: {clusters.MinimumHeightOffset}..{clusters.MaximumHeightOffset}; " +
            $"scale: {clusters.MinimumScaleMultiplier}..{clusters.MaximumScaleMultiplier}; " +
            $"maximum size: {clusters.MaximumSatelliteSize}; ignore repeat gap: {clusters.IgnoreRepeatGapForSatellites}; " +
            $"reserved satellite slots: {clusters.ReservedSatelliteSlots}; keep center biome: {clusters.KeepCenterBiome}");

        BackgroundPerformanceSettings performance = generator.Performance;
        text.AppendLine();
        text.AppendLine("## Budgets");
        text.AppendLine(
            $"Maximum islands: {performance.MaximumBackgroundIslands}; maximum visual cost: {performance.MaximumVisualCost}");

        text.AppendLine();
        text.AppendLine("## Layers");
        for (int i = 0; i < generator.Layers.Count; i++)
        {
            BackgroundLayerSettings layer = generator.Layers[i];
            if (layer == null)
                continue;
            text.AppendLine(
                $"- {layer.Name} ({layer.Layer}): enabled={layer.Enabled}; count={layer.MinimumCount}..{layer.MaximumCount}; " +
                $"density/100={layer.DensityPer100Units}; distance={layer.MinimumLateralDistance}..{layer.MaximumLateralDistance}; " +
                $"height={layer.MinimumHeightOffset}..{layer.MaximumHeightOffset}; spacing={layer.MinimumSpacing}; " +
                $"scale={layer.MinimumScaleMultiplier}..{layer.MaximumScaleMultiplier}; max size={layer.MaximumSize}; " +
                $"empty cells={layer.EmptyCellChance}%; max/cell={layer.MaximumIslandsPerCell}");
        }

        text.AppendLine();
        text.AppendLine("## Prefab Pool");
        for (int i = 0; i < generator.BackgroundPrefabs.Count; i++)
        {
            BackgroundIslandPoolEntry entry = generator.BackgroundPrefabs[i];
            if (entry == null || entry.Prefab == null)
            {
                text.AppendLine($"- Row {i + 1}: MISSING PREFAB");
                continue;
            }

            BackgroundIsland prefab = entry.Prefab;
            string biomes = prefab.AllowedBiomes == null || prefab.AllowedBiomes.Count == 0
                ? "All"
                : JoinValues(prefab.AllowedBiomes);
            text.AppendLine(
                $"- {prefab.name}: chance={entry.ChancePercent}%; repeat gap={entry.MinimumRepeatGap}; max/run={entry.MaximumPerRun}; " +
                $"biomes={biomes}; layers={prefab.AllowedLayers}; size={prefab.Size}; visual cost={prefab.VisualCost}; " +
                $"radius={prefab.CalculateLocalPlacementRadius():0.##}; random yaw={prefab.AllowRandomYaw}; " +
                $"scale={prefab.MinimumScaleMultiplier:0.##}..{prefab.MaximumScaleMultiplier:0.##}");
        }

        return text.ToString().TrimEnd();
    }

    private static string JoinValues<T>(IReadOnlyList<T> values)
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                text.Append(", ");
            text.Append(values[i]);
        }
        return text.ToString();
    }

    private ReorderableList CreatePrefabList()
    {
        ReorderableList list = new ReorderableList(serializedObject, backgroundPrefabs, true, true, true, true);
        list.drawHeaderCallback = rect =>
        {
            DrawColumnLabel(rect, 0f, 0.49f, "Background Prefab");
            DrawColumnLabel(rect, 0.51f, 0.15f, "Chance %");
            DrawColumnLabel(rect, 0.68f, 0.14f, "Repeat Gap");
            DrawColumnLabel(rect, 0.84f, 0.14f, "Max / Run");
            HandlePrefabDrop(rect);
        };
        list.drawElementCallback = (rect, index, active, focused) => DrawPrefabRow(rect, index);
        list.onAddCallback = _ => AddPrefabRow(null, true);
        list.onRemoveCallback = targetList =>
        {
            ReorderableList.defaultBehaviours.DoRemoveButton(targetList);
            if (autoEvenChances.boolValue)
                NormalizeAllChances(backgroundPrefabs);
        };
        list.elementHeight = EditorGUIUtility.singleLineHeight + 5f;
        return list;
    }

    private void DrawPrefabRow(Rect rect, int index)
    {
        SerializedProperty element = backgroundPrefabs.GetArrayElementAtIndex(index);
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
            AutoEvenBelow(backgroundPrefabs, index);
        else if (prefabChanged && prefab.objectReferenceValue != null)
            NormalizeAllChances(backgroundPrefabs);
    }

    private void HandlePrefabDrop(Rect dropArea)
    {
        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition) ||
            (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
            return;

        List<BackgroundIsland> accepted = new List<BackgroundIsland>();
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            BackgroundIsland prefab = GetDraggedPrefab(DragAndDrop.objectReferences[i]);
            if (prefab == null || !EditorUtility.IsPersistent(prefab) || ContainsPrefab(prefab) || accepted.Contains(prefab))
                continue;
            accepted.Add(prefab);
        }
        if (accepted.Count == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            for (int i = 0; i < accepted.Count; i++)
                AddPrefabRow(accepted[i], false);
            if (autoEvenChances.boolValue)
                NormalizeAllChances(backgroundPrefabs);
            serializedObject.ApplyModifiedProperties();
        }
        current.Use();
    }

    private void AddPrefabRow(BackgroundIsland prefab, bool rebalance)
    {
        float chance = autoEvenChances.boolValue ? GetAverageChance(backgroundPrefabs) : 100f;
        int index = backgroundPrefabs.arraySize;
        backgroundPrefabs.InsertArrayElementAtIndex(index);
        SerializedProperty element = backgroundPrefabs.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        element.FindPropertyRelative("ChancePercent").floatValue = chance;
        element.FindPropertyRelative("MinimumRepeatGap").intValue = 0;
        element.FindPropertyRelative("MaximumPerRun").intValue = -1;
        if (autoEvenChances.boolValue && rebalance)
            NormalizeAllChances(backgroundPrefabs);
    }

    private bool ContainsPrefab(BackgroundIsland prefab)
    {
        for (int i = 0; i < backgroundPrefabs.arraySize; i++)
        {
            if (backgroundPrefabs.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)
                return true;
        }
        return false;
    }

    private static BackgroundIsland GetDraggedPrefab(UnityEngine.Object dragged)
    {
        if (dragged is BackgroundIsland island)
            return island;
        GameObject gameObject = dragged as GameObject;
        return gameObject != null ? gameObject.GetComponent<BackgroundIsland>() : null;
    }

    private static void DrawPropertyFoldout(string label, SerializedProperty property, ref bool expanded)
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

    private static void DrawColumnLabel(Rect rect, float start, float width, string label)
    {
        EditorGUI.LabelField(Column(rect, start, width), label, EditorStyles.miniBoldLabel);
    }

    private static void DrawPropertyColumn(Rect rect, float start, float width, SerializedProperty property)
    {
        EditorGUI.PropertyField(Column(rect, start, width), property, GUIContent.none);
    }

    private static Rect Column(Rect rect, float start, float width)
    {
        return new Rect(rect.x + rect.width * start, rect.y, rect.width * width, rect.height);
    }

    private static float GetAverageChance(SerializedProperty table)
    {
        if (table.arraySize == 0)
            return 100f;
        float total = 0f;
        for (int i = 0; i < table.arraySize; i++)
            total += Mathf.Max(0f, table.GetArrayElementAtIndex(i).FindPropertyRelative("ChancePercent").floatValue);
        return total > 0.0001f ? total / table.arraySize : 1f;
    }

    private static void AutoEvenBelow(SerializedProperty table, int editedIndex)
    {
        float lockedTotal = 0f;
        for (int i = 0; i < editedIndex; i++)
            lockedTotal += Mathf.Max(0f, table.GetArrayElementAtIndex(i).FindPropertyRelative("ChancePercent").floatValue);
        SerializedProperty edited = table.GetArrayElementAtIndex(editedIndex).FindPropertyRelative("ChancePercent");
        float available = Mathf.Max(0f, 100f - lockedTotal);
        if (editedIndex == table.arraySize - 1)
        {
            edited.floatValue = RoundChance(available);
            return;
        }
        edited.floatValue = RoundChance(Mathf.Clamp(edited.floatValue, 0f, available));
        DistributeChance(table, editedIndex + 1, Mathf.Max(0f, available - edited.floatValue));
    }

    private static void NormalizeAllChances(SerializedProperty table)
    {
        DistributeChance(table, 0, 100f);
    }

    private static void DistributeChance(SerializedProperty table, int startIndex, float targetTotal)
    {
        int count = table.arraySize - startIndex;
        if (count <= 0)
            return;
        int targetUnits = Mathf.Max(0, Mathf.RoundToInt(targetTotal * 100f));
        double totalWeight = 0d;
        double[] weights = new double[count];
        for (int i = 0; i < count; i++)
        {
            weights[i] = Math.Max(0d, table.GetArrayElementAtIndex(startIndex + i).FindPropertyRelative("ChancePercent").floatValue);
            totalWeight += weights[i];
        }
        if (totalWeight <= 0.000001d)
        {
            totalWeight = count;
            for (int i = 0; i < count; i++)
                weights[i] = 1d;
        }
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            int units = i == count - 1 ? targetUnits - used : Mathf.RoundToInt((float)(targetUnits * weights[i] / totalWeight));
            units = Mathf.Clamp(units, 0, targetUnits - used);
            table.GetArrayElementAtIndex(startIndex + i).FindPropertyRelative("ChancePercent").floatValue = units / 100f;
            used += units;
        }
    }

    private static float RoundChance(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }
}
#endif
