using UnityEngine;

public class ArmorVestPickup : MonoBehaviour
{
    [SerializeField] private int armorAmount = 100;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        Health playerHealth = other.GetComponentInParent<Health>();

        if (playerHealth == null || playerHealth.IsDead)
            return;

        collected = true;
        playerHealth.AddArmor(armorAmount);

        Destroy(gameObject);
    }
}