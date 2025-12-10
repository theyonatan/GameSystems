using UnityEngine;
using UnityEngine.UI;

public class CrosshairHighlighter : MonoBehaviour
{
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color defaultColor = Color.black;
    [SerializeField] private Color highlightColor = Color.red;
    [SerializeField] private float maxCheckRange = 100f;
    [SerializeField] private LayerMask aimLayers;
    [SerializeField] private bool debug;

    private Transform _cam;

    private void Start()
    {
        if (Camera.main != null)
            _cam = Camera.main.transform;

        if (crosshairImage != null)
        {
            crosshairImage.color = defaultColor;
        }
    }

    private void Update()
    {
        if (_cam is null || crosshairImage is null) return;
        
        Ray ray = new(_cam.position, _cam.forward);
        
        if (debug)
            DebugCrosshair(ray);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxCheckRange, aimLayers))
        {
            // If what we hit can be damaged, highlight
            if (hit.collider.GetComponent<IDamageable>() != null)
            {
                crosshairImage.color = highlightColor;
                return;
            }
        }
        
        // If we got here, we didn't hit a damageable
        crosshairImage.color = defaultColor;
    }

    private void DebugCrosshair(Ray ray)
    {
        Debug.DrawRay(_cam.position, _cam.forward * 10, Color.cadetBlue);
        if (Physics.Raycast(ray, out RaycastHit hit, maxCheckRange))
            Debug.Log(hit.collider.name);
    }
}
