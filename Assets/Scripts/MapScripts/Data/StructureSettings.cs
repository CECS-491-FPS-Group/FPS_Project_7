using UnityEngine;

/// <summary>Prefabs the layout spawns on building pads and across bridge spans.</summary>
[CreateAssetMenu(menuName = "Map/Structure Settings")]
public class StructureSettings : UpdatableData
{
    [Header("Buildings")]
    public GameObject[] buildingPrefabs;

    [Tooltip("Fraction of the pad footprint a building may fill.")]
    [Range(0.3f, 1f)]
    public float buildingFootprintFill = 0.85f;

    [Tooltip("Uniform scale range allowed when fitting a prefab to its pad.")]
    public Vector2 buildingScaleRange = new Vector2(0.6f, 2.5f);

    public bool faceNearestRoad = true;

    [Header("Bridges")]
    [Tooltip("Optional. Placed at the first dry point, facing along the span.")]
    public GameObject bridgeStart;

    [Tooltip("Tiled along the span. Leave everything empty to keep the procedural deck slab.")]
    public GameObject bridgeMiddle;

    [Tooltip("Optional. Placed at the last dry point, facing back along the span.")]
    public GameObject bridgeEnd;

    [Tooltip("Length of one middle tile along its local Z. Zero measures it from the renderer bounds.")]
    public float bridgeMiddleLength;

    [Tooltip("Lift bridge prefabs so their pivot sits this far above the deck height.")]
    public float bridgePivotOffset;

    public bool HasBridgePrefabs
    {
        get { return bridgeMiddle != null; }
    }

    public bool HasBuildingPrefabs
    {
        get { return buildingPrefabs != null && buildingPrefabs.Length > 0; }
    }
}
