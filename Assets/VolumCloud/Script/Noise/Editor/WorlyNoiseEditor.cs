using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorlyNoise))]
public class WorlyNoiseEditor : Editor
{
    private WorlyNoise instance;
    private void OnEnable() {
        instance = target as WorlyNoise;
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        GUILayout.Space(30);
        if (GUILayout.Button("Generate", GUILayout.Height(30))) {
            instance.Generate();
        }

        GUILayout.Space(30);
        if (GUILayout.Button("SaveToDisk", GUILayout.Height(30))) {
            instance.SaveToDisk();
        }
    }
}
