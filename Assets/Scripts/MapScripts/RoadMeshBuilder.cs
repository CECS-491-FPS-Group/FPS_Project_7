using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the visible road surface as its own mesh strip with UVs that run along the road, so a tiling asphalt or gravel texture and any lane markings follow the road direction instead of being projected triplanar from the terrain.</summary>
[DisallowMultipleComponent]
public class RoadMeshBuilder : MonoBehaviour
{
    public TerrainGenerator terrainGenerator;
    public Material roadMaterial;

    [Tooltip("Falls back to roadMaterial when empty.")]
    public Material bridgeMaterial;

    [Tooltip("Rebuild automatically whenever the generator produces a new layout.")]
    public bool rebuildOnLayout = true;

    [Tooltip("Procedural deck slab for spans. Skipped automatically when StructureSettings supplies bridge prefabs.")]
    public bool buildBridgeDecks = true;

    Transform container;

    public int LandVertexCount { get; private set; }
    public int BridgeVertexCount { get; private set; }

    void Awake()
    {
        if (terrainGenerator == null)
        {
#if UNITY_2023_1_OR_NEWER
            terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
#else
            terrainGenerator = Object.FindObjectOfType<TerrainGenerator>();
#endif
        }
    }

    void OnEnable()
    {
        if (terrainGenerator == null)
        {
            return;
        }

        if (rebuildOnLayout)
        {
            terrainGenerator.OnLayoutBuilt += Build;
        }

        if (terrainGenerator.Layout != null)
        {
            Build();
        }
    }

    void OnDisable()
    {
        if (terrainGenerator != null)
        {
            terrainGenerator.OnLayoutBuilt -= Build;
        }
    }

    public void Build()
    {
        Clear();

        if (terrainGenerator == null || terrainGenerator.Layout == null || terrainGenerator.Layout.Roads == null)
        {
            return;
        }

        WorldLayout layout = terrainGenerator.Layout;
        RoadNetwork roads = layout.Roads;
        LayoutSettings settings = terrainGenerator.worldSettings != null ? terrainGenerator.worldSettings.layoutSettings : null;

        if (roads.RoadCount == 0 || settings == null)
        {
            return;
        }

        bool renders = terrainGenerator.buildProfile == WorldBuildProfile.Full;
        int layer = terrainGenerator.terrainLayer;
        StructureSettings structures = terrainGenerator.worldSettings.structureSettings;
        bool decks = buildBridgeDecks && (structures == null || !structures.HasBridgePrefabs);

        GameObject host = new GameObject("Roads");
        host.transform.SetParent(transform, false);
        container = host.transform;

        Ribbon land = new Ribbon();
        Ribbon bridges = new Ribbon();
        Vector3[] points = roads.Points;
        float halfWidth = roads.HalfWidth;
        float lift = settings.roadSurfaceLift;
        float textureLength = Mathf.Max(0.5f, settings.roadTextureLength);
        float deckThickness = Mathf.Max(0.05f, settings.bridgeDeckThickness);

        for (int road = 0; road < roads.RoadCount; road++)
        {
            int first;
            int count;
            roads.GetRoad(road, out first, out count);

            if (count < 2)
            {
                continue;
            }

            float along = 0f;

            for (int i = 0; i < count - 1; i++)
            {
                int ia = first + i;
                int ib = first + i + 1;
                Vector3 a = points[ia];
                Vector3 b = points[ib];

                Vector3 perpA = Perpendicular(points, first, count, i);
                Vector3 perpB = Perpendicular(points, first, count, i + 1);

                float segmentLength = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                float v0 = along / textureLength;
                float v1 = (along + segmentLength) / textureLength;
                along += segmentLength;

                Vector3 aLeft = a + perpA * halfWidth + Vector3.up * lift;
                Vector3 aRight = a - perpA * halfWidth + Vector3.up * lift;
                Vector3 bLeft = b + perpB * halfWidth + Vector3.up * lift;
                Vector3 bRight = b - perpB * halfWidth + Vector3.up * lift;

                bool deck = roads.IsBridgePoint(ia) || roads.IsBridgePoint(ib);
                if (deck && !decks)
                {
                    continue;
                }

                Ribbon target = deck ? bridges : land;

                target.AddQuad(aLeft, bLeft, bRight, aRight, v0, v1, Vector3.up);

                if (deck)
                {
                    Vector3 drop = Vector3.down * deckThickness;
                    target.AddQuad(aLeft + drop, bLeft + drop, bRight + drop, aRight + drop, v0, v1, Vector3.down);
                    target.AddQuad(aLeft, bLeft, bLeft + drop, aLeft + drop, v0, v1, perpA);
                    target.AddQuad(aRight, bRight, bRight + drop, aRight + drop, v0, v1, -perpA);
                }
            }
        }

        LandVertexCount = land.VertexCount;
        BridgeVertexCount = bridges.VertexCount;

        if (renders && land.VertexCount > 0)
        {
            CreateMeshObject("Road Surface", land.ToMesh(), roadMaterial, layer, false, true);
        }

        if (bridges.VertexCount > 0)
        {
            Material deckMaterial = bridgeMaterial != null ? bridgeMaterial : roadMaterial;
            CreateMeshObject("Bridge Decks", bridges.ToMesh(), deckMaterial, layer, true, renders);
        }
    }

    public void Clear()
    {
        if (container == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(container.gameObject);
        }
        else
        {
            DestroyImmediate(container.gameObject);
        }

        container = null;
        LandVertexCount = 0;
        BridgeVertexCount = 0;
    }

    void CreateMeshObject(string objectName, Mesh mesh, Material material, int layer, bool withCollider, bool withRenderer)
    {
        GameObject meshObject = new GameObject(objectName);
        meshObject.layer = layer;
        meshObject.transform.SetParent(container, false);

        if (withRenderer)
        {
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (withCollider)
        {
            meshObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }

    /// <summary>Unit vector across the road at a point, from the averaged direction of its neighbours.</summary>
    static Vector3 Perpendicular(Vector3[] points, int first, int count, int local)
    {
        int previous = first + Mathf.Max(local - 1, 0);
        int next = first + Mathf.Min(local + 1, count - 1);

        Vector3 tangent = points[next] - points[previous];
        tangent.y = 0f;

        if (tangent.sqrMagnitude < 1e-6f)
        {
            return Vector3.right;
        }

        tangent.Normalize();
        return new Vector3(-tangent.z, 0f, tangent.x);
    }

    sealed class Ribbon
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public int VertexCount
        {
            get { return vertices.Count; }
        }

        /// <summary>Adds a quad a-b-c-d with u across (0 at a/b, 1 at c/d) and v along, wound so the face points toward <paramref name="facing"/>.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float v0, float v1, Vector3 facing)
        {
            int start = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            uvs.Add(new Vector2(0f, v0));
            uvs.Add(new Vector2(0f, v1));
            uvs.Add(new Vector2(1f, v1));
            uvs.Add(new Vector2(1f, v0));

            Vector3 normal = Vector3.Cross(b - a, c - a);
            bool flip = Vector3.Dot(normal, facing) < 0f;

            AddTriangle(start, start + 1, start + 2, flip);
            AddTriangle(start, start + 2, start + 3, flip);
        }

        void AddTriangle(int a, int b, int c, bool flip)
        {
            triangles.Add(a);
            if (flip)
            {
                triangles.Add(c);
                triangles.Add(b);
            }
            else
            {
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
