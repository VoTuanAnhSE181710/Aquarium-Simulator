using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class NpcStationary : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    [Header("Gravity Settings")]
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private CharacterGroundProbe groundProbe;
    private Animator animator;
    private float verticalVelocity;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundProbe = GetComponent<CharacterGroundProbe>();
        animator = GetComponent<Animator>();

        // Ngay khi bắt đầu, ép NPC vào trạng thái đứng yên
        SetWalking(false);
    }

    private void Update()
    {
        // 1. Áp dụng trọng lực để NPC luôn bám sát mặt đất
        bool isGrounded = groundProbe != null ? groundProbe.IsGrounded : characterController.isGrounded;
        verticalVelocity = isGrounded ? -2f : verticalVelocity + gravity * Time.deltaTime;

        // NPC CHỈ di chuyển theo trục Y (rơi xuống), không có vận tốc ngang (X, Z)
        Vector3 velocity = Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);

        // 2. Đảm bảo Animation luôn là Idle phòng trường hợp bị lỗi
        SetWalking(false);
    }

    private void SetWalking(bool isWalking)
    {
        if (animator == null) return;
        animator.SetBool(IsWalkingHash, isWalking);
    }
}