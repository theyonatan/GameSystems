using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Settings", menuName = "Utility/GrassSettings")]
public class SO_GrassSettings : ScriptableObject
{
    public ComputeShader shaderToUse;
    public Material materialToUse;
    // Blade
    [Header("Blade")]
    public float grassRandomHeightMin = 0.0f;
    public float grassRandomHeightMax = 0.0f;
    [Range(0, 1)] public float bladeRadius = 0.2f;
    [Range(0, 1)] public float bladeForwardAmount = 0.38f;
    [Range(1, 5)] public float bladeCurveAmount = 2;

    [Range(0, 1)] public float bottomWidth = 0.1f;

    [Min(0.001f)] public float MinWidth = 0.01f;
    [Min(0.001f)] public float MinHeight = 0.01f;

    [Min(0.001f)] public float MaxWidth = 1f;
    [Min(0.001f)] public float MaxHeight = 1f;


    // Wind
    [Header("Wind")]
    public float windSpeed = 10;
    public float windStrength = 0.05f;

    [Header("Grass")]
    [Range(1, 8)] public int allowedBladesPerVertex = 4;
    [Range(1, 4)] public int allowedSegmentsPerBlade = 4;
    // Interactor

    [Header("Interactor Strength")]
    public float affectStrength = 1;

    // Material
    [Header("Material")]
    public Color topTint = new Color(1, 1, 1);
    public Color bottomTint = new Color(0, 0, 1);


    [Header("LOD/ Culling")]
    public bool drawBounds;
    public float minFadeDistance = 40;

    public float maxDrawDistance = 125;


    public int cullingTreeDepth = 4;

    [Header("Particles")]
    public GameObject cuttingParticles;

    [Header("Other")]
    public UnityEngine.Rendering.ShadowCastingMode castShadow;

    private void OnValidate()
    {
        MinWidth = Mathf.Max(0.001f, MinWidth);
        MinHeight = Mathf.Max(0.001f, MinHeight);
        MaxWidth = Mathf.Max(MinWidth, MaxWidth);
        MaxHeight = Mathf.Max(MinHeight, MaxHeight);
        allowedBladesPerVertex = Mathf.Clamp(allowedBladesPerVertex, 1, 8);
        allowedSegmentsPerBlade = Mathf.Clamp(allowedSegmentsPerBlade, 1, 4);
        cullingTreeDepth = Mathf.Max(1, cullingTreeDepth);
        minFadeDistance = Mathf.Max(0f, minFadeDistance);
        maxDrawDistance = Mathf.Max(minFadeDistance + 0.01f, maxDrawDistance);
    }
}
