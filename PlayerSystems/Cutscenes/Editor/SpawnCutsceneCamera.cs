#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[CustomEditor(typeof(Cutscene))]
public class SpawnCutsceneCamera : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        Cutscene cutscene = (Cutscene)target;

        if (GUILayout.Button("Spawn new Cutscene Camera"))
        {
            var newCam = SpawnNewaCutsceneCamera("CutsceneCamera");
            if (!newCam)
                return;
            
            SetCutsceneCameraPositionAsSceneView(newCam);
            cutscene.ConfigSpawnedCamera(newCam);
        }

        if (GUILayout.Button("Spawn new Trail Camera"))
        {
            var newCam = SpawnNewaCutsceneCamera("TrailCamera");
            if (!newCam)
                return;
            
            ResetCutsceneCameraPosition(newCam);
            cutscene.ConfigSpawnedCamera(newCam);
        }
    }

    private Transform SpawnNewaCutsceneCamera(string cameraType)
    {
        Debug.Log($"Spawning New Cutscene Camera of type {cameraType}");
        
        // load camera prefab
        var cameraPrefab = Resources.Load<GameObject>(cameraType);
        if (!cameraPrefab)
        {
            Debug.LogError("Camera prefab not found in resources.");
            return null;
        }
        
        // spawn camera outside playmode
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
        
        // actually save it in the editor
        Undo.RegisterCreatedObjectUndo(instance, "Created Cutscene Camera");
        
        // setup new camera
        Selection.activeObject = instance;
        
        return instance.transform;
    }

    /// <summary>
    /// Regular cameras should spawn where the SceneView is
    /// </summary>
    private void SetCutsceneCameraPositionAsSceneView(Transform cameraTransform)
    {
        var sceneView = SceneView.lastActiveSceneView;
        var sceneCamera = sceneView.camera;
        
        cameraTransform.transform.position = sceneCamera.transform.position;
        cameraTransform.transform.rotation = sceneCamera.transform.rotation;
    }
    
    /// <summary>
    /// Trail cameras should spawn at world zero
    /// </summary>
    private void ResetCutsceneCameraPosition(Transform cameraTransform)
    {
        cameraTransform.position = Vector3.zero;
        cameraTransform.rotation = Quaternion.Euler(Vector3.zero);
    }
}
