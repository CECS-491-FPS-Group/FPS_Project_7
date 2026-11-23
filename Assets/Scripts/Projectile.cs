using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    public float lifetime = 10f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}