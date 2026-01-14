using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    private float _deltaTime = 0.0f;
    private GUIStyle _style;
    private Rect _rect;

    private void Start()
    {
        int w = Screen.width, h = Screen.height;
        _rect = new Rect(10, 10, w, h * 2 / 100);
        _style = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = h * 2 / 50,
            normal = { textColor = Color.white }
        };
    }

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        float fps = 1.0f / _deltaTime;
        string text = $"FPS: {fps:F1}";
        GUI.Label(_rect, text, _style);
    }
}