using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class VersionUpdater
{
    static VersionUpdater()
    {
        string path = "Assets/OverallAssets/version verifier/RuinsVersion.asset";
        var versionAsset = AssetDatabase.LoadAssetAtPath<VersionTracker>(path);

        if (versionAsset == null)
        {
            versionAsset = ScriptableObject.CreateInstance<VersionTracker>();
            versionAsset.version = 1;
            AssetDatabase.CreateAsset(versionAsset, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[VersionUpdater] Created new version tracker");
        }
        else
        {
            versionAsset.version++;
            EditorUtility.SetDirty(versionAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VersionUpdater] Version incremented to {versionAsset.version}");
        }
    }
}