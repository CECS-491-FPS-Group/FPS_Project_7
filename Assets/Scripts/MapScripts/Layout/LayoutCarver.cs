using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the layout to terrain heights. One instance per chunk worker: the layout itself is
/// shared and read-only, but the query buffers are not, so they live here.
/// </summary>
public sealed class LayoutCarver
{
    readonly WorldLayout layout;
    readonly List<int> roadBuffer = new List<int>(32);
    readonly List<int> plotBuffer = new List<int>(16);

    public LayoutCarver(WorldLayout layout)
    {
        this.layout = layout;
    }

    /// <summary>
    /// Blends natural terrain toward road and pad heights.
    /// <paramref name="surfaceMask"/> is 1 on a fully carved surface and 0 on untouched terrain;
    /// the texture layer uses it to paint roads.
    /// </summary>
    public float Apply(Vector2 worldXZ, float height, out float surfaceMask)
    {
        surfaceMask = 0f;

        if (layout == null)
        {
            return height;
        }

        BuildingPlot[] plots = layout.Plots;
        if (plots.Length > 0)
        {
            layout.PlotGrid.Query(worldXZ, 0f, plotBuffer);

            for (int i = 0; i < plotBuffer.Count; i++)
            {
                BuildingPlot plot = plots[plotBuffer[i]];
                float distance = plot.DistanceOutside(worldXZ);

                if (distance >= plot.Shoulder)
                {
                    continue;
                }

                float blend = SmoothStep(0f, plot.Shoulder, distance);
                height = Mathf.Lerp(plot.Height, height, blend);
                surfaceMask = Mathf.Max(surfaceMask, 1f - blend);
            }
        }

        // Roads are applied last so they win wherever a pad overlaps one.
        RoadNetwork roads = layout.Roads;
        if (roads != null && roads.SegmentCount > 0)
        {
            RoadSample road = roads.Sample(worldXZ, 0f, roadBuffer);

            if (road.Hit && road.Distance < roads.MaxInfluence)
            {
                float blend = SmoothStep(roads.HalfWidth, roads.MaxInfluence, road.Distance);
                height = Mathf.Lerp(road.Height, height, blend);
                surfaceMask = Mathf.Max(surfaceMask, 1f - blend);
            }
        }

        return height;
    }

    static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
