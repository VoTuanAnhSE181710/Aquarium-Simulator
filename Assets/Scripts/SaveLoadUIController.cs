using UnityEngine;
using TMPro; // THÊM THƯ VIỆN NÀY ĐỂ DÙNG TEXT MESH PRO

public class SaveLoadUIController : MonoBehaviour
{
    [Header("Các Panel UI")]
    public GameObject pauseMenuPanel;
    public GameObject saveLoadPanel;

    [Header("Hiển thị Chữ")]
    // ĐÃ ĐỔI TỪ 'Text' SANG 'TextMeshProUGUI'
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI[] slotTexts;

    private bool isSavingMode = false;

    private void Start()
    {
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
    }

    public void OpenSaveMenu()
    {
        isSavingMode = true;
        if (titleText != null) titleText.text = "Save File";
        RefreshSlots();

        pauseMenuPanel.SetActive(false);
        saveLoadPanel.SetActive(true);
    }

    public void OpenLoadMenu()
    {
        isSavingMode = false;
        if (titleText != null) titleText.text = "Load File";
        RefreshSlots();

        pauseMenuPanel.SetActive(false);
        saveLoadPanel.SetActive(true);
    }

    public void CloseSaveLoadMenu()
    {
        saveLoadPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);
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

    public void OnSlotClicked(int slotIndex)
    {
        if (isSavingMode)
        {
            if (slotIndex == 0)
            {
                Debug.LogWarning("Can't save on autosave file!");
                return;
            }

            GameSaveManager.SaveGame(slotIndex);
            RefreshSlots();
        }
        else
        {
            if (GameSaveManager.HasSave(slotIndex))
            {
                GameSaveManager.LoadGame(slotIndex);
                CloseSaveLoadMenu();

                PauseMenuManager pauseManager = Object.FindFirstObjectByType<PauseMenuManager>();
                if (pauseManager != null) pauseManager.Resume();
            }
        }
    }
}