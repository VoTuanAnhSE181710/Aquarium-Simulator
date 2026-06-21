using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Bắt buộc để dùng TextMeshPro

public class StartMenuManager : MonoBehaviour
{
    [Header("Chuyển Scene")]
    public string sceneToLoad = "SampleScene";

    [Header("Giao diện UI")]
    public GameObject mainMenuUI;
    public GameObject settingsMenuUI;
    public GameObject saveLoadPanel; // Kéo bảng SaveLoadPanel vừa paste vào đây

    [Header("Hiển thị Chữ")]
    public TextMeshProUGUI[] slotTexts; // Kéo 4 cái Text của 4 nút Slot vào đây

    [Tooltip("Kéo Nút Continue (Button component) vào đây")]
    public Button continueButton;

    void Start()
    {
        mainMenuUI.SetActive(true);
        settingsMenuUI.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);

        // Kiểm tra xem có BẤT KỲ ô save nào tồn tại không để bật/tắt nút Continue ở Menu
        if (continueButton != null)
        {
            bool hasAnySave = false;
            for (int i = 0; i <= 3; i++)
            {
                if (GameSaveManager.HasSave(i))
                {
                    hasAnySave = true;
                    break;
                }
            }
            continueButton.interactable = hasAnySave;
        }
    }

    public void NewGame()
    {
        GameSaveManager.TargetSlotToLoad = -1; // -1 báo hiệu là tạo màn chơi mới tinh
        LoadGameScene();
    }

    public void ContinueGame()
    {
        // Ẩn Menu chính đi và bật bảng Chọn Slot lên
        mainMenuUI.SetActive(false);
        RefreshSlots();
        saveLoadPanel.SetActive(true);
    }

    public void CloseSaveLoadMenu()
    {
        // Nút quay lại: Tắt bảng Load, bật lại Menu chính
        saveLoadPanel.SetActive(false);
        mainMenuUI.SetActive(true);
    }

    private void RefreshSlots()
    {
        for (int i = 0; i <= 3; i++)
        {
            string slotName = (i == 0) ? "[ AUTOSAVE ]" : $"[ SLOT {i} ]";
            string info = GameSaveManager.GetSaveSummary(i);

            if (slotTexts.Length > i && slotTexts[i] != null)
            {
                slotTexts[i].text = $"{slotName}\n{info}";
            }
        }
    }

    // Hàm gắn vào 4 nút (0, 1, 2, 3)
    public void OnSlotClicked(int slotIndex)
    {
        // Chỉ cho phép Load nếu ô đó thực sự có dữ liệu
        if (GameSaveManager.HasSave(slotIndex))
        {
            GameSaveManager.TargetSlotToLoad = slotIndex; // Ghi nhớ Slot được chọn
            LoadGameScene(); // Bắt đầu Load chuyển Scene
        }
        else
        {
            Debug.LogWarning("File is empty!");
        }
    }

    private void LoadGameScene()
    {
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    public void OpenSettings()
    {
        mainMenuUI.SetActive(false);
        settingsMenuUI.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        mainMenuUI.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}