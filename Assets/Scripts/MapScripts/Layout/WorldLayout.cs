using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global structure derived from the world seed: points of interest, the road network
/// connecting them, and the building pads beside those roads.
///
/// This layer must exist before any chunk heightmap is finalised, because a road flattens
/// the terrain it crosses. It is built once on the main thread and then read concurrently
/// by chunk workers through <see cref="LayoutCarver"/>.
/// </summary>
public sealed class WorldLayout
{
    public readonly Vector2[] PointsOfInterest;
    public readonly RoadNetwork Roads;
    public readonly BuildingPlot[] Plots;
    public readonly Rect WorldBounds;

    readonly SpatialGrid plotGrid;
    readonly float maxPlotInfluence;

    WorldLayout(Vector2[] pois, RoadNetwork roads, BuildingPlot[] plots, Rect worldBounds)
    {
        PointsOfInterest = pois;
        Roads = roads;
        Plots = plots;
        WorldBounds = worldBounds;

        float influence = 0f;
        for (int i = 0; i < plots.Length; i++)
        {
            influence = Mathf.Max(influence, plots[i].Radius);
        }

        maxPlotInfluence = influence;
        plotGrid = SpatialGrid.Build(plots.Length, index => plots[index].Bounds, worldBounds, Mathf.Max(influence * 2f, 16f));
    }

    public float MaxRoadInfluence
    {
        get { return Roads != null ? Roads.MaxInfluence : 0f; }
    }

    public float MaxPlotInfluence
    {
        get { return maxPlotInfluence; }
    }

    public SpatialGrid PlotGrid
    {
        get { return plotGrid; }
    }

    public static WorldLayout Build(int seed, Rect worldBounds, TerrainHeightField field, LayoutSettings settings)
    {
        DeterministicRandom rng = new DeterministicRandom(DeterministicRandom.Hash((uint)seed, 0x4C41594Fu, 0u, 0u));

        Vector2[] pois = PlacePointsOfInterest(ref rng, worldBounds, field, settings);
        RoadNetwork roads = BuildRoads(ref rng, pois, worldBounds, field, settings);
        BuildingPlot[] plots = PlacePlots(ref rng, pois, roads, field, settings);

        return new WorldLayout(pois, roads, plots, worldBounds);
    }

    const int GradeClampIterations = 4;

    // Each pass relaxes the terrain filters. A sparse map is recoverable; an empty one is not,
    // so the final pass accepts anything rather than returning a layout with no roads.
    static readonly float[] SlopeRelaxation = { 1f, 1.75f, 3f, 1000f };
    static readonly float[] SpacingRelaxation = { 1f, 0.85f, 0.65f, 0.45f };
    static readonly float[] HeightRelaxation = { 1f, 0.5f, 0.25f, 0f };

