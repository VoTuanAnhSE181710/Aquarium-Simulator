using UnityEngine;
using System.IO;
using System;

[System.Serializable]
public class SaveData
{
    public string realTimeSaved; // Lưu thời gian thực tế (VD: 21/06/2026 10:00)
    public string gameTimeInfo;  // Lưu thông tin trong game (VD: Ngày 5 - 10000 VND)
    public int money;
    public int day, month, weekDay;
    public float timeOfDay;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
}

public class GameSaveManager : MonoBehaviour
{
    // -1 nghĩa là New Game, 0-3 là số thứ tự Slot cần Load
    public static int TargetSlotToLoad = -1;

    private void Start()
    {
        // Khi SampleScene mở lên, nếu có chỉ định Slot thì Load
        if (TargetSlotToLoad >= 0)
        {
            LoadGame(TargetSlotToLoad);
            TargetSlotToLoad = -1; // Reset sau khi load xong
        }
    }

    public static string GetSavePath(int slot)
    {
        string prefix = (slot == 0) ? "Auto" : slot.ToString();
        return Application.persistentDataPath + $"/AquariumSave_{prefix}.json";
    }

    public static bool HasSave(int slot)
    {
        return File.Exists(GetSavePath(slot));
    }

    // Hàm lấy thông tin tóm tắt để hiển thị lên UI (nút bấm)
    public static string GetSaveSummary(int slot)
    {
        if (!HasSave(slot)) return "Empty";
        try
        {
            string json = File.ReadAllText(GetSavePath(slot));
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return $"{data.gameTimeInfo}\n({data.realTimeSaved})";
        }
        catch
        {
            return "Corrupted";
        }
    }

    // Tự động tìm Slot được lưu gần đây nhất (Để dùng cho nút Continue)
    public static int GetMostRecentSaveSlot()
    {
        int bestSlot = -1;
        DateTime newestTime = DateTime.MinValue;

        for (int i = 0; i <= 3; i++)
        {
            if (HasSave(i))
            {
                DateTime lastWrite = File.GetLastWriteTime(GetSavePath(i));
                if (lastWrite > newestTime)
                {
                    newestTime = lastWrite;
                    bestSlot = i;
                }
            }
        }
        return bestSlot;
    }

    public static void SaveGame(int slot)
    {
        SaveData data = new SaveData();
        data.realTimeSaved = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        data.money = PlayerMoneyDisplay.CurrentMoney;

        // Đã sửa thành UnityEngine.Object
        DayNightCycle dnc = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc != null)
        {
            data.day = dnc.CurrentDay;
            data.month = dnc.CurrentMonth;
            data.weekDay = (int)dnc.CurrentWeekDay;
            data.timeOfDay = dnc.CurrentTimeOfDay;
            data.gameTimeInfo = $"{data.day}/{data.month} - {data.money} VND";
        }

        // Đã sửa thành UnityEngine.Object
        MinhThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<MinhThirdPersonController>();
        if (player != null)
        {
            data.posX = player.transform.position.x;
            data.posY = player.transform.position.y;
            data.posZ = player.transform.position.z;
            data.rotX = player.transform.rotation.x;
            data.rotY = player.transform.rotation.y;
            data.rotZ = player.transform.rotation.z;
            data.rotW = player.transform.rotation.w;
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(slot), json);
        Debug.Log($"Saved Game into Slot {slot} successfully!");
    }

    public static void LoadGame(int slot)
    {
        if (!HasSave(slot)) return;

        string json = File.ReadAllText(GetSavePath(slot));
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        PlayerMoneyDisplay.SetMoney(data.money);

        // Đã sửa thành UnityEngine.Object
        DayNightCycle dnc = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc != null)
        {
            dnc.SetDateTime(data.day, data.month, (DayNightCycle.WeekDay)data.weekDay, data.timeOfDay);
        }

        // Đã sửa thành UnityEngine.Object
        MinhThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<MinhThirdPersonController>();
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
            player.transform.rotation = new Quaternion(data.rotX, data.rotY, data.rotZ, data.rotW);

            if (cc != null) cc.enabled = true;
        }

        Debug.Log($"Loaded Game from Slot {slot} successfully!");
    }
}