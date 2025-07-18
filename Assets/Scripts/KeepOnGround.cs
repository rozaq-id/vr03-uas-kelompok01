using UnityEngine;

public class KeepOnGround : MonoBehaviour
{
    [SerializeField] private float groundLevel = 0f;
    
    void LateUpdate()
    {
        // Force the object to stay at ground level
        if (transform.position.y != groundLevel)
        {
            transform.position = new Vector3(transform.position.x, groundLevel, transform.position.z);
        }
    }
}