using UnityEngine;
using StarterAssets;

public class BoostPickup : MonoBehaviour
{
    public PlayerBoosts.BoostType boostType =
        PlayerBoosts.BoostType.InfiniteHealth;

    [SerializeField] private float lifetime = 10f;

    private bool collected;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        PlayerBoosts playerBoosts =
            other.GetComponentInParent<PlayerBoosts>();

        if (playerBoosts == null)
            return;

        collected = true;

        playerBoosts.ApplyBoost(boostType);

        Destroy(gameObject);
    }
}