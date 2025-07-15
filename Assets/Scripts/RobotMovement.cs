using UnityEngine;

public class RobotMovement : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v).normalized;

        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }
}
