using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the properties the generator has to hold: the layout is deterministic for a seed,
/// carved roads actually come out flat in the chunk heightmaps, and terrain away from the
/// layout is untouched.
/// </summary>
public static class MapValidation
{
    const string MeshSettingsPath = "Assets/Terrain Assets/New Mesh Settings 1.asset";
    const string HeightSettingsPath = "Assets/Terrain Assets/New Height Map Settings 1.asset";
    const string WorldSettingsPath = "Assets/Terrain Assets/New World Settings.asset";

    [MenuItem("Tools/Map/Validate Layout")]
    public static void Run()
    {
        StringBuilder report = new StringBuilder();
        bool passed = true;

        MeshSettings meshSettings = AssetDatabase.LoadAssetAtPath<MeshSettings>(MeshSettingsPath);
        HeightMapSettings heightSettings = AssetDatabase.LoadAssetAtPath<HeightMapSettings>(HeightSettingsPath);
        WorldSettings worldSettings = AssetDatabase.LoadAssetAtPath<WorldSettings>(WorldSettingsPath);

        if (meshSettings == null || heightSettings == null || worldSettings == null)
        {
            Debug.LogError("[MapValidation] Could not load settings assets.");
            EditorApplication.Exit(1);
            return;
        }

        int seed = worldSettings.editorSeed;
        float meshWorldSize = meshSettings.meshWorldSize;
        WorldFalloff falloff = WorldFalloff.From(worldSettings, meshWorldSize);
        Rect worldRect = worldSettings.WorldRect(meshWorldSize);

        report.AppendLine("chunk size      : " + meshWorldSize.ToString("F1") + " m");
        report.AppendLine("world size      : " + worldSettings.WorldSize(meshWorldSize).ToString("F0") + " m");
        report.AppendLine("seed            : " + seed);

        TerrainHeightField field = new TerrainHeightField(heightSettings, falloff, seed, meshSettings.meshScale);
        WorldLayout layout = WorldLayout.Build(seed, worldRect, field, worldSettings.layoutSettings);

        report.AppendLine("points of interest: " + layout.PointsOfInterest.Length);
        report.AppendLine("road segments     : " + layout.Roads.SegmentCount);
        report.AppendLine("building plots    : " + layout.Plots.Length);

        if (layout.PointsOfInterest.Length < 2)
        {
            report.AppendLine("FAIL: fewer than 2 points of interest placed");
            passed = false;
        }

        if (layout.Roads.SegmentCount == 0)
        {
            report.AppendLine("FAIL: no road segments generated");
            passed = false;
        }

        // Determinism: the same seed must produce a bit-identical layout.
        WorldLayout repeat = WorldLayout.Build(seed, worldRect, field, worldSettings.layoutSettings);
        bool identical = repeat.Roads.SegmentCount == layout.Roads.SegmentCount
            && repeat.Plots.Length == layout.Plots.Length
            && repeat.PointsOfInterest.Length == layout.PointsOfInterest.Length;

        if (identical)
        {
            for (int i = 0; i < layout.Roads.Points.Length; i++)
            {
                if (layout.Roads.Points[i] != repeat.Roads.Points[i])
                {
                    identical = false;
                    break;
                }
            }
        }

        report.AppendLine("deterministic     : " + (identical ? "yes" : "NO"));
        if (!identical)
        {
            report.AppendLine("FAIL: rebuilding the same seed produced a different layout");
            passed = false;
        }

        // A different seed must produce a different layout, or the seed is not wired through.
        WorldLayout other = WorldLayout.Build(seed + 1, worldRect, field, worldSettings.layoutSettings);
        bool differs = other.Roads.Points.Length != layout.Roads.Points.Length;
        if (!differs)
        {
            for (int i = 0; i < layout.Roads.Points.Length; i++)
            {
                if (layout.Roads.Points[i] != other.Roads.Points[i])
                {
                    differs = true;
                    break;
                }
            }
        }

        report.AppendLine("seed changes map  : " + (differs ? "yes" : "NO"));
        if (!differs)
        {
            report.AppendLine("FAIL: a different seed produced the same layout");
            passed = false;
        }

        passed &= ValidateCarving(report, meshSettings, heightSettings, worldSettings, layout, seed);

        Debug.Log("[MapValidation]\n" + report);

        if (!passed)
        {
            Debug.LogError("[MapValidation] FAILED");
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    static bool ValidateCarving(StringBuilder report, MeshSettings meshSettings, HeightMapSettings heightSettings,
        WorldSettings worldSettings, WorldLayout layout, int seed)
    {
        bool passed = true;
        float meshWorldSize = meshSettings.meshWorldSize;
        int size = meshSettings.numVertsPerLine;

        Dictionary<Vector2, HeightMap> carvedChunks = new Dictionary<Vector2, HeightMap>();
        Dictionary<Vector2, HeightMap> bareChunks = new Dictionary<Vector2, HeightMap>();

        float worstRoadError = 0f;
        int sampled = 0;

        Vector3[] roadPoints = layout.Roads.Points;
        int stride = Mathf.Max(1, roadPoints.Length / 200);

        for (int i = 0; i < roadPoints.Length; i += stride)
        {
            Vector3 point = roadPoints[i];
            Vector2 worldXZ = new Vector2(point.x, point.z);
            Vector2 coord = new Vector2(
                Mathf.Round(worldXZ.x / meshWorldSize),
                Mathf.Round(worldXZ.y / meshWorldSize));

            if (coord.x < worldSettings.ChunkCoordMin || coord.x > worldSettings.ChunkCoordMax ||
                coord.y < worldSettings.ChunkCoordMin || coord.y > worldSettings.ChunkCoordMax)
            {
                continue;
            }

            HeightMap carved;
            if (!carvedChunks.TryGetValue(coord, out carved))
            {
                HeightMapContext context = HeightMapContext.ForChunk(coord, meshSettings, worldSettings, seed, layout);
                carved = HeightMapGenerator.GenerateHeightMap(size, size, heightSettings, context);
                carvedChunks[coord] = carved;
            }

            HeightMapSampler sampler = new HeightMapSampler(carved.values, size, meshWorldSize, coord * meshWorldSize);
            float error = Mathf.Abs(sampler.SampleHeight(worldXZ) - point.y);
            worstRoadError = Mathf.Max(worstRoadError, error);
            sampled++;
        }

        report.AppendLine("road points tested: " + sampled);
        report.AppendLine("max road error    : " + worstRoadError.ToString("F3") + " m");

        if (sampled == 0)
        {
            report.AppendLine("FAIL: no road points fell inside the world grid");
            passed = false;
        }
        else if (worstRoadError > 1.0f)
        {
            report.AppendLine("FAIL: carved road surface deviates from the graded centreline by more than 1 m");
            passed = false;
        }

        // Terrain outside every layout influence must be byte-for-byte what the bare generator produces.
        float influence = Mathf.Max(layout.MaxRoadInfluence, layout.MaxPlotInfluence);
        LayoutCarver carver = new LayoutCarver(layout);
        int untouchedChecked = 0;
        int untouchedMismatch = 0;

        foreach (KeyValuePair<Vector2, HeightMap> entry in carvedChunks)
        {
            Vector2 coord = entry.Key;
            HeightMap bare;
            if (!bareChunks.TryGetValue(coord, out bare))
            {
                HeightMapContext bareContext = HeightMapContext.ForChunk(coord, meshSettings, worldSettings, seed, null);
                bare = HeightMapGenerator.GenerateHeightMap(size, size, heightSettings, bareContext);
                bareChunks[coord] = bare;
            }

            HeightMapSampler sampler = new HeightMapSampler(bare.values, size, meshWorldSize, coord * meshWorldSize);

            for (int x = 1; x < size - 1; x += 7)
            {
                for (int y = 1; y < size - 1; y += 7)
                {
                    Vector2 world = sampler.IndexToWorld(x, y);

                    float mask;
                    carver.Apply(world, 0f, out mask);
                    if (mask > 0f)
                    {
                        continue;
                    }

                    untouchedChecked++;
                    if (!Mathf.Approximately(entry.Value.values[x, y], bare.values[x, y]))
                    {
                        untouchedMismatch++;
                    }
                }
            }
        }

        // Surface mask: it drives the road paint, so it has to be present, in range,
        // saturated on the centreline, and zero on open ground.
        int maskOutOfRange = 0;
        int maskMissing = 0;
        int maskCovered = 0;
        int maskTotal = 0;

        foreach (KeyValuePair<Vector2, HeightMap> entry in carvedChunks)
        {
            float[,] mask = entry.Value.surfaceMask;
            if (mask == null)
            {
                maskMissing++;
                continue;
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float value = mask[x, y];
                    maskTotal++;
                    if (value < 0f || value > 1f)
                    {
                        maskOutOfRange++;
                    }
                    if (value > 0f)
                    {
                        maskCovered++;
                    }
                }
            }
        }

        float coverage = maskTotal == 0 ? 0f : maskCovered / (float)maskTotal;
        report.AppendLine("mask coverage     : " + (coverage * 100f).ToString("F1") + "% of vertices carved");

        if (maskMissing > 0)
        {
            report.AppendLine("FAIL: " + maskMissing + " chunk(s) generated no surface mask despite having a layout");
            passed = false;
        }

        if (maskOutOfRange > 0)
        {
            report.AppendLine("FAIL: " + maskOutOfRange + " mask values outside 0..1");
            passed = false;
        }

        if (maskTotal > 0 && coverage <= 0f)
        {
            report.AppendLine("FAIL: surface mask is empty, so roads will not be painted");
            passed = false;
        }

        if (coverage > 0.6f)
        {
            report.AppendLine("FAIL: surface mask covers " + (coverage * 100f).ToString("F0") + "% of the terrain, which means the carve is far too wide");
            passed = false;
        }

        float weakestCentreline = 1f;
        for (int i = 0; i < roadPoints.Length; i += stride)
        {
            Vector3 point = roadPoints[i];
            Vector2 worldXZ = new Vector2(point.x, point.z);
            Vector2 coord = new Vector2(
                Mathf.Round(worldXZ.x / meshWorldSize),
                Mathf.Round(worldXZ.y / meshWorldSize));

            HeightMap carved;
            if (!carvedChunks.TryGetValue(coord, out carved) || carved.surfaceMask == null)
            {
                continue;
            }

            HeightMapSampler sampler = new HeightMapSampler(carved.values, size, meshWorldSize, coord * meshWorldSize);
            Vector2 index = sampler.WorldToIndex(worldXZ);
            int ix = Mathf.Clamp(Mathf.RoundToInt(index.x), 0, size - 1);
            int iy = Mathf.Clamp(Mathf.RoundToInt(index.y), 0, size - 1);
            weakestCentreline = Mathf.Min(weakestCentreline, carved.surfaceMask[ix, iy]);
        }

        report.AppendLine("weakest centreline: " + weakestCentreline.ToString("F3") + " (1 = fully painted)");

        if (sampled > 0 && weakestCentreline < 0.9f)
        {
            report.AppendLine("FAIL: road centreline is not fully masked, so the paint will not line up with the carve");
            passed = false;
        }

        report.AppendLine("untouched checked : " + untouchedChecked + " (mismatches: " + untouchedMismatch + ")");
        report.AppendLine("layout influence  : " + influence.ToString("F1") + " m");

        if (untouchedMismatch > 0)
        {
            report.AppendLine("FAIL: terrain outside the layout influence was modified");
            passed = false;
        }

        return passed;
    }
}
