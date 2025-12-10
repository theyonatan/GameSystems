using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Cutscene : MonoBehaviour
{
    public string CutsceneId;
    public List<CutsceneCamera> CutsceneCameras = new ();
    
    public void ConfigSpawnedCamera(Transform newCamera)
    {
        newCamera.SetParent(transform);
        CutsceneCameras.Add(newCamera.GetComponent<CutsceneCamera>());
    }

    private void Awake()
    {
        CutsceneCameras = GetComponentsInChildren<CutsceneCamera>().ToList();
    }
}
