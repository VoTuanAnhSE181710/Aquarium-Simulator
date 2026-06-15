using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLock : MonoBehaviour
{
    [Header("Cài đặt xoay nhân vật")]
    public Transform playerBody;
    public float rotationSpeed = 0.2f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // NẾU GAME ĐANG TẠM DỪNG -> THOÁT RA, KHÔNG LÀM GÌ CẢ
        if (PauseMenuManager.GameIsPaused)
            return;

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            float mouseX = Mouse.current.delta.x.ReadValue() * rotationSpeed;
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}