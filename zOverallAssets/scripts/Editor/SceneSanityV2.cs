// Assets/Editor/SceneSanityV2.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class SceneSanityV2
{
    [MenuItem("Tools/Check Scene Sanity (v2)")]
    static void Check()
    {
        int missing = 0, loops = 0, deep = 0, maxDepth = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // ----- missing scripts -----
            foreach (var c in t.GetComponents<Component>())
                if (c == null)
                {
                    Debug.LogError($"Missing script ➜ {t.GetHierarchy()}");
                    missing++;
                }

            // ----- parent-loop check -----
            if (HasLoop(t))
            {
                Debug.LogError($"Hierarchy loop ➜ {t.GetHierarchy()}");
                loops++;
            }

            // ----- depth check -----
            int d = Depth(t);
            maxDepth = Mathf.Max(maxDepth, d);
            if (d > 128)   // Unity’s recursion limit is ~256, play safe at 128
            {
                Debug.LogError($"Depth {d} ➜ {t.GetHierarchy()}");
                deep++;
            }
        }

        Debug.Log($"Scene scan complete · Missing:{missing} · Loops:{loops} · Deep:{deep} · MaxDepth:{maxDepth}");
    }

    static bool HasLoop(Transform root)
    {
        var seen = new HashSet<Transform>();
        var p = root;
        while (p)
        {
            if (!seen.Add(p)) return true;
            p = p.parent;
        }
        return false;
    }

    static int Depth(Transform t)
    {
        int d = 0;
        while (t.parent) { d++; t = t.parent; }
        return d;
    }

    public static string GetHierarchy(this Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
