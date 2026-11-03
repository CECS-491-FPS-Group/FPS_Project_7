using UnityEngine;
using UnityEngine.AI;

public class AIBotRagdoll : MonoBehaviour
{
    private Animator botAnimator;
    private NavMeshAgent agent;
    private Collider rootCollider;
    private Rigidbody[] bodyParts;
    private Collider[] bodyPartColliders;

    private void Awake()
    {
        botAnimator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rootCollider = GetComponent<Collider>();

        bodyParts = GetComponentsInChildren<Rigidbody>(true);
        bodyPartColliders = new Collider[bodyParts.Length];

        for (int i = 0; i < bodyParts.Length; i++)
            bodyPartColliders[i] = bodyParts[i].GetComponent<Collider>();

        SetRagdoll(false);
    }

    public void SetRagdoll(bool isDead)
    {
        if (botAnimator != null)
            botAnimator.enabled = !isDead;

        if (agent != null)
        {
            if (isDead && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;

            agent.enabled = !isDead;
        }

        if (rootCollider != null)
            rootCollider.enabled = !isDead;

        for (int i = 0; i < bodyParts.Length; i++)
        {
            if (bodyPartColliders[i] != null)
                bodyPartColliders[i].enabled = isDead;

            bodyParts[i].isKinematic = !isDead;
            bodyParts[i].useGravity = isDead;

            if (!isDead)
            {
                bodyParts[i].linearVelocity = Vector3.zero;
                bodyParts[i].angularVelocity = Vector3.zero;
            }
        }
    }

    public void EnableRagdoll()
    {
        SetRagdoll(true);
    }

    public void DisableRagdoll()
    {
        SetRagdoll(false);
    }
}