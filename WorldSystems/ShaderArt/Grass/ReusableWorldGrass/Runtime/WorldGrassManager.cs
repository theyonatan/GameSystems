using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class WorldGrassManager : MonoBehaviour
{
    private const string BatchRootName = "[Combined Grass Batches]";

    private static readonly List<WorldGrassManager> ActiveManagers =
        new List<WorldGrassManager>();

    [Header("Sources")]
    [Tooltip("Optional. When assigned, only GrassSource components beneath this transform are combined. Otherwise, sources in this scene are used.")]
    [SerializeField]
    private Transform sourceRoot;

    [Tooltip("Used only when a GrassSource has no preset of its own.")]
    [SerializeField]
    private SO_GrassSettings defaultPreset;

    [Header("Rebuilding")]
    [SerializeField]
    private bool rebuildOnEnable = true;

    [Tooltip("Combines multiple source changes into one rebuild at the end of the frame.")]
    [SerializeField]
    private bool automaticallyRebuildWhenSourcesChange = true;

    private readonly List<GrassComputeScript> activeBatches =
        new List<GrassComputeScript>();

    private Transform batchRoot;
    private bool rebuildRequested;

    public IReadOnlyList<GrassComputeScript> ActiveBatches => activeBatches;

    private void OnEnable()
    {
        if (!ActiveManagers.Contains(this))
            ActiveManagers.Add(this);

        if (rebuildOnEnable)
            RequestRebuild();
    }

    private void OnDisable()
    {
        ActiveManagers.Remove(this);
        DestroyBatches();
    }

    private void LateUpdate()
    {
        if (!rebuildRequested)
            return;

        rebuildRequested = false;
        RebuildNow();
    }

    public static void NotifySourcesChanged()
    {
        for (int i = 0; i < ActiveManagers.Count; i++)
        {
            WorldGrassManager manager = ActiveManagers[i];
            if (manager != null && manager.automaticallyRebuildWhenSourcesChange)
                manager.RequestRebuild();
        }
    }

    public void RequestRebuild()
    {
        rebuildRequested = true;
    }

    [ContextMenu("Rebuild World Grass Now")]
    public void RebuildNow()
    {
        rebuildRequested = false;

        Dictionary<SO_GrassSettings, List<GrassData>> groupedData =
            new Dictionary<SO_GrassSettings, List<GrassData>>();

        GrassSource[] sources = FindObjectsByType<GrassSource>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            GrassSource source = sources[i];
            if (!ShouldInclude(source))
                continue;

            SO_GrassSettings preset = source.ResolvePreset(defaultPreset);
            if (preset == null)
            {
                Debug.LogWarning(
                    $"Grass Source '{source.name}' has no grass preset and was skipped.",
                    source);
                continue;
            }

            if (!groupedData.TryGetValue(preset, out List<GrassData> points))
            {
                points = new List<GrassData>();
                groupedData.Add(preset, points);
            }

            source.AppendWorldData(points);
        }

        DestroyBatches();

        if (groupedData.Count == 0)
            return;

        GameObject rootObject = new GameObject(BatchRootName);
        batchRoot = rootObject.transform;
        batchRoot.SetParent(transform, false);

        foreach (KeyValuePair<SO_GrassSettings, List<GrassData>> group in groupedData)
        {
            if (group.Value.Count == 0)
                continue;

            CreateBatch(group.Key, group.Value);
        }
    }

    public void CutGrass(Vector3 worldPosition, float radius)
    {
        for (int i = 0; i < activeBatches.Count; i++)
        {
            GrassComputeScript batch = activeBatches[i];
            if (batch != null && batch.isActiveAndEnabled)
                batch.UpdateCutBuffer(worldPosition, radius);
        }
    }

    private bool ShouldInclude(GrassSource source)
    {
        if (source == null || !source.isActiveAndEnabled || !source.IncludeInWorldGrass)
            return false;

        if (source.gameObject.scene != gameObject.scene)
            return false;

        return sourceRoot == null || source.transform.IsChildOf(sourceRoot);
    }

    private void CreateBatch(SO_GrassSettings preset, List<GrassData> points)
    {
        GameObject batchObject = new GameObject($"Grass - {preset.name}");
        batchObject.layer = gameObject.layer;
        batchObject.transform.SetParent(batchRoot, false);

        GrassComputeScript renderer = batchObject.AddComponent<GrassComputeScript>();
        renderer.enabled = false;
        renderer.currentPresets = preset;
        renderer.grassDataIsWorldSpace = true;
        renderer.SetGrassPaintedDataList = points;
        renderer.enabled = true;

        activeBatches.Add(renderer);
    }

    private void DestroyBatches()
    {
        for (int i = 0; i < activeBatches.Count; i++)
        {
            if (activeBatches[i] != null)
                activeBatches[i].enabled = false;
        }

        activeBatches.Clear();

        if (batchRoot == null)
        {
            Transform existing = transform.Find(BatchRootName);
            if (existing != null)
                batchRoot = existing;
        }

        if (batchRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(batchRoot.gameObject);
        else
            DestroyImmediate(batchRoot.gameObject);

        batchRoot = null;
    }
}
