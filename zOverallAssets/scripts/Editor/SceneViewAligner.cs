using UnityEngine;
using UnityEditor;

public class SceneViewAligner : MonoBehaviour
{
    [MenuItem("Tools/Above/Utilities/Scene View/Align Scene View to Selected Object", false, 500)]
    public static void AlignSceneToSelected()
    {
        if (Selection.activeTransform == null)
        {
            Debug.LogWarning("No object selected!");
            return;
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            // This aligns the Scene view directly to the object's transform
            sceneView.AlignViewToObject(Selection.activeTransform);
            sceneView.Repaint(); // Refresh the view
        }
    }
}