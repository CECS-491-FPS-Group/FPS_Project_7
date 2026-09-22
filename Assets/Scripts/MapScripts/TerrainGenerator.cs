using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TerrainGenerator : MonoBehaviour {

	const float viewerMoveThresholdForChunkUpdate = 25f;
	const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

	[Tooltip("Which entry of detailLevels is used to build MeshColliders. MUST be a valid index into detailLevels. 0 = full detail collider.")]
	public int colliderLODIndex;
	public LODInfo[] detailLevels;

	[Tooltip("How close (in world units) the viewer must get to a chunk before its MeshCollider is baked. Baking is a main-thread cost, so keep it well below the chunk size, but large enough that the collider exists before the player arrives.")]
	public float colliderGenerationDistanceThreshold = 5f;

	[Tooltip("Layer assigned to every generated chunk. This layer MUST be included in your character controller's GroundLayers mask or the player will never register as grounded.")]
	public int terrainLayer = 0;

	public MeshSettings meshSettings;
	public HeightMapSettings heightMapSettings;
	public TextureData textureSettings;

	[Tooltip("Transform the terrain streams around. May be left empty and assigned at runtime via SetViewer() - useful when the player is spawned by a network manager.")]
	public Transform viewer;
	public Material mapMaterial;

	Vector2 viewerPosition;
	Vector2 viewerPositionOld;

	float meshWorldSize;
	int chunksVisibleInViewDst;
	bool initialised;

	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

	public float MeshWorldSize { get { return meshWorldSize; } }

	void OnValidate() {
		if (detailLevels != null && detailLevels.Length > 0) {
			colliderLODIndex = Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1);
		}
		colliderGenerationDistanceThreshold = Mathf.Max(0.01f, colliderGenerationDistanceThreshold);
	}

	void Start() {

		if (detailLevels == null || detailLevels.Length == 0) {
			Debug.LogError("[TerrainGenerator] detailLevels is empty - no terrain can be generated.", this);
			enabled = false;
			return;
		}

		if (colliderLODIndex < 0 || colliderLODIndex >= detailLevels.Length) {
			Debug.LogWarning(string.Format(
				"[TerrainGenerator] colliderLODIndex ({0}) is outside detailLevels (length {1}). Clamping to {2}. " +
				"Left unclamped this prevents any MeshCollider from being created and characters fall through the terrain.",
				colliderLODIndex, detailLevels.Length, Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1)), this);
			colliderLODIndex = Mathf.Clamp(colliderLODIndex, 0, detailLevels.Length - 1);
		}

		textureSettings.ApplyToMaterial(mapMaterial);
		textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

		float maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
		meshWorldSize = meshSettings.meshWorldSize;
		chunksVisibleInViewDst = Mathf.Max(1, Mathf.RoundToInt(maxViewDst / meshWorldSize));
		initialised = true;

		if (viewer != null) {
			viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
			viewerPositionOld = viewerPosition;
		}

		UpdateVisibleChunks();
	}

	/// <summary>
	/// Point the terrain streaming at a different transform. Call this once the player exists
	/// (e.g. from OnStartClient on the owning FishNet client).
	/// </summary>
	public void SetViewer(Transform newViewer) {
		viewer = newViewer;

		foreach (KeyValuePair<Vector2, TerrainChunk> entry in terrainChunkDictionary) {
			entry.Value.SetViewer(newViewer);
		}

		if (viewer != null && initialised) {
			viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks();
		}
	}

	/// <summary>True when the chunk containing this world position has a baked MeshCollider.</summary>
	public bool IsColliderReadyAt(Vector3 worldPosition) {
		if (!initialised || meshWorldSize <= 0f) {
			return false;
		}

		Vector2 chunkCoord = new Vector2(
			Mathf.RoundToInt(worldPosition.x / meshWorldSize),
			Mathf.RoundToInt(worldPosition.z / meshWorldSize));

		TerrainChunk chunk;
		return terrainChunkDictionary.TryGetValue(chunkCoord, out chunk) && chunk.HasCollider;
	}

	void Update() {
		if (!initialised || viewer == null) {
			return;
		}

		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

		// Run every frame rather than only when the viewer moves: a player standing still at
		// spawn would otherwise never trigger the collider bake for the chunk beneath them.
		// UpdateCollisionMesh early-outs once a chunk's collider is set, so this is cheap.
		for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--) {
			visibleTerrainChunks[i].UpdateCollisionMesh();
		}

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks();
		}
	}

	void UpdateVisibleChunks() {
		HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
		for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--) {
			alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
			visibleTerrainChunks[i].UpdateTerrainChunk();
		}

		int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
		int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

		for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++) {
			for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
				if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
					if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
						terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
					} else {
						TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, colliderLODIndex, transform, viewer, mapMaterial, terrainLayer, colliderGenerationDistanceThreshold);
						terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
						newChunk.onVisibilityChanged += OnTerrainChunkVisibilityChanged;
						newChunk.Load();
					}
				}
			}
		}
	}

	void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible) {
		if (isVisible) {
			visibleTerrainChunks.Add(chunk);
		} else {
			visibleTerrainChunks.Remove(chunk);
		}
	}

}

[System.Serializable]
public struct LODInfo {
	[Range(0, MeshSettings.numSupportedLODs - 1)]
	public int lod;
	public float visibleDstThreshold;


	public float sqrVisibleDstThreshold {
		get {
			return visibleDstThreshold * visibleDstThreshold;
		}
	}
}
