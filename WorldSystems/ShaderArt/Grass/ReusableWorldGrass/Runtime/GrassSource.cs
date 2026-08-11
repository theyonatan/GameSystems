using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum GrassSourceDataSpace
{
    LocalToSource,
    World
}

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class GrassSource : MonoBehaviour
{
    [Header("Painted Grass")]
    [Tooltip("The Minions Art GrassComputeScript containing this object's painted points.")]
    [SerializeField]
    private GrassComputeScript paintedGrass;

    [Tooltip("Optional. Leave empty to use the preset assigned to Painted Grass.")]
    [SerializeField]
    private SO_GrassSettings presetOverride;

    [Tooltip("Usually Local To Source for reusable prefabs. Use World only for legacy data that must never follow this object.")]
    [SerializeField]
    private GrassSourceDataSpace dataSpace = GrassSourceDataSpace.LocalToSource;

    [Tooltip("Optional coordinate-space override. When empty, the Painted Grass transform is used.")]
    [SerializeField]
    private Transform dataTransform;

    [Header("Runtime")]
    [SerializeField]
    private bool includeInWorldGrass = true;

    [Tooltip("Prevents the authoring renderer from allocating its own buffers at runtime. The World Grass Manager renders the combined copy instead.")]
    [SerializeField]
    private bool disablePaintedRendererAtRuntime = true;

    private SO_GrassSettings runtimePresetOverride;

    public GrassComputeScript PaintedGrass => paintedGrass;
    public GrassSourceDataSpace DataSpace => dataSpace;
    public bool IncludeInWorldGrass => includeInWorldGrass;
    public int PointCount => paintedGrass == null
        ? 0
        : paintedGrass.SetGrassPaintedDataList.Count;

    private void Awake()
    {
        ResolveReferences();

        if (Application.isPlaying && disablePaintedRendererAtRuntime && paintedGrass != null)
            paintedGrass.enabled = false;
    }

    private void OnEnable()
    {
        WorldGrassManager.NotifySourcesChanged();
    }

    private void OnDisable()
    {
        WorldGrassManager.NotifySourcesChanged();
    }

    private void OnValidate()
    {
        ResolveReferences();
        WorldGrassManager.NotifySourcesChanged();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public SO_GrassSettings ResolvePreset(SO_GrassSettings fallback)
    {
        if (runtimePresetOverride != null)
            return runtimePresetOverride;

        if (presetOverride != null)
            return presetOverride;

        if (paintedGrass != null && paintedGrass.currentPresets != null)
            return paintedGrass.currentPresets;

        return fallback;
    }

    public void SetRuntimePreset(SO_GrassSettings preset)
    {
        if (runtimePresetOverride == preset)
            return;

        runtimePresetOverride = preset;
        WorldGrassManager.NotifySourcesChanged();
    }

    public void ClearRuntimePreset()
    {
        SetRuntimePreset(null);
    }

    public void AppendWorldData(List<GrassData> destination)
    {
        if (destination == null || paintedGrass == null)
            return;

        List<GrassData> sourceData = paintedGrass.SetGrassPaintedDataList;
        if (sourceData == null || sourceData.Count == 0)
            return;

        if (dataSpace == GrassSourceDataSpace.World)
        {
            destination.AddRange(sourceData);
            return;
        }

        Transform coordinates = dataTransform != null
            ? dataTransform
            : paintedGrass.transform;

        Matrix4x4 pointMatrix = coordinates.localToWorldMatrix;
        Matrix4x4 normalMatrix = pointMatrix.inverse.transpose;

        for (int i = 0; i < sourceData.Count; i++)
        {
            GrassData point = sourceData[i];
            point.position = pointMatrix.MultiplyPoint3x4(point.position);

            Vector3 worldNormal = normalMatrix.MultiplyVector(point.normal);
            point.normal = worldNormal.sqrMagnitude > 0.000001f
                ? worldNormal.normalized
                : Vector3.up;

            destination.Add(point);
        }
    }

    [ContextMenu("Refresh Grass Reference")]
    public void ResolveReferences()
    {
        if (paintedGrass == null)
            paintedGrass = GetComponentInChildren<GrassComputeScript>(true);
    }

#if UNITY_EDITOR
    [ContextMenu("Convert Legacy World Data To Local")]
    private void ConvertLegacyWorldDataToLocal()
    {
        ResolveReferences();
        if (paintedGrass == null || paintedGrass.SetGrassPaintedDataList.Count == 0)
            return;

        Transform coordinates = dataTransform != null
            ? dataTransform
            : paintedGrass.transform;

        Matrix4x4 worldToLocal = coordinates.worldToLocalMatrix;
        Matrix4x4 worldToLocalNormal = coordinates.localToWorldMatrix.transpose;

        Undo.RecordObject(paintedGrass, "Convert Grass Data To Local Space");
        List<GrassData> points = paintedGrass.SetGrassPaintedDataList;

        for (int i = 0; i < points.Count; i++)
        {
            GrassData point = points[i];
            point.position = worldToLocal.MultiplyPoint3x4(point.position);

            Vector3 localNormal = worldToLocalNormal.MultiplyVector(point.normal);
            point.normal = localNormal.sqrMagnitude > 0.000001f
                ? localNormal.normalized
                : Vector3.up;

            points[i] = point;
        }

        dataSpace = GrassSourceDataSpace.LocalToSource;
        EditorUtility.SetDirty(paintedGrass);
        EditorUtility.SetDirty(this);
        WorldGrassManager.NotifySourcesChanged();
    }
#endif
}
