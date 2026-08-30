#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RouteConfiguration))]
public sealed class RouteConfigurationEditor : Editor
{
    private SerializedProperty routeId;
    private SerializedProperty displayName;
    private SerializedProperty generation;
    private SerializedProperty rhythm;
    private SerializedProperty routeShape;
    private SerializedProperty detours;
    private SerializedProperty biomePhases;
    private SerializedProperty clusterPhases;
    private SerializedProperty islandPrefabs;
    private SerializedProperty connectionPrefabs;
    private SerializedProperty specialIslands;

    private bool showGeneration = true;

    private void OnEnable()
    {
        routeId = serializedObject.FindProperty("routeId");
        displayName = serializedObject.FindProperty("displayName");
        generation = serializedObject.FindProperty("generation");
        rhythm = serializedObject.FindProperty("rhythm");
        routeShape = serializedObject.FindProperty("routeShape");
        detours = serializedObject.FindProperty("detours");
        biomePhases = serializedObject.FindProperty("biomePhases");
        clusterPhases = serializedObject.FindProperty("clusterPhases");
        islandPrefabs = serializedObject.FindProperty("islandPrefabs");
        connectionPrefabs = serializedObject.FindProperty("connectionPrefabs");
        specialIslands = serializedObject.FindProperty("specialIslands");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(routeId);
        EditorGUILayout.PropertyField(displayName);

        if (string.IsNullOrWhiteSpace(routeId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Route ID cannot be empty. It will later be stored in Wind.",
                MessageType.Error);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Route Start and Generated Parent are scene-specific and remain " +
            "on the IslandRouteGenerator. This asset stores everything else.",
            MessageType.Info);

        showGeneration = EditorGUILayout.Foldout(
            showGeneration,
            "Generation",
            true);

        if (showGeneration)
            DrawGenerationWithoutSceneAnchors();

        EditorGUILayout.PropertyField(rhythm, true);
        EditorGUILayout.PropertyField(routeShape, true);
        EditorGUILayout.PropertyField(detours, true);
        EditorGUILayout.PropertyField(biomePhases, true);
        EditorGUILayout.PropertyField(clusterPhases, true);
        EditorGUILayout.PropertyField(islandPrefabs, true);
        EditorGUILayout.PropertyField(connectionPrefabs, true);
        EditorGUILayout.PropertyField(specialIslands, true);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGenerationWithoutSceneAnchors()
    {
        EditorGUI.indentLevel++;

        SerializedProperty child = generation.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) &&
               !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;

            if (child.name == "RouteStart" ||
                child.name == "GeneratedParent")
            {
                continue;
            }

            EditorGUILayout.PropertyField(child, true);
        }

        EditorGUI.indentLevel--;
    }
}
#endif
