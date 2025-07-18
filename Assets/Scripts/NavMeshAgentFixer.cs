using UnityEngine;
using UnityEngine.AI;

public class NavMeshAgentFixer : MonoBehaviour
{
    void Start()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // Fix flying issue
            agent.baseOffset = 0;
            agent.height = 2;
            agent.radius = 0.5f;
            
            // Ensure it sticks to the ground
            agent.updateUpAxis = false;
            
            // Make movement smoother
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
            agent.speed = 3.5f;
            
            // Make sure rotation is updated
            agent.updateRotation = true;
            
            // Add this to fix any position issues
            agent.Warp(new Vector3(transform.position.x, 0, transform.position.z));
            
            Debug.Log("NavMeshAgent configured for proper ground movement");
        }
    }
}