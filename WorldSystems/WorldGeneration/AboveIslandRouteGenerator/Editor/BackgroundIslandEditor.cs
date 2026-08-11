#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BackgroundIsland))]
[CanEditMultipleObjects]
public sealed class BackgroundIslandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Placement Bounds should surround the complete visible silhouette, including trees and tall rocks. " +
            "A BackgroundIsland may share this object with AboveIsland in 1.5. Visual Cost is only a performance-budget unit: 1 is normal and it never changes spawn odds.",
            MessageType.Info);

        if (GUILayout.Button("Add Placement Bounds Box"))
        {
            GameObject lastCreated = null;
            for (int i = 0; i < targets.Length; i++)
                lastCreated = AddPlacementBounds((BackgroundIsland)targets[i]);

            if (targets.Length == 1 && lastCreated != null)
            {
                Selection.activeGameObject = lastCreated;
                EditorGUIUtility.PingObject(lastCreated);
            }
        }
    }

    private static GameObject AddPlacementBounds(BackgroundIsland island)
    {
        GameObject boundsObject = new GameObject("Placement Bounds");
        Undo.RegisterCreatedObjectUndo(boundsObject, "Add Background Island Placement Bounds");
        boundsObject.transform.SetParent(island.transform, false);

        BoxCollider collider = boundsObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.enabled = false;

        SerializedObject islandObject = new SerializedObject(island);
        islandObject.Update();
        SerializedProperty boundsArray = islandObject.FindProperty("placementBounds");
        int index = boundsArray.arraySize;
        boundsArray.InsertArrayElementAtIndex(index);
        boundsArray.GetArrayElementAtIndex(index).objectReferenceValue = collider;
        islandObject.ApplyModifiedProperties();
        return boundsObject;
    }
}
#endif