    static Vector2[] PlacePointsOfInterest(ref DeterministicRandom rng, Rect worldBounds, TerrainHeightField field, LayoutSettings settings)
    {
        Rect inner = Rect.MinMaxRect(
            worldBounds.xMin + settings.edgeInset,
            worldBounds.yMin + settings.edgeInset,
            worldBounds.xMax - settings.edgeInset,
            worldBounds.yMax - settings.edgeInset);

        if (inner.width <= 0f || inner.height <= 0f)
        {
            inner = worldBounds;
        }

        List<Vector2> accepted = new List<Vector2>(settings.poiCount);
        float slopeStep = Mathf.Max(1f, settings.roadHalfWidth);
        int attemptsPerPass = settings.poiCount * 250;

        for (int pass = 0; pass < SlopeRelaxation.Length && accepted.Count < settings.poiCount; pass++)
        {
            float slopeLimit = Mathf.Min(89f, settings.maxPoiSlopeDegrees * SlopeRelaxation[pass]);
            float minHeight = settings.minPoiHeight * HeightRelaxation[pass];
            float spacing = settings.minPoiSpacing * SpacingRelaxation[pass];
            float spacingSqr = spacing * spacing;

            for (int attempt = 0; attempt < attemptsPerPass && accepted.Count < settings.poiCount; attempt++)
            {
                Vector2 candidate = new Vector2(
                    rng.NextFloat(inner.xMin, inner.xMax),
                    rng.NextFloat(inner.yMin, inner.yMax));

                bool tooClose = false;
                for (int i = 0; i < accepted.Count; i++)
                {
                    if ((accepted[i] - candidate).sqrMagnitude < spacingSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                if (field.Height(candidate) < minHeight)
                {
                    continue;
                }

                if (field.SlopeDegrees(candidate, slopeStep) > slopeLimit)
                {
                    continue;
                }

                accepted.Add(candidate);
            }
        }

        if (accepted.Count < settings.poiCount)
        {
            Debug.LogWarning(string.Format(
                "[WorldLayout] Placed {0} of {1} points of interest. Lower minPoiSpacing or edgeInset, or raise maxPoiSlopeDegrees, if the road network looks sparse.",
                accepted.Count, settings.poiCount));
        }

        return accepted.ToArray();
    }

    static RoadNetwork BuildRoads(ref DeterministicRandom rng, Vector2[] pois, Rect worldBounds, TerrainHeightField field, LayoutSettings settings)
    {
        List<Vector3> points = new List<Vector3>();
        List<int> segmentA = new List<int>();
        List<int> segmentB = new List<int>();

        if (pois.Length >= 2)
        {
            List<int> edgeFrom = new List<int>();
            List<int> edgeTo = new List<int>();
            BuildRoadGraph(pois, settings.extraRoadLinks, edgeFrom, edgeTo);

            for (int e = 0; e < edgeFrom.Count; e++)
            {
                AppendRoad(ref rng, pois[edgeFrom[e]], pois[edgeTo[e]], field, settings, points, segmentA, segmentB);
            }
        }

        return new RoadNetwork(points.ToArray(), segmentA.ToArray(), segmentB.ToArray(),
            worldBounds, settings.roadHalfWidth, settings.roadShoulder);
    }

    /// <summary>Minimum spanning tree over the points of interest, plus the shortest unused links.</summary>
    static void BuildRoadGraph(Vector2[] pois, int extraLinks, List<int> edgeFrom, List<int> edgeTo)
    {
        int count = pois.Length;
        bool[] inTree = new bool[count];
        float[] bestCost = new float[count];
        int[] bestParent = new int[count];

        for (int i = 0; i < count; i++)
        {
            bestCost[i] = float.MaxValue;
            bestParent[i] = -1;
        }

        bestCost[0] = 0f;

        for (int iteration = 0; iteration < count; iteration++)
        {
            int next = -1;
            float nextCost = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!inTree[i] && bestCost[i] < nextCost)
                {
                    nextCost = bestCost[i];
                    next = i;
                }
            }

            if (next < 0)
            {
                break;
            }

            inTree[next] = true;

            if (bestParent[next] >= 0)
            {
                edgeFrom.Add(bestParent[next]);
                edgeTo.Add(next);
            }

            for (int i = 0; i < count; i++)
            {
                if (inTree[i])
                {
                    continue;
                }

                float cost = (pois[i] - pois[next]).sqrMagnitude;
                if (cost < bestCost[i])
                {
                    bestCost[i] = cost;
                    bestParent[i] = next;
                }
            }
        }

        for (int added = 0; added < extraLinks; added++)
        {
            int bestA = -1;
            int bestB = -1;
            float bestLength = float.MaxValue;

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    if (HasEdge(edgeFrom, edgeTo, a, b))
                    {
                        continue;
                    }

                    float length = (pois[a] - pois[b]).sqrMagnitude;
                    if (length < bestLength)
                    {
                        bestLength = length;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (bestA < 0)
            {
                break;
            }

            edgeFrom.Add(bestA);
            edgeTo.Add(bestB);
        }
    }

    static bool HasEdge(List<int> edgeFrom, List<int> edgeTo, int a, int b)
    {
        for (int i = 0; i < edgeFrom.Count; i++)
        {
            if ((edgeFrom[i] == a && edgeTo[i] == b) || (edgeFrom[i] == b && edgeTo[i] == a))
            {
                return true;
            }
        }

        return false;
    }

    static void AppendRoad(ref DeterministicRandom rng, Vector2 from, Vector2 to, TerrainHeightField field,
        LayoutSettings settings, List<Vector3> points, List<int> segmentA, List<int> segmentB)
    {
        Vector2 direction = to - from;
        float length = direction.magnitude;
        if (length < 1f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-direction.y, direction.x) / length;
        float jitter = length * settings.roadCurveJitter;

        Vector2[] control = new Vector2[4];
        control[0] = from;
        control[1] = from + direction * (1f / 3f) + perpendicular * rng.NextFloat(-jitter, jitter);
        control[2] = from + direction * (2f / 3f) + perpendicular * rng.NextFloat(-jitter, jitter);
        control[3] = to;

        int spans = control.Length - 1;
        int sampleCount = spans * settings.samplesPerRoadSpan + 1;

        Vector2[] centreline = new Vector2[sampleCount];
        float[] heights = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            centreline[i] = CatmullRom(control, t);
            heights[i] = field.Height(centreline[i]);
        }

        SmoothGrade(heights, settings.gradeSmoothingPasses);
        ClampGrade(centreline, heights, settings.maxRoadGradeDegrees);

        int baseIndex = points.Count;
        for (int i = 0; i < sampleCount; i++)
        {
            points.Add(new Vector3(centreline[i].x, heights[i], centreline[i].y));
        }

        for (int i = 0; i < sampleCount - 1; i++)
        {
            segmentA.Add(baseIndex + i);
            segmentB.Add(baseIndex + i + 1);
        }
    }

