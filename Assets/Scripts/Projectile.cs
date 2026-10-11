using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    // Lifetime
    public float lifetime = 5f;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Hitting a wall removes sideways motion so the ball drops straight down.
        body.linearVelocity = new Vector3(
            0f,
            Mathf.Min(body.linearVelocity.y, 0f),
            0f
        );
    }
}
