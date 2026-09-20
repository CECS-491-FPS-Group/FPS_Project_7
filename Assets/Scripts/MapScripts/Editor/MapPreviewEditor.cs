using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapPreview))]
/// <summary>Generate button for MapPreview.</summary>
public class MapPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapPreview preview = (MapPreview)target;

        if (DrawDefaultInspector() && preview.autoUpdate)
        {
            preview.DrawMapInEditor();
        }

        if (GUILayout.Button("Generate"))
        {
            preview.DrawMapInEditor();
        }
    }
}
