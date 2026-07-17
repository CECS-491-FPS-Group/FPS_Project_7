using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 100;
    public int currentHp;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;

        Debug.Log(gameObject.name + " took " + damage + " damage. HP left: " + currentHp);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log(gameObject.name + " died.");
        Destroy(gameObject);
    }
}
