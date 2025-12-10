using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class BiomeManager : MonoBehaviour
{
    [System.Serializable] public class BiomeEvent : UnityEvent<BiomeProfile> { }
    public BiomeEvent OnBiomeChanged = new();

    [Tooltip("Default biome if none active")] public BiomeProfile defaultBiome;
    [Tooltip("If false, this component does nothing")]
    public bool overwriteCurrentSettings = true;

    private readonly Dictionary<string, BiomeProfile> biomes = new();
    private BiomeProfile current;

    // ------------------------- API
    public void ResetManager()
    {
        biomes.Clear();
        current = defaultBiome;
        OnBiomeChanged.Invoke(current);
    }

    public void AddBiome(string name, BiomeProfile profile)
    {
        if (!biomes.ContainsKey(name))
            biomes.Add(name, profile);
    }

    public void AddAllBiomes()
    {
        foreach (var lb in FindObjectsByType<LevelBiome>(FindObjectsSortMode.None))
        {
            AddBiome(lb.BiomeName, lb.Profile);
        }
    }

    public void SetBiome(string name)
    {
        if (!overwriteCurrentSettings) return;
        if (biomes.TryGetValue(name, out var profile) && profile != current)
        {
            current = profile;
            OnBiomeChanged.Invoke(current);
        }
    }

    public BiomeProfile GetCurrentProfile() => current ?? defaultBiome;
}