using UnityEngine;
using System.Collections; // Bắt buộc để dùng Coroutine

public class AquariumDecorationMode : MonoBehaviour
{
    public static AquariumDecorationMode Instance { get; private set; }

    public static bool IsDecorationMode = false;
    public static bool IsTransitioning = false; // Xác định xem camera có đang bay không

    [Header("Decoration Settings")]
    [SerializeField] private Transform decorationCameraPosition;
    [SerializeField] private float activationDistance = 3.5f;
    [SerializeField] private LayerMask placementLayerMask = ~0;

    [Header("Transition Settings")]
    [Tooltip("Thời gian bay của Camera (tính bằng giây)")]
    [SerializeField] private float transitionDuration = 0.6f;

    [Header("Visibility Settings")]
    [Tooltip("Các object sẽ bị tàng hình hoàn toàn (VD: Filter, OxiBubbles)")]
    [SerializeField] private GameObject[] objectsToHide;

    [Header("Glass Settings (Chỉ tàng hình kính)")]
    [Tooltip("Kéo object BeCa vào đây để chỉ làm trong suốt lớp kính")]
    [SerializeField] private Renderer glassRenderer;
    [Tooltip("Vị trí của vật liệu kính trong Mesh Renderer (Thường là 0)")]
    [SerializeField] private int glassMaterialIndex = 0;

    [Header("Placement Rules")]
    [Tooltip("Kéo thả Box Collider đại diện cho vùng nước bên trong bể (VD: object 'Inside')")]
    [SerializeField] private Collider validPlacementVolume;

    private Transform playerCamera;
    private Transform originalCameraParent;

    // Đổi tên thành biến lưu vị trí để dùng cho cả Local lẫn World
    private Vector3 originalCameraPos;
    private Quaternion originalCameraRot;

    private MinhThirdPersonController playerController;

    private Material[] originalGlassMaterials;
    private Material invisibleGlassMaterial;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        playerController = FindFirstObjectByType<MinhThirdPersonController>();
        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    public bool CanEnterMode()
    {
        if (playerController == null || IsDecorationMode || IsTransitioning) return false;
        float distance = Vector3.Distance(transform.position, playerController.transform.position);
        return distance <= activationDistance;
    }

    public void EnterDecorationMode()
    {
        if (IsTransitioning) return;
        IsDecorationMode = true;

        // Lưu lại vị trí cũ của Camera
        originalCameraParent = playerCamera.parent;

        // KIỂM TRA BẢO VỆ: Nếu Camera có parent thì lưu Local, không có thì lưu World
        if (originalCameraParent != null)
        {
            originalCameraPos = playerCamera.localPosition;
            originalCameraRot = playerCamera.localRotation;
        }
        else
        {
            originalCameraPos = playerCamera.position;
            originalCameraRot = playerCamera.rotation;
        }

        // Bắt đầu chuỗi diễn hoạt bay Camera vào bể
        StartCoroutine(TransitionCameraCoroutine(true));
    }

    public void ExitDecorationMode()
    {
        if (IsTransitioning) return;

        // Bắt đầu chuỗi diễn hoạt bay Camera về lại người chơi
        StartCoroutine(TransitionCameraCoroutine(false));
    }

    // --- COROUTINE BAY CAMERA MƯỢT MÀ ---
    private IEnumerator TransitionCameraCoroutine(bool isEntering)
    {
        IsTransitioning = true;

        Vector3 startPos = playerCamera.position;
        Quaternion startRot = playerCamera.rotation;
        Vector3 targetPos;
        Quaternion targetRot;

        if (isEntering)
        {
            // Điểm đến là chỗ ngắm bể cá
            targetPos = decorationCameraPosition.position;
            targetRot = decorationCameraPosition.rotation;

            playerCamera.SetParent(null); // Tách khỏi người chơi để tự do bay lượn

            // Tàng hình kính và các vật cản NGAY LẬP TỨC để lúc zoom vào không bị chắn
            SetObjectsVisibility(false);
            SetGlassInvisible(true);
        }
        else
        {
            // Khóa chuột ngay lập tức khi vừa ấn thoát
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // KIỂM TRA BẢO VỆ: Tính toán vị trí thế giới để bay về
            if (originalCameraParent != null)
            {
                targetPos = originalCameraParent.TransformPoint(originalCameraPos);
                targetRot = originalCameraParent.rotation * originalCameraRot;
            }
            else
            {
                targetPos = originalCameraPos;
                targetRot = originalCameraRot;
            }
        }

        // Vòng lặp di chuyển Camera theo thời gian
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            // Tính toán tỷ lệ 0 -> 1
            float t = Mathf.Clamp01(elapsed / transitionDuration);

            // Làm mượt đường cong tốc độ (Nhanh ở giữa, chậm dần ở 2 đầu)
            float smoothT = t * t * (3f - 2f * t);

            playerCamera.position = Vector3.Lerp(startPos, targetPos, smoothT);
            playerCamera.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null; // Chờ đến khung hình tiếp theo
        }

        // Đảm bảo Camera chốt hạ ở vị trí chính xác tuyệt đối
        playerCamera.position = targetPos;
        playerCamera.rotation = targetRot;

        if (isEntering)
        {
            // Hiện chuột khi đã đến đích
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Trả lại cho người chơi điều khiển
            playerCamera.SetParent(originalCameraParent);
            if (originalCameraParent != null)
            {
                playerCamera.localPosition = originalCameraPos;
                playerCamera.localRotation = originalCameraRot;
            }
            else
            {
                playerCamera.position = originalCameraPos;
                playerCamera.rotation = originalCameraRot;
            }

            // Hiện lại kính và vật cản
            SetObjectsVisibility(true);
            SetGlassInvisible(false);

            IsDecorationMode = false;
        }

        IsTransitioning = false; // Kết thúc chuỗi chuyển động
    }

    // --- CÁC HÀM HỖ TRỢ ---
    private void SetObjectsVisibility(bool isVisible)
    {
        if (objectsToHide == null) return;
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null) obj.SetActive(isVisible);
        }
    }

    private void SetGlassInvisible(bool makeInvisible)
    {
        if (glassRenderer == null) return;

        if (makeInvisible)
        {
            originalGlassMaterials = glassRenderer.sharedMaterials;
            Material[] tempMaterials = (Material[])originalGlassMaterials.Clone();

            if (glassMaterialIndex >= 0 && glassMaterialIndex < tempMaterials.Length)
            {
                if (invisibleGlassMaterial == null)
                {
                    invisibleGlassMaterial = new Material(originalGlassMaterials[glassMaterialIndex]);
                    if (invisibleGlassMaterial.HasProperty("_BaseColor"))
                        invisibleGlassMaterial.SetColor("_BaseColor", new Color(0, 0, 0, 0));
                    if (invisibleGlassMaterial.HasProperty("_Smoothness"))
                        invisibleGlassMaterial.SetFloat("_Smoothness", 0f);
                }
                tempMaterials[glassMaterialIndex] = invisibleGlassMaterial;
                glassRenderer.materials = tempMaterials;
            }
        }
        else
        {
            if (originalGlassMaterials != null)
                glassRenderer.sharedMaterials = originalGlassMaterials;
        }
    }

    public LayerMask GetPlacementMask() { return placementLayerMask; }
    public Collider GetPlacementVolume() { return validPlacementVolume; }
}