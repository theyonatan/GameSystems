using UnityEngine;

[SelectionBase]
public sealed class AboveIsland : AboveRoutePiece
{
    [Header("Catalog Identity")]
    [Tooltip("Readable authored-island name shown by the Island Forge catalog. Leave empty while upgrading to temporarily use the prefab instance name.")]
    [SerializeField]
    private string islandName = string.Empty;

    [Tooltip("Project-wide unique authored-island ID. -1 means not assigned yet.")]
    [Min(-1)]
    [SerializeField]
    private int islandId = -1;

    [Header("Generation")]
    [SerializeField]
    private IslandSize size = IslandSize.Small;

    [SerializeField]
    private IslandRole role = IslandRole.Regular;

    [Tooltip("Controls whether this prefab may be selected for normal linear phases, inserted cluster phases, or both. Forced Special/Centerpiece prefabs override this filter.")]
    [SerializeField]
    private IslandPhaseUsage phaseUsage = IslandPhaseUsage.Both;

    public string IslandName => islandName;
    public int IslandId => islandId;
    public bool HasCatalogName => !string.IsNullOrWhiteSpace(islandName);
    public bool HasCatalogId => islandId >= 0;
    public IslandSize Size => size;
    public IslandRole Role => role;
    public IslandPhaseUsage PhaseUsage => phaseUsage;

    public bool SupportsPhase(IslandPhaseUsage requiredUsage)
    {
        return (phaseUsage & requiredUsage) != 0;
    }
}
