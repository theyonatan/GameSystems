using UnityEngine;

public enum ForgeMultiBiomeLayoutMode
{
    SharedRows,
    FirstSupportedBiome
}

[DisallowMultipleComponent]
[AddComponentMenu("Above/Island Forge Organizer")]
public sealed class IslandForgeOrganizer : MonoBehaviour
{
    [Header("Collection")]
    [Tooltip("Includes inactive direct children in statistics, browsing, filtering and layout.")]
    [SerializeField]
    private bool includeInactive = true;

    [Tooltip("Shared Rows keeps multi-biome and all-biome prefabs in honest dedicated rows. First Supported Biome places each in its first supported biome row instead.")]
    [SerializeField]
    private ForgeMultiBiomeLayoutMode multiBiomeLayout = ForgeMultiBiomeLayoutMode.SharedRows;

    [Tooltip("Places Normal, Launch Pad, Drop Down and Zipline connections in separate rows.")]
    [SerializeField]
    private bool groupConnectionsByType = true;

    [Header("Grid Layout")]
    [Tooltip("Top-center of the generated catalog grid in Forge local space.")]
    [SerializeField]
    private Vector3 localOrigin;

    [Tooltip("Maximum items in one physical row before that category wraps onto another row.")]
    [Min(1)]
    [SerializeField]
    private int columnsPerRow = 6;

    [Tooltip("Minimum width reserved for an item before padding is added.")]
    [Min(0.1f)]
    [SerializeField]
    private float minimumCellWidth = 80f;

    [Tooltip("Minimum depth reserved for an item before padding is added.")]
    [Min(0.1f)]
    [SerializeField]
    private float minimumCellDepth = 80f;

    [Tooltip("Horizontal empty space between neighboring item bounds.")]
    [Min(0f)]
    [SerializeField]
    private float horizontalGap = 20f;

    [Tooltip("Empty space between wrapped rows in the same category.")]
    [Min(0f)]
    [SerializeField]
    private float rowGap = 25f;

    [Tooltip("Additional empty space after a category, such as Grass Medium or Grass Small.")]
    [Min(0f)]
    [SerializeField]
    private float categoryGap = 45f;

    [Tooltip("Additional empty space between the island catalog and the connection catalog.")]
    [Min(0f)]
    [SerializeField]
    private float connectionSectionGap = 90f;

    [Tooltip("Centers every physical row around Local Origin X. Disable to make every row begin at Local Origin X.")]
    [SerializeField]
    private bool centerRows = true;

    [Tooltip("Keeps every child's current local Y. Disable to align prefab pivots to Local Origin Y.")]
    [SerializeField]
    private bool preserveLocalHeight;

    [Header("Bounds & Scene View")]
    [Tooltip("Uses each route piece's authored Placement Bounds to prevent visual overlap in the catalog.")]
    [SerializeField]
    private bool usePlacementBounds = true;

    [Tooltip("Uses Renderer bounds when a prefab has no usable Placement Bounds.")]
    [SerializeField]
    private bool fallBackToRendererBounds = true;

    [Tooltip("Draws category names and row guides while the Forge object is selected.")]
    [SerializeField]
    private bool showSceneGuides = true;

    [Tooltip("Draws the names of recognized direct children in the Scene View while Forge is selected.")]
    [SerializeField]
    private bool showItemLabels;

    public bool IncludeInactive => includeInactive;
    public ForgeMultiBiomeLayoutMode MultiBiomeLayout => multiBiomeLayout;
    public bool GroupConnectionsByType => groupConnectionsByType;
    public Vector3 LocalOrigin => localOrigin;
    public int ColumnsPerRow => columnsPerRow;
    public float MinimumCellWidth => minimumCellWidth;
    public float MinimumCellDepth => minimumCellDepth;
    public float HorizontalGap => horizontalGap;
    public float RowGap => rowGap;
    public float CategoryGap => categoryGap;
    public float ConnectionSectionGap => connectionSectionGap;
    public bool CenterRows => centerRows;
    public bool PreserveLocalHeight => preserveLocalHeight;
    public bool UsePlacementBounds => usePlacementBounds;
    public bool FallBackToRendererBounds => fallBackToRendererBounds;
    public bool ShowSceneGuides => showSceneGuides;
    public bool ShowItemLabels => showItemLabels;

    private void OnValidate()
    {
        columnsPerRow = Mathf.Max(1, columnsPerRow);
        minimumCellWidth = Mathf.Max(0.1f, minimumCellWidth);
        minimumCellDepth = Mathf.Max(0.1f, minimumCellDepth);
        horizontalGap = Mathf.Max(0f, horizontalGap);
        rowGap = Mathf.Max(0f, rowGap);
        categoryGap = Mathf.Max(0f, categoryGap);
        connectionSectionGap = Mathf.Max(0f, connectionSectionGap);
    }
}
