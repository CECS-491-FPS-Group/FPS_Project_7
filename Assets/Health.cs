using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [Min(1)] public int maxHp = 100;
    private readonly SyncVar<int> _currentHp = new SyncVar<int>();
    public int CurrentHealth => _currentHp.Value;

    private void Awake()
    {
        _currentHp.OnChange += OnHealthChanged;
    }

    private void OnDestroy()
    {
        _currentHp.OnChange -= OnHealthChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHp.Value = Mathf.Max(1, maxHp);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Health] Client {LocalConnection.ClientId}: player owner={OwnerId} initial HP={CurrentHealth}.", this);
    }

    // Not an RPC: only server combat code may change health.
    public void TakeDamage(int damage, int shooterOwnerId)
    {
        if (!IsServerInitialized || !IsSpawned || damage <= 0 || CurrentHealth <= 0) return;
        int previous = CurrentHealth;
        _currentHp.Value = Mathf.Max(0, previous - damage);
        Debug.Log($"[Combat] Damage shooter={shooterOwnerId} victim={OwnerId} damage={damage} HP={previous}->{CurrentHealth}.", this);
        // Zero HP does not destroy, respawn, or disable the player.
    }

    private void OnHealthChanged(int previous, int next, bool asServer)
    {
        if (!asServer)
            Debug.Log($"[Health] Client {LocalConnection.ClientId}: player owner={OwnerId} synchronized HP={previous}->{next}.", this);
    }
}
