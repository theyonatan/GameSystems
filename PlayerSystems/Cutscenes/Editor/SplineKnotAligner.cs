#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(KnotAligner))]
public class SplineKnotAligner : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        // MoveKnot moveKnot = (MoveKnot)target;
        
        if (GUILayout.Button("Move Knot to SceneView Camera"))
            MoveSelectedKnotToCamera();
    }
    public void MoveSelectedKnotToCamera()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Move Knot To Camera: run this in Edit mode, not Play mode.");
            return;
        }
        Debug.Log("Moving selected knot to Sceneview Camera");
        
        // --------------------------
        // get active selected knot
        // --------------------------
        List<SplineInfo> splineInfos = new List<SplineInfo>();
        
        // get selected spline
        var activeObject = Selection.activeGameObject;
        if (!activeObject) return;
        var splineContainer = activeObject.GetComponent<SplineContainer>();
        if (!splineContainer) return;
        var availableSplines = splineContainer.Splines;

        // create list of spline infos (there should only be one, in my game I will never make more per object)
        for (int i = 0; i < availableSplines.Count; i++)
        {
            splineInfos.Add(new SplineInfo(splineContainer, i));
        }

        // check for the active knot
        var active = SplineSelection.GetActiveElement(splineInfos);
        if (active == null)
        {
            Debug.LogWarning("No spline selected!");
            return;
        }
        var splineInfo = active.SplineInfo.Spline;
        
        var knot = splineInfo[active.KnotIndex];
        
        // --------------------------
        // move selected knot to camera
        // --------------------------
        var sceneView = SceneView.lastActiveSceneView;
        if (!sceneView) return;
        
        var sceneViewCamera = sceneView.camera;
        if (!sceneViewCamera) return;
        
        knot.Position = sceneViewCamera.transform.position;
        knot.Rotation = sceneViewCamera.transform.rotation;
        
        // re-assign knot
        Undo.RecordObject(splineContainer, "Move Knot to SceneView Camera");
        splineContainer.Spline.SetKnot(active.KnotIndex, knot);
        EditorUtility.SetDirty(splineContainer);
    }
}
