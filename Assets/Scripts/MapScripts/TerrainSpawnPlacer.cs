using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Safely drops a character onto procedurally generated terrain.
///
/// Terrain chunks are built on a background thread, so for the first frames after a scene loads
/// (or after a networked player spawns) there is no MeshCollider under the character and gravity
/// pulls it straight through the world. This component freezes the character, registers it as the
/// TerrainGenerator's viewer so the chunk beneath it is generated, waits for that chunk's collider,
/// then raycasts down and places the character on the surface before handing control back.
///
/// Put this on the root of your player prefab (the object with the CharacterController/Rigidbody).
/// For a FishNet setup, enable it only for the owning client.
/// </summary>
[DisallowMultipleComponent]
public class TerrainSpawnPlacer : MonoBehaviour {

	[Tooltip("Left empty, the first TerrainGenerator in the scene is used.")]
	public TerrainGenerator terrainGenerator;

	[Tooltip("Register this transform as the TerrainGenerator's viewer so chunks stream around the player.")]
	public bool registerAsViewer = true;

	[Tooltip("Height the downward probe ray starts from. Must be above the tallest possible terrain (heightMultiplier).")]
	public float probeHeight = 500f;

	[Tooltip("Extra clearance left between the character's feet and the ground.")]
	public float groundClearance = 0.05f;

	[Tooltip("Give up after this many seconds and unfreeze the character anyway.")]
	public float timeout = 30f;

	[Tooltip("Layers treated as ground. Must include the TerrainGenerator's terrainLayer.")]
	public LayerMask terrainMask = ~0;

	[Tooltip("Scripts disabled while waiting for the ground (e.g. FirstPersonController).")]
	public MonoBehaviour[] scriptsToDisableWhileWaiting;

	[Tooltip("Re-place the character if it ends up under the world. Last line of defence against a fall through.")]
	public bool recoverOnFallThrough = true;

	[Tooltip("Metres below the lowest possible terrain before a fall counts as out of bounds.")]
	public float fallRecoveryDepth = 30f;

	[Tooltip("Log placement details to the console.")]
	public bool verboseLogging;

	CharacterController characterController;
	Rigidbody body;
	CapsuleCollider capsule;

	bool rigidbodyWasKinematic;
	bool placed;
	bool placing;

	/// <summary>True once the character has been placed on solid ground.</summary>
	public bool Placed { get { return placed; } }

	void Awake() {
		characterController = GetComponent<CharacterController>();
		body = GetComponent<Rigidbody>();
		capsule = GetComponent<CapsuleCollider>();

		if (terrainGenerator == null) {
#if UNITY_2023_1_OR_NEWER
			terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
#else
			terrainGenerator = Object.FindObjectOfType<TerrainGenerator>();
#endif
		}
	}

	void OnEnable() {
		placed = false;

		ValidateGroundLayers();

		if (characterController != null && (scriptsToDisableWhileWaiting == null || scriptsToDisableWhileWaiting.Length == 0)) {
			Debug.LogWarning("[TerrainSpawnPlacer] scriptsToDisableWhileWaiting is empty. Add your movement script (e.g. FirstPersonController) so it does not call Move() on the disabled CharacterController while we wait for the ground.", this);
		}

		StartCoroutine(PlaceOnTerrainRoutine());
	}

	/// <summary>
	/// Chunks are spawned on TerrainGenerator.terrainLayer. Anything that tests for ground with a
	/// LayerMask has to include that layer, or the character never registers as grounded and behaves
	/// as though it is permanently falling - which in game reads as falling through the world.
	/// </summary>
	void ValidateGroundLayers() {
		if (terrainGenerator == null) {
			return;
		}

		int layer = terrainGenerator.terrainLayer;
		int bit = 1 << layer;
		string layerName = LayerMask.LayerToName(layer);
		if (string.IsNullOrEmpty(layerName)) {
			layerName = "layer " + layer;
		}

		if ((terrainMask.value & bit) == 0) {
			Debug.LogError(string.Format(
				"[TerrainSpawnPlacer] terrainMask excludes '{0}', the layer TerrainGenerator puts chunks on. The spawn probe will never find ground.",
				layerName), this);
		}

		MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
		for (int i = 0; i < behaviours.Length; i++) {
			MonoBehaviour behaviour = behaviours[i];
			if (behaviour == null) {
				continue;
			}

			FieldInfo field = behaviour.GetType().GetField("GroundLayers", BindingFlags.Public | BindingFlags.Instance);
			if (field == null || field.FieldType != typeof(LayerMask)) {
				continue;
			}

			LayerMask mask = (LayerMask)field.GetValue(behaviour);
			if ((mask.value & bit) == 0) {
				Debug.LogError(string.Format(
					"[TerrainSpawnPlacer] {0}.GroundLayers excludes '{1}', the layer the terrain is on, so this character will never be grounded. Add '{1}' to GroundLayers or change TerrainGenerator.terrainLayer.",
					behaviour.GetType().Name, layerName), behaviour);
			}
		}
	}

