using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Supplies the existing grass floor-blend shader with a scene's ground albedo map.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class WorldGrassGroundBlend : MonoBehaviour
{
    [Tooltip("Only submeshes using these ground materials are captured. Trees, grass and clouds are excluded.")]
    public Material[] groundMaterials = Array.Empty<Material>();
    [Tooltip("Enable ground blending for every grass preset in this scene without editing shared materials.")]
    public bool enableForAllGrass;
    [Range(256, 4096)] public int resolution = 2048;
    [Min(0.1f)] public float padding = 2f;

    static readonly List<WorldGrassGroundBlend> maps = new List<WorldGrassGroundBlend>();
    static readonly int blendId = Shader.PropertyToID("_Blend");
    static readonly int textureId = Shader.PropertyToID("_TerrainDiffuse");
    static readonly int sizeId = Shader.PropertyToID("_OrthographicCamSizeTerrain");
    static readonly int positionId = Shader.PropertyToID("_OrthographicCamPosTerrain");
    RenderTexture map;
    Camera captureCamera;
    bool dirty = true;
    float mapSize;
    Vector3 mapPosition;
    public RenderTexture GroundMap => map;
    public Vector3 MapPosition => mapPosition;
    public float MapHalfSize => mapSize;
    public bool Ready { get; private set; }
    public int CaptureCount { get; private set; }
    public int CapturedSubmeshes { get; private set; }

    void OnEnable()
    {
        if (!maps.Contains(this)) maps.Add(this);
        RequestRefresh();
    }
    void OnValidate() => RequestRefresh();
    void LateUpdate() { if (dirty) CaptureNow(); }
    void OnDisable()
    {
        maps.Remove(this);
        Ready = false;
        ReleaseMap();
        if (captureCamera != null) DestroyOwned(captureCamera.gameObject);
        captureCamera = null;
    }
    [ContextMenu("Refresh Ground Blend Map")]
    public void RequestRefresh() { dirty = true; }
    public static void RequestSceneRefresh(Scene scene)
    {
        foreach (var entry in maps)
            if (entry != null && entry.gameObject.scene == scene) entry.RequestRefresh();
    }

    // Uses the same property block as the batch buffer: no global shader state
    // leaks between scenes, and no changes to shared grass materials at runtime.
    public static void Apply(GrassComputeScript grass, Material material, MaterialPropertyBlock properties)
    {
        if (!material.HasProperty(blendId)) return;
        float enabledBlend = material.GetFloat(blendId);
        properties.SetFloat(blendId, enabledBlend);
        foreach (var entry in maps)
        {
            if (entry == null || entry.gameObject.scene != grass.gameObject.scene) continue;
            if (entry.enableForAllGrass) properties.SetFloat(blendId, 1f);
            else if (enabledBlend == 0) return;
            if (!entry.Ready || entry.map == null || !entry.map.IsCreated())
            {
                properties.SetFloat(blendId, 0f);
                entry.RequestRefresh();
                return;
            }
            properties.SetTexture(textureId, entry.map);
            properties.SetFloat(sizeId, entry.mapSize);
            properties.SetVector(positionId, entry.mapPosition);
            return;
        }
    }

    public void CaptureNow()
    {
        dirty = false;
        Ready = false;
        CapturedSubmeshes = 0;
        if (groundMaterials == null || groundMaterials.Length == 0) return;
        var draws = new List<(Renderer renderer, Material material, int submesh, int pass)>();
        Bounds bounds = default;
        bool haveBounds = false;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.gameObject.scene != gameObject.scene) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var materials = renderer.sharedMaterials;
            for (int submesh = 0; submesh < Mathf.Min(materials.Length, filter.sharedMesh.subMeshCount); submesh++)
            {
                var material = materials[submesh];
                if (material == null || Array.IndexOf(groundMaterials, material) < 0) continue;
                // URP's GBuffer target 0 contains albedo, without sun/shadows.
                // Grass applies its own lighting after the floor-color blend.
                int pass = material.FindPass("GBuffer");
                if (pass < 0)
                {
                    Debug.LogWarning($"Ground blend: '{material.name}' needs a URP GBuffer pass to capture albedo.", this);
                    continue;
                }
                draws.Add((renderer, material, submesh, pass));
                if (!haveBounds) { bounds = renderer.bounds; haveBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!haveBounds) return;
        int size = Mathf.Clamp(resolution, 256, 4096);
        if (map == null || map.width != size || !map.IsCreated())
        {
            ReleaseMap();
            // Linear target: the sampled albedo is already in shader color space.
            map = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "Grass ground albedo", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            map.Create();
        }
        if (captureCamera == null)
        {
            var cameraObject = new GameObject("Grass albedo capture (disabled)") { hideFlags = HideFlags.HideAndDontSave };
            captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.enabled = false;
        }
        mapSize = Mathf.Max(bounds.extents.x, bounds.extents.z) + Mathf.Max(.1f, padding);
        mapPosition = new Vector3(bounds.center.x, bounds.max.y + 10f, bounds.center.z);
        captureCamera.transform.SetPositionAndRotation(mapPosition, Quaternion.Euler(90f, 0f, 0f));
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = mapSize;
        captureCamera.aspect = 1f;
        captureCamera.nearClipPlane = .1f;
        captureCamera.farClipPlane = bounds.size.y + 20f;

        var commands = new CommandBuffer { name = "Capture grass ground albedo" };
        var previousTarget = RenderTexture.active;
        try
        {
            commands.SetRenderTarget(map);
            commands.ClearRenderTarget(true, true, Color.clear);
            commands.SetViewport(new Rect(0, 0, size, size));
            // GetWorldUV in the existing grass shader maps +Z to +V. Use the
            // unflipped projection: a camera-style render-texture Y flip would
            // mirror this data map relative to those world-space coordinates.
            commands.SetViewProjectionMatrices(captureCamera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(captureCamera.projectionMatrix, false));
            foreach (var draw in draws) commands.DrawRenderer(draw.renderer, draw.material, draw.submesh, draw.pass);
            Graphics.ExecuteCommandBuffer(commands);
            CapturedSubmeshes = draws.Count;
            CaptureCount++;
            Ready = true;
        }
        finally
        {
            commands.Release();
            RenderTexture.active = previousTarget;
        }
    }
    void ReleaseMap()
    {
        if (map == null) return;
        map.Release(); DestroyOwned(map); map = null;
    }
    static void DestroyOwned(UnityEngine.Object item)
    {
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
