using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Patrol Settings")]
    public int numberOfPatrolPoints = 4;
    public float patrolRadius = 10f;
    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;

    [Header("Detection Settings")]
    public float detectionRange = 15f;      // Increased from 10f
    public float attackRange = 3f;          // Increased from 2f
    public float fieldOfView = 90f;         // Increased from 60f
    public LayerMask playerLayer;

    [Header("Movement Settings")]
    public float patrolSpeed = 3f;          // Increased from 2f
    public float chaseSpeed = 6f;           // Increased from 4f
    public float pursuitSpeed = 7f;         // New: faster when in pursuit mode

    [Header("Attack Settings")]
    public float attackCooldown = 1.2f;     // Reduced from 2f
    public float attackDamage = 15f;        // Increased from 10f

    [Header("Aggression Settings")]
    public float pursuitTime = 8f;          // How long to pursue after losing sight
    public float pursuitRange = 25f;        // How far to pursue
    public float alertCooldown = 3f;        // Time before returning to patrol

    private UnityEngine.AI.NavMeshAgent agent;
    private Transform player;
    private float lastAttackTime;
    private float lastSightTime;            // When we last saw the player
    private Vector3 lastKnownPosition;      // Last known player position
    private float alertStartTime;           // When we became alert

    public enum EnemyState
    {
        Patrolling,
        Chasing,
        Attacking,
        Pursuing    // New state for persistent hunting
    }

    public EnemyState currentState = EnemyState.Patrolling;
    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        // Find the player in the scene
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // Generate random patrol points
        GenerateRandomPatrolPoints();

        // Start patrolling
        if (patrolPoints.Length > 0)
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(patrolPoints[currentPatrolIndex]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();

        // Update last known position and sight time
        if (canSeePlayer)
        {
            lastSightTime = Time.time;
            lastKnownPosition = player.position;
        }

        // State machine logic
        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrol();
                if (canSeePlayer && distanceToPlayer <= detectionRange)
                {
                    currentState = EnemyState.Chasing;
                    agent.speed = chaseSpeed;
                    alertStartTime = Time.time;
                }
                break;

            case EnemyState.Chasing:
                ChasePlayer();
                if (distanceToPlayer <= attackRange)
                {
                    currentState = EnemyState.Attacking;
                    agent.isStopped = true;
                }
                else if (!canSeePlayer && Time.time - lastSightTime > 1f)
                {
                    // Lost sight, switch to pursuing
                    currentState = EnemyState.Pursuing;
                    agent.speed = pursuitSpeed;
                    agent.SetDestination(lastKnownPosition);
                }
                else if (distanceToPlayer > detectionRange * 2f && Time.time - alertStartTime > alertCooldown)
                {
                    // Return to patrol if very far and alert time expired
                    currentState = EnemyState.Patrolling;
                    agent.speed = patrolSpeed;
                    agent.isStopped = false;
                }
                break;

            case EnemyState.Pursuing:
                PursuePlayer();
                if (canSeePlayer && distanceToPlayer <= detectionRange)
                {
                    // Found player again, resume chasing
                    currentState = EnemyState.Chasing;
                    agent.speed = chaseSpeed;
                }
                else if (distanceToPlayer <= attackRange)
                {
                    currentState = EnemyState.Attacking;
                    agent.isStopped = true;
                }
                else if (Time.time - lastSightTime > pursuitTime || distanceToPlayer > pursuitRange)
                {
                    // Give up pursuit
                    currentState = EnemyState.Patrolling;
                    agent.speed = patrolSpeed;
                    agent.isStopped = false;
                }
                break;

            case EnemyState.Attacking:
                AttackPlayer();
                if (distanceToPlayer > attackRange)
                {
                    if (canSeePlayer)
                    {
                        currentState = EnemyState.Chasing;
                        agent.speed = chaseSpeed;
                    }
                    else
                    {
                        currentState = EnemyState.Pursuing;
                        agent.speed = pursuitSpeed;
                        agent.SetDestination(lastKnownPosition);
                    }
                    agent.isStopped = false;
                }
                break;
        }
    }

    void Patrol()
    {
        if (patrolPoints.Length == 0) return;

        // Move faster between patrol points when alert
        if (Time.time - lastSightTime < alertCooldown)
        {
            agent.speed = patrolSpeed * 1.5f;
        }
        else
        {
            agent.speed = patrolSpeed;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[currentPatrolIndex]);
        }

        // Occasionally look around while patrolling
        if (Random.Range(0f, 1f) < 0.01f) // 1% chance per frame
        {
            Vector3 lookDirection = Random.insideUnitSphere;
            lookDirection.y = 0;
            lookDirection.Normalize();

            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDirection), Time.deltaTime * 2f);
            }
        }
    }

    void ChasePlayer()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    void PursuePlayer()
    {
        // Move towards last known position
        if (agent.remainingDistance < 1f)
        {
            // Search around the last known position
            Vector3 searchPosition = lastKnownPosition + Random.insideUnitSphere * 5f;
            searchPosition.y = transform.position.y;

            // Find valid position on NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(searchPosition, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    void AttackPlayer()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            // Perform attack
            PerformAttack();
            lastAttackTime = Time.time;
        }

        // Look at player while attacking
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Keep rotation only on Y axis
        transform.rotation = Quaternion.LookRotation(direction);
    }

    void PerformAttack()
    {
        // You can add attack animation trigger here
        Debug.Log("Enemy attacks for " + attackDamage + " damage!");

        // More aggressive attack with knockback potential
        var playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);

            // Optional: Add knockback effect
            var playerController = player.GetComponent<CharacterController>();
            if (playerController != null)
            {
                Vector3 knockbackDirection = (player.position - transform.position).normalized;
                knockbackDirection.y = 0;
                // You can implement knockback here if desired
            }
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Closer detection is more reliable
        if (distanceToPlayer <= attackRange * 1.5f)
        {
            return true; // Always detect at close range
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        // Check if player is within field of view
        if (angle < fieldOfView / 2)
        {
            // Check if there's a clear line of sight
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            Vector3 playerCenter = player.position + Vector3.up * 1f; // Aim at player's center

            if (Physics.Raycast(rayStart, (playerCenter - rayStart).normalized, out hit, detectionRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void GenerateRandomPatrolPoints()
    {
        patrolPoints = new Vector3[numberOfPatrolPoints];
        Vector3 startPosition = transform.position;

        for (int i = 0; i < numberOfPatrolPoints; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += startPosition;
            randomDirection.y = startPosition.y; // Keep same Y level

            // Try to find a valid position on the NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                patrolPoints[i] = hit.position;
            }
            else
            {
                // If no valid NavMesh position found, use the original position
                patrolPoints[i] = startPosition;
            }
        }
    }

    // Visualize detection range in Scene view
    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Pursuit range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pursuitRange);

        // Field of view
        Gizmos.color = Color.blue;
        Vector3 leftBoundary = Quaternion.AngleAxis(-fieldOfView / 2, Vector3.up) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.AngleAxis(fieldOfView / 2, Vector3.up) * transform.forward * detectionRange;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Last known position
        if (lastKnownPosition != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange color
            Gizmos.DrawWireSphere(lastKnownPosition, 1f);
            Gizmos.DrawLine(transform.position, lastKnownPosition);
        }

        // Patrol points
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Gizmos.DrawWireSphere(patrolPoints[i], 0.5f);

                // Draw lines between patrol points
                if (i < patrolPoints.Length - 1)
                {
                    Gizmos.DrawLine(patrolPoints[i], patrolPoints[i + 1]);
                }
                else
                {
                    // Connect last point to first point
                    Gizmos.DrawLine(patrolPoints[i], patrolPoints[0]);
                }
            }
        }

        // Patrol radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
