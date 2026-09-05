using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public enum BotState { Idle, Chase, Attack, Retreat }

    [Header("Targeting")]
    [SerializeField] private float detectionRange = 30f;
    [SerializeField] private float attackRange = 15f;
    [SerializeField] private float aimHeight = 1.2f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Combat")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float shotsPerSecond = 1.5f;
    [SerializeField, Range(0f, 0.9f)] private float retreatHealthPercent = 0f;

    [Header("Debug")]
    [SerializeField] private BotState currentState;

    private Health botHealth;
    private NavMeshAgent agent;
    private Transform target;
    private float nextShotTime;
    private float nextTargetSearchTime;

    private void Awake()
    {
        botHealth = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 60f;
        agent.stoppingDistance = attackRange * 0.8f;
    }

    private void Update()
    {
        if (Time.time >= nextTargetSearchTime)
        {
            FindClosestPlayer();
            nextTargetSearchTime = Time.time + 0.5f;
        }

        currentState = DecideState();
        RunState(currentState);
    }

    private BotState DecideState()
    {
        if (target == null)
            return BotState.Idle;

        if (botHealth != null &&
            botHealth.currentHp <= botHealth.maxHp * retreatHealthPercent)
            return BotState.Retreat;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attackRange && HasLineOfSight())
            return BotState.Attack;

        if (distance <= detectionRange)
            return BotState.Chase;

        return BotState.Idle;
    }

    private void RunState(BotState state)
    {
        if (!agent.isOnNavMesh)
            return;

        switch (state)
        {
            case BotState.Idle:
                agent.ResetPath();
                break;

            case BotState.Chase:
                agent.stoppingDistance = attackRange * 0.8f;
                agent.SetDestination(target.position);
                break;

            case BotState.Attack:
                agent.ResetPath();
                FaceTarget();
                TryShoot();
                break;

            case BotState.Retreat:
                FaceTarget();
                MoveAwayFromTarget();
                break;
        }
    }

    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform closest = null;
        float closestDistance = detectionRange;

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player.transform;
            }
        }

        target = closest;
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSpeed * Time.deltaTime);
    }

    private void MoveAwayFromTarget()
    {
        if (target == null)
            return;

        Vector3 away = transform.position - target.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f)
            away = -transform.forward;

        Vector3 desiredPosition = transform.position + away.normalized * 6f;
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            agent.stoppingDistance = 0f;
            agent.SetDestination(hit.position);
        }
    }

    private bool HasLineOfSight()
    {
        if (target == null)
            return false;

        Vector3 origin = transform.position + Vector3.up * aimHeight + transform.forward * 0.7f;
        Vector3 destination = target.position + Vector3.up * aimHeight;
        Vector3 direction = destination - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                attackRange, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        return false;
    }

    private void TryShoot()
    {
        if (target == null || Time.time < nextShotTime)
            return;

        nextShotTime = Time.time + 1f / Mathf.Max(0.01f, shotsPerSecond);

        Vector3 origin = transform.position + Vector3.up * aimHeight + transform.forward * 0.7f;
        Vector3 destination = target.position + Vector3.up * aimHeight;
        Vector3 direction = destination - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                attackRange, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            Health health = hit.collider.GetComponentInParent<Health>();
            if (health != null && health != botHealth)
                health.TakeDamage(damage);
        }

        Debug.DrawRay(origin, direction.normalized * attackRange, Color.red, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}