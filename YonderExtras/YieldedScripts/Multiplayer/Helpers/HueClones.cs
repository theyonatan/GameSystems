#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ParrelSync;

[InitializeOnLoad]
public static class HueClones
{
    // Very subtle, I chose colors with this website https://rgbcolorpicker.com/0-1
    static readonly Color CloneHue = new (0.851f, 0f, 1f  , 0.05f);

    static HueClones()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.delayCall += RepaintAll;
    }

    static void OnSceneGUI(SceneView view)
    {
        DrawOverlay();
    }

    static void DrawOverlay()
    {
        if (!ClonesManager.IsClone())
            return;

        Handles.BeginGUI();
        EditorGUI.DrawRect(
            new Rect(0, 0, Screen.width, Screen.height),
            CloneHue
        );
        Handles.EndGUI();
    }

    static void RepaintAll()
    {
        SceneView.RepaintAll();
    }
}
#endif