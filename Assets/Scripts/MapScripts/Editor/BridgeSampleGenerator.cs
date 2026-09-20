using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Builds low-poly start, middle and end bridge prefabs sized to the road and assigns them to the structure settings.</summary>
public static class BridgeSampleGenerator
{
    const string Folder = "Assets/Terrain Assets/Bridges";
    const string StructureSettingsPath = "Assets/Terrain Assets/New Structure Settings.asset";
    const string LayoutSettingsPath = "Assets/Terrain Assets/New Layout Settings.asset";
    const string RoadMaterialPath = "Assets/Terrain Assets/Road.mat";

    const float MiddleLength = 8f;
    const float EndLength = 6f;
    const float DeckThickness = 0.5f;
    const float PillarDrop = 14f;

    [MenuItem("Tools/Map/Generate Sample Bridge Prefabs")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Terrain Assets", "Bridges");
        }

        LayoutSettings layout = AssetDatabase.LoadAssetAtPath<LayoutSettings>(LayoutSettingsPath);
        float halfWidth = layout != null ? layout.roadHalfWidth : 5f;

        Material structure = LoadOrCreateMaterial(Folder + "/Bridge.mat", new Color(0.52f, 0.5f, 0.47f), 0.15f);
        Material deck = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
        if (deck == null)
        {
            deck = structure;
        }

        GameObject middle = BuildPiece("Bridge_Middle", halfWidth, MiddleLength, false, structure, deck);
        GameObject start = BuildPiece("Bridge_Start", halfWidth, EndLength, true, structure, deck);
        GameObject end = BuildPiece("Bridge_End", halfWidth, EndLength, true, structure, deck);

        StructureSettings settings = AssetDatabase.LoadAssetAtPath<StructureSettings>(StructureSettingsPath);
        if (settings != null)
        {
            settings.bridgeStart = start;
            settings.bridgeMiddle = middle;
            settings.bridgeEnd = end;
            settings.bridgeMiddleLength = MiddleLength;
            settings.bridgePivotOffset = 0f;
            EditorUtility.SetDirty(settings);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BridgeSampleGenerator] Wrote " + Folder + " and assigned the pieces to " + StructureSettingsPath);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    static Material LoadOrCreateMaterial(string path, Color colour, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>Pivot at the near edge, +Z along the span, deck top at y = 0, so the spawner can chain tiles by their bounds.</summary>
    static GameObject BuildPiece(string name, float halfWidth, float length, bool abutment, Material structure, Material deck)
    {
        BoxMesh mesh = new BoxMesh();
        float w = halfWidth;
        float kerb = 0.6f;
        float railTop = 1.1f;

        mesh.Add(1, new Vector3(0f, -DeckThickness * 0.5f, length * 0.5f), new Vector3(w * 2f, DeckThickness, length));

        mesh.Add(0, new Vector3(-w + kerb * 0.5f, 0.125f, length * 0.5f), new Vector3(kerb, 0.25f, length));
        mesh.Add(0, new Vector3(w - kerb * 0.5f, 0.125f, length * 0.5f), new Vector3(kerb, 0.25f, length));

        mesh.Add(0, new Vector3(-w + 0.3f, railTop - 0.1f, length * 0.5f), new Vector3(0.3f, 0.2f, length));
        mesh.Add(0, new Vector3(w - 0.3f, railTop - 0.1f, length * 0.5f), new Vector3(0.3f, 0.2f, length));

        for (float z = 1f; z < length; z += 3f)
        {
            mesh.Add(0, new Vector3(-w + 0.3f, railTop * 0.5f, z), new Vector3(0.3f, railTop, 0.3f));
            mesh.Add(0, new Vector3(w - 0.3f, railTop * 0.5f, z), new Vector3(0.3f, railTop, 0.3f));
        }

        if (abutment)
        {
            mesh.Add(0, new Vector3(0f, -DeckThickness - 1.5f, length * 0.5f), new Vector3(w * 2f + 0.6f, 3f, length));
            mesh.Add(0, new Vector3(-w - 0.5f, -DeckThickness - 0.75f, length * 0.5f), new Vector3(0.4f, 1.5f, length));
            mesh.Add(0, new Vector3(w + 0.5f, -DeckThickness - 0.75f, length * 0.5f), new Vector3(0.4f, 1.5f, length));
        }
        else
        {
            mesh.Add(0, new Vector3(0f, -DeckThickness - 0.4f, length - 0.6f), new Vector3(w * 2f, 0.8f, 1.2f));
            mesh.Add(0, new Vector3(-w * 0.65f, -DeckThickness - PillarDrop * 0.5f, length - 0.6f), new Vector3(1.2f, PillarDrop, 1.2f));
            mesh.Add(0, new Vector3(w * 0.65f, -DeckThickness - PillarDrop * 0.5f, length - 0.6f), new Vector3(1.2f, PillarDrop, 1.2f));
        }

        Mesh built = mesh.ToMesh(name);
        AssetDatabase.CreateAsset(built, Folder + "/" + name + ".asset");

        GameObject piece = new GameObject(name);
        piece.AddComponent<MeshFilter>().sharedMesh = built;
        piece.AddComponent<MeshRenderer>().sharedMaterials = new[] { structure, deck };

        AddBox(piece, new Vector3(0f, -DeckThickness * 0.5f, length * 0.5f), new Vector3(w * 2f, DeckThickness, length));
        AddBox(piece, new Vector3(-w + kerb * 0.5f, railTop * 0.5f, length * 0.5f), new Vector3(kerb, railTop, length));
        AddBox(piece, new Vector3(w - kerb * 0.5f, railTop * 0.5f, length * 0.5f), new Vector3(kerb, railTop, length));

        string prefabPath = Folder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(piece, prefabPath);
        Object.DestroyImmediate(piece);
        return prefab;
    }

    static void AddBox(GameObject host, Vector3 centre, Vector3 size)
    {
        BoxCollider collider = host.AddComponent<BoxCollider>();
        collider.center = centre;
        collider.size = size;
    }

    sealed class BoxMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles = { new List<int>(), new List<int>() };

        static readonly Vector3[] FaceNormals =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };

        public void Add(int submesh, Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;

            for (int f = 0; f < FaceNormals.Length; f++)
            {
                Vector3 n = FaceNormals[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(n, Vector3.up).normalized;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 faceCentre = centre + Vector3.Scale(n, h);
                float extentU = Mathf.Abs(Vector3.Dot(u, h));
                float extentV = Mathf.Abs(Vector3.Dot(v, h));

                int start = vertices.Count;
                vertices.Add(faceCentre - u * extentU - v * extentV);
                vertices.Add(faceCentre - u * extentU + v * extentV);
                vertices.Add(faceCentre + u * extentU + v * extentV);
                vertices.Add(faceCentre + u * extentU - v * extentV);

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(n);
                }

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, extentV * 2f));
                uvs.Add(new Vector2(extentU * 2f, extentV * 2f));
                uvs.Add(new Vector2(extentU * 2f, 0f));

                bool flip = Vector3.Dot(Vector3.Cross(vertices[start + 1] - vertices[start], vertices[start + 2] - vertices[start]), n) < 0f;
                AddTriangle(submesh, start, start + 1, start + 2, flip);
                AddTriangle(submesh, start, start + 2, start + 3, flip);
            }
        }

        void AddTriangle(int submesh, int a, int b, int c, bool flip)
        {
            List<int> list = triangles[submesh];
            list.Add(a);
            list.Add(flip ? c : b);
            list.Add(flip ? b : c);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triangles[0], 0);
            mesh.SetTriangles(triangles[1], 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
