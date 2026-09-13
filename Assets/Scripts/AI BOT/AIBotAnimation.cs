using UnityEngine;
using UnityEngine.AI;

public class AIBotAnimation : MonoBehaviour
{
    [Header("Recoil Bone")]
    [SerializeField] private Transform weapon;
    [SerializeField] private float recoilAngle = 30f;
    [SerializeField] private float recoilTime = 0.15f;

    private Animator animator;
    private NavMeshAgent agent;
    private float recoilTimer;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (agent != null && animator != null)
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;

            animator.SetFloat("Speed", velocity.magnitude);
        }

        if (recoilTimer > 0f)
            recoilTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (weapon == null || recoilTimer <= 0f)
            return;

        float strength = recoilTimer / recoilTime;

        weapon.localRotation *= Quaternion.Euler(
            0f,
            0f,
            recoilAngle * strength
        );
    }

    public void PlayShootAnimation()
    {
        if (animator != null)
            animator.SetTrigger("Shoot");

        recoilTimer = recoilTime;
    }
}