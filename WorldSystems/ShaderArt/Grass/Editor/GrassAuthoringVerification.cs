using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class GrassAuthoringVerification
{
    const string Request = "Library/GrassAuthoringVerification.request";
    const string Report = "Library/GrassAuthoringVerification.txt";
    const string Forest = "Assets/GameSystems/WorldSystems/ShaderArt/Grass/copyToAssetsSettings/Forest Grass Settings.asset";
    const string TestPrefab = "Assets/__GrassAuthoringVerification.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static GrassAuthoringVerification() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "verify") Verify();
            else if (command == "preview") Preview();
            else if (command == "close-preview") ClosePreview();
            else throw new Exception("Unknown verification command: " + command);
        }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); }
    }

    static void Require(bool condition, string label, StringBuilder report)
    {
        if (!condition) throw new Exception("FAIL: " + label + "\n" + report);
        report.AppendLine("PASS: " + label);
    }
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    static void Field(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static void Operation(GrassPainterWindow window, string name)
    {
        var field = typeof(GrassPainterWindow).GetField("sculptOperation", Private);
        field.SetValue(window, Enum.Parse(field.FieldType, name));
    }

    [MenuItem("Tools/Grass Verification/Verify Prefab Brushes")]
    internal static void Verify()
    {
        if (Application.isPlaying) throw new Exception("Run verification outside Play Mode.");
        if (File.Exists(TestPrefab)) throw new Exception("Verification prefab path already exists; leaving it untouched.");
        var report = new StringBuilder();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Grass verification");
        SceneManager.MoveGameObjectToScene(root, scene);
        var grass = root.AddComponent<GrassComputeScript>();
        var preset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(Forest));
        preset.windStrength = 0; preset.bladeForwardAmount = 0;
        preset.MinHeight = .1f; preset.MaxHeight = .2f;
        preset.grassRandomHeightMin = .1f; preset.grassRandomHeightMax = .5f;
        var settings = ScriptableObject.CreateInstance<SO_GrassToolSettings>();
        settings.brushSize = 1; settings.brushFalloffSize = 0; settings.heightStep = .05f;
        settings.sculptTargetHeight = .8f; settings.hitMask = ~0; settings.paintMask = ~0;
        var window = ScriptableObject.CreateInstance<GrassPainterWindow>();
        var cameraObject = new GameObject("Verification camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(0, 1, -3); camera.transform.LookAt(new Vector3(0, .5f, 0));
        bool createdPrefab = false;
        try
        {
            grass.currentPresets = preset;
            var sample = new GrassData { position = Vector3.zero, normal = Vector3.up, length = new Vector2(.03f, 5f), color = Vector3.one };
            var outside = sample; outside.position = new Vector3(3,0,0);
            grass.SetGrassPaintedDataList = new List<GrassData> { sample, outside };
            var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0,-.5f,0); collider.size = new Vector3(8,1,8);
            Field(window, "toolSettings", settings);
            Call(window, "SetSource", grass);
            Physics.SyncTransforms();
            var args = new object[] { new Ray(new Vector3(0, 3, 0), Vector3.down), default(RaycastHit), false };
            Require((bool)Call(window, "TryGetSurfaceHit", args), "Brush hits a collider in an isolated prefab-style preview scene", report);
            Require(Marshal.SizeOf<GrassData>() == 48, "CPU source stride is 48 bytes, matching both compute shaders", report);

            Operation(window, "RaiseHeight");
            Call(window, "BeginStroke"); Call(window, "StampHeight", Vector3.zero, false);
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .45f)<.001f, "First raise starts at visible clamped height, not hidden legacy value", report);
            Call(window, "StampHeight", Vector3.zero, false);
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .45f)<.001f, "Holding/dragging does not repeatedly increase the same point", report);
            Call(window, "FinishStroke");
            Call(window, "BeginStroke"); Call(window, "StampHeight", Vector3.zero, false); Call(window, "FinishStroke");
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .5f)<.001f, "A second click adds exactly one height step", report);
            Operation(window, "LowerHeight");
            Call(window, "BeginStroke"); Call(window, "StampHeight", Vector3.zero, false); Call(window, "FinishStroke");
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .45f)<.001f, "Lower subtracts one step", report);
            Operation(window, "SetHeight");
            Undo.IncrementCurrentGroup();
            Call(window, "BeginStroke"); Call(window, "StampHeight", Vector3.zero, false); Call(window, "FinishStroke");
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .8f)<.001f, "Set Height writes exact metres beyond the preset maximum", report);
            Require(grass.SetGrassPaintedDataList[1].heightOverride == 0, "Points outside the brush retain their legacy appearance", report);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .45f)<.001f, "Undo restores previous painted height", report);
            Undo.PerformRedo();
            Require(Mathf.Abs(grass.SetGrassPaintedDataList[0].heightOverride - .8f)<.001f, "Redo restores the stroke", report);

            var source = root.AddComponent<GrassSource>(); source.ResolveReferences(); source.SetRuntimePreset(preset);
            var combined = new List<GrassData>(); source.AppendWorldData(combined);
            Require(combined[0].heightOverride == .8f, "Runtime preset selection and batch copying preserve painted height", report);
            VerifyGpu(grass, camera, report);
            var meadow = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/IslandForge/GrassAuthoring/Shaders/MeadowBlades.compute");
            if (meadow) { preset.shaderToUse = meadow; VerifyGpu(grass, camera, report); }

            // Persist only a temporary test copy; never save an actual island.
            grass.currentPresets = AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(Forest);
            PrefabUtility.SaveAsPrefabAsset(root, TestPrefab);
            createdPrefab = true;
            var loaded = PrefabUtility.LoadPrefabContents(TestPrefab);
            try { Require(loaded.GetComponent<GrassComputeScript>().SetGrassPaintedDataList[0].heightOverride == .8f, "Painted heights survive saving and reopening a prefab", report); }
            finally { PrefabUtility.UnloadPrefabContents(loaded); }

            // Exercise Add and Remove through the same painter used by Scene view.
            int count = grass.SetGrassPaintedDataList.Count;
            settings.sizeLength = .45f; settings.sizeWidth = .02f; settings.pointSpacing = .01f;
            Call(window, "BeginStroke"); Call(window, "AddAtHit", (RaycastHit)args[1]); Call(window, "FinishStroke");
            Require(grass.SetGrassPaintedDataList.Count > count, "Add brush creates points on the isolated surface", report);
            Call(window, "BeginStroke"); Call(window, "RemoveAtHit", Vector3.zero); Call(window, "FinishStroke");
            Require(grass.SetGrassPaintedDataList.Count == 1, "Remove affects points under the brush only", report);
        }
        finally
        {
            Object.DestroyImmediate(window);
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(preset); Object.DestroyImmediate(settings);
            if (createdPrefab) AssetDatabase.DeleteAsset(TestPrefab);
        }
        File.WriteAllText(Report, report.ToString());
        Debug.Log(report.ToString());
    }

    [StructLayout(LayoutKind.Sequential)] struct Vertex { public Vector3 position; public Vector2 uv; }
    [StructLayout(LayoutKind.Sequential)] struct Triangle { public Vector3 normal, color; public Vector4 extra; public Vertex a,b,c; }
    static void VerifyGpu(GrassComputeScript grass, Camera camera, StringBuilder report)
    {
        grass.ResetFaster();
        Field(grass, "m_MainCamera", camera);
        Call(grass, "RenderGrass", camera);
        var argsBuffer = (ComputeBuffer)typeof(GrassComputeScript).GetField("m_ArgsBuffer", Private).GetValue(grass);
        var args = new uint[5]; argsBuffer.GetData(args);
        Require(args[0] > 0, grass.currentPresets.shaderToUse.name + " emits GPU triangles", report);
        var buffer = (ComputeBuffer)typeof(GrassComputeScript).GetField("m_DrawBuffer", Private).GetValue(grass);
        var triangles = new Triangle[args[0]/3]; buffer.GetData(triangles,0,0,triangles.Length);
        float tallest = triangles.Max(t=>Mathf.Max(t.a.position.y, Mathf.Max(t.b.position.y,t.c.position.y)));
        report.AppendLine($"GPU {grass.currentPresets.shaderToUse.name}: tallest={tallest}, vertices={args[0]}, CPU height={grass.SetGrassPaintedDataList[0].heightOverride}, sample={triangles[0].a.position}/{triangles[0].b.position}/{triangles[0].c.position}");
        Require(Mathf.Abs(tallest - .8f)<.002f, grass.currentPresets.shaderToUse.name + " actually renders the explicit 0.8m height above its 0.2m preset cap", report);
    }

    static void Preview()
    {
        if (Application.isPlaying || File.Exists(TestPrefab)) throw new Exception("Preview requires edit mode and a free test path.");
        const string island = "Assets/IslandForge/Islands/1 Grass/1 Small/Island 08 Grass Small D.prefab";
        SessionState.SetString("GrassAuthoringPreviousScene", SceneManager.GetActiveScene().path);
        // Keep the user's current scene loaded and unsaved changes intact.
        var empty = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        SceneManager.SetActiveScene(empty);
        SessionState.SetInt("GrassAuthoringEmptyScene", empty.handle);
        AssetDatabase.CopyAsset(island, TestPrefab);
        var stage = PrefabStageUtility.OpenPrefab(TestPrefab);
        var grass = stage.prefabContentsRoot.GetComponentsInChildren<GrassComputeScript>(true).First(g=>g.SetGrassPaintedDataList.Count>0);
        Selection.activeGameObject = grass.gameObject;
        GrassPainterWindow.EditGrass(grass);
        var view = SceneView.lastActiveSceneView;
        var points = grass.SetGrassPaintedDataList;
        var bounds = new Bounds(grass.transform.TransformPoint(points[0].position), Vector3.one);
        foreach(var point in points) bounds.Encapsulate(grass.transform.TransformPoint(point.position));
        view.LookAt(bounds.center, Quaternion.Euler(40,0,0), 7f, false, true);
        view.Repaint();
        File.WriteAllText(Report, "Preview opened on a disposable island copy from a camera/light-only scene.\nPoints=" + points.Count + " renderer enabled=" + grass.enabled);
    }
    static void ClosePreview()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath != TestPrefab) throw new Exception("Another prefab is open; leaving it untouched.");
        StageUtility.GoToMainStage();
        int handle = SessionState.GetInt("GrassAuthoringEmptyScene", -1);
        var empty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(s=>s.handle == handle);
        if (empty.IsValid()) EditorSceneManager.CloseScene(empty, true);
        var previous = SceneManager.GetSceneByPath(SessionState.GetString("GrassAuthoringPreviousScene", ""));
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        if (File.Exists(TestPrefab)) AssetDatabase.DeleteAsset(TestPrefab);
        File.WriteAllText(Report, "Preview closed; original scene restored.");
    }
}
