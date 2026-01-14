using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using UnityEditor.SceneManagement;

class SceneDebugBuildLogger : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    private static string logPath = "BuildLogs/SceneScanLog.txt";

    public void OnPreprocessBuild(BuildReport report)
    {
        Directory.CreateDirectory("BuildLogs");
        using (StreamWriter writer = new StreamWriter(logPath, false))
        {
            writer.WriteLine("==== SCENE DEBUG START ====");
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                writer.WriteLine($"Scene: {scene.path}");

                var openedScene = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                foreach (var root in openedScene.GetRootGameObjects())
                {
                    try
                    {
                        LogGameObjectRecursive(root, 0, writer);
                    }
                    catch (System.Exception ex)
                    {
                        writer.WriteLine($"[ERROR] Exception on {root.name}: {ex.Message}");
                    }
                }
            }
            writer.WriteLine("==== SCENE DEBUG END ====");
        }

        Debug.Log($"Scene scan written to {Path.GetFullPath(logPath)}");
    }

    void LogGameObjectRecursive(GameObject go, int indent, StreamWriter writer)
    {
        var prefix = new string(' ', indent * 2);
        writer.WriteLine($"{prefix}- {go.name}");

        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null)
                writer.WriteLine($"{prefix}  * Missing component on {go.name}");
            else
                writer.WriteLine($"{prefix}  * Component: {comp.GetType()}");
        }

        foreach (Transform child in go.transform)
            LogGameObjectRecursive(child.gameObject, indent + 1, writer);
    }
}
