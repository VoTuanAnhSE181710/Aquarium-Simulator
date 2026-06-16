using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ThirdPersonCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform playerBody;
    [SerializeField] private bool firstPerson;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float distance = 5f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float yaw;
    private float pitch = 15f;
    private Vector3 positionVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject minh = GameObject.Find("Minh");
            if (minh != null)
            {
                target = minh.transform;
            }
        }

        if (playerBody == null && target != null)
        {
            playerBody = target.root;
        }

        yaw = transform.eulerAngles.y;
        SnapToTarget();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null || PauseMenuManager.GameIsPaused)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -20f, 70f);
        }

        if (firstPerson)
        {
            ApplyFirstPersonCamera();
            return;
        }

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 cameraDirection = orbitRotation * Vector3.back;
        float adjustedDistance = GetAdjustedDistance(pivot, cameraDirection);
        Vector3 desiredPosition = pivot + cameraDirection * adjustedDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);
        transform.LookAt(pivot);
    }

    private void ApplyFirstPersonCamera()
    {
        Vector3 pivot = target.position + targetOffset;
        transform.position = pivot;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        positionVelocity = Vector3.zero;

        if (playerBody != null)
        {
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void OnEnable()
    {
        if (target != null)
        {
            SnapToTarget();
        }
    }

    private void SnapToTarget()
    {
        if (firstPerson)
        {
            ApplyFirstPersonCamera();
            return;
        }

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        Vector3 cameraDirection = orbitRotation * Vector3.back;

        transform.position = pivot + cameraDirection * distance;
        transform.LookAt(pivot);
        positionVelocity = Vector3.zero;
    }

    private float GetAdjustedDistance(Vector3 pivot, Vector3 cameraDirection)
    {
        if (Physics.SphereCast(
                pivot,
                collisionRadius,
                cameraDirection,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(hit.distance - collisionRadius, 0.5f);
        }

        return distance;
    }
}
