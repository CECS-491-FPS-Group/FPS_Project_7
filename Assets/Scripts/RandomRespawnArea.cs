using UnityEngine;

public class RandomRespawnArea : MonoBehaviour
{
    public BoxCollider mapBounds;
    public LayerMask groundLayer;

    [Header("Player Settings")]
    public float groundOffset = -0.05f;

    public Vector3 GetRandomSpawnPosition(
        CharacterController controller
    )
    {
        if (mapBounds == null)
        {
            Debug.LogWarning(
                "Map Bounds is not assigned."
            );

            return transform.position;
        }

        Bounds bounds = mapBounds.bounds;

        float randomX = Random.Range(
            bounds.min.x,
            bounds.max.x
        );

        float randomZ = Random.Range(
            bounds.min.z,
            bounds.max.z
        );

        Vector3 rayStart = new Vector3(
            randomX,
            bounds.max.y + 100f,
            randomZ
        );

        if (Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                200f,
                groundLayer))
        {
            float characterBottomOffset = 0f;

            if (controller != null)
            {
                characterBottomOffset =
                    controller.center.y -
                    controller.height / 2f;
            }

            return hit.point
                - Vector3.up * characterBottomOffset
                + Vector3.up * groundOffset;
        }

        Debug.LogWarning(
            "Could not find ground at the random position."
        );

        return new Vector3(
            randomX,
            bounds.max.y,
            randomZ
        );
    }
}