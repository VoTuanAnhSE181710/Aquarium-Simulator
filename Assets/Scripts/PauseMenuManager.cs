using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    // Biến static để các script khác có thể dễ dàng kiểm tra xem game có đang pause không
    public static bool GameIsPaused = false;

    [Header("Giao diện UI")]
    public GameObject pauseMenuUI;   // Kéo Panel chứa nút Tiếp tục, Cài đặt, Thoát vào đây
    public GameObject settingsMenuUI; // Kéo Panel chứa Slider âm lượng vào đây

    void Start()
    {
        // Đảm bảo khi bắt đầu game, các menu này đều ẩn đi
        pauseMenuUI.SetActive(false);
        settingsMenuUI.SetActive(false);
    }

    void Update()
    {
        // Lắng nghe phím ESC
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameIsPaused)
            {
                // Nếu đang ở trong Settings, bấm ESC sẽ quay lại Pause Menu thay vì Resume luôn
                if (settingsMenuUI.activeSelf)
                {
                    CloseSettings();
                }
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
        pauseMenuUI.SetActive(false);
        settingsMenuUI.SetActive(false);

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
        pauseMenuUI.SetActive(true);

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
        pauseMenuUI.SetActive(false); // Ẩn menu chính
        settingsMenuUI.SetActive(true); // Hiện menu cài đặt
    }

    // Hàm cho nút "Quay lại" ở trong bảng Cài đặt
    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        pauseMenuUI.SetActive(true);
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
        // hasFocus = true nghĩa là người chơi vừa Alt+Tab quay trở lại game
        if (hasFocus)
        {
            // Kiểm tra: Nếu quay lại mà game KHÔNG ĐANG PAUSE thì khóa chuột lại
            if (!GameIsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            // hasFocus = false nghĩa là người chơi vừa Alt+Tab ra ngoài
            // [MẸO NÂNG CAO]: Ép game tự động bật Pause Menu khi người chơi tab ra ngoài
            if (!GameIsPaused)
            {
                Pause();
            }
        }
    }
}