    /// <summary>
    /// Box filter with pinned ends. Endpoints stay at the natural terrain height so roads
    /// meeting at a point of interest agree, and junctions do not step.
    /// </summary>
    static void SmoothGrade(float[] heights, int passes)
    {
        if (heights.Length < 3 || passes <= 0)
        {
            return;
        }

        float[] scratch = new float[heights.Length];

        for (int pass = 0; pass < passes; pass++)
        {
            scratch[0] = heights[0];
            scratch[heights.Length - 1] = heights[heights.Length - 1];

            for (int i = 1; i < heights.Length - 1; i++)
            {
                scratch[i] = (heights[i - 1] + heights[i] + heights[i + 1]) / 3f;
            }

            System.Array.Copy(scratch, heights, heights.Length);
        }
    }

    /// <summary>
    /// Enforces a hard steepness ceiling. Alternating forward and backward sweeps converge on a
    /// profile within the limit while leaving the endpoints pinned, so junctions still line up.
    /// A box filter cannot do this: it lowers the average grade but leaves the worst spike.
    /// </summary>
    static void ClampGrade(Vector2[] centreline, float[] heights, float maxGradeDegrees)
    {
        if (heights.Length < 3 || maxGradeDegrees >= 89f)
        {
            return;
        }

        float tangent = Mathf.Tan(maxGradeDegrees * Mathf.Deg2Rad);

        for (int pass = 0; pass < GradeClampIterations; pass++)
        {
            for (int i = 1; i < heights.Length - 1; i++)
            {
                float limit = Vector2.Distance(centreline[i - 1], centreline[i]) * tangent;
                heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - limit, heights[i - 1] + limit);
            }

            for (int i = heights.Length - 2; i >= 1; i--)
            {
                float limit = Vector2.Distance(centreline[i], centreline[i + 1]) * tangent;
                heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - limit, heights[i + 1] + limit);
            }
        }
    }

    static Vector2 CatmullRom(Vector2[] control, float t)
    {
        int spans = control.Length - 1;
        float scaled = Mathf.Clamp01(t) * spans;
        int span = Mathf.Min((int)scaled, spans - 1);
        float local = scaled - span;

        Vector2 p0 = control[Mathf.Max(span - 1, 0)];
        Vector2 p1 = control[span];
        Vector2 p2 = control[span + 1];
        Vector2 p3 = control[Mathf.Min(span + 2, control.Length - 1)];

        float t2 = local * local;
        float t3 = t2 * local;

        return 0.5f * ((2f * p1) +
            (-p0 + p2) * local +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static BuildingPlot[] PlacePlots(ref DeterministicRandom rng, Vector2[] pois, RoadNetwork roads,
        TerrainHeightField field, LayoutSettings settings)
    {
        List<BuildingPlot> plots = new List<BuildingPlot>();

        if (settings.plotsPerPoi <= 0 || roads.SegmentCount == 0)
        {
            return plots.ToArray();
        }

        List<int> roadBuffer = new List<int>(32);
        float searchRadius = settings.plotMaxRoadDistance + roads.MaxInfluence + settings.plotSizeRange.y;
        float slopeStep = Mathf.Max(1f, settings.plotShoulder);

        for (int p = 0; p < pois.Length; p++)
        {
            int placed = 0;

            for (int attempt = 0; attempt < settings.plotPlacementAttempts && placed < settings.plotsPerPoi; attempt++)
            {
                Vector2 offset = rng.NextInsideUnitCircle() * settings.minPoiSpacing * 0.45f;
                Vector2 centre = pois[p] + offset;

                BuildingPlot plot = new BuildingPlot
                {
                    Centre = centre,
                    HalfExtents = new Vector2(
                        rng.NextFloat(settings.plotSizeRange.x, settings.plotSizeRange.y) * 0.5f,
                        rng.NextFloat(settings.plotSizeRange.x, settings.plotSizeRange.y) * 0.5f),
                    Rotation = rng.NextFloat(0f, Mathf.PI * 2f),
                    Shoulder = settings.plotShoulder,
                    Height = field.Height(centre)
                };

                RoadSample road = roads.Sample(centre, searchRadius, roadBuffer);
                if (!road.Hit)
                {
                    continue;
                }

                float edgeToRoad = road.Distance - plot.HalfExtents.magnitude - roads.HalfWidth;
                if (edgeToRoad < settings.plotRoadClearance || edgeToRoad > settings.plotMaxRoadDistance)
                {
                    continue;
                }

                if (field.SlopeDegrees(centre, slopeStep) > settings.maxPlotSlopeDegrees)
                {
                    continue;
                }

                bool overlaps = false;
                for (int i = 0; i < plots.Count; i++)
                {
                    if (plot.Overlaps(plots[i], settings.plotSpacing))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps)
                {
                    continue;
                }

                // Sit the pad at the road height it fronts onto so driveways are walkable.
                plot.Height = Mathf.Lerp(plot.Height, road.Height, 0.6f);
                plots.Add(plot);
                placed++;
            }
        }

        return plots.ToArray();
    }
}
