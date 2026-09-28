using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Supplies the existing grass floor-blend shader with a scene's ground albedo map.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class WorldGrassGroundBlend : MonoBehaviour
{
    [Tooltip("Ground materials captured using a WorldGrassAlbedoShader mapping in Resources/GrassGroundAlbedo, or their own GrassGroundAlbedo pass.")]
    public Material[] groundMaterials = Array.Empty<Material>();
    [Tooltip("Also capture surfaces whose shader explicitly provides a GrassGroundAlbedo pass, such as soft-edged dirt paths. No layer or material-list changes are needed for those surfaces.")]
    public bool includeAlbedoPasses = true;
    [Tooltip("Enable ground blending for every grass preset in this scene without editing shared materials.")]
    public bool enableForAllGrass;
    [Range(256, 4096)] public int resolution = 2048;
    [Min(0.1f)] public float padding = 2f;

    static readonly List<WorldGrassGroundBlend> maps = new List<WorldGrassGroundBlend>();
    // Optional integrations can focus map resolution without coupling this
    // reusable capture to a particular generator. Registration is order-safe.
    static readonly Dictionary<Component, Transform> captureRoots = new Dictionary<Component, Transform>();
    static readonly int blendId = Shader.PropertyToID("_Blend");
    static readonly int textureId = Shader.PropertyToID("_TerrainDiffuse");
    static readonly int sizeId = Shader.PropertyToID("_OrthographicCamSizeTerrain");
    static readonly int positionId = Shader.PropertyToID("_OrthographicCamPosTerrain");
    static readonly int coverageId = Shader.PropertyToID("_GrassGroundCoverage");
    static readonly int useCoverageId = Shader.PropertyToID("_GrassGroundUseCoverage");
    readonly Dictionary<Material, Material> captureMaterials = new Dictionary<Material, Material>();
    WorldGrassAlbedoShader[] captureShaders;
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
        foreach (var material in captureMaterials.Values) if (material) DestroyOwned(material);
        captureMaterials.Clear();
        captureShaders = null;
    }
    [ContextMenu("Refresh Ground Blend Map")]
    public void RequestRefresh() { dirty = true; }
    public static void RequestSceneRefresh(Scene scene)
    {
        foreach (var entry in maps)
            if (entry != null && entry.gameObject.scene == scene) entry.RequestRefresh();
    }

    public static void RegisterCaptureRoot(Component owner, Transform root)
    {
        if (owner == null || root == null) return;
        if (captureRoots.TryGetValue(owner, out var existing) && existing == root) return;
        captureRoots[owner] = root;
        RequestSceneRefresh(owner.gameObject.scene);
    }

    public static void UnregisterCaptureRoot(Component owner)
    {
        if (owner != null && captureRoots.Remove(owner)) RequestSceneRefresh(owner.gameObject.scene);
    }

    // Uses the same property block as the batch buffer: no global shader state
    // leaks between scenes, and no changes to shared grass materials at runtime.
    public static void Apply(GrassComputeScript grass, Material material, MaterialPropertyBlock properties)
    {
        if (!material.HasProperty(blendId)) return;
        properties.SetFloat(useCoverageId, 0f);
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
            properties.SetTexture(coverageId, entry.map);
            properties.SetFloat(useCoverageId, 1f);
            properties.SetFloat(sizeId, entry.mapSize);
            properties.SetVector(positionId, entry.mapPosition);
            return;
        }
#if UNITY_EDITOR
        // A prefab opened from an empty scene has no ground-map capture. Fall back
        // to its material colour without changing the shared gameplay material.
        if (!Application.IsPlaying(grass.gameObject)) properties.SetFloat(blendId, 0f);
