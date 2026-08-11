using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public sealed class BackgroundIsland : MonoBehaviour
{
    [Tooltip("Leave empty to allow this prefab in every biome.")]
    [SerializeField]
    private IslandBiome[] allowedBiomes = Array.Empty<IslandBiome>();

    [SerializeField]
    private BackgroundIslandLayerMask allowedLayers = BackgroundIslandLayerMask.All;

    [SerializeField]
    private BackgroundIslandSize size = BackgroundIslandSize.Small;

    [Tooltip("Dedicated disabled or trigger BoxColliders describing this prefab's visual footprint.")]
    [SerializeField]
    private BoxCollider[] placementBounds = Array.Empty<BoxCollider>();

    [Tooltip("Performance budget units, not spawn weight. Leave at 1 for a normal island; use larger values only for unusually expensive prefabs. This never changes selection odds.")]
    [Min(1)]
    [SerializeField]
    private int visualCost = 1;

    [SerializeField]
    private bool allowRandomYaw = true;

    [Min(0.01f)]
    [SerializeField]
    private float minimumScaleMultiplier = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float maximumScaleMultiplier = 1f;

    public IReadOnlyList<IslandBiome> AllowedBiomes => allowedBiomes;
    public BackgroundIslandLayerMask AllowedLayers => allowedLayers;
    public BackgroundIslandSize Size => size;
    public IReadOnlyList<BoxCollider> PlacementBounds => placementBounds;
    public int VisualCost => Mathf.Max(1, visualCost);
    public bool AllowRandomYaw => allowRandomYaw;
    public float MinimumScaleMultiplier => Mathf.Max(0.01f, minimumScaleMultiplier);
    public float MaximumScaleMultiplier => Mathf.Max(MinimumScaleMultiplier, maximumScaleMultiplier);
    public bool HasGeneratedContext { get; private set; }
    public IslandBiome GeneratedBiome { get; private set; }
    public BackgroundIslandLayer GeneratedLayer { get; private set; }

    public void InitializeGeneratedContext(IslandBiome biome, BackgroundIslandLayer layer)
    {
        HasGeneratedContext = true;
        GeneratedBiome = biome;
        GeneratedLayer = layer;
    }

    public bool SupportsBiome(IslandBiome biome)
    {
        if (allowedBiomes == null || allowedBiomes.Length == 0)
            return true;

        for (int i = 0; i < allowedBiomes.Length; i++)
        {
            if (allowedBiomes[i] == biome)
                return true;
        }

        return false;
    }

    public bool SupportsLayer(BackgroundIslandLayer layer)
    {
        return (allowedLayers & layer.ToMask()) != 0;
    }

    public bool HasUsablePlacementBounds()
    {
        if (placementBounds == null || placementBounds.Length == 0)
            return false;

        for (int i = 0; i < placementBounds.Length; i++)
        {
            if (placementBounds[i] != null)
                return true;
        }

        return false;
    }

    public float CalculateLocalPlacementRadius()
    {
        float radius = 0f;
        if (placementBounds == null)
            return radius;

        Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;
        for (int i = 0; i < placementBounds.Length; i++)
        {
            BoxCollider box = placementBounds[i];
            if (box == null)
                continue;

            Vector3 half = box.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = box.center + Vector3.Scale(half, new Vector3(x, y, z));
                        Vector3 rootLocal = rootWorldToLocal.MultiplyPoint3x4(box.transform.TransformPoint(corner));
                        radius = Mathf.Max(radius, rootLocal.magnitude);
                    }
                }
            }
        }

        return radius;
    }
}
