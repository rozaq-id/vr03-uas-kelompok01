using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Patrol Settings")]
    public GameObject[] patrolPointObjects; // Reference to Point1, Point2, etc.
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

    [Header("Debug Settings")]
    public bool showDebugInfo = true;       // Whether to show debug overlay
    public bool logDebugMessages = true;    // Whether to log debug messages

    private UnityEngine.AI.NavMeshAgent agent;
    public Transform player;
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
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on enemy! Adding one.");
            agent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        // Make sure agent is properly configured
        agent.baseOffset = 0; // Keep the enemy on the ground
        agent.updateRotation = true; // Let agent update rotation
        agent.autoTraverseOffMeshLink = true;
        agent.avoidancePriority = 50;
        agent.stoppingDistance = attackRange * 0.8f; // Stop slightly before attack range
        agent.acceleration = 12f; // Faster acceleration to improve responsiveness
        agent.angularSpeed = 200f; // Faster turning

        // Make sure the enemy has a proper size for navigation
        if (agent.radius <= 0.1f)
        {
            agent.radius = 0.5f;
            if (logDebugMessages) Debug.Log("Setting NavMeshAgent radius to " + agent.radius);
        }

        // Log agent configuration
        if (logDebugMessages)
        {
            Debug.Log("NavMeshAgent configured: " +
                     "Radius: " + agent.radius +
                     ", Height: " + agent.height +
                     ", Stopping Distance: " + agent.stoppingDistance);
        }

        // Find the player in the scene
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log("Player found at position: " + player.position);
        }
        else
        {
            Debug.LogWarning("Player not found! Make sure it has the 'Player' tag.");
        }

        // Generate random patrol points
        GenerateRandomPatrolPoints();

        // Start patrolling
        if (patrolPoints.Length > 0)
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(patrolPoints[currentPatrolIndex]);
            Debug.Log("Enemy starting patrol to: " + patrolPoints[currentPatrolIndex]);
        }

        // Check if we're on the NavMesh
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("Enemy is not on NavMesh at start! Trying to recover position...");
            TryRecoverNavMeshPosition();
        }

        // Check if player layer is set
        if (playerLayer == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
            if (playerLayer == 0)
            {
                // Default to everything if no player layer exists
                playerLayer = ~0;
                Debug.LogWarning("Player layer not found. Defaulting to everything layer.");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null)
        {
            // Try to find player again
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                if (logDebugMessages) Debug.Log("Found player at: " + player.position);
            }
            else
            {
                if (logDebugMessages) Debug.LogWarning("Player still not found!");
                return;
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();

        // Update last known position and sight time
        if (canSeePlayer)
        {
            lastSightTime = Time.time;
            lastKnownPosition = player.position;
            if (logDebugMessages) Debug.Log("Updated last known position to: " + lastKnownPosition);
        }

        // Check for potential issues with NavMeshAgent
        if (agent.isOnNavMesh == false)
        {
            if (logDebugMessages) Debug.LogError("Enemy is not on NavMesh! Trying to recover position...");
            TryRecoverNavMeshPosition();
            return;
        }

        // State machine logic
        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrol();
                if (canSeePlayer && distanceToPlayer <= detectionRange)
                {
                    if (logDebugMessages) Debug.Log("TRANSITION: Patrolling -> Chasing");
                    currentState = EnemyState.Chasing;
                    agent.speed = chaseSpeed;
                    alertStartTime = Time.time;
                }
                break;

            case EnemyState.Chasing:
                ChasePlayer();
                if (distanceToPlayer <= attackRange)
                {
                    if (logDebugMessages) Debug.Log("TRANSITION: Chasing -> Attacking");
                    currentState = EnemyState.Attacking;
                    agent.isStopped = true;
                }
                else if (!canSeePlayer && Time.time - lastSightTime > 1f)
                {
                    // Lost sight, switch to pursuing
                    if (logDebugMessages) Debug.Log("TRANSITION: Chasing -> Pursuing (lost sight)");
                    currentState = EnemyState.Pursuing;
                    agent.speed = pursuitSpeed;
                    agent.SetDestination(lastKnownPosition);
                }
                else if (distanceToPlayer > detectionRange * 2f && Time.time - alertStartTime > alertCooldown)
                {
                    // Return to patrol if very far and alert time expired
                    if (logDebugMessages) Debug.Log("TRANSITION: Chasing -> Patrolling (distance too far)");
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
                    if (logDebugMessages) Debug.Log("TRANSITION: Pursuing -> Chasing (found player)");
                    currentState = EnemyState.Chasing;
                    agent.speed = chaseSpeed;
                }
                else if (distanceToPlayer <= attackRange)
                {
                    if (logDebugMessages) Debug.Log("TRANSITION: Pursuing -> Attacking");
                    currentState = EnemyState.Attacking;
                    agent.isStopped = true;
                }
                else if (Time.time - lastSightTime > pursuitTime || distanceToPlayer > pursuitRange)
                {
                    // Give up pursuit
                    if (logDebugMessages) Debug.Log("TRANSITION: Pursuing -> Patrolling (giving up)");
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
                        if (logDebugMessages) Debug.Log("TRANSITION: Attacking -> Chasing");
                        currentState = EnemyState.Chasing;
                        agent.speed = chaseSpeed;
                    }
                    else
                    {
                        if (logDebugMessages) Debug.Log("TRANSITION: Attacking -> Pursuing");
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
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning("No patrol points available for patrolling");
            return;
        }

        // Make sure agent is active
        if (agent.isStopped)
        {
            agent.isStopped = false;
            if (logDebugMessages) Debug.Log("Reactivating NavMeshAgent");
        }

        // Move faster between patrol points when alert
        if (Time.time - lastSightTime < alertCooldown)
        {
            agent.speed = patrolSpeed * 1.5f;
        }
        else
        {
            agent.speed = patrolSpeed;
        }

        // Debug information if enabled
        if (logDebugMessages)
        {
            Debug.Log("Current position: " + transform.position +
                     ", Target: " + patrolPoints[currentPatrolIndex] +
                     ", Distance: " + agent.remainingDistance +
                     ", Path pending: " + agent.pathPending +
                     ", Has path: " + agent.hasPath);
        }

        // Check if the path is invalid
        if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid ||
            agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial)
        {
            if (logDebugMessages) Debug.LogWarning("Invalid path to patrol point. Trying next point.");
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[currentPatrolIndex]);
            return;
        }

        // Check if we've reached the current patrol point
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // Wait at patrol point for a short time (0.5-2 seconds)
            if (Random.Range(0f, 1f) < 0.1f) // 10% chance to pause and look around
            {
                StartCoroutine(LookAroundAtPatrolPoint());
            }
            else
            {
                if (logDebugMessages) Debug.Log("Reached patrol point " + currentPatrolIndex + ". Moving to next point.");
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex]);
            }
        }
    }

    IEnumerator LookAroundAtPatrolPoint()
    {
        if (logDebugMessages) Debug.Log("Pausing at patrol point to look around");

        // Save original speed and stop
        float originalSpeed = agent.speed;
        agent.isStopped = true;

        float pauseTime = Random.Range(0.5f, 2f);
        float elapsedTime = 0f;

        // Look around while waiting
        while (elapsedTime < pauseTime)
        {
            // Look in different directions
            float lookAngle = Mathf.Sin(elapsedTime * 3f) * 180f;
            Vector3 lookDirection = Quaternion.Euler(0, lookAngle, 0) * Vector3.forward;

            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDirection), Time.deltaTime * 3f);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Resume patrol
        agent.isStopped = false;
        agent.speed = originalSpeed;
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        agent.SetDestination(patrolPoints[currentPatrolIndex]);
    }

    void ChasePlayer()
    {
        if (player != null)
        {
            // Set destination to current player position for active pursuit
            agent.SetDestination(player.position);

            // Make sure we're not stopped
            if (agent.isStopped)
            {
                agent.isStopped = false;
                if (logDebugMessages) Debug.Log("Re-enabling NavMeshAgent during chase");
            }

            // Look at player while chasing
            Vector3 lookDirection = (player.position - transform.position).normalized;
            lookDirection.y = 0; // Keep rotation only on Y axis
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDirection), Time.deltaTime * 5f);
            }

            // Log chase information
            if (logDebugMessages)
            {
                Debug.Log("Chasing player. Distance: " + Vector3.Distance(transform.position, player.position) +
                         ", Path Status: " + agent.pathStatus);
            }

            // Check if path is valid, if not, try to recalculate
            if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid ||
                agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial)
            {
                if (logDebugMessages) Debug.LogWarning("Invalid path to player. Recalculating...");
                agent.ResetPath();
                agent.SetDestination(player.position);
            }
        }
    }

    void PursuePlayer()
    {
        // Move towards last known position
        if (agent.remainingDistance < 1f || agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
        {
            // Search around the last known position in a more systematic way
            float searchRadius = 5f;
            float searchAngle = (Time.time * 90f) % 360f; // Rotate search direction over time

            // Calculate search position using angle for more systematic search
            Vector3 searchDirection = new Vector3(
                Mathf.Sin(searchAngle * Mathf.Deg2Rad),
                0,
                Mathf.Cos(searchAngle * Mathf.Deg2Rad)
            );

            Vector3 searchPosition = lastKnownPosition + searchDirection * searchRadius;
            searchPosition.y = transform.position.y;

            // Find valid position on NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(searchPosition, out hit, searchRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (logDebugMessages) Debug.Log("Pursuing: Searching around last known position: " + hit.position);
                agent.SetDestination(hit.position);

                // Draw debug line to search position
                Debug.DrawLine(transform.position, hit.position, Color.magenta, 0.5f);
            }
            else if (logDebugMessages)
            {
                Debug.LogWarning("Couldn't find valid pursuit position on NavMesh");
            }
        }

        // Occasionally look around during pursuit to find player
        if (Random.Range(0f, 1f) < 0.05f) // 5% chance per frame
        {
            // Look in a random direction to try to spot the player again
            Vector3 randomLook = Random.insideUnitSphere;
            randomLook.y = 0;
            randomLook.Normalize();

            if (randomLook != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(randomLook), Time.deltaTime * 3f);
            }

            if (logDebugMessages) Debug.Log("Looking around during pursuit");
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

    public bool CanSeePlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Closer detection is more reliable
        if (distanceToPlayer <= attackRange * 1.5f)
        {
            if (logDebugMessages) Debug.Log("Enemy close to player: Auto-detecting player");
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
            Debug.DrawRay(rayStart, (playerCenter - rayStart).normalized * detectionRange, Color.red, 0.1f);

            if (Physics.Raycast(rayStart, (playerCenter - rayStart).normalized, out hit, detectionRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    if (logDebugMessages) Debug.Log("Enemy can see player via raycast hit");
                    return true;
                }
                else if (logDebugMessages)
                {
                    Debug.Log("Raycast hit " + hit.collider.name + " instead of player");
                }
            }
            else if (logDebugMessages)
            {
                Debug.Log("Raycast didn't hit anything");
            }
        }
        else if (logDebugMessages)
        {
            Debug.Log("Player outside FOV. Angle: " + angle + ", FOV: " + fieldOfView);
        }

        return false;
    }

    private void TryRecoverNavMeshPosition()
    {
        // Try to find a nearby valid NavMesh position
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            transform.position = hit.position;
            if (logDebugMessages) Debug.Log("Successfully recovered NavMesh position");
        }
        else
        {
            if (logDebugMessages) Debug.LogError("Failed to find valid NavMesh position nearby!");
        }
    }

    void GenerateRandomPatrolPoints()
    {
        // Find patrol point GameObjects if not assigned
        if (patrolPointObjects == null || patrolPointObjects.Length == 0)
        {
            // Try to find the point1 through point5 objects in the scene
            GameObject point1 = GameObject.Find("Point1");
            GameObject point2 = GameObject.Find("Point2");
            GameObject point3 = GameObject.Find("Point3");
            GameObject point4 = GameObject.Find("Point4");
            GameObject point5 = GameObject.Find("Point5");

            // Create a list of all found points
            List<GameObject> foundPoints = new List<GameObject>();
            if (point1 != null) foundPoints.Add(point1);
            if (point2 != null) foundPoints.Add(point2);
            if (point3 != null) foundPoints.Add(point3);
            if (point4 != null) foundPoints.Add(point4);
            if (point5 != null) foundPoints.Add(point5);

            // If no points were found, create random patrol points
            if (foundPoints.Count == 0)
            {
                Debug.Log("No patrol points found in scene. Creating random patrol points.");
                foundPoints = CreateRandomPatrolPoints();
            }

            // Assign the found points to the patrolPointObjects array
            patrolPointObjects = foundPoints.ToArray();
        }

        // Check if we have patrol points
        if (patrolPointObjects.Length > 0)
        {
            // Create the patrol points array from the positions of the GameObjects
            patrolPoints = new Vector3[patrolPointObjects.Length];
            for (int i = 0; i < patrolPointObjects.Length; i++)
            {
                if (patrolPointObjects[i] != null)
                {
                    patrolPoints[i] = patrolPointObjects[i].transform.position;
                }
                else
                {
                    // Use current position as fallback if point is null
                    patrolPoints[i] = transform.position;
                }
            }
        }
        else
        {
            // Fallback: If no patrol points are found, just use the current position
            Debug.LogWarning("No patrol points found. Enemy will stay at the current position.");
            patrolPoints = new Vector3[1];
            patrolPoints[0] = transform.position;
        }
    }

    private List<GameObject> CreateRandomPatrolPoints()
    {
        List<GameObject> points = new List<GameObject>();
        int numPoints = Random.Range(3, 6); // Create 3-5 random points

        for (int i = 0; i < numPoints; i++)
        {
            // Create a new GameObject for each patrol point
            GameObject pointObj = new GameObject("PatrolPoint_" + i);

            // Position the point randomly within a radius around the enemy
            float radius = Random.Range(5f, 15f);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float x = transform.position.x + radius * Mathf.Cos(angle);
            float z = transform.position.z + radius * Mathf.Sin(angle);

            // Find a valid position on the NavMesh
            UnityEngine.AI.NavMeshHit hit;
            Vector3 randomPos = new Vector3(x, transform.position.y, z);
            if (UnityEngine.AI.NavMesh.SamplePosition(randomPos, out hit, radius, UnityEngine.AI.NavMesh.AllAreas))
            {
                pointObj.transform.position = hit.position;
            }
            else
            {
                // If no valid NavMesh position found, use the original calculated position
                pointObj.transform.position = randomPos;
            }

            // Add to the list
            points.Add(pointObj);

            // Make points visible in the scene
            // You could add a small sphere or other visual indicator here if needed
        }

        return points;
    }

    // Display the current state of the enemy as a text in the game view
    void OnGUI()
    {
        // Display state information if enabled
        if (showDebugInfo && Camera.main != null)
        {
            // Position the text directly above the enemy's head
            Vector3 worldPosition = transform.position + Vector3.up * 2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);

            // Only show GUI if enemy is in front of the camera
            if (screenPos.z > 0)
            {
                // Convert to GUI coordinates (invert y)
                screenPos.y = Screen.height - screenPos.y;

                // Calculate width based on text length
                int width = 200;

                // Set up styles for better visibility
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.red;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                // Add background for better readability
                Rect backgroundRect = new Rect(screenPos.x - width / 2, screenPos.y - 10, width, 100);
                GUI.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black
                GUI.Box(backgroundRect, "");

                // Display enemy info
                GUI.color = Color.red;
                string stateText = "State: " + currentState.ToString();
                string distanceText = player != null ? "Distance: " + Vector3.Distance(transform.position, player.position).ToString("F1") : "No Player";
                string seePlayerText = "Can See Player: " + CanSeePlayer().ToString();
                string destinationText = "Dest: " + (agent.hasPath ? agent.destination.ToString("F1") : "No Path");
                string agentInfoText = "Speed: " + agent.speed.ToString("F1") + " | Stopped: " + agent.isStopped;

                // Draw labels centered above enemy
                GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y, width, 20), stateText, style);
                GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y + 20, width, 20), distanceText, style);
                GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y + 40, width, 20), seePlayerText, style);
                GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y + 60, width, 20), destinationText, style);
                GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y + 80, width, 20), agentInfoText, style);
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
    }
}
