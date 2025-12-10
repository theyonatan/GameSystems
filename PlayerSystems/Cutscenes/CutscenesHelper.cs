using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

public class CutscenesHelper
{
    public static CutsceneCamera CurrentCutsceneCamera;
    
    public static Dictionary<String, CutsceneCamera> GatherCutsceneCameras()
    {
        // Gather all cutscenes in the scene
        Dictionary<string, CutsceneCamera> cutscenesCameras = Object.FindObjectsByType<CutsceneCamera>(FindObjectsSortMode.None)
            .ToDictionary(cutsceneCam => cutsceneCam.CutsceneCameraName, cutsceneCam => cutsceneCam);

        // verify cutscenes
        if (cutscenesCameras.Count == 0)
        {
            Debug.LogError("No cutscenes found in scene!");
            return new Dictionary<string, CutsceneCamera>();
        }

        return cutscenesCameras;
    }

    /// <summary>
    /// Gives priority to a cutscene camera.
    /// this means it will be the active camera, a blend/transition will start from the previous camera to this one.
    /// </summary>
    /// <param name="cinemachineCamera">New active camera</param>
    public static void GiveCameraPriority(CinemachineCamera cinemachineCamera)
    {
        var availableCameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        if (availableCameras.Length == 0)
        {
            Debug.LogError($"No Cinemachine Cameras found in scene! how can {cinemachineCamera.name} exist?");
            return;
        }
        
        // lower priority of all cameras
        foreach (var camera in availableCameras)
            camera.Priority = 1;
        
        cinemachineCamera.Priority = 10;
    }

    /// <summary>
    /// in the future this will be replaced when we have different types of cameras.
    /// </summary>
    public static Transform GetActive() => Camera.main?.transform;
    
    
    // todo: cutscene helper editor script possibly sits on the story object that has input boxes, allows inputting name of camera, story object and story character, 3 select buttons which select the corresponding thing in the editor.
}
