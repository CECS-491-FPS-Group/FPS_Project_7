
using UnityEngine;
using System.Collections.Generic;

public class ProjectileBounce : MonoBehaviour
{
    [Header("Explosion Radius Visual")]
    public Material radiusMaterial;
    public float indicatorDuration = 2f;

    [Header("Bounce Settings")]
    private int maxBounces;
    private int bounceCount;
    private float bounciness;

    [Header("Explosion Settings")]
    private float explosionRadius;
    private float explosionDamage;
    private float explosionForce;

    [Header("Lifetime Settings")]
    private float projectileLifetime;
    private float detonationDelay;
    private float spawnTime;

    private ProjectileDetonationMode detonationMode;

    private Rigidbody rb;
    private Collider projectileCollider;
    private PhysicsMaterial bounceMaterial;

    private bool hasExploded = false;


    // Called by ProjectileShooter when the projectile is created.
    public void Setup(
        int bounces,
        float bounceValue,
        float radius,
        float damage,
        float force,
        float lifetime,
        ProjectileDetonationMode mode,
        float delay)
    {
        maxBounces = Mathf.Max(1, bounces);
        bounciness = bounceValue;

        explosionRadius = radius;
        explosionDamage = damage;
        explosionForce = force;

        projectileLifetime = lifetime;
        detonationMode = mode;
        detonationDelay = delay;

        spawnTime = Time.time;

        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        // Configure the projectile's physics material.
        if (projectileCollider != null)
        {
            bounceMaterial = new PhysicsMaterial("Projectile Bounce");

            bounceMaterial.bounciness = bounciness;
            bounceMaterial.dynamicFriction = 0f;
            bounceMaterial.staticFriction = 0f;

            bounceMaterial.bounceCombine =
                PhysicsMaterialCombine.Maximum;

            bounceMaterial.frictionCombine =
                PhysicsMaterialCombine.Minimum;

            projectileCollider.material = bounceMaterial;
        }

        // Optional safety lifetime.
        if (projectileLifetime > 0f)
        {
            Destroy(gameObject, projectileLifetime);
        }
    }


    private void Update()
    {
        if (hasExploded)
            return;

        // TIMED MODE:
        // Explode and destroy the projectile after the configured delay.
        if (detonationMode == ProjectileDetonationMode.Timed)
        {
            if (Time.time - spawnTime >= detonationDelay)
            {
                DetonateAndDestroy(transform.position);
            }
        }
    }


    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded)
            return;

        // Get the collision location.
        Vector3 impactPosition = transform.position;

        if (collision.contactCount > 0)
        {
            impactPosition = collision.GetContact(0).point;
        }

        // Timed mode does not detonate based on collisions.
        if (detonationMode == ProjectileDetonationMode.Timed)
            return;

        bounceCount++;

        Debug.Log("Projectile bounce: " + bounceCount);


        switch (detonationMode)
        {
            case ProjectileDetonationMode.EveryBounce:

                // Explode on every bounce.
                ExplodeAtPosition(impactPosition);

                // Destroy only on the final configured bounce.
                if (bounceCount >= maxBounces)
                {
                    hasExploded = true;
                    Destroy(gameObject);
                }

                break;


            case ProjectileDetonationMode.FinalBounce:

                // Only explode and destroy on the final bounce.
                if (bounceCount >= maxBounces)
                {
                    DetonateAndDestroy(impactPosition);
                }

                break;
        }
    }


    // Creates an explosion visual at the given position.
    private void CreateExplosionVisual(Vector3 explosionPosition)
    {
        if (radiusMaterial == null)
        {
            Debug.LogWarning(
                "Explosion Radius Material has not been assigned!"
            );

            return;
        }

        GameObject visual =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        // Remove collider so it cannot interfere with physics.
        Collider visualCollider = visual.GetComponent<Collider>();

        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        // Assign the ExplosionRadiusMaterial.
        MeshRenderer renderer =
            visual.GetComponent<MeshRenderer>();

        renderer.sharedMaterial = radiusMaterial;

        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        // Position and scale the explosion sphere.
        visual.transform.position = explosionPosition;

        visual.transform.localScale =
            Vector3.one * (explosionRadius * 2f);

        // Remove the visual after its display duration.
        Destroy(visual, indicatorDuration);
    }


    // Creates the explosion effect and applies damage/force.
    // Does NOT destroy the projectile.
    private void ExplodeAtPosition(Vector3 explosionPosition)
    {
        Debug.Log("Explosion at bounce " + bounceCount);

        // Display the explosion radius.
        CreateExplosionVisual(explosionPosition);

        // Find all colliders within the explosion radius.
        Collider[] nearbyObjects =
            Physics.OverlapSphere(explosionPosition, explosionRadius);

        // Prevent multiple damage hits on the same enemy.
        HashSet<Health> damagedEnemies = new HashSet<Health>();

        // Prevent applying force multiple times to the same Rigidbody.
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();

        foreach (Collider nearby in nearbyObjects)
        {
            // Find Health on the object or its parent.
            Health health =
                nearby.GetComponentInParent<Health>();

            if (health != null)
            {
                // Don't damage the projectile itself.
                if (health.transform.root != transform.root)
                {
                    if (damagedEnemies.Add(health))
                    {
                        health.TakeDamage(
                            Mathf.RoundToInt(explosionDamage)
                        );
                    }
                }
            }

            // Apply explosion force.
            Rigidbody nearbyRb = nearby.attachedRigidbody;

            if (nearbyRb != null && nearbyRb != rb)
            {
                if (affectedBodies.Add(nearbyRb))
                {
                    nearbyRb.AddExplosionForce(
                        explosionForce,
                        explosionPosition,
                        explosionRadius
                    );
                }
            }
        }
    }


    // Explodes and destroys the projectile.
    private void DetonateAndDestroy(Vector3 explosionPosition)
    {
        if (hasExploded)
            return;

        hasExploded = true;

        Debug.Log("Projectile final explosion!");

        ExplodeAtPosition(explosionPosition);

        Destroy(gameObject);
    }


    private void OnDestroy()
    {
        // Clean up the runtime-created physics material.
        if (bounceMaterial != null)
        {
            Destroy(bounceMaterial);
        }
    }
}
