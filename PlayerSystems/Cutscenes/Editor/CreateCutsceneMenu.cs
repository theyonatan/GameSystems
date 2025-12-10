#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class CreateCutsceneMenu
{
    [MenuItem("GameObject/New Cutscene", false, 20)]
    static void CreateCutscene()
    {
        var cutscenePrefab = Resources.Load<GameObject>("Cutscene");
        if (!cutscenePrefab)
        {
            Debug.LogError("Cutscene Prefab not found in Resources!");
            return;
        }
        
        var cutsceneInstance = PrefabUtility.InstantiatePrefab(cutscenePrefab) as GameObject;
        if (!cutsceneInstance)
        {
            Debug.LogError("Could not instantiate Cutscene Prefab!");
            return;
        }
        
        cutsceneInstance.transform.position = Vector3.zero;
        Selection.activeObject = cutsceneInstance;
    }
}
