using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuManager : MonoBehaviour
{
    [Header("Chuyển Scene")]
    [Tooltip("Tên của Scene bạn muốn load khi bấm Play (VD: SampleScene)")]
    public string sceneToLoad = "SampleScene";

    [Header("Giao diện UI")]
    public GameObject mainMenuUI;     // Kéo Panel chứa nút Play, Settings, Quit vào đây
    public GameObject settingsMenuUI; // Kéo Panel Settings (chứa các cài đặt) vào đây

    void Start()
    {
        // Đảm bảo khi vừa mở game lên, Menu chính hiện ra và Menu cài đặt ẩn đi
        mainMenuUI.SetActive(true);
        settingsMenuUI.SetActive(false);
    }

    // Hàm này sẽ được gọi khi bấm nút Play
    public void PlayGame()
    {
        Debug.Log("Nút Play đã nhận tín hiệu click!");
        SceneManager.LoadScene(sceneToLoad);
    }

    // Hàm này gọi khi bấm nút Cài đặt (Settings) ở Menu chính
    public void OpenSettings()
    {
        mainMenuUI.SetActive(false);     // Ẩn menu chính
        settingsMenuUI.SetActive(true);  // Hiện menu cài đặt
    }

    // Hàm này gọi khi bấm nút Quay lại (Back) ở trong Menu cài đặt
    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false); // Ẩn menu cài đặt
        mainMenuUI.SetActive(true);      // Hiện lại menu chính
    }

    // Hàm này sẽ được gọi khi bấm nút Quit
    public void QuitGame()
    {
        Debug.Log("Đã thoát Game!");
        Application.Quit();
    }
}