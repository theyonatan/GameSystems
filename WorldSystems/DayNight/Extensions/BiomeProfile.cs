using UnityEngine;

[CreateAssetMenu(fileName="BiomeProfile", menuName="DayNight/Biome Profile", order=0)]
public class BiomeProfile : ScriptableObject
{
    [Header("Lighting Curves (evaluated by time 0‑1)")]
    public Gradient lightColor;
    public AnimationCurve lightIntensity = AnimationCurve.Linear(0, 1, 1, 0);

    public Gradient ambientColor;
    public Gradient fogColor;
    public AnimationCurve fogDensity = AnimationCurve.Constant(0, 1, 0.01f);

    [Header("Skybox")]
    public Material skyboxDay;
    public Material skyboxNight;
    public AnimationCurve skyboxExposure = AnimationCurve.EaseInOut(0, 1f, 1, 0.05f);

    [Header("Starfield")] public AnimationCurve starsAlpha = AnimationCurve.Linear(0, 0, 1, 1);
}
