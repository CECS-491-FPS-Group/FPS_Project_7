using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    static readonly Vector2[] plotCorners = new Vector2[4];

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TerrainGenerator generator = (TerrainGenerator)target;

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Generation runs on background threads and is only available in play mode. Use MapPreview to iterate on settings.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Seed", generator.Seed.ToString());
        EditorGUILayout.LabelField("World Size", generator.WorldSize.ToString("F0") + " m");
        EditorGUILayout.LabelField("Pending Jobs", GenerationScheduler.Outstanding.ToString());

        WorldLayout layout = generator.Layout;
        if (layout != null)
        {
            EditorGUILayout.LabelField("Points of Interest", layout.PointsOfInterest.Length.ToString());
            EditorGUILayout.LabelField("Road Segments", layout.Roads.SegmentCount.ToString());
            EditorGUILayout.LabelField("Building Plots", layout.Plots.Length.ToString());
        }

        Rect progressRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        EditorGUI.ProgressBar(progressRect, generator.GenerationProgress, generator.IsGenerated ? "Complete" : "Generating");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate"))
            {
                generator.Generate();
            }

            if (GUILayout.Button("Random Seed"))
            {
                generator.Generate(Random.Range(int.MinValue, int.MaxValue));
            }

            if (GUILayout.Button("Clear"))
            {
                generator.Clear();
            }
        }

        Repaint();
    }

    void OnSceneGUI()
    {
        TerrainGenerator generator = (TerrainGenerator)target;
        WorldLayout layout = generator.Layout;

        if (layout == null)
        {
            return;
        }

        DrawWorldBounds(layout);
        DrawRoads(layout);
        DrawPlots(layout);
        DrawPointsOfInterest(layout, generator);
    }

    static void DrawWorldBounds(WorldLayout layout)
    {
        Rect bounds = layout.WorldBounds;
        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(bounds.xMin, 0f, bounds.yMin),
            new Vector3(bounds.xMax, 0f, bounds.yMin),
            new Vector3(bounds.xMax, 0f, bounds.yMax),
            new Vector3(bounds.xMin, 0f, bounds.yMax),
            new Vector3(bounds.xMin, 0f, bounds.yMin));
    }

    static void DrawRoads(WorldLayout layout)
    {
        RoadNetwork roads = layout.Roads;
        if (roads == null)
        {
            return;
        }

        Handles.color = new Color(1f, 0.75f, 0.2f, 0.9f);

        for (int i = 0; i < roads.SegmentCount; i++)
        {
            Vector3 a, b;
            roads.GetSegment(i, out a, out b);
            Handles.DrawAAPolyLine(4f, a + Vector3.up * 0.5f, b + Vector3.up * 0.5f);
        }
    }

    static void DrawPlots(WorldLayout layout)
    {
        Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);

        for (int i = 0; i < layout.Plots.Length; i++)
        {
            BuildingPlot plot = layout.Plots[i];
            plot.GetCorners(plotCorners);

            Vector3[] corners = new Vector3[5];
            for (int c = 0; c < 4; c++)
            {
                corners[c] = new Vector3(plotCorners[c].x, plot.Height + 0.5f, plotCorners[c].y);
            }
            corners[4] = corners[0];

            Handles.DrawAAPolyLine(3f, corners);
        }
    }

    static void DrawPointsOfInterest(WorldLayout layout, TerrainGenerator generator)
    {
        Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);

        for (int i = 0; i < layout.PointsOfInterest.Length; i++)
        {
            Vector2 poi = layout.PointsOfInterest[i];
            Vector3 position = new Vector3(poi.x, 0f, poi.y);

            float height;
            if (generator.TrySampleHeight(position, out height))
            {
                position.y = height;
            }

            Handles.DrawWireDisc(position + Vector3.up * 0.5f, Vector3.up, 8f);
        }
    }
}
