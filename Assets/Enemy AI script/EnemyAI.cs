using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class EnemyAI : MonoBehaviour
{
    private enum AIState
    {
        Patrol,
        Chase,
        KeepDistance
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private Transform player;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointTolerance = 0.5f;

    [Header("Vision")]
    [SerializeField] private float viewDistance = 15f;
    [SerializeField, Range(0f, 180f)]
    private float viewAngle = 60f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Combat")]
    [FormerlySerializedAs("keepDistance")]
    [SerializeField, Min(0.02f), Tooltip("Distance at which chasing stops to face the player.")]
    private float attackStopDistance = 15f;
    [SerializeField, Min(0f), Tooltip("Start retreating when the player reaches this distance.")]
    private float retreatStartDistance = 3f;
    [SerializeField, Min(0.01f), Tooltip("Continue retreating until this safe distance is reached.")]
    private float retreatStopDistance = 4f;
    [SerializeField, Min(0f), Tooltip("Extra distance beyond Attack Stop Distance before chasing resumes.")] private float distanceTolerance = 1f;

    [Header("Lost Player")]
    [SerializeField] private float losePlayerTime = 3f;

    private AIState currentState;

    private int currentPatrolIndex;
    private float lostPlayerTimer;

    private bool playerDetected;
    private bool isRetreating;
    private float originalStoppingDistance;

    private void OnValidate()
    {
        ValidateCombatDistances();
    }

    private void ValidateCombatDistances()
    {
        attackStopDistance = Mathf.Max(0.02f, attackStopDistance);
        retreatStopDistance = Mathf.Clamp(retreatStopDistance, 0.01f, attackStopDistance);
        retreatStartDistance = Mathf.Clamp(retreatStartDistance, 0f, retreatStopDistance - 0.01f);
        distanceTolerance = Mathf.Max(0f, distanceTolerance);
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        ValidateCombatDistances();
        originalStoppingDistance = agent.stoppingDistance;
    }

    private void Start()
    {
        currentState = AIState.Patrol;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            MoveToPatrolPoint();
        }
    }

    private void Update()
    {
        UpdatePlayerDetection();

        switch (currentState)
        {
            case AIState.Patrol:
                UpdatePatrol();
                break;

            case AIState.Chase:
                UpdateChase();
                break;

            case AIState.KeepDistance:
                UpdateKeepDistance();
                break;
        }
    }

    // =========================================================
    // Detection
    // =========================================================

    private void UpdatePlayerDetection()
    {
        if (player == null)
        {
            playerDetected = false;
            return;
        }

        playerDetected = CanSeePlayer();

        if (playerDetected)
        {
            lostPlayerTimer = 0f;

            if (currentState == AIState.Patrol)
            {
                ChangeState(AIState.Chase);
            }
        }
        else
        {
            if (currentState == AIState.Chase ||
                currentState == AIState.KeepDistance)
            {
                lostPlayerTimer += Time.deltaTime;

                if (lostPlayerTimer >= losePlayerTime)
                {
                    ChangeState(AIState.Patrol);
                }
            }
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 direction =
            player.position - eyePoint.position;

        float distance = direction.magnitude;

        // 시야 거리
        if (distance > viewDistance)
            return false;

        // 시야각
        float angle = Vector3.Angle(
            eyePoint.forward,
            direction.normalized
        );

        if (angle > viewAngle)
            return false;

        // 장애물 확인
        if (Physics.Raycast(
            eyePoint.position,
            direction.normalized,
            distance,
            obstacleLayer))
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // Patrol
    // =========================================================

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        if (!agent.hasPath)
        {
            MoveToPatrolPoint();
            return;
        }

        if (agent.remainingDistance <= patrolPointTolerance)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >= patrolPoints.Length)
            {
                currentPatrolIndex = 0;
            }

            MoveToPatrolPoint();
        }
    }

    private void MoveToPatrolPoint()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return;
        }

        Transform target = patrolPoints[currentPatrolIndex];

        if (target == null)
            return;

        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    // =========================================================
    // Chase
    // =========================================================

    private void UpdateChase()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // 일정 거리 안으로 들어오면
        // 거리를 유지하는 상태로 변경
        if (distance <= attackStopDistance)
        {
            ChangeState(AIState.KeepDistance);
            UpdateKeepDistance();
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    // =========================================================
    // Keep Distance
    // =========================================================

    private void UpdateKeepDistance()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (isRetreating)
        {
            if (distance >= retreatStopDistance)
                isRetreating = false;
        }
        else if (distance <= retreatStartDistance)
        {
            isRetreating = true;
        }

        if (!isRetreating && distance > attackStopDistance + distanceTolerance)
        {
            ChangeState(AIState.Chase);
            return;
        }

        // Stop the previous path if no valid retreat destination can be found.
        agent.isStopped = true;

        if (isRetreating)
        {
            Vector3 directionFromPlayer = transform.position - player.position;
            directionFromPlayer.y = 0f;
            if (directionFromPlayer.sqrMagnitude <= 0.001f)
                directionFromPlayer = -transform.forward;
            directionFromPlayer.Normalize();

            Vector3 targetPosition = player.position + directionFromPlayer * retreatStopDistance;
            targetPosition.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPosition, out hit, 2f, agent.areaMask))
            {
                // Avoid the agent stopping short of the safe distance.
                agent.stoppingDistance = 0f;
                agent.isStopped = !agent.SetDestination(hit.position);
            }
        }

        LookAtPlayer();
    }

    // =========================================================
    // State
    // =========================================================

    private void ChangeState(AIState newState)
    {
        if (currentState == newState)
            return;

        isRetreating = false;
        agent.stoppingDistance = originalStoppingDistance;
        currentState = newState;

        switch (currentState)
        {
            case AIState.Patrol:
                MoveToPatrolPoint();
                break;

            case AIState.Chase:
                agent.isStopped = false;
                break;

            case AIState.KeepDistance:
                agent.isStopped = false;
                break;
        }
    }

    // =========================================================
    // Look
    // =========================================================

    private void LookAtPlayer()
    {
        if (player == null)
            return;

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * 10f
        );
    }

    // =========================================================
    // Debug
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;

        if (eyePoint != null)
        {
            Vector3 origin = eyePoint.position;
            Vector3 forward = eyePoint.forward;
            float distance = Mathf.Max(0f, viewDistance);
            float angle = Mathf.Clamp(viewAngle, 0f, 180f);
            float angleRadians = angle * Mathf.Deg2Rad;
            const int segments = 64;

            // The detection test uses a 3D angle from eyePoint.forward.
            // Draw the circular boundary of that cone at the maximum range.
            Gizmos.color = Color.yellow;
            Vector3 previousPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float azimuth = i * 2f * Mathf.PI / segments;
                Vector3 localDirection = new Vector3(
                    Mathf.Sin(angleRadians) * Mathf.Cos(azimuth),
                    Mathf.Sin(angleRadians) * Mathf.Sin(azimuth),
                    Mathf.Cos(angleRadians)
                );
                Vector3 point = origin + eyePoint.TransformDirection(localDirection) * distance;

                if (i > 0)
                    Gizmos.DrawLine(previousPoint, point);
                if (i % (segments / 8) == 0)
                    Gizmos.DrawLine(origin, point);

                previousPoint = point;
            }

            // Horizontal and vertical arcs show the spherical distance limit.
            Vector3 previousHorizontal = Vector3.zero;
            Vector3 previousVertical = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float sweepAngle = -angle + 2f * angle * i / segments;
                Vector3 horizontal = origin +
                    Quaternion.AngleAxis(sweepAngle, eyePoint.up) * forward * distance;
                Vector3 vertical = origin +
                    Quaternion.AngleAxis(sweepAngle, eyePoint.right) * forward * distance;

                if (i > 0)
                {
                    Gizmos.DrawLine(previousHorizontal, horizontal);
                    Gizmos.DrawLine(previousVertical, vertical);
                }

                previousHorizontal = horizontal;
                previousVertical = vertical;
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(origin, forward * distance);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackStopDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, retreatStartDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, retreatStopDistance);

        Gizmos.color = previousColor;
    }
}
