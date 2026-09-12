using UnityEngine;

[CreateAssetMenu(menuName = "Map/Layout Settings")]
public class LayoutSettings : UpdatableData
{
    [Header("Points of Interest")]
    [Min(2)]
    public int poiCount = 8;
    public float minPoiSpacing = 190f;
    [Tooltip("Keep points of interest this far inside the world edge.")]
    public float edgeInset = 130f;
    public float maxPoiSlopeDegrees = 14f;
    [Tooltip("Reject points of interest below this terrain height, which keeps them out of the falloff skirt.")]
    public float minPoiHeight = 2f;

    [Header("Roads")]
    public float roadHalfWidth = 5f;
    [Tooltip("Width of the blend from road height back to natural terrain.")]
    public float roadShoulder = 10f;
    [Tooltip("Extra links beyond the minimum spanning tree, which turn a road tree into a road network with loops.")]
    [Min(0)]
    public int extraRoadLinks = 2;
    [Tooltip("Sideways wander of road control points as a fraction of link length.")]
    [Range(0f, 0.5f)]
    public float roadCurveJitter = 0.18f;
    [Min(2)]
    public int samplesPerRoadSpan = 10;
    [Tooltip("Box-filter passes over road point heights. More passes give gentler grades that cut deeper.")]
    [Min(0)]
    public int gradeSmoothingPasses = 6;
    [Tooltip("Hard ceiling on road steepness. Smoothing alone cannot guarantee this, so it is enforced separately.")]
    [Range(1f, 45f)]
    public float maxRoadGradeDegrees = 12f;

    [Header("Building Plots")]
    [Min(0)]
    public int plotsPerPoi = 4;
    public Vector2 plotSizeRange = new Vector2(9f, 18f);
    public float plotShoulder = 5f;
    public float maxPlotSlopeDegrees = 12f;
    [Tooltip("Minimum gap between a pad edge and the road surface.")]
    public float plotRoadClearance = 3f;
    [Tooltip("Pads must sit within this distance of a road, so buildings front onto one.")]
    public float plotMaxRoadDistance = 26f;
    public float plotSpacing = 6f;
    [Min(1)]
    public int plotPlacementAttempts = 40;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        plotSizeRange.x = Mathf.Max(1f, plotSizeRange.x);
        plotSizeRange.y = Mathf.Max(plotSizeRange.x, plotSizeRange.y);
        roadShoulder = Mathf.Max(0.1f, roadShoulder);
        roadHalfWidth = Mathf.Max(0.5f, roadHalfWidth);
        base.OnValidate();
    }
#endif
}
