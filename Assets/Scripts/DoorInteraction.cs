using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DoorInteraction : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private float interactionDistance = 1f;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private bool startOpen;

    // --- PHẦN THÊM MỚI CHO ÂM THANH ---
    [Header("Cài đặt Âm thanh (Audio)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;  // File âm thanh khi mở
    [SerializeField] private AudioClip closeSound; // File âm thanh khi đóng
    // ----------------------------------

    private Transform rotationPivot;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    private void Awake()
    {
        FindDoorLeaf();
        FindInteractionCollider();
        rotationPivot = CreateEdgePivot();
        closedRotation = rotationPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startOpen;
        rotationPivot.localRotation = isOpen ? openRotation : closedRotation;

        FindPlayer();
    }

    private void Update()
    {
        // Chặn tương tác nếu đang mở Pause Menu
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            AnimateDoor();
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        // KHI NGƯỜI CHƠI NHẤN PHÍM 'F' HỢP LỆ
        if (player != null && IsWithinInteractionDistance(player.position) && IsClosestDoorToPlayer())
        {
            isOpen = !isOpen; // Đảo ngược trạng thái cửa

            // --- GỌI HÀM PHÁT ÂM THANH TẠI ĐÂY ---
            PlayDoorSound(isOpen);
        }

        AnimateDoor();
    }

    // --- HÀM XỬ LÝ PHÁT ÂM THANH ---
    private void PlayDoorSound(bool isOpening)
    {
        if (audioSource == null) return;

        // Chọn file âm thanh tương ứng với trạng thái
        AudioClip clipToPlay = isOpening ? openSound : closeSound;

        if (clipToPlay != null)
        {
            // Thay đổi cao độ (pitch) ngẫu nhiên một chút xíu để nghe không bị nhàm chán nếu mở/đóng liên tục
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clipToPlay);
        }
    }
    // ---------------------------------

    private void AnimateDoor()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        rotationPivot.localRotation = Quaternion.RotateTowards(
            rotationPivot.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    private bool IsClosestDoorToPlayer()
    {
        float ownDistance = GetSqrDistance(player.position);
        DoorInteraction[] doors = FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);

        foreach (DoorInteraction door in doors)
        {
            if (door != this &&
                door.player != null &&
                door.IsWithinInteractionDistance(player.position) &&
                door.GetSqrDistance(player.position) < ownDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsWithinInteractionDistance(Vector3 playerPosition)
    {
        return GetSqrDistance(playerPosition) <= interactionDistance * interactionDistance;
    }

    private float GetSqrDistance(Vector3 playerPosition)
    {
        Vector3 nearestPoint = interactionCollider != null
            ? interactionCollider.bounds.ClosestPoint(playerPosition)
            : transform.position;
        return (nearestPoint - playerPosition).sqrMagnitude;
    }

    private void FindDoorLeaf()
    {
        if (doorLeaf == null)
        {
            doorLeaf = transform.Find("Door");
        }
    }

    private void FindInteractionCollider()
    {
        if (interactionCollider == null && doorLeaf != null)
        {
            interactionCollider = doorLeaf.GetComponent<Collider>();
        }

        if (interactionCollider == null)
        {
            interactionCollider = GetComponentInChildren<BoxCollider>();
        }
    }

    private Transform CreateEdgePivot()
    {
        BoxCollider doorCollider = doorLeaf != null
            ? doorLeaf.GetComponent<BoxCollider>()
            : null;
        if (doorLeaf == null || doorCollider == null)
        {
            return transform;
        }

        Vector3 widthAxis = doorCollider.size.x > doorCollider.size.z
            ? Vector3.right
            : Vector3.forward;
        float halfWidth = Vector3.Scale(doorCollider.size, widthAxis).magnitude * 0.5f;
        Vector3 negativeEdge = doorCollider.transform.TransformPoint(
            doorCollider.center - widthAxis * halfWidth);
        Vector3 positiveEdge = doorCollider.transform.TransformPoint(
            doorCollider.center + widthAxis * halfWidth);
        Vector3 edgePosition = (negativeEdge - transform.position).sqrMagnitude <
                               (positiveEdge - transform.position).sqrMagnitude
            ? negativeEdge
            : positiveEdge;
        edgePosition.y = transform.position.y;

        GameObject pivotObject = new($"{doorLeaf.name} Edge Pivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(transform, false);
        pivot.position = edgePosition;
        doorLeaf.SetParent(pivot, true);
        return pivot;
    }

    private void FindPlayer()
    {
        GameObject minh = GameObject.Find("Minh");
        if (minh != null)
        {
            player = minh.transform;
        }
    }
}