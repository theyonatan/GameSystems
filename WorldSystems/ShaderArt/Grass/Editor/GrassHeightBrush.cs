using UnityEngine;

internal static class GrassHeightBrush
{
    internal static float VisibleHeight(GrassData point, SO_GrassSettings preset, Vector3 worldPosition)
    {
        if (point.heightOverride > 0) return point.heightOverride;
        if (!preset) return Mathf.Max(0.001f, point.length.y);
        // Match the legacy compute shader's root-height calculation before the
        // first stamp, rather than adding to a hidden value far beyond its clamp.
        float noise = Mathf.Repeat(Mathf.Sin(worldPosition.x * 12.9898f + worldPosition.z * 78.233f) * 43758.5453f, 1f);
        float offset = Mathf.Lerp(preset.grassRandomHeightMin, preset.grassRandomHeightMax, noise);
        float height = Mathf.Clamp(point.length.y + offset, preset.MinHeight, preset.MaxHeight);
        // The original kernel adds its height both at the root and in the vertex
        // builder. MeadowBlades already uses one height contribution.
        return preset.shaderToUse && preset.shaderToUse.name == "GrassBlades" ? height * 2f : height;
    }

    internal static void SetHeight(ref GrassData point, float metres)
    {
        if (float.IsNaN(metres) || float.IsInfinity(metres)) return;
        point.heightOverride = Mathf.Max(0.001f, metres);
        point.length.y = point.heightOverride;
    }
}
