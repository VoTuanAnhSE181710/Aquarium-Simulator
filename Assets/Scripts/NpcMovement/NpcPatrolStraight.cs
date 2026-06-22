using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class NpcPatrolStraight : MonoBehaviour
{
    // Tạo 2 trạng thái cho NPC
    private enum NpcState
    {
        Walking,
        Idle
    }

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.4f;
    [SerializeField] private float turnSpeed = 360f; // Tăng tốc độ quay để quay đầu nhanh hơn
    [SerializeField] private float gravity = -20f;

    [Header("Obstacle Detection")]
    [SerializeField] private float obstacleCheckDistance = 3f; // Đổi thành 3 mét
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Wait Settings")]
    [SerializeField] private float waitTime = 3f; // Thời gian chờ khi gặp vật cản

    [Header("Animation")]
    [SerializeField] private float walkingSpeedThreshold = 0.05f;
    [SerializeField] private float walkingReleaseGraceTime = 0.08f;

    private CharacterController characterController;
    private CharacterGroundProbe groundProbe; // Component của bạn giữ nguyên
    private Animator animator;

    private Vector3 heading;
    private float verticalVelocity;
    private float lastMovingTime = float.NegativeInfinity;

    // Các biến quản lý trạng thái
    private NpcState currentState = NpcState.Walking;
    private float waitEndTime;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundProbe = GetComponent<CharacterGroundProbe>();
        animator = GetComponent<Animator>();

        // Khởi tạo hướng đi ban đầu là hướng NPC đang nhìn
        heading = transform.forward;
    }

    private void Update()
    {
        // Luôn tính toán trọng lực để NPC không bị bay lên trời
        bool isGrounded = groundProbe != null ? groundProbe.IsGrounded : characterController.isGrounded;
        verticalVelocity = isGrounded ? -2f : verticalVelocity + gravity * Time.deltaTime;

        // XỬ LÝ TRẠNG THÁI ĐỨNG CHỜ
        if (currentState == NpcState.Idle)
        {
            // Kiểm tra xem đã hết 3 giây chưa
            if (Time.time >= waitEndTime)
            {
                // Đảo ngược hướng đi (quay 180 độ)
                heading = -heading;
                currentState = NpcState.Walking;
            }
            else
            {
                // Đang đứng chờ: Chỉ apply trọng lực, không di chuyển ngang
                characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
                HandleAnimation(0f); // Tắt anim đi bộ
                return;
            }
        }

        // XỬ LÝ TRẠNG THÁI ĐI BỘ
        if (currentState == NpcState.Walking)
        {
            // Nếu phát hiện vật cản ở khoảng cách 3m
            if (IsBlocked())
            {
                currentState = NpcState.Idle;
                waitEndTime = Time.time + waitTime; // Đặt mốc thời gian chờ 3 giây
                HandleAnimation(0f); // Tắt anim đi bộ
                return;
            }

            // Tính toán hướng di chuyển (chiếu lên mặt đất nếu có groundProbe)
            Vector3 movementHeading = groundProbe != null ? groundProbe.ProjectOnGround(heading) : heading;
            if (movementHeading.sqrMagnitude < 0.01f)
            {
                movementHeading = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
            }

            // Xoay NPC về hướng di chuyển
            Quaternion targetRotation = Quaternion.LookRotation(movementHeading, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);

            // Di chuyển NPC
            Vector3 velocity = movementHeading * moveSpeed + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);

            // Cập nhật Animation
            float horizontalSpeed = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude;
            HandleAnimation(horizontalSpeed);
        }
    }

    private bool IsBlocked()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        // Debug.DrawRay(rayOrigin, heading * obstacleCheckDistance, Color.red);

        return Physics.Raycast(
            rayOrigin,
            heading,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void HandleAnimation(float horizontalSpeed)
    {
        if (horizontalSpeed > walkingSpeedThreshold)
        {
            lastMovingTime = Time.time;
        }

        bool shouldWalk =
            horizontalSpeed > walkingSpeedThreshold ||
            Time.time - lastMovingTime <= walkingReleaseGraceTime;

        SetWalking(shouldWalk);
    }

    private void SetWalking(bool isWalking)
    {
        if (animator == null) return;
        animator.SetBool(IsWalkingHash, isWalking);
    }
}