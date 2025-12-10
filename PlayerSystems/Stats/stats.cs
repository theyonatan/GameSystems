using UnityEditor;

public enum StatType
{
    Health,
    Speed,
    Strength,
    Money,
    Luck,
    Snow
}

public class Modifier
{
    public StatType StatType;
    public float Value;
    public int Priority;
    public float BaseTime;
    public float TimeRemaining;
}
