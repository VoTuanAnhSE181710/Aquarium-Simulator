using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    // Biến static để các script khác có thể dễ dàng kiểm tra xem game có đang pause không
    public static bool GameIsPaused = false;

    [Header("Giao diện UI")]
    public GameObject pauseMenuUI;    // Kéo Panel Pause vào đây
    public GameObject settingsMenuUI; // Kéo Panel Settings vào đây
    public GameObject saveLoadMenuUI; // THÊM MỚI: Kéo SaveLoadPanel vào đây

    void Start()
    {
        // Đảm bảo khi bắt đầu game, các menu này đều ẩn đi
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (settingsMenuUI != null) settingsMenuUI.SetActive(false);
        if (saveLoadMenuUI != null) saveLoadMenuUI.SetActive(false);
    }

    void Update()
    {
        // Lắng nghe phím ESC
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameIsPaused)
            {
                // Nếu đang ở Settings, bấm ESC quay lại Pause Menu
                if (settingsMenuUI != null && settingsMenuUI.activeSelf)
                {
                    CloseSettings();
                }
                // THÊM MỚI: Nếu đang ở bảng Save/Load, bấm ESC quay lại Pause Menu
                else if (saveLoadMenuUI != null && saveLoadMenuUI.activeSelf)
                {
                    CloseSaveLoadMenu();
                }
                // Nếu chỉ đang ở Pause Menu bình thường, bấm ESC thì Resume
                else
                {
                    Resume();
                }
            }
            else
            {
                Pause();
            }
        }
    }

    // Hàm cho nút "Tiếp tục"
    public void Resume()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (settingsMenuUI != null) settingsMenuUI.SetActive(false);
        if (saveLoadMenuUI != null) saveLoadMenuUI.SetActive(false); // Dọn dẹp sạch sẽ

        // Khôi phục thời gian
        Time.timeScale = 1f;
        GameIsPaused = false;

        // Khóa và ẩn chuột lại
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Hàm gọi khi tạm dừng
    void Pause()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);

        // Đóng băng thời gian
        Time.timeScale = 0f;
        GameIsPaused = true;

        // Mở khóa và hiện chuột để bấm UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hàm cho nút "Cài đặt"
    public void OpenSettings()
    {
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        if (settingsMenuUI != null) settingsMenuUI.SetActive(true);
    }

    // Hàm cho nút "Quay lại" ở trong bảng Cài đặt
    public void CloseSettings()
    {
        if (settingsMenuUI != null) settingsMenuUI.SetActive(false);
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
    }

    // THÊM MỚI: Hàm để ESC tự đóng bảng Save/Load
    private void CloseSaveLoadMenu()
    {
        if (saveLoadMenuUI != null) saveLoadMenuUI.SetActive(false);
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
    }

    // Hàm cho nút "Thoát game"
    public void QuitGame()
    {
        Debug.Log("Đã thoát game!");
        Application.Quit();
    }

    // Hàm này tự động chạy khi người chơi chuyển cửa sổ (Alt+Tab) hoặc click ra ngoài
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            if (!GameIsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            if (!GameIsPaused)
            {
                Pause();
            }
        }
    }
}