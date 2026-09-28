using UnityEngine;

/// <summary>
/// Maps a surface shader to an unlit grass capture shader with matching property names.
/// Place assets in Resources/GrassGroundAlbedo so captures and shader references survive builds.
/// The capture pass must be named GrassGroundAlbedo and write coverage into alpha.
/// </summary>
[CreateAssetMenu(menuName = "Grass/Ground Albedo Shader")]
public sealed class WorldGrassAlbedoShader : ScriptableObject
{
    public Shader surfaceShader;
    public Shader captureShader;
}
