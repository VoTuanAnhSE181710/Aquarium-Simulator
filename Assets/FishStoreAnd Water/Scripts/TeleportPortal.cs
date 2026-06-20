using UnityEngine;
using UnityEngine.InputSystem;

public class TeleportPortal : MonoBehaviour
{
    [Header("Cấu hình Dịch Chuyển")]
    [Tooltip("Kéo một GameObject rỗng làm điểm đích mà nhân vật sẽ biến đầu tới")]
    public Transform destination;
    
    [Tooltip("Khoảng cách để nhân vật có thể tương tác")]
    public float interactionDistance = 2.5f;
    
    [Tooltip("Dòng chữ nhắc nhở người chơi")]
    public string promptMessage = "Nhấn F để vào cửa hàng";

    [Header("Âm thanh (Không bắt buộc)")]
    public AudioClip teleportSound;
    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    private Transform player;
    private CharacterController playerCharacterController;
    private bool isPlayerNear;
    private GUIStyle promptStyle;

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        MinhThirdPersonController controller = FindFirstObjectByType<MinhThirdPersonController>();
        if (controller != null)
        {
            player = controller.transform;
            playerCharacterController = controller.GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        if (player == null || playerCharacterController == null)
        {
            FindPlayer();
            return;
        }

        // Đo khoảng cách giữa cổng dịch chuyển và người chơi
        float distance = Vector3.Distance(transform.position, player.position);
        isPlayerNear = distance <= interactionDistance;

        // Nếu người chơi ở gần, không tạm dừng game, và nhấn phím F
        if (isPlayerNear && !PauseMenuManager.GameIsPaused && !FishStoreMerchant.IsOpen)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                Teleport();
            }
        }
    }

    private void Teleport()
    {
        if (destination == null)
        {
            Debug.LogWarning("Chưa gán điểm đích (Destination) cho cổng dịch chuyển này!");
            return;
        }

        // Bắt buộc phải tắt CharacterController trước khi set position trong Unity, nếu không sẽ bị giật lại vị trí cũ
        bool wasEnabled = playerCharacterController.enabled;
        playerCharacterController.enabled = false;
        
        player.position = destination.position;
        player.rotation = destination.rotation;
        
        playerCharacterController.enabled = wasEnabled;

        // Phát âm thanh dịch chuyển nếu có gán
        if (teleportSound != null)
        {
            AudioSource source = player.GetComponent<AudioSource>();
            if (source != null)
            {
                source.PlayOneShot(teleportSound, soundVolume);
            }
        }

        Debug.Log("Đã dịch chuyển người chơi tới: " + destination.name);
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused || FishStoreMerchant.IsOpen) return;

        if (isPlayerNear)
        {
            EnsureStyle();
            float width = 280f;
            float height = 42f;
            Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 160f, width, height);
            GUI.Box(rect, promptMessage, promptStyle);
        }
    }

    private void EnsureStyle()
    {
        if (promptStyle != null) return;

        promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }
}
