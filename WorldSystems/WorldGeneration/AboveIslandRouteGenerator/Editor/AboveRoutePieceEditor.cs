#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AboveRoutePiece), true)]
[CanEditMultipleObjects]
public sealed class AboveRoutePieceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Socket forward points in the direction players travel. Placement Bounds are dedicated disabled BoxColliders that reserve visual space during generation.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Socket References"))
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    AboveRoutePiece piece = (AboveRoutePiece)targets[i];
                    Undo.RecordObject(piece, "Refresh Island Socket References");
                    piece.RefreshPrefabReferences();
                    EditorUtility.SetDirty(piece);
                }
            }

            if (GUILayout.Button("Add Placement Bounds Box"))
            {
                GameObject lastCreated = null;
                for (int i = 0; i < targets.Length; i++)
                    lastCreated = AddPlacementBounds((AboveRoutePiece)targets[i]);

                if (targets.Length == 1 && lastCreated != null)
                {
                    Selection.activeGameObject = lastCreated;
                    EditorGUIUtility.PingObject(lastCreated);
                }
            }
        }
    }

    private static GameObject AddPlacementBounds(AboveRoutePiece piece)
    {
        GameObject boundsObject = new GameObject("Placement Bounds");
        Undo.RegisterCreatedObjectUndo(boundsObject, "Add Island Placement Bounds");
        boundsObject.transform.SetParent(piece.transform, false);

        BoxCollider collider = boundsObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.enabled = false;

        SerializedObject pieceObject = new SerializedObject(piece);
        pieceObject.Update();
        SerializedProperty boundsArray = pieceObject.FindProperty("placementBounds");
        int index = boundsArray.arraySize;
        boundsArray.InsertArrayElementAtIndex(index);
        boundsArray.GetArrayElementAtIndex(index).objectReferenceValue = collider;
        pieceObject.ApplyModifiedProperties();

        return boundsObject;
    }
}
#endif
