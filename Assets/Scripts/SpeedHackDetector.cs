using UnityEngine;
using FishNet.Object;

public class SpeedHackDetector : NetworkBehaviour
{
    // Set this slightly higher than their actual moveSpeed (5f) to account for slight internet lag
    public float maxAllowedSpeed = 6.5f; 
    public float checkInterval = 1f; // How often the server checks (in seconds)

    private float timer = 0f;
    private Vector3 lastValidPosition;

    // This FishNet function runs the exact moment the object spawns on the server
    public override void OnStartServer()
    {
        base.OnStartServer();
        lastValidPosition = transform.position;
    }

    void Update()
    {
        // THE SECURITY LOCK: Only the Host/Server is allowed to run this code!
        // If a client tries to run this, it immediately stops.
        if (!base.IsServerInitialized)
            return;

        timer += Time.deltaTime;

        if (timer >= checkInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastValidPosition);

            // Did they move further in 1 second than mathematically possible?
            if (distanceMoved > maxAllowedSpeed * checkInterval)
            {
                Debug.LogWarning($"[ANTI-CHEAT] Speed hack detected! Rubberbanding {gameObject.name}.");
                
                // Force the player back to their last safe position.
                // FishNet's NetworkTransform will automatically pull the cheating client back on their screen.
                transform.position = lastValidPosition;
            }
            else
            {
                // They are playing fair. Update their safe position.
                lastValidPosition = transform.position;
            }

            timer = 0f; // Reset the clock for the next check
        }
    }
}