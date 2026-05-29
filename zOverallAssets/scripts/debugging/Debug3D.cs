using UnityEngine;

public static class Debug3D
{
    public static void PlaceCube(Vector3 position, float duration = 10f, Color? color = null)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        cube.transform.position = position;
        cube.transform.localScale = Vector3.one * 0.25f;

        var renderer = cube.GetComponent<Renderer>();

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color ?? Color.red;

        renderer.material = mat;

        Object.Destroy(cube.GetComponent<Collider>());
        Object.Destroy(cube, duration);
    }
}