#endif
    }

    public void CaptureNow()
    {
        dirty = false;
        Ready = false;
        CapturedSubmeshes = 0;
        if (!includeAlbedoPasses && (groundMaterials == null || groundMaterials.Length == 0)) return;
        var draws = new List<(Renderer renderer, Material material, int submesh, int pass)>();
        var preparedMaterials = new HashSet<Material>();
        if (captureShaders == null) captureShaders = Resources.LoadAll<WorldGrassAlbedoShader>("GrassGroundAlbedo");
        var focusRoots = new List<Transform>();
        foreach (var entry in captureRoots)
            if (entry.Key != null && entry.Value != null && entry.Key.gameObject.scene == gameObject.scene && entry.Value.gameObject.activeInHierarchy)
                focusRoots.Add(entry.Value);
        Bounds bounds = default;
        Bounds focusBounds = default;
        bool haveBounds = false;
        bool haveFocusBounds = false;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.gameObject.scene != gameObject.scene) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var materials = renderer.sharedMaterials;
            for (int submesh = 0; submesh < Mathf.Min(materials.Length, filter.sharedMesh.subMeshCount); submesh++)
            {
                var material = materials[submesh];
                if (material == null) continue;
                // A dedicated pass explicitly opts a surface into the map and can
                // preserve alpha-blended edges without capturing scene lighting.
                int pass = includeAlbedoPasses ? material.FindPass("GrassGroundAlbedo") : -1;
                bool selected = groundMaterials != null && Array.IndexOf(groundMaterials, material) >= 0;
                if (pass < 0 && !selected) continue;
                if (pass < 0)
                {
                    var source = material;
                    // A GBuffer pass can disappear from Forward/Forward+ player builds.
                    // Use an explicitly referenced single-target albedo shader instead.
                    if (!captureMaterials.TryGetValue(source, out material))
                    {
                        foreach (var mapping in captureShaders)
                            if (mapping && mapping.surfaceShader == source.shader && mapping.captureShader)
                            {
                                material = new Material(mapping.captureShader)
                                { name = source.name + " (grass albedo)", hideFlags = HideFlags.HideAndDontSave };
                                captureMaterials.Add(source, material);
                                break;
                            }
                    }
                    if (material && preparedMaterials.Add(source)) material.CopyPropertiesFromMaterial(source);
                    pass = material ? material.FindPass("GrassGroundAlbedo") : source.FindPass("GrassGroundAlbedo");
                    if (!material) material = source;
                }
                if (pass < 0)
                {
                    Debug.LogWarning($"Ground blend: '{material.name}' needs a GrassGroundAlbedo pass or a WorldGrassAlbedoShader mapping in Resources/GrassGroundAlbedo.", this);
                    continue;
                }
                draws.Add((renderer, material, submesh, pass));
                if (!haveBounds) { bounds = renderer.bounds; haveBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
                foreach (var root in focusRoots)
                    if (renderer.transform.IsChildOf(root))
                    {
                        if (!haveFocusBounds) { focusBounds = renderer.bounds; haveFocusBounds = true; }
                        else focusBounds.Encapsulate(renderer.bounds);
                        break;
                    }
            }
        }
        if (!haveBounds) return;
        // Focus XZ coverage on the playable surface. Nearby background ground
        // inside that square is still captured for its grass; far surfaces are
        // skipped. Resolution, texture count and refresh frequency stay fixed.
        Bounds footprint = haveFocusBounds ? focusBounds : bounds;
        mapSize = Mathf.Max(footprint.extents.x, footprint.extents.z) + Mathf.Max(.1f, padding);
        if (haveFocusBounds)
        {
            draws.RemoveAll(draw => draw.renderer.bounds.max.x < footprint.center.x - mapSize ||
                draw.renderer.bounds.min.x > footprint.center.x + mapSize ||
                draw.renderer.bounds.max.z < footprint.center.z - mapSize ||
                draw.renderer.bounds.min.z > footprint.center.z + mapSize);
            // Retain all captured heights so nearby elevated/lower surfaces
            // still participate in depth testing inside the focused map.
            bounds = draws[0].renderer.bounds;
            for (int i = 1; i < draws.Count; i++) bounds.Encapsulate(draws[i].renderer.bounds);
        }
        // DrawRenderer does not sort for us. Capture opaque ground first, then
        // composite transparent paths against its depth, from lower to higher
        // surfaces (back to front for the downward-facing capture).
        draws.Sort((a, b) =>
        {
            int queue = a.material.renderQueue.CompareTo(b.material.renderQueue);
            if (queue != 0) return queue;
            return a.material.renderQueue > (int)RenderQueue.GeometryLast
                ? a.renderer.bounds.center.y.CompareTo(b.renderer.bounds.center.y)
                : 0;
        });
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
        mapPosition = new Vector3(footprint.center.x, bounds.max.y + 10f, footprint.center.z);
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
            // This command converts the camera projection to the graphics API.
            // Pre-converting it with GL.GetGPUProjectionMatrix reverses depth
            // twice on D3D, letting surfaces below the ground win the depth test.
            // The unflipped map keeps +Z aligned with +V in grass GetWorldUV.
            commands.SetViewProjectionMatrices(captureCamera.worldToCameraMatrix, captureCamera.projectionMatrix);
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
