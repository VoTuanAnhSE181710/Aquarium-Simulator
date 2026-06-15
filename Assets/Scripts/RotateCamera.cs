using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    [Header("Cài đặt xoay")]
    [Tooltip("Tốc độ xoay của camera")]
    public float rotationSpeed = 5f;

    void Update()
    {
        // Xoay từ từ quanh trục Y (trục đứng) theo thời gian thực
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}