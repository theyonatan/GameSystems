using UnityEditor;
using UnityEngine;

public class CheckForEditorScripts : MonoBehaviour
{
    [MenuItem("Tools/Check for Editor Scripts in Scene")]
    static void CheckForEditorScriptsFunc()
    {
        foreach (var comp in GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (comp == null) continue;
            var asm = comp.GetType().Assembly.FullName;
            if (asm.Contains("Editor"))
                Debug.LogWarning($"Editor script on {comp.gameObject.name}", comp.gameObject);
        }
    }

    [MenuItem("Tools/Find InputAction by GUID")]
    static void FindInputAsset()
    {
        string guid = "ca9f5fa95ffab41fb9a615ab714db018";
        string path = AssetDatabase.GUIDToAssetPath(guid);
        Debug.Log("InputAction path: " + path);
        var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
        Selection.activeObject = obj;
    }
}
