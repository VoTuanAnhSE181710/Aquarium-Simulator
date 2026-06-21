using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuManager : MonoBehaviour
{
    [Header("Chuyển Scene")]
    [Tooltip("Tên của Scene bạn muốn load khi bấm Play (VD: SampleScene)")]
    public string sceneToLoad = "SampleScene";

    [Header("Giao diện UI")]
    public GameObject mainMenuUI;
    public GameObject settingsMenuUI;

    void Start()
    {
        mainMenuUI.SetActive(true);
        settingsMenuUI.SetActive(false);
    }

    public void PlayGame()
    {
        Debug.Log("Nút Play đã nhận tín hiệu click!");

        // --- THAY ĐỔI Ở ĐÂY ---
        // Ra lệnh cho LoadingManager bắt đầu quá trình mờ ảo và load scene
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(sceneToLoad);
        }
        else
        {
            // Dự phòng: Nếu bạn quên tạo LoadingManager, game vẫn load thẳng được
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
        Debug.Log("Đã thoát Game!");
        Application.Quit();
    }
}