	IEnumerator PlaceOnTerrainRoutine() {
		placing = true;
		Freeze(true);

		// Probing outside the grid would never hit anything, so a character recovered after
		// falling past the edge is pulled back inside before we look for ground.
		Vector3 spawnXZ = ClampToWorld(transform.position);

		// Lift to the probe height first, so the character is nowhere near any geometry while
		// frozen and the generator streams chunks around the correct XZ.
		transform.position = new Vector3(spawnXZ.x, probeHeight, spawnXZ.z);

		if (terrainGenerator != null && registerAsViewer) {
			terrainGenerator.SetViewer(transform);
		}

		Vector3 rayOrigin = new Vector3(spawnXZ.x, probeHeight, spawnXZ.z);
		float rayLength = probeHeight * 2f + 1000f;
		float deadline = Time.time + Mathf.Max(1f, timeout);

		while (Time.time < deadline) {
			// Wait for the chunk under us to report a baked collider when we can ask; otherwise
			// just keep probing. Physics.Raycast against a MeshCollider with a null sharedMesh
			// simply misses, so the raycast alone is already a valid readiness test.
			RaycastHit hit;
			if (Physics.Raycast(rayOrigin, Vector3.down, out hit, rayLength, terrainMask, QueryTriggerInteraction.Ignore)) {
				PlaceAt(hit.point);
				Freeze(false);
				placed = true;
				placing = false;

				if (verboseLogging) {
					Debug.Log(string.Format("[TerrainSpawnPlacer] Placed on '{0}' at {1}.", hit.collider.name, hit.point), this);
				}
				yield break;
			}

			yield return null;
		}

		Debug.LogWarning("[TerrainSpawnPlacer] Timed out waiting for a terrain collider. Check that colliderLODIndex is a valid index into detailLevels and that terrainMask includes the terrain layer.", this);
		Freeze(false);
		placing = false;
	}

	void Update() {
		if (!recoverOnFallThrough || placing || !placed) {
			return;
		}

		if (transform.position.y >= FallRecoveryHeight) {
			return;
		}

		Debug.LogWarning(string.Format(
			"[TerrainSpawnPlacer] {0} fell to y={1:F1}, below the world. Re-placing it on the terrain.",
			name, transform.position.y), this);

		placed = false;
		StartCoroutine(PlaceOnTerrainRoutine());
	}

	/// <summary>Height below which the character is considered to have left the world.</summary>
	float FallRecoveryHeight {
		get {
			float lowest = 0f;
			if (terrainGenerator != null && terrainGenerator.heightMapSettings != null) {
				lowest = terrainGenerator.heightMapSettings.minHeight;
			}
			return lowest - Mathf.Max(1f, fallRecoveryDepth);
		}
	}

	/// <summary>Pulls an XZ position inside the generated grid, leaving a margin off the edge.</summary>
	Vector3 ClampToWorld(Vector3 position) {
		if (terrainGenerator == null || terrainGenerator.worldSettings == null || terrainGenerator.MeshWorldSize <= 0f) {
			return position;
		}

		Rect bounds = terrainGenerator.worldSettings.WorldRect(terrainGenerator.MeshWorldSize);
		float margin = Mathf.Min(bounds.width, bounds.height) * 0.05f;

		return new Vector3(
			Mathf.Clamp(position.x, bounds.xMin + margin, bounds.xMax - margin),
			position.y,
			Mathf.Clamp(position.z, bounds.yMin + margin, bounds.yMax - margin));
	}

	void PlaceAt(Vector3 groundPoint) {
		float feetOffset = FeetOffset();
		Vector3 target = groundPoint + Vector3.up * (feetOffset + groundClearance);

		// A CharacterController must be disabled before its transform is moved, or it will
		// snap back to its internal position. Freeze(true) has already disabled it.
		transform.position = target;

		if (body != null) {
			body.position = target;
#if UNITY_6000_0_OR_NEWER
			body.linearVelocity = Vector3.zero;
#else
			body.velocity = Vector3.zero;
#endif
			body.angularVelocity = Vector3.zero;
		}
	}

	/// <summary>Distance from the transform origin down to the bottom of the character's capsule.</summary>
	float FeetOffset() {
		if (characterController != null) {
			return Mathf.Max(characterController.height * 0.5f, characterController.radius) - characterController.center.y;
		}
		if (capsule != null) {
			return Mathf.Max(capsule.height * 0.5f, capsule.radius) - capsule.center.y;
		}
		return 0f;
	}

	void Freeze(bool frozen) {
		if (characterController != null) {
			characterController.enabled = !frozen;
		}

		if (body != null) {
			if (frozen) {
				rigidbodyWasKinematic = body.isKinematic;
				body.isKinematic = true;
			} else {
				body.isKinematic = rigidbodyWasKinematic;
			}
		}

		if (scriptsToDisableWhileWaiting != null) {
			for (int i = 0; i < scriptsToDisableWhileWaiting.Length; i++) {
				if (scriptsToDisableWhileWaiting[i] != null) {
					scriptsToDisableWhileWaiting[i].enabled = !frozen;
				}
			}
		}
	}
}
