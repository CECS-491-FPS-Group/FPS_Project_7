using UnityEngine;

public class TerrainChunk {

	public event System.Action<TerrainChunk, bool> onVisibilityChanged;
	public Vector2 coord;

	GameObject meshObject;
	Vector2 sampleCentre;
	Bounds bounds;

	MeshRenderer meshRenderer;
	MeshFilter meshFilter;
	MeshCollider meshCollider;

	LODInfo[] detailLevels;
	LODMesh[] lodMeshes;
	int colliderLODIndex;
	float colliderGenerationDistanceThreshold;

	HeightMap heightMap;
	bool heightMapReceived;
	int previousLODIndex = -1;
	bool hasSetCollider;
	float maxViewDst;

	HeightMapSettings heightMapSettings;
	MeshSettings meshSettings;
	Transform viewer;

	/// <summary>True once this chunk's MeshCollider has an actual mesh assigned (i.e. it is safe to stand on).</summary>
	public bool HasCollider { get { return hasSetCollider; } }

	public Bounds Bounds { get { return bounds; } }

	public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Transform viewer, Material material, int layer, float colliderGenerationDistanceThreshold) {
		this.coord = coord;
		this.detailLevels = detailLevels;
		// Guard against a colliderLODIndex that points past the end of detailLevels.
		// Left unclamped this throws IndexOutOfRangeException inside UpdateCollisionMesh every
		// frame, the MeshCollider never receives a mesh, and anything standing on the chunk
		// falls straight through it.
		this.colliderLODIndex = Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1);
		this.colliderGenerationDistanceThreshold = Mathf.Max(0.01f, colliderGenerationDistanceThreshold);
		this.heightMapSettings = heightMapSettings;
		this.meshSettings = meshSettings;
		this.viewer = viewer;

		sampleCentre = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
		Vector2 position = coord * meshSettings.meshWorldSize;
		bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

		meshObject = new GameObject("Terrain Chunk " + coord.x + "," + coord.y);
		meshObject.layer = layer;
		meshRenderer = meshObject.AddComponent<MeshRenderer>();
		meshFilter = meshObject.AddComponent<MeshFilter>();
		meshCollider = meshObject.AddComponent<MeshCollider>();
		meshRenderer.material = material;

		meshObject.transform.position = new Vector3(position.x, 0, position.y);
		meshObject.transform.parent = parent;
		SetVisible(false);

		lodMeshes = new LODMesh[detailLevels.Length];
		for (int i = 0; i < detailLevels.Length; i++) {
			lodMeshes[i] = new LODMesh(detailLevels[i].lod);
			lodMeshes[i].updateCallback += UpdateTerrainChunk;
			if (i == this.colliderLODIndex) {
				lodMeshes[i].updateCallback += UpdateCollisionMesh;
			}
		}

		maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
	}

	public void Load() {
		ThreadedDataRequester.RequestData(() => HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, sampleCentre), OnHeightMapReceived);
	}

	/// <summary>Swap the transform this chunk measures distance from (e.g. when the networked player spawns).</summary>
	public void SetViewer(Transform newViewer) {
		viewer = newViewer;
	}

	void OnHeightMapReceived(object heightMapObject) {
		this.heightMap = (HeightMap)heightMapObject;
		heightMapReceived = true;

		UpdateTerrainChunk();
	}

	Vector2 viewerPosition {
		get {
			if (viewer == null) {
				return Vector2.zero;
			}
			return new Vector2(viewer.position.x, viewer.position.z);
		}
	}

	public void UpdateTerrainChunk() {
		if (heightMapReceived) {
			float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

			bool wasVisible = IsVisible();
			// Never hide a chunk we are currently standing on, otherwise disabling the
			// GameObject also disables its collider and the player drops through the world.
			bool visible = viewerDstFromNearestEdge <= maxViewDst || (hasSetCollider && viewerDstFromNearestEdge <= 0.001f);

			if (visible) {
				int lodIndex = 0;

				for (int i = 0; i < detailLevels.Length - 1; i++) {
					if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold) {
						lodIndex = i + 1;
					} else {
						break;
					}
				}

				if (lodIndex != previousLODIndex) {
					LODMesh lodMesh = lodMeshes[lodIndex];
					if (lodMesh.hasMesh) {
						previousLODIndex = lodIndex;
						meshFilter.mesh = lodMesh.mesh;
					} else if (!lodMesh.hasRequestedMesh) {
						lodMesh.RequestMesh(heightMap, meshSettings);
					}
				}
			}

			if (wasVisible != visible) {
				SetVisible(visible);
				if (onVisibilityChanged != null) {
					onVisibilityChanged(this, visible);
				}
			}
		}
	}

	public void UpdateCollisionMesh() {
		if (hasSetCollider || detailLevels.Length == 0) {
			return;
		}

		float sqrDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);

		if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDstThreshold) {
			if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
				lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
			}
		}

		if (sqrDstFromViewerToEdge < colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold) {
			if (lodMeshes[colliderLODIndex].hasMesh) {
				meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
				hasSetCollider = true;
			}
		}
	}

	public void SetVisible(bool visible) {
		meshObject.SetActive(visible);
	}

	public bool IsVisible() {
		return meshObject.activeSelf;
	}

}

class LODMesh {

	public Mesh mesh;
	public bool hasRequestedMesh;
	public bool hasMesh;
	int lod;
	public event System.Action updateCallback;

	public LODMesh(int lod) {
		this.lod = lod;
	}

	void OnMeshDataReceived(object meshDataObject) {
		mesh = ((MeshData)meshDataObject).CreateMesh();
		hasMesh = true;

		if (updateCallback != null) {
			updateCallback();
		}
	}

	public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings) {
		hasRequestedMesh = true;
		ThreadedDataRequester.RequestData(() => MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, lod), OnMeshDataReceived);
	}

}
