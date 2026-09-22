using System.Collections;
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

	[Tooltip("Log placement details to the console.")]
	public bool verboseLogging;

	CharacterController characterController;
	Rigidbody body;
	CapsuleCollider capsule;

	bool rigidbodyWasKinematic;
	bool placed;

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

		if (characterController != null && (scriptsToDisableWhileWaiting == null || scriptsToDisableWhileWaiting.Length == 0)) {
			Debug.LogWarning("[TerrainSpawnPlacer] scriptsToDisableWhileWaiting is empty. Add your movement script (e.g. FirstPersonController) so it does not call Move() on the disabled CharacterController while we wait for the ground.", this);
		}

		StartCoroutine(PlaceOnTerrainRoutine());
	}

	IEnumerator PlaceOnTerrainRoutine() {
		Freeze(true);

		Vector3 spawnXZ = transform.position;

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

				if (verboseLogging) {
					Debug.Log(string.Format("[TerrainSpawnPlacer] Placed on '{0}' at {1}.", hit.collider.name, hit.point), this);
				}
				yield break;
			}

			yield return null;
		}

		Debug.LogWarning("[TerrainSpawnPlacer] Timed out waiting for a terrain collider. Check that colliderLODIndex is a valid index into detailLevels and that terrainMask includes the terrain layer.", this);
		Freeze(false);
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
