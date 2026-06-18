using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLock : MonoBehaviour
{
    [Header("Cài đặt xoay nhân vật")]
    public Transform playerBody;
    public float rotationSpeed = 0.2f;

    public void SetPlayerBody(Transform newPlayerBody)
    {
        playerBody = newPlayerBody;
    }

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

        if (playerBody == null)
        {
            FindPlayerBody();
        }

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            float mouseX = Mouse.current.delta.x.ReadValue() * rotationSpeed;
            if (playerBody != null)
            {
                playerBody.Rotate(Vector3.up * mouseX);
            }
        }
    }

    private void FindPlayerBody()
    {
        MinhThirdPersonController controller = FindFirstObjectByType<MinhThirdPersonController>();
        if (controller != null)
        {
            playerBody = controller.transform;
        }
    }
}
