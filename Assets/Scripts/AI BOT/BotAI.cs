using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI : MonoBehaviour
{
    public enum BotState { Patrol, Chase, Search, Attack, Retreat }

    [Header("Targeting")]
    [SerializeField] private float detectionRange = 30f;
    [SerializeField] private float attackRange = 15f;
    [SerializeField] private float aimHeight = 1.2f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Patrol and Search")]
    [SerializeField] private float patrolRadius = 12f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float searchDuration = 4f;

    [Header("Combat")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float shotsPerSecond = 1.5f;
    [SerializeField, Range(0f, 0.9f)] private float retreatHealthPercent = 0f;
    [SerializeField] private Color tracerColor = Color.yellow;
    [SerializeField] private float tracerDuration = 0.08f;

    [Header("Debug")]
    [SerializeField] private BotState currentState;

    private Health botHealth;
    private NavMeshAgent agent;
    private Transform target;
    private LineRenderer tracer;
    private Vector3 lastKnownPosition;
    private float lastSeenTime = -999f;
    private float nextShotTime;
    private float nextTargetSearchTime;
    private float nextPatrolTime;

    private void Awake()
    {
        botHealth = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 60f;
        agent.stoppingDistance = attackRange * 0.8f;

        tracer = gameObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.startWidth = 0.045f;
        tracer.endWidth = 0.015f;
        tracer.startColor = tracerColor;
        tracer.endColor = tracerColor;
        tracer.material = new Material(Shader.Find("Sprites/Default"));
        tracer.enabled = false;
    }

    private void Update()
    {
        if (Time.time >= nextTargetSearchTime)
        {
            FindClosestPlayer();
            nextTargetSearchTime = Time.time + 0.25f;
        }

        bool canSeeTarget = HasLineOfSight(detectionRange);
        if (target != null && canSeeTarget)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
        }

        currentState = DecideState(canSeeTarget);
        RunState(currentState);
    }

    private BotState DecideState(bool canSeeTarget)
    {
        if (retreatHealthPercent > 0f && botHealth != null &&
            botHealth.currentHp <= botHealth.maxHp * retreatHealthPercent)
            return BotState.Retreat;

        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (canSeeTarget && distance <= attackRange)
                return BotState.Attack;
            if (canSeeTarget)
                return BotState.Chase;
        }

        if (Time.time - lastSeenTime <= searchDuration)
            return BotState.Search;

        return BotState.Patrol;
    }

    private void RunState(BotState state)
    {
        if (!agent.isOnNavMesh)
            return;

        switch (state)
        {
            case BotState.Patrol:
                Patrol();
                break;
            case BotState.Chase:
                agent.stoppingDistance = attackRange * 0.8f;
                agent.SetDestination(target.position);
                break;
            case BotState.Search:
                SearchLastKnownPosition();
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

    private void Patrol()
    {
        agent.stoppingDistance = 0f;
        if (agent.hasPath && agent.remainingDistance > 0.5f)
            return;
        if (Time.time < nextPatrolTime)
            return;

        Vector3 randomPoint = transform.position + Random.insideUnitSphere * patrolRadius;
        randomPoint.y = transform.position.y;
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        nextPatrolTime = Time.time + patrolWaitTime;
    }

    private void SearchLastKnownPosition()
    {
        agent.stoppingDistance = 0f;
        agent.SetDestination(lastKnownPosition);
        if (!agent.pathPending && agent.remainingDistance <= 0.6f)
            transform.Rotate(0f, 60f * Time.deltaTime, 0f);
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
            transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
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

    private bool HasLineOfSight(float maxDistance)
    {
        if (target == null)
            return false;

        Vector3 origin = GetShotOrigin();
        Vector3 destination = target.position + Vector3.up * aimHeight;
        Vector3 direction = destination - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                maxDistance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == target || hit.transform.IsChildOf(target);

        return false;
    }

    private void TryShoot()
    {
        if (target == null || Time.time < nextShotTime)
            return;

        nextShotTime = Time.time + 1f / Mathf.Max(0.01f, shotsPerSecond);
        Vector3 origin = GetShotOrigin();
        Vector3 destination = target.position + Vector3.up * aimHeight;
        Vector3 direction = (destination - origin).normalized;
        Vector3 tracerEnd = origin + direction * attackRange;

        if (Physics.Raycast(origin, direction, out RaycastHit hit,
                attackRange, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            tracerEnd = hit.point;
            Health health = hit.collider.GetComponentInParent<Health>();
            if (health != null && health != botHealth)
                health.TakeDamage(damage);
        }

        StartCoroutine(ShowTracer(origin, tracerEnd));
    }

    private Vector3 GetShotOrigin()
    {
        return transform.position + Vector3.up * aimHeight + transform.forward * 0.7f;
    }

    private IEnumerator ShowTracer(Vector3 start, Vector3 end)
    {
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        yield return new WaitForSeconds(tracerDuration);
        tracer.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
