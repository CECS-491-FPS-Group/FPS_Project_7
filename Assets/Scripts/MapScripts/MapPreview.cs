using UnityEngine;

public class MapPreview : MonoBehaviour {

	public Renderer textureRender;
	public MeshFilter meshFilter;
	public MeshRenderer meshRenderer;


	public enum DrawMode {NoiseMap, Mesh, FalloffMap, SurfaceMask};
	public DrawMode drawMode;

	public MeshSettings meshSettings;
	public HeightMapSettings heightMapSettings;
	public TextureData textureData;

	[Tooltip("Optional. Assign to preview the chunk as it appears inside the bounded world, including edge falloff.")]
	public WorldSettings worldSettings;
	public Vector2 previewChunkCoord;

	public Material terrainMaterial;



	[Range(0,MeshSettings.numSupportedLODs-1)]
	public int editorPreviewLOD;
	public bool autoUpdate;




	public void DrawMapInEditor() {
		textureData.ApplyToMaterial (terrainMaterial);
		textureData.UpdateMeshHeights (terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);
		HeightMap heightMap = HeightMapGenerator.GenerateHeightMap (meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, BuildContext ());

		if (drawMode == DrawMode.NoiseMap) {
			DrawTexture (TextureGenerator.TextureFromHeightMap (heightMap));
		} else if (drawMode == DrawMode.Mesh) {
			DrawMesh (MeshGenerator.GenerateTerrainMesh (heightMap.values, heightMap.surfaceMask, meshSettings, editorPreviewLOD));
		} else if (drawMode == DrawMode.SurfaceMask) {
			if (heightMap.surfaceMask == null) {
				Debug.LogWarning ("[MapPreview] No surface mask. Assign worldSettings with a layoutSettings reference to preview roads and pads.", this);
			} else {
				DrawTexture (TextureGenerator.TextureFromHeightMap (new HeightMap (heightMap.surfaceMask, 0, 1)));
			}
		} else if (drawMode == DrawMode.FalloffMap) {
			DrawTexture(TextureGenerator.TextureFromHeightMap(new HeightMap(FalloffGenerator.GenerateFalloffMap(meshSettings.numVertsPerLine),0,1)));
		}
	}

	HeightMapContext BuildContext() {
		int seed = heightMapSettings.noiseSettings.seed;

		if (worldSettings == null) {
			return HeightMapContext.Preview (meshSettings, seed);
		}

		WorldLayout layout = null;
		if (worldSettings.layoutSettings != null) {
			float meshWorldSize = meshSettings.meshWorldSize;
			WorldFalloff falloff = WorldFalloff.From (worldSettings, meshWorldSize);
			TerrainHeightField field = new TerrainHeightField (heightMapSettings, falloff, seed, meshSettings.meshScale);
			layout = WorldLayout.Build (seed, worldSettings.WorldRect (meshWorldSize), field, worldSettings.layoutSettings);
		}

		return HeightMapContext.ForChunk (previewChunkCoord, meshSettings, worldSettings, seed, layout);
	}




	public void DrawTexture(Texture2D texture) {
		textureRender.sharedMaterial.mainTexture = texture;
		textureRender.transform.localScale = new Vector3 (texture.width, 1, texture.height) /10f;

		textureRender.gameObject.SetActive (true);
		meshFilter.gameObject.SetActive (false);
	}

	public void DrawMesh(MeshData meshData) {
		meshFilter.sharedMesh = meshData.CreateMesh ();

		textureRender.gameObject.SetActive (false);
		meshFilter.gameObject.SetActive (true);
	}



	void OnValuesUpdated() {
		if (!Application.isPlaying) {
			DrawMapInEditor ();
		}
	}

	void OnTextureValuesUpdated() {
		textureData.ApplyToMaterial (terrainMaterial);
	}

	void OnValidate() {

		if (meshSettings != null) {
			meshSettings.OnValuesUpdated -= OnValuesUpdated;
			meshSettings.OnValuesUpdated += OnValuesUpdated;
		}
		if (heightMapSettings != null) {
			heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
			heightMapSettings.OnValuesUpdated += OnValuesUpdated;
		}
		if (textureData != null) {
			textureData.OnValuesUpdated -= OnTextureValuesUpdated;
			textureData.OnValuesUpdated += OnTextureValuesUpdated;
		}
		if (worldSettings != null) {
			worldSettings.OnValuesUpdated -= OnValuesUpdated;
			worldSettings.OnValuesUpdated += OnValuesUpdated;
		}

	}

}
