using UnityEngine;
using UnityEngine.InputSystem;

public class AquariumDecorationMode : MonoBehaviour
{
    public static AquariumDecorationMode Instance { get; private set; }
    public static bool IsDecorationMode = false;

    [Header("Decoration Settings")]
    [SerializeField] private Transform decorationCameraPosition;
    [SerializeField] private float activationDistance = 3.5f;
    [SerializeField] private LayerMask placementLayerMask = ~0;

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

    public Collider GetPlacementVolume()
    {
        return validPlacementVolume;
    }

    private Transform playerCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;

    private MinhThirdPersonController playerController;

    // Các biến dùng để lưu trữ và khôi phục vật liệu kính
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

    private void Update()
    {

    }

    public bool CanEnterMode()
    {
        if (playerController == null || IsDecorationMode) return false;
        float distance = Vector3.Distance(transform.position, playerController.transform.position);
        return distance <= activationDistance;
    }

    public void EnterDecorationMode()
    {
        IsDecorationMode = true;

        // Lưu lại vị trí cũ của Camera
        originalCameraParent = playerCamera.parent;
        originalCameraLocalPos = playerCamera.localPosition;
        originalCameraLocalRot = playerCamera.localRotation;

        // Tách Camera khỏi Player và đưa vào vị trí ngắm bể cá
        playerCamera.SetParent(null);
        playerCamera.position = decorationCameraPosition.position;
        playerCamera.rotation = decorationCameraPosition.rotation;

        // Mở khóa và hiện con trỏ chuột
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Tàng hình các object gây vướng tầm nhìn
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // Xử lý làm trong suốt riêng lớp kính
        if (glassRenderer != null)
        {
            // Lưu lại danh sách vật liệu gốc
            originalGlassMaterials = glassRenderer.sharedMaterials;
            Material[] tempMaterials = (Material[])originalGlassMaterials.Clone();

            if (glassMaterialIndex >= 0 && glassMaterialIndex < tempMaterials.Length)
            {
                // Chỉ tạo vật liệu tàng hình 1 lần để tối ưu hiệu suất
                if (invisibleGlassMaterial == null)
                {
                    invisibleGlassMaterial = new Material(originalGlassMaterials[glassMaterialIndex]);

                    if (invisibleGlassMaterial.HasProperty("_BaseColor"))
                    {
                        // Giảm Alpha về 0 để trong suốt hoàn toàn
                        invisibleGlassMaterial.SetColor("_BaseColor", new Color(0, 0, 0, 0));
                    }
                    if (invisibleGlassMaterial.HasProperty("_Smoothness"))
                    {
                        // Tắt độ bóng để kính không bị phản chiếu ánh sáng trắng gây chói
                        invisibleGlassMaterial.SetFloat("_Smoothness", 0f);
                    }
                }

                tempMaterials[glassMaterialIndex] = invisibleGlassMaterial;
                glassRenderer.materials = tempMaterials; // Áp dụng vật liệu mới
            }
        }
    }

    public void ExitDecorationMode()
    {
        IsDecorationMode = false;

        // Trả Camera về chỗ cũ
        playerCamera.SetParent(originalCameraParent);
        playerCamera.localPosition = originalCameraLocalPos;
        playerCamera.localRotation = originalCameraLocalRot;

        // Khóa và ẩn con trỏ chuột
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Hiện lại các object sau khi thoát
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Trả lại lớp kính gốc
        if (glassRenderer != null && originalGlassMaterials != null)
        {
            glassRenderer.sharedMaterials = originalGlassMaterials;
        }
    }

    public LayerMask GetPlacementMask()
    {
        return placementLayerMask;
    }
}