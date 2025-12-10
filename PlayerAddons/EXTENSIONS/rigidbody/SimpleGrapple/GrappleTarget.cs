using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class GrappleTarget : MonoBehaviour
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private Renderer _rend;
    private Color _baseColor;
    private bool _hasProperty;

    private void Awake()
    {
        _rend = GetComponent<Renderer>();
        
        if (!_rend.material.HasProperty(ColorProperty)) return;
        _hasProperty = true;
        _baseColor = _rend.material.color;
    }

    public void SetHighlighted(Color? highlightColor = null)
    {
        if (!_hasProperty) return;
        _rend.material.color = highlightColor ?? _baseColor;
        
        Debug.Log(gameObject.name + " has been set to " + highlightColor);
    }
}