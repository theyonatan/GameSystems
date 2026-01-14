using UnityEditor;
using UnityEngine;
using System.IO;

public class SceneAssetInspector
{
    [MenuItem("Tools/Log Raw Scene File References")]
    static void LogSceneReferences()
    {
        string path = "Assets/GAMES/FallingRuins/Shooter.unity";
        var text = File.ReadAllText(path);
        File.WriteAllText("SceneRawLog.txt", text);
        Debug.Log("Raw scene content dumped to SceneRawLog.txt");
    }
}
