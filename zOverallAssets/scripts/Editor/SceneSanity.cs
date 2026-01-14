// Editor folder
using UnityEditor;
using UnityEngine;

public class SceneSanity : MonoBehaviour
{
    [MenuItem("Tools/Check Scene Sanity")]
    static void Check()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (HasLoop(t))
                Debug.LogError($"Loop found: {t.GetHierarchy()}");
            foreach (var c in t.GetComponents<Component>())
                if (c == null)
                    Debug.LogError($"Missing script on {t.GetHierarchy()}");
        }
        Debug.Log("Scene scan complete");
    }

    static bool HasLoop(Transform root)
    {
        var p = root.parent;
        while (p)
        {
            if (p == root) return true;
            p = p.parent;
        }
        return false;
    }
}

