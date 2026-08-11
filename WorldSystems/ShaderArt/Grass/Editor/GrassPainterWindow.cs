using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GrassPainterWindow : EditorWindow
{
    private enum ToolTab
    {
        Paint,
        Sculpt,
        Style,
        Generate,
        Utilities
    }

    private enum PaintOperation
    {
        Add,
        Remove
    }

    private enum SculptOperation
    {
        SetSize,
        AddSize,
        SmoothSize,
        RandomizeSize,
        PaintColor
    }

    private const double RebuildDelay = 0.08d;
    private const int RaycastCapacity = 32;

    [SerializeField] private GrassComputeScript grassCompute;
    [SerializeField] private SO_GrassToolSettings toolSettings;
    [SerializeField] private ToolTab currentTab;
    [SerializeField] private PaintOperation paintOperation;
    [SerializeField] private SculptOperation sculptOperation;
    [SerializeField] private bool sceneBrushEnabled;
    [SerializeField] private bool sculptHeight = true;
    [SerializeField] private bool sculptWidth;
    [SerializeField] private bool showValidation = true;
    [SerializeField] private bool showTerrainLayers;
    [SerializeField] private Vector2 scrollPosition;

    private readonly RaycastHit[] raycastHits = new RaycastHit[RaycastCapacity];
    private bool strokeActive;
    private bool strokeChanged;
    private bool rebuildRequested;
    private bool fullRebuildRequested;
    private double rebuildRequestedAt;
    private double lastBrushTime;

    [MenuItem("Tools/Grass Tool 2.0")]
    private static void OpenWindow()
    {
        GrassPainterWindow window = GetWindow<GrassPainterWindow>(false, "Grass Tool 2.0", true);
        window.minSize = new Vector2(390f, 520f);
        Texture icon = EditorGUIUtility.FindTexture("tree_icon");
        window.titleContent = new GUIContent("Grass Tool 2.0", icon);
        window.Show();
    }

    [MenuItem("Tools/Grass Tool")]
    private static void OpenLegacyMenuPath()
    {
        OpenWindow();
    }

    private void OnEnable()
    {
        EnsureToolSettings();
        SceneView.duringSceneGui += DuringSceneGUI;
        Undo.undoRedoPerformed += HandleUndoRedo;
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        FinishStroke();
        SceneView.duringSceneGui -= DuringSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        EditorApplication.update -= EditorUpdate;
    }

    private void OnDestroy()
    {
        OnDisable();
    }

    private void OnGUI()
    {
        EnsureToolSettings();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawHeader();
        DrawSourcePanel();

        if (grassCompute == null)
        {
            EditorGUILayout.HelpBox(
                "Choose the exact GrassComputeScript you want to author. The tool never silently switches to another grass object.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawStatistics();
        DrawValidation();

        EditorGUILayout.Space(4f);
        currentTab = (ToolTab)GUILayout.Toolbar((int)currentTab, Enum.GetNames(typeof(ToolTab)), GUILayout.Height(28f));
        EditorGUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();
        switch (currentTab)
        {
            case ToolTab.Paint:
                DrawPaintPanel();
                break;
            case ToolTab.Sculpt:
                DrawSculptPanel();
                break;
            case ToolTab.Style:
                DrawStylePanel();
                break;
            case ToolTab.Generate:
                DrawGeneratePanel();
                break;
            case ToolTab.Utilities:
                DrawUtilitiesPanel();
                break;
        }

        if (EditorGUI.EndChangeCheck() && toolSettings != null)
        {
            toolSettings.EnsureValid();
            EditorUtility.SetDirty(toolSettings);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.HelpBox(
            "Prefab-safe grass authoring. Painted points are stored on the selected component in local space; Style edits the referenced Grass Settings asset directly.",
            MessageType.Info);
    }

    private void DrawSourcePanel()
    {
        EditorGUILayout.LabelField("Grass Source", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        GrassComputeScript selected = (GrassComputeScript)EditorGUILayout.ObjectField(
            "Grass Compute", grassCompute, typeof(GrassComputeScript), true);
        if (EditorGUI.EndChangeCheck())
            SetSource(selected);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Use Selection"))
            UseSelection();
        if (GUILayout.Button("Create Grass Child"))
            CreateGrassChild();
        using (new EditorGUI.DisabledScope(grassCompute == null))
        {
            if (GUILayout.Button("Manual Update"))
                RebuildNow(true);
        }
        EditorGUILayout.EndHorizontal();

        if (grassCompute == null)
            return;

        EditorGUI.BeginChangeCheck();
        SO_GrassSettings preset = (SO_GrassSettings)EditorGUILayout.ObjectField(
            "Grass Settings", grassCompute.currentPresets, typeof(SO_GrassSettings), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(grassCompute, "Assign Grass Settings");
            grassCompute.currentPresets = preset;
            MarkGrassDirty();
            RequestRebuild(true);
        }

        if (grassCompute.grassDataIsWorldSpace)
        {
            EditorGUILayout.HelpBox(
                "This source is marked World Space. Prefab-authored grass should normally be Local Space. Use the conversion utility only when the existing points truly contain world coordinates.",
                MessageType.Warning);
        }
    }

    private void DrawStatistics()
    {
        int pointCount = Data.Count;
        int bladesPerPoint = grassCompute.currentPresets != null
            ? Mathf.Max(1, grassCompute.currentPresets.allowedBladesPerVertex)
            : 0;
        int segments = grassCompute.currentPresets != null
            ? Mathf.Max(1, grassCompute.currentPresets.allowedSegmentsPerBlade)
            : 0;
        long blades = (long)pointCount * bladesPerPoint;
        long trianglesPerBlade = segments > 0 ? ((segments - 1) * 2L) + 1L : 0L;
        long triangles = blades * trianglesPerBlade;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Saved State",
            EditorUtility.IsDirty(grassCompute) ? "Unsaved grass changes" : "Saved");
        EditorGUILayout.LabelField("Painted Points", pointCount.ToString("N0"));
        EditorGUILayout.LabelField("Maximum Blades", blades.ToString("N0"));
        EditorGUILayout.LabelField("Maximum Triangles", triangles.ToString("N0"));
        EditorGUILayout.EndVertical();
    }

    private void DrawValidation()
    {
        showValidation = EditorGUILayout.Foldout(showValidation, "Setup Validation", true);
        if (!showValidation)
            return;

        SO_GrassSettings preset = grassCompute.currentPresets;
        if (preset == null)
        {
            EditorGUILayout.HelpBox("Assign a Grass Settings asset before rendering.", MessageType.Error);
            return;
        }

        if (preset.shaderToUse == null)
            EditorGUILayout.HelpBox("Grass Settings is missing GrassBlades.compute.", MessageType.Error);
        if (preset.materialToUse == null)
            EditorGUILayout.HelpBox("Grass Settings is missing its procedural grass material.", MessageType.Error);

        if (preset.materialToUse != null &&
            preset.materialToUse.HasProperty("_Blend") &&
            preset.materialToUse.GetFloat("_Blend") > 0.5f)
        {
            EditorGUILayout.HelpBox("Material Blend is enabled and can make the procedural grass invisible.", MessageType.Warning);
            if (GUILayout.Button("Disable Blend On Material"))
            {
                Undo.RecordObject(preset.materialToUse, "Disable Grass Material Blend");
                preset.materialToUse.SetFloat("_Blend", 0f);
                EditorUtility.SetDirty(preset.materialToUse);
                RequestRebuild(true);
            }
        }

        if (toolSettings.hitMask.value == 0)
            EditorGUILayout.HelpBox("Hit Mask is empty; the scene brush cannot find a surface.", MessageType.Warning);
        if (toolSettings.paintMask.value == 0)
            EditorGUILayout.HelpBox("Painting Mask is empty; Add cannot place points.", MessageType.Warning);

        Transform searchRoot = grassCompute.transform.root;
        if (searchRoot.GetComponentInChildren<Collider>(true) == null)
            EditorGUILayout.HelpBox("No Collider was found under this prefab root.", MessageType.Warning);

        if (preset.MaxWidth < preset.MinWidth || preset.MaxHeight < preset.MinHeight)
            EditorGUILayout.HelpBox("Grass Settings contains an invalid size range.", MessageType.Error);
    }

    private void DrawSceneBrushToggle()
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = sceneBrushEnabled ? new Color(0.45f, 0.9f, 0.55f) : previous;
        if (GUILayout.Button(sceneBrushEnabled ? "Scene Brush Enabled" : "Enable Scene Brush", GUILayout.Height(30f)))
        {
            sceneBrushEnabled = !sceneBrushEnabled;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = previous;
        EditorGUILayout.LabelField("Right-click and drag in Scene view. Alt + mouse remains available for camera navigation.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawPaintPanel()
    {
        DrawSceneBrushToggle();
        paintOperation = (PaintOperation)GUILayout.Toolbar((int)paintOperation, Enum.GetNames(typeof(PaintOperation)));

        DrawMaskFields();
        toolSettings.brushSize = EditorGUILayout.Slider("Brush Radius", toolSettings.brushSize, 0.05f, 50f);
        toolSettings.brushFalloffSize = EditorGUILayout.Slider("Soft Falloff", toolSettings.brushFalloffSize, 0f, 1f);
        toolSettings.normalLimit = EditorGUILayout.Slider("Slope Tolerance", toolSettings.normalLimit, 0f, 1f);
        EditorGUILayout.LabelField($"Minimum accepted upward normal: {(1f - toolSettings.normalLimit):0.00}", EditorStyles.miniLabel);

        if (paintOperation == PaintOperation.Remove)
            return;

        toolSettings.density = EditorGUILayout.Slider("Paint Density", toolSettings.density, 0.1f, 20f);
        toolSettings.pointSpacing = EditorGUILayout.Slider("Minimum Spacing", toolSettings.pointSpacing, 0.01f, 2f);

        DrawNewPointSizeFields();
        DrawColorFields();
    }

    private void DrawSculptPanel()
    {
        DrawSceneBrushToggle();
        sculptOperation = (SculptOperation)EditorGUILayout.EnumPopup("Operation", sculptOperation);
        toolSettings.brushSize = EditorGUILayout.Slider("Brush Radius", toolSettings.brushSize, 0.05f, 50f);
        toolSettings.brushFalloffSize = EditorGUILayout.Slider("Soft Falloff", toolSettings.brushFalloffSize, 0f, 1f);
        toolSettings.sculptStrength = EditorGUILayout.Slider("Strength", toolSettings.sculptStrength, 0.01f, 1f);

        SO_GrassSettings preset = grassCompute.currentPresets;
        float minWidth = preset != null ? preset.MinWidth : 0.001f;
        float maxWidth = preset != null ? preset.MaxWidth : 3f;
        float minHeight = preset != null ? preset.MinHeight : 0.001f;
        float maxHeight = preset != null ? preset.MaxHeight : 5f;

        switch (sculptOperation)
        {
            case SculptOperation.SetSize:
                sculptHeight = EditorGUILayout.ToggleLeft("Edit Height", sculptHeight);
                using (new EditorGUI.DisabledScope(!sculptHeight))
                    toolSettings.sculptTargetHeight = EditorGUILayout.Slider("Target Height", toolSettings.sculptTargetHeight, minHeight, maxHeight);
                sculptWidth = EditorGUILayout.ToggleLeft("Edit Width", sculptWidth);
                using (new EditorGUI.DisabledScope(!sculptWidth))
                    toolSettings.sculptTargetWidth = EditorGUILayout.Slider("Target Width", toolSettings.sculptTargetWidth, minWidth, maxWidth);
                EditorGUILayout.HelpBox("Affected points move toward these exact visible values. Stored sizes are immediately clamped to the Grass Settings range.", MessageType.Info);
                break;

            case SculptOperation.AddSize:
                sculptHeight = EditorGUILayout.ToggleLeft("Adjust Height", sculptHeight);
                using (new EditorGUI.DisabledScope(!sculptHeight))
                    toolSettings.sculptHeightPerSecond = EditorGUILayout.Slider("Height Change / Second", toolSettings.sculptHeightPerSecond, -2f, 2f);
                sculptWidth = EditorGUILayout.ToggleLeft("Adjust Width", sculptWidth);
                using (new EditorGUI.DisabledScope(!sculptWidth))
                    toolSettings.sculptWidthPerSecond = EditorGUILayout.Slider("Width Change / Second", toolSettings.sculptWidthPerSecond, -1f, 1f);
                EditorGUILayout.HelpBox("Positive values grow grass; negative values shrink it. Values stop at the visible preset limits—there is no hidden accumulated size.", MessageType.Info);
                break;

            case SculptOperation.SmoothSize:
                sculptHeight = EditorGUILayout.ToggleLeft("Smooth Height", sculptHeight);
                sculptWidth = EditorGUILayout.ToggleLeft("Smooth Width", sculptWidth);
                break;

            case SculptOperation.RandomizeSize:
                sculptHeight = EditorGUILayout.ToggleLeft("Randomize Height", sculptHeight);
                using (new EditorGUI.DisabledScope(!sculptHeight))
                    toolSettings.randomHeightAmount = EditorGUILayout.Slider("Height Variation", toolSettings.randomHeightAmount, 0f, 1f);
                sculptWidth = EditorGUILayout.ToggleLeft("Randomize Width", sculptWidth);
                using (new EditorGUI.DisabledScope(!sculptWidth))
                    toolSettings.randomWidthAmount = EditorGUILayout.Slider("Width Variation", toolSettings.randomWidthAmount, 0f, 1f);
                break;

            case SculptOperation.PaintColor:
                DrawColorFields();
                break;
        }
    }

    private void DrawStylePanel()
    {
        SO_GrassSettings preset = grassCompute.currentPresets;
        if (preset == null)
        {
            EditorGUILayout.HelpBox("Assign a Grass Settings asset above.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("Every field below edits the referenced Grass Settings asset directly and supports Undo.", MessageType.Info);

        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Resources", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "shaderToUse", "Compute Shader");
        DrawPresetProperty(serializedPreset, "materialToUse", "Grass Material");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Visible Size Range", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "MinWidth", "Minimum Width");
        DrawPresetProperty(serializedPreset, "MaxWidth", "Maximum Width");
        DrawPresetProperty(serializedPreset, "MinHeight", "Minimum Height");
        DrawPresetProperty(serializedPreset, "MaxHeight", "Maximum Height");
        DrawPresetProperty(serializedPreset, "grassRandomHeightMin", "Random Height Minimum");
        DrawPresetProperty(serializedPreset, "grassRandomHeightMax", "Random Height Maximum");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Blade Shape", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "allowedBladesPerVertex", "Blades Per Point");
        DrawPresetProperty(serializedPreset, "allowedSegmentsPerBlade", "Segments Per Blade");
        DrawPresetProperty(serializedPreset, "bladeRadius", "Blade Radius");
        DrawPresetProperty(serializedPreset, "bladeForwardAmount", "Forward Bend");
        DrawPresetProperty(serializedPreset, "bladeCurveAmount", "Curve");
        DrawPresetProperty(serializedPreset, "bottomWidth", "Base Width Multiplier");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "bottomTint", "Bottom Tint");
        DrawPresetProperty(serializedPreset, "topTint", "Top Tint");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Wind and Interaction", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "windSpeed", "Wind Speed");
        DrawPresetProperty(serializedPreset, "windStrength", "Wind Strength");
        DrawPresetProperty(serializedPreset, "affectStrength", "Interactor Strength");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("LOD and Rendering", EditorStyles.boldLabel);
        DrawPresetProperty(serializedPreset, "minFadeDistance", "Fade Start");
        DrawPresetProperty(serializedPreset, "maxDrawDistance", "Maximum Draw Distance");
        DrawPresetProperty(serializedPreset, "cullingTreeDepth", "Culling Tree Depth");
        DrawPresetProperty(serializedPreset, "castShadow", "Shadow Mode");
        DrawPresetProperty(serializedPreset, "drawBounds", "Show Culling Bounds");
        DrawPresetProperty(serializedPreset, "cuttingParticles", "Cutting Particles");

        if (EditorGUI.EndChangeCheck())
        {
            serializedPreset.ApplyModifiedProperties();
            NormalizePreset(preset);
            EditorUtility.SetDirty(preset);
            if (Data.Count > 0)
                Undo.RecordObject(grassCompute, "Clamp Grass To Preset");
            ClampAllPointSizesToPreset();
            MarkGrassDirty();
            RequestRebuild(true);
        }
        else
        {
            serializedPreset.ApplyModifiedProperties();
        }
    }

    private void DrawGeneratePanel()
    {
        EditorGUILayout.HelpBox("Select one or more MeshFilter or Terrain objects. Generated points are stored in the chosen Grass Compute object's local space.", MessageType.Info);
        DrawMaskFields();
        toolSettings.generationDensity = EditorGUILayout.FloatField("Points Per Square Unit", toolSettings.generationDensity);
        toolSettings.grassAmountToGenerate = EditorGUILayout.IntField("Maximum Added Points", toolSettings.grassAmountToGenerate);
        toolSettings.normalLimit = EditorGUILayout.Slider("Slope Tolerance", toolSettings.normalLimit, 0f, 1f);
        DrawNewPointSizeFields();
        DrawColorFields();

        showTerrainLayers = EditorGUILayout.Foldout(showTerrainLayers, "Terrain / Vertex Colour Filters", true);
        if (showTerrainLayers)
        {
            toolSettings.VertexColorSettings = (SO_GrassToolSettings.VertexColorSetting)EditorGUILayout.EnumPopup("Block Vertex Channel", toolSettings.VertexColorSettings);
            toolSettings.VertexFade = (SO_GrassToolSettings.VertexColorSetting)EditorGUILayout.EnumPopup("Fade Vertex Channel", toolSettings.VertexFade);

            for (int i = 0; i < toolSettings.layerBlocking.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                toolSettings.layerBlocking[i] = EditorGUILayout.Slider($"Terrain Layer {i}", toolSettings.layerBlocking[i], 0f, 1f);
                toolSettings.layerFading[i] = EditorGUILayout.ToggleLeft("Fade", toolSettings.layerFading[i], GUILayout.Width(55f));
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add From Selection", GUILayout.Height(28f)))
            GenerateFromSelection(false);
        if (GUILayout.Button("Replace From Selection", GUILayout.Height(28f)))
            GenerateFromSelection(true);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawUtilitiesPanel()
    {
        EditorGUILayout.LabelField("Whole Source Operations", EditorStyles.boldLabel);
        DrawNewPointSizeFields();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set All Sizes"))
            SetAllSizes();
        if (GUILayout.Button("Clamp Sizes To Preset"))
        {
            Undo.RegisterCompleteObjectUndo(grassCompute, "Clamp Grass Sizes");
            ClampAllPointSizesToPreset();
            CommitDataChange(true);
        }
        EditorGUILayout.EndHorizontal();

        DrawColorFields();
        if (GUILayout.Button("Set All Colours"))
            SetAllColors();

        EditorGUILayout.Space(6f);
        DrawMaskFields();
        toolSettings.reprojectOffset = EditorGUILayout.FloatField("Reproject Height", toolSettings.reprojectOffset);
        if (GUILayout.Button("Reproject All To Painting Mask"))
            ReprojectAll();

        if (GUILayout.Button("Remove Points On Steep / Vertical Surfaces"))
            RemoveInvalidNormals();

        EditorGUILayout.Space(6f);
        if (grassCompute.grassDataIsWorldSpace)
        {
            if (GUILayout.Button("Convert World Data To Local Space"))
                ConvertWorldDataToLocal();
        }
        else
        {
            EditorGUILayout.HelpBox("Data is stored in prefab-local space and will follow moved, rotated and generated instances.", MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
        if (GUILayout.Button("Clear All Grass", GUILayout.Height(28f)) &&
            EditorUtility.DisplayDialog("Clear All Grass?", "This removes every painted point from the selected Grass Compute object.", "Clear", "Cancel"))
        {
            Undo.RegisterCompleteObjectUndo(grassCompute, "Clear Grass");
            Data.Clear();
            CommitDataChange(true);
        }
        GUI.backgroundColor = previous;
    }

    private void DrawMaskFields()
    {
        LayerMask hitMask = EditorGUILayout.MaskField(
            "Hit Mask",
            InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.hitMask),
            InternalEditorUtility.layers);
        toolSettings.hitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(hitMask);

        LayerMask paintMask = EditorGUILayout.MaskField(
            "Painting Mask",
            InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintMask),
            InternalEditorUtility.layers);
        toolSettings.paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintMask);

        LayerMask blockMask = EditorGUILayout.MaskField(
            "Blocking Mask",
            InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintBlockMask),
            InternalEditorUtility.layers);
        toolSettings.paintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(blockMask);
    }

    private void DrawNewPointSizeFields()
    {
        SO_GrassSettings preset = grassCompute.currentPresets;
        if (preset != null)
        {
            toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth, preset.MinWidth, preset.MaxWidth);
            toolSettings.sizeLength = EditorGUILayout.Slider("Grass Height", toolSettings.sizeLength, preset.MinHeight, preset.MaxHeight);
            EditorGUILayout.LabelField(
                $"Visible limits — Width {preset.MinWidth:0.###}–{preset.MaxWidth:0.###}, Height {preset.MinHeight:0.###}–{preset.MaxHeight:0.###}",
                EditorStyles.miniLabel);
        }
        else
        {
            toolSettings.sizeWidth = Mathf.Max(0.001f, EditorGUILayout.FloatField("Grass Width", toolSettings.sizeWidth));
            toolSettings.sizeLength = Mathf.Max(0.001f, EditorGUILayout.FloatField("Grass Height", toolSettings.sizeLength));
        }
    }

    private void DrawColorFields()
    {
        toolSettings.AdjustedColor = EditorGUILayout.ColorField("Painted Colour", toolSettings.AdjustedColor);
        toolSettings.rangeR = EditorGUILayout.Slider("Red Variation", toolSettings.rangeR, 0f, 1f);
        toolSettings.rangeG = EditorGUILayout.Slider("Green Variation", toolSettings.rangeG, 0f, 1f);
        toolSettings.rangeB = EditorGUILayout.Slider("Blue Variation", toolSettings.rangeB, 0f, 1f);
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (!sceneBrushEnabled || grassCompute == null ||
            (currentTab != ToolTab.Paint && currentTab != ToolTab.Sculpt))
            return;

        Event current = Event.current;
        if (current == null)
            return;

        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Ray mouseRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        bool hasHit = TryGetSurfaceHit(mouseRay, out RaycastHit hit, false);
        if (hasHit)
            DrawBrushHandle(hit);

        if (current.alt)
            return;

        bool rightDown = current.type == EventType.MouseDown && current.button == 1;
        bool rightDrag = current.type == EventType.MouseDrag && current.button == 1;
        bool rightUp = current.type == EventType.MouseUp && current.button == 1;

        if (rightDown && hasHit)
        {
            BeginStroke();
            ApplyBrush(hit);
            current.Use();
        }
        else if (rightDrag && strokeActive && hasHit)
        {
            ApplyBrush(hit);
            current.Use();
        }
        else if (rightUp && strokeActive)
        {
            FinishStroke();
            current.Use();
        }

        if (current.type == EventType.MouseMove)
            sceneView.Repaint();
    }

    private void DrawBrushHandle(RaycastHit hit)
    {
        bool validPaintLayer = IsLayerInMask(hit.collider.gameObject.layer, toolSettings.paintMask);
        bool validSlope = IsValidSlope(hit.normal);
        Color color;

        if (!validPaintLayer || !validSlope)
            color = Color.red;
        else if (currentTab == ToolTab.Paint && paintOperation == PaintOperation.Remove)
            color = new Color(1f, 0.25f, 0.2f, 1f);
        else if (currentTab == ToolTab.Sculpt)
            color = new Color(1f, 0.75f, 0.15f, 1f);
        else
            color = new Color(0.2f, 1f, 0.35f, 1f);

        Handles.color = color;
        Handles.DrawWireDisc(hit.point, hit.normal, toolSettings.brushSize);
        Color fill = color;
        fill.a = 0.12f;
        Handles.color = fill;
        Handles.DrawSolidDisc(hit.point, hit.normal, toolSettings.brushSize);
    }

    private void BeginStroke()
    {
        if (strokeActive)
            return;

        strokeActive = true;
        strokeChanged = false;
        lastBrushTime = EditorApplication.timeSinceStartup;
        Undo.RegisterCompleteObjectUndo(grassCompute, "Edit Grass");
    }

    private void FinishStroke()
    {
        if (!strokeActive)
            return;

        strokeActive = false;
        if (strokeChanged)
            CommitDataChange(true);
        strokeChanged = false;
    }

    private void ApplyBrush(RaycastHit hit)
    {
        if (currentTab == ToolTab.Paint)
        {
            if (paintOperation == PaintOperation.Add)
                AddAtHit(hit);
            else
                RemoveAtHit(hit.point);
        }
        else
        {
            SculptAtHit(hit.point);
        }
    }

    private void AddAtHit(RaycastHit centerHit)
    {
        if (!IsLayerInMask(centerHit.collider.gameObject.layer, toolSettings.paintMask) || !IsValidSlope(centerHit.normal))
            return;

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Clamp((float)(now - lastBrushTime), 1f / 120f, 0.1f);
        lastBrushTime = now;
        int attempts = Mathf.Clamp(Mathf.CeilToInt(toolSettings.density * toolSettings.brushSize * deltaTime * 12f), 1, 128);

        Vector3 normal = centerHit.normal.normalized;
        Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up).normalized;
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        for (int i = 0; i < attempts; i++)
        {
            Vector2 disc = UnityEngine.Random.insideUnitCircle * toolSettings.brushSize;
            Vector3 sampleCenter = centerHit.point + tangent * disc.x + bitangent * disc.y;
            Ray sampleRay = new Ray(sampleCenter + normal * (toolSettings.brushSize + 2f), -normal);

            if (!TryGetSurfaceHit(sampleRay, out RaycastHit sampleHit, true) || !IsValidSlope(sampleHit.normal))
                continue;
            if (!IsLayerInMask(sampleHit.collider.gameObject.layer, toolSettings.paintMask))
                continue;
            if (Vector3.Distance(sampleHit.point, centerHit.point) > toolSettings.brushSize * 1.2f)
                continue;
            if (IsBlocked(sampleHit.point))
                continue;
            if (!HasMinimumSpacing(sampleHit.point, toolSettings.pointSpacing))
                continue;

            Data.Add(CreateGrassData(sampleHit.point, sampleHit.normal, 1f));
            strokeChanged = true;
        }

        if (strokeChanged)
        {
            MarkGrassDirty();
            RequestRebuild(false);
        }
    }

    private void RemoveAtHit(Vector3 worldPoint)
    {
        float radiusSquared = toolSettings.brushSize * toolSettings.brushSize;
        bool removed = false;

        for (int i = Data.Count - 1; i >= 0; i--)
        {
            Vector3 point = GetWorldPosition(Data[i]);
            if ((point - worldPoint).sqrMagnitude > radiusSquared)
                continue;

            Data.RemoveAt(i);
            removed = true;
        }

        if (!removed)
            return;

        strokeChanged = true;
        MarkGrassDirty();
        RequestRebuild(false);
    }

    private void SculptAtHit(Vector3 worldPoint)
    {
        List<int> affected = new List<int>();
        float radius = Mathf.Max(0.001f, toolSettings.brushSize);
        float radiusSquared = radius * radius;

        for (int i = 0; i < Data.Count; i++)
        {
            if ((GetWorldPosition(Data[i]) - worldPoint).sqrMagnitude <= radiusSquared)
                affected.Add(i);
        }

        if (affected.Count == 0)
            return;

        Vector2 averageSize = Vector2.zero;
        for (int i = 0; i < affected.Count; i++)
            averageSize += Data[affected[i]].length;
        averageSize /= affected.Count;

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Clamp((float)(now - lastBrushTime), 1f / 120f, 0.1f);
        lastBrushTime = now;

        for (int i = 0; i < affected.Count; i++)
        {
            int index = affected[i];
            GrassData point = Data[index];
            float distance = Vector3.Distance(GetWorldPosition(point), worldPoint);
            float falloff = GetBrushFalloff(distance, radius);
            float blend = Mathf.Clamp01(toolSettings.sculptStrength * falloff);

            switch (sculptOperation)
            {
                case SculptOperation.SetSize:
                    if (sculptWidth)
                        point.length.x = Mathf.Lerp(point.length.x, toolSettings.sculptTargetWidth, blend);
                    if (sculptHeight)
                        point.length.y = Mathf.Lerp(point.length.y, toolSettings.sculptTargetHeight, blend);
                    break;

                case SculptOperation.AddSize:
                    if (sculptWidth)
                        point.length.x += toolSettings.sculptWidthPerSecond * deltaTime * blend;
                    if (sculptHeight)
                        point.length.y += toolSettings.sculptHeightPerSecond * deltaTime * blend;
                    break;

                case SculptOperation.SmoothSize:
                    if (sculptWidth)
                        point.length.x = Mathf.Lerp(point.length.x, averageSize.x, blend);
                    if (sculptHeight)
                        point.length.y = Mathf.Lerp(point.length.y, averageSize.y, blend);
                    break;

                case SculptOperation.RandomizeSize:
                    if (sculptWidth)
                    {
                        float target = point.length.x * (1f + UnityEngine.Random.Range(-toolSettings.randomWidthAmount, toolSettings.randomWidthAmount));
                        point.length.x = Mathf.Lerp(point.length.x, target, blend);
                    }
                    if (sculptHeight)
                    {
                        float target = point.length.y * (1f + UnityEngine.Random.Range(-toolSettings.randomHeightAmount, toolSettings.randomHeightAmount));
                        point.length.y = Mathf.Lerp(point.length.y, target, blend);
                    }
                    break;

                case SculptOperation.PaintColor:
                    Vector3 targetColor = GetRandomColor();
                    point.color = Vector3.Lerp(point.color, targetColor, blend);
                    break;
            }

            ClampSize(ref point);
            Data[index] = point;
        }

        strokeChanged = true;
        MarkGrassDirty();
        RequestRebuild(false);
    }

    private float GetBrushFalloff(float distance, float radius)
    {
        float softPortion = Mathf.Clamp01(toolSettings.brushFalloffSize);
        if (softPortion <= 0f)
            return 1f;

        float softStart = radius * (1f - softPortion);
        return 1f - Mathf.InverseLerp(softStart, radius, distance);
    }

    private bool TryGetSurfaceHit(Ray ray, out RaycastHit bestHit, bool requirePaintLayer)
    {
        int count = Physics.RaycastNonAlloc(ray, raycastHits, 500f, toolSettings.hitMask.value, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        bestHit = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = raycastHits[i];
            if (candidate.collider == null)
                continue;
            if (requirePaintLayer && !IsLayerInMask(candidate.collider.gameObject.layer, toolSettings.paintMask))
                continue;
            if (candidate.distance >= bestDistance)
                continue;

            bestDistance = candidate.distance;
            bestHit = candidate;
            found = true;
        }

        return found;
    }

    private bool IsBlocked(Vector3 worldPoint)
    {
        if (toolSettings.paintBlockMask.value == 0)
            return false;

        return Physics.CheckSphere(
            worldPoint,
            Mathf.Max(0.02f, toolSettings.pointSpacing * 0.25f),
            toolSettings.paintBlockMask.value,
            QueryTriggerInteraction.Ignore);
    }

    private bool HasMinimumSpacing(Vector3 worldPoint, float spacing)
    {
        float spacingSquared = spacing * spacing;
        for (int i = 0; i < Data.Count; i++)
        {
            if ((GetWorldPosition(Data[i]) - worldPoint).sqrMagnitude < spacingSquared)
                return false;
        }
        return true;
    }

    private GrassData CreateGrassData(Vector3 worldPosition, Vector3 worldNormal, float sizeMultiplier)
    {
        GrassData point = new GrassData
        {
            position = grassCompute.grassDataIsWorldSpace
                ? worldPosition
                : grassCompute.transform.InverseTransformPoint(worldPosition),
            normal = grassCompute.grassDataIsWorldSpace
                ? worldNormal.normalized
                : WorldNormalToLocal(worldNormal),
            length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeLength) * sizeMultiplier,
            color = GetRandomColor()
        };
        ClampSize(ref point);
        return point;
    }

    private Vector3 GetWorldPosition(GrassData point)
    {
        return grassCompute.grassDataIsWorldSpace
            ? point.position
            : grassCompute.transform.TransformPoint(point.position);
    }

    private Vector3 GetWorldNormal(GrassData point)
    {
        if (grassCompute.grassDataIsWorldSpace)
            return point.normal.normalized;

        Matrix4x4 normalMatrix = grassCompute.transform.localToWorldMatrix.inverse.transpose;
        Vector3 normal = normalMatrix.MultiplyVector(point.normal);
        return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
    }

    private Vector3 WorldNormalToLocal(Vector3 worldNormal)
    {
        Matrix4x4 worldToLocalNormal = grassCompute.transform.localToWorldMatrix.transpose;
        Vector3 local = worldToLocalNormal.MultiplyVector(worldNormal);
        return local.sqrMagnitude > 0.000001f ? local.normalized : Vector3.up;
    }

    private bool IsValidSlope(Vector3 worldNormal)
    {
        return worldNormal.normalized.y >= 1f - Mathf.Clamp01(toolSettings.normalLimit);
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private Vector3 GetRandomColor()
    {
        Color source = toolSettings.AdjustedColor;
        return new Vector3(
            Mathf.Clamp01(source.r + UnityEngine.Random.Range(-toolSettings.rangeR, toolSettings.rangeR)),
            Mathf.Clamp01(source.g + UnityEngine.Random.Range(-toolSettings.rangeG, toolSettings.rangeG)),
            Mathf.Clamp01(source.b + UnityEngine.Random.Range(-toolSettings.rangeB, toolSettings.rangeB)));
    }

    private void ClampSize(ref GrassData point)
    {
        SO_GrassSettings preset = grassCompute.currentPresets;
        if (preset == null)
        {
            point.length.x = Mathf.Max(0.001f, point.length.x);
            point.length.y = Mathf.Max(0.001f, point.length.y);
            return;
        }

        point.length.x = Mathf.Clamp(point.length.x, preset.MinWidth, preset.MaxWidth);
        point.length.y = Mathf.Clamp(point.length.y, preset.MinHeight, preset.MaxHeight);
    }

    private void ClampAllPointSizesToPreset()
    {
        for (int i = 0; i < Data.Count; i++)
        {
            GrassData point = Data[i];
            ClampSize(ref point);
            Data[i] = point;
        }

        SO_GrassSettings preset = grassCompute.currentPresets;
        if (preset != null)
        {
            toolSettings.sizeWidth = Mathf.Clamp(toolSettings.sizeWidth, preset.MinWidth, preset.MaxWidth);
            toolSettings.sizeLength = Mathf.Clamp(toolSettings.sizeLength, preset.MinHeight, preset.MaxHeight);
            toolSettings.sculptTargetWidth = Mathf.Clamp(toolSettings.sculptTargetWidth, preset.MinWidth, preset.MaxWidth);
            toolSettings.sculptTargetHeight = Mathf.Clamp(toolSettings.sculptTargetHeight, preset.MinHeight, preset.MaxHeight);
        }
    }

    private void GenerateFromSelection(bool replace)
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Generate Grass", "Select at least one MeshFilter or Terrain object.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(grassCompute, replace ? "Replace Generated Grass" : "Generate Grass");
        List<GrassData> previousData = replace ? new List<GrassData>(Data) : null;
        if (replace)
            Data.Clear();

        int startingCount = Data.Count;
        int remaining = Mathf.Max(0, toolSettings.grassAmountToGenerate);

        for (int i = 0; i < selectedObjects.Length && remaining > 0; i++)
        {
            GameObject selected = selectedObjects[i];
            MeshFilter meshFilter = selected.GetComponent<MeshFilter>();
            Terrain terrain = selected.GetComponent<Terrain>();

            int added = 0;
            if (meshFilter != null && meshFilter.sharedMesh != null)
                added = GenerateOnMesh(meshFilter, remaining);
            else if (terrain != null && terrain.terrainData != null)
                added = GenerateOnTerrain(terrain, remaining);

            remaining -= added;
        }

        if (Data.Count == startingCount)
        {
            if (replace)
            {
                Data.Clear();
                Data.AddRange(previousData);
            }
            EditorUtility.DisplayDialog("Generate Grass", "No valid points were generated. Check selection, masks and slope tolerance.", "OK");
            return;
        }

        CommitDataChange(true);
    }

    private int GenerateOnMesh(MeshFilter meshFilter, int maximum)
    {
        Mesh mesh = meshFilter.sharedMesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Color[] colors = mesh.colors;
        int triangleCount = triangles.Length / 3;
        if (triangleCount == 0 || vertices.Length == 0)
            return 0;

        float[] cumulativeAreas = new float[triangleCount];
        float totalArea = 0f;
        for (int i = 0; i < triangleCount; i++)
        {
            Vector3 a = vertices[triangles[i * 3]];
            Vector3 b = vertices[triangles[i * 3 + 1]];
            Vector3 c = vertices[triangles[i * 3 + 2]];
            Vector3 worldA = meshFilter.transform.TransformPoint(a);
            Vector3 worldB = meshFilter.transform.TransformPoint(b);
            Vector3 worldC = meshFilter.transform.TransformPoint(c);
            totalArea += Vector3.Cross(worldB - worldA, worldC - worldA).magnitude * 0.5f;
            cumulativeAreas[i] = totalArea;
        }

        int requested = Mathf.Min(maximum, Mathf.CeilToInt(totalArea * Mathf.Max(0.001f, toolSettings.generationDensity)));
        int added = 0;

        for (int i = 0; i < requested; i++)
        {
            float sample = UnityEngine.Random.value * totalArea;
            int triangleIndex = Array.BinarySearch(cumulativeAreas, sample);
            if (triangleIndex < 0)
                triangleIndex = ~triangleIndex;
            triangleIndex = Mathf.Clamp(triangleIndex, 0, triangleCount - 1);

            int ia = triangles[triangleIndex * 3];
            int ib = triangles[triangleIndex * 3 + 1];
            int ic = triangles[triangleIndex * 3 + 2];
            float r1 = Mathf.Sqrt(UnityEngine.Random.value);
            float r2 = UnityEngine.Random.value;
            float wa = 1f - r1;
            float wb = r1 * (1f - r2);
            float wc = r1 * r2;

            Vector3 localPosition = vertices[ia] * wa + vertices[ib] * wb + vertices[ic] * wc;
            Vector3 localNormal;
            if (normals != null && normals.Length == vertices.Length)
                localNormal = (normals[ia] * wa + normals[ib] * wb + normals[ic] * wc).normalized;
            else
                localNormal = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]).normalized;

            Vector3 worldPosition = meshFilter.transform.TransformPoint(localPosition);
            Matrix4x4 normalMatrix = meshFilter.transform.localToWorldMatrix.inverse.transpose;
            Vector3 worldNormal = normalMatrix.MultiplyVector(localNormal).normalized;

            if (!IsValidSlope(worldNormal) || IsBlocked(worldPosition))
                continue;
            if (!IsLayerInMask(meshFilter.gameObject.layer, toolSettings.paintMask))
                continue;

            Color vertexColor = colors != null && colors.Length == vertices.Length
                ? colors[ia] * wa + colors[ib] * wb + colors[ic] * wc
                : Color.black;
            if (GetVertexChannel(vertexColor, toolSettings.VertexColorSettings) > 0.5f)
                continue;

            float sizeMultiplier = 1f - GetVertexChannel(vertexColor, toolSettings.VertexFade);
            Data.Add(CreateGrassData(worldPosition, worldNormal, Mathf.Clamp01(sizeMultiplier)));
            added++;
        }

        return added;
    }

    private int GenerateOnTerrain(Terrain terrain, int maximum)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 size = terrainData.size;
        float area = size.x * size.z;
        int requested = Mathf.Min(maximum, Mathf.CeilToInt(area * Mathf.Max(0.001f, toolSettings.generationDensity)));
        int added = 0;

        for (int i = 0; i < requested; i++)
        {
            float normalizedX = UnityEngine.Random.value;
            float normalizedZ = UnityEngine.Random.value;
            Vector3 worldPosition = terrain.transform.position + new Vector3(normalizedX * size.x, 0f, normalizedZ * size.z);
            worldPosition.y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
            Vector3 worldNormal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ).normalized;

            if (!IsValidSlope(worldNormal) || IsBlocked(worldPosition))
                continue;
            if (!IsLayerInMask(terrain.gameObject.layer, toolSettings.paintMask))
                continue;

            float sizeMultiplier = GetTerrainSizeMultiplier(terrainData, normalizedX, normalizedZ, out bool blocked);
            if (blocked)
                continue;

            Data.Add(CreateGrassData(worldPosition, worldNormal, sizeMultiplier));
            added++;
        }

        return added;
    }

    private float GetTerrainSizeMultiplier(TerrainData terrainData, float normalizedX, float normalizedZ, out bool blocked)
    {
        blocked = false;
        if (terrainData.alphamapLayers == 0)
            return 1f;

        int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * terrainData.alphamapWidth), 0, terrainData.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * terrainData.alphamapHeight), 0, terrainData.alphamapHeight - 1);
        float[,,] maps = terrainData.GetAlphamaps(x, z, 1, 1);
        float fade = 0f;

        int layers = Mathf.Min(maps.GetLength(2), toolSettings.layerBlocking.Length);
        for (int i = 0; i < layers; i++)
        {
            float weight = maps[0, 0, i];
            if (weight > toolSettings.layerBlocking[i])
                blocked = true;
            if (toolSettings.layerFading[i])
                fade += weight;
        }

        return Mathf.Clamp01(1f - fade);
    }

    private static float GetVertexChannel(Color color, SO_GrassToolSettings.VertexColorSetting channel)
    {
        switch (channel)
        {
            case SO_GrassToolSettings.VertexColorSetting.Red: return color.r;
            case SO_GrassToolSettings.VertexColorSetting.Green: return color.g;
            case SO_GrassToolSettings.VertexColorSetting.Blue: return color.b;
            default: return 0f;
        }
    }

    private void SetAllSizes()
    {
        Undo.RegisterCompleteObjectUndo(grassCompute, "Set All Grass Sizes");
        for (int i = 0; i < Data.Count; i++)
        {
            GrassData point = Data[i];
            point.length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeLength);
            ClampSize(ref point);
            Data[i] = point;
        }
        CommitDataChange(true);
    }

    private void SetAllColors()
    {
        Undo.RegisterCompleteObjectUndo(grassCompute, "Set All Grass Colours");
        for (int i = 0; i < Data.Count; i++)
        {
            GrassData point = Data[i];
            point.color = GetRandomColor();
            Data[i] = point;
        }
        CommitDataChange(true);
    }

    private void ReprojectAll()
    {
        Undo.RegisterCompleteObjectUndo(grassCompute, "Reproject Grass");
        bool changed = false;
        float offset = Mathf.Max(0.01f, toolSettings.reprojectOffset);

        for (int i = 0; i < Data.Count; i++)
        {
            GrassData point = Data[i];
            Vector3 worldPosition = GetWorldPosition(point);
            Ray ray = new Ray(worldPosition + Vector3.up * offset, Vector3.down);
            if (!TryGetSurfaceHit(ray, out RaycastHit hit, true) || !IsValidSlope(hit.normal))
                continue;

            point.position = grassCompute.grassDataIsWorldSpace
                ? hit.point
                : grassCompute.transform.InverseTransformPoint(hit.point);
            point.normal = grassCompute.grassDataIsWorldSpace
                ? hit.normal.normalized
                : WorldNormalToLocal(hit.normal);
            Data[i] = point;
            changed = true;
        }

        if (changed)
            CommitDataChange(true);
    }

    private void RemoveInvalidNormals()
    {
        Undo.RegisterCompleteObjectUndo(grassCompute, "Remove Invalid Grass Normals");
        int removed = Data.RemoveAll(point => !IsValidSlope(GetWorldNormal(point)));
        if (removed > 0)
            CommitDataChange(true);
    }

    private void ConvertWorldDataToLocal()
    {
        if (!EditorUtility.DisplayDialog(
                "Convert World Data To Local?",
                "Use this only when the stored positions are truly world-space legacy data. The operation supports Undo.",
                "Convert",
                "Cancel"))
            return;

        Undo.RegisterCompleteObjectUndo(grassCompute, "Convert Grass To Local Space");
        for (int i = 0; i < Data.Count; i++)
        {
            GrassData point = Data[i];
            point.position = grassCompute.transform.InverseTransformPoint(point.position);
            point.normal = WorldNormalToLocal(point.normal);
            Data[i] = point;
        }
        grassCompute.grassDataIsWorldSpace = false;
        CommitDataChange(true);
    }

    private void SetSource(GrassComputeScript source)
    {
        FinishStroke();
        grassCompute = source;
        if (grassCompute != null && grassCompute.SetGrassPaintedDataList == null)
            grassCompute.SetGrassPaintedDataList = new List<GrassData>();
        Repaint();
        SceneView.RepaintAll();
    }

    private void UseSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Grass Tool", "Select a Grass object or a prefab containing one.", "OK");
            return;
        }

        GrassComputeScript source = selected.GetComponent<GrassComputeScript>();
        if (source == null)
            source = selected.GetComponentInChildren<GrassComputeScript>(true);
        if (source == null)
            source = selected.GetComponentInParent<GrassComputeScript>();

        if (source == null)
            EditorUtility.DisplayDialog("Grass Tool", "No GrassComputeScript was found on the selection.", "OK");
        else
            SetSource(source);
    }

    private void CreateGrassChild()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Grass Tool", "Select the prefab or scene root that should own the Grass child.", "OK");
            return;
        }

        GameObject child = new GameObject("Grass");
        Undo.RegisterCreatedObjectUndo(child, "Create Grass Child");
        child.transform.SetParent(selected.transform, false);
        GrassComputeScript source = Undo.AddComponent<GrassComputeScript>(child);
        source.grassDataIsWorldSpace = false;
        SetSource(source);
        Selection.activeGameObject = child;
        MarkGrassDirty();
    }

    private List<GrassData> Data
    {
        get
        {
            if (grassCompute.SetGrassPaintedDataList == null)
                grassCompute.SetGrassPaintedDataList = new List<GrassData>();
            return grassCompute.SetGrassPaintedDataList;
        }
    }

    private void CommitDataChange(bool fullRebuild)
    {
        MarkGrassDirty();
        RebuildNow(fullRebuild);
    }

    private void MarkGrassDirty()
    {
        if (grassCompute == null)
            return;

        EditorUtility.SetDirty(grassCompute);
        PrefabUtility.RecordPrefabInstancePropertyModifications(grassCompute);
        Scene scene = grassCompute.gameObject.scene;
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private void RequestRebuild(bool full)
    {
        rebuildRequested = true;
        fullRebuildRequested |= full;
        rebuildRequestedAt = EditorApplication.timeSinceStartup;
    }

    private void EditorUpdate()
    {
        if (!rebuildRequested || EditorApplication.timeSinceStartup - rebuildRequestedAt < RebuildDelay)
            return;

        bool full = fullRebuildRequested;
        rebuildRequested = false;
        fullRebuildRequested = false;
        RebuildNow(full);
    }

    private void RebuildNow(bool full)
    {
        if (grassCompute == null)
            return;

        if (full)
            grassCompute.Reset();
        else
            grassCompute.ResetFaster();

        SceneView.RepaintAll();
        Repaint();
    }

    private void HandleUndoRedo()
    {
        if (grassCompute != null)
            RebuildNow(true);
    }

    private void EnsureToolSettings()
    {
        if (toolSettings == null)
            toolSettings = FindOrCreateToolSettings();

        if (toolSettings != null)
            toolSettings.EnsureValid();
    }

    private static SO_GrassToolSettings FindOrCreateToolSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:SO_GrassToolSettings");
        SO_GrassToolSettings fallback = null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SO_GrassToolSettings candidate = AssetDatabase.LoadAssetAtPath<SO_GrassToolSettings>(path);
            if (candidate == null)
                continue;

            if (string.Equals(Path.GetFileNameWithoutExtension(path), "grassToolSettings", StringComparison.OrdinalIgnoreCase))
                return candidate;
            if (fallback == null)
                fallback = candidate;
        }

        if (fallback != null)
            return fallback;

        SO_GrassToolSettings created = CreateInstance<SO_GrassToolSettings>();
        created.CreateNewLayers();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/grassToolSettings.asset");
        AssetDatabase.CreateAsset(created, assetPath);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static void DrawPresetProperty(SerializedObject serializedPreset, string propertyName, string label)
    {
        SerializedProperty property = serializedPreset.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private static void NormalizePreset(SO_GrassSettings preset)
    {
        preset.MinWidth = Mathf.Max(0.001f, preset.MinWidth);
        preset.MinHeight = Mathf.Max(0.001f, preset.MinHeight);
        preset.MaxWidth = Mathf.Max(preset.MinWidth, preset.MaxWidth);
        preset.MaxHeight = Mathf.Max(preset.MinHeight, preset.MaxHeight);
        preset.allowedBladesPerVertex = Mathf.Clamp(preset.allowedBladesPerVertex, 1, 8);
        preset.allowedSegmentsPerBlade = Mathf.Clamp(preset.allowedSegmentsPerBlade, 1, 4);
        preset.cullingTreeDepth = Mathf.Max(1, preset.cullingTreeDepth);
        preset.minFadeDistance = Mathf.Max(0f, preset.minFadeDistance);
        preset.maxDrawDistance = Mathf.Max(preset.minFadeDistance + 0.01f, preset.maxDrawDistance);
    }
}
