using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Tool Settings", menuName = "Utility/GrassToolSettings")]
public class SO_GrassToolSettings : ScriptableObject
{
    public enum VertexColorSetting { None, Red, Blue, Green };


    [Header("Terrain Layer Settings")]
    public float[] layerBlocking;
    public bool[] layerFading;

    [Header("Vertex Color Settings")]
    public VertexColorSetting VertexColorSettings;
    public VertexColorSetting VertexFade;

    // length/width

    public float sizeWidth = 1f;

    public float sizeLength = 1f;

    // length/width adjustments

    public float adjustWidth = 0f;

    public float adjustLength = 0f;

    public float adjustWidthMax = 2f;
    public float adjustHeightMax = 2f;

    // reproject settings

    public float reprojectOffset = 1f;

    // color settings

    public float rangeR, rangeG, rangeB;

    public Color AdjustedColor = Color.white;

    // brush settings

    public LayerMask paintBlockMask = 0;

    public LayerMask hitMask = 1;

    public LayerMask paintMask = 1;
    public float brushSize = 4f;

    public float brushFalloffSize = 0.8f;

    public float Flow;

    public float density = 1f;

    public float normalLimit = 1;

    public int grassAmountToGenerate = 100000;

    public float generationDensity = 1;

    [Header("Grass Tool 2.0")]
    [Min(0.001f)] public float pointSpacing = 0.12f;
    [Range(0.01f, 1f)] public float sculptStrength = 0.35f;
    [Min(0.001f)] public float sculptTargetWidth = 0.15f;
    [Min(0.001f)] public float sculptTargetHeight = 0.55f;
    public float sculptWidthPerSecond = 0.1f;
    public float sculptHeightPerSecond = 0.25f;
    [Range(0f, 1f)] public float randomWidthAmount = 0.15f;
    [Range(0f, 1f)] public float randomHeightAmount = 0.2f;


    public void CreateNewLayers()
    {
        Debug.Log("Setting up initial tool settings");
        layerBlocking = new float[8];
        for (int i = 0; i < layerBlocking.Length; i++)
        {
            layerBlocking[i] = 1;
        }
        layerFading = new bool[8];
        layerFading[0] = true;
    }

    private void OnValidate()
    {
        EnsureValid();
    }

    public void EnsureValid()
    {
        if (layerBlocking == null || layerBlocking.Length != 8)
        {
            float[] previous = layerBlocking;
            layerBlocking = new float[8];
            for (int i = 0; i < layerBlocking.Length; i++)
                layerBlocking[i] = previous != null && i < previous.Length ? previous[i] : 1f;
        }

        if (layerFading == null || layerFading.Length != 8)
        {
            bool[] previous = layerFading;
            layerFading = new bool[8];
            for (int i = 0; i < layerFading.Length; i++)
                layerFading[i] = previous != null && i < previous.Length && previous[i];
        }

        brushSize = Mathf.Max(0.05f, brushSize);
        pointSpacing = Mathf.Max(0.001f, pointSpacing);
        sizeWidth = Mathf.Max(0.001f, sizeWidth);
        sizeLength = Mathf.Max(0.001f, sizeLength);
        sculptTargetWidth = Mathf.Max(0.001f, sculptTargetWidth);
        sculptTargetHeight = Mathf.Max(0.001f, sculptTargetHeight);
        grassAmountToGenerate = Mathf.Max(1, grassAmountToGenerate);
        generationDensity = Mathf.Max(0.001f, generationDensity);
    }
}
