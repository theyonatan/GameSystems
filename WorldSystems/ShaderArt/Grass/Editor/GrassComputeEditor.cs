using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassComputeScript))]
public sealed class GrassComputeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var grass = (GrassComputeScript)target;
        EditorGUILayout.LabelField("Painted Points", grass.SetGrassPaintedDataList.Count.ToString("N0"));
        using (new EditorGUI.DisabledScope(Application.IsPlaying(grass.gameObject) || EditorUtility.IsPersistent(grass)))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Edit Height")) GrassPainterWindow.EditGrass(grass);
            if (GUILayout.Button("Add / Remove Grass")) GrassPainterWindow.EditGrass(grass, false);
            EditorGUILayout.EndHorizontal();
            if (!grass.enabled && GUILayout.Button("Enable Grass Preview"))
            {
                Undo.RecordObject(grass, "Enable Grass Preview");
                grass.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(grass);
            }
        }
        if (EditorUtility.IsPersistent(grass))
            EditorGUILayout.HelpBox("Double-click the island prefab in the Project tab, then select its Grass child to paint.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && !Application.IsPlaying(grass.gameObject) && grass.enabled && !EditorUtility.IsPersistent(grass))
            grass.Reset();
    }
}
