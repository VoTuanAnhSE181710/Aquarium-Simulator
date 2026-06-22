using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

[System.Serializable]
public class PlacedObjectData
{
    public string itemName;
    public float colorR, colorG, colorB, colorA;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
}

[System.Serializable]
public class InventorySlotData
{
    public string itemName;
    public float colorR, colorG, colorB, colorA;
    public int slotIndex;
}

[System.Serializable]
public class DoorStateData
{
    public string doorId;
    public bool isOpen;
}

[System.Serializable]
public class SaveData
{
    public string realTimeSaved;
    public string gameTimeInfo;
    public int money;
    public int day, month, weekDay;
    public float timeOfDay;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;

    // A. Đi học
    public int absenceCount;
    public int failedSubjectCount;
    public int attendanceDayKey;
    public bool attendedToday;
    public bool processedAbsenceToday;

    // B. Nước trong bể
    public float cleanliness;
    public float oxygen;
    public float chlorine;
    public bool aeratorOn;

    // C. Mảng List quản lý
    public List<PlacedObjectData> placedObjects;
    public List<InventorySlotData> inventorySlots;
    public List<DoorStateData> doorStates;
    public bool houseLightsOn;
    public List<PlacedObjectData> worldItems;
}

public class GameSaveManager : MonoBehaviour
{
    public static int TargetSlotToLoad = -1;

    private void Start()
    {
        if (TargetSlotToLoad >= 0)
        {
            LoadGame(TargetSlotToLoad);
            TargetSlotToLoad = -1;
        }
    }

    public GameObject GetPrefabByName(string name)
    {
        // Hàm Resources.Load sẽ tự động mò vào thư mục "Resources" trong dự án
        // để tìm file Prefab có tên khớp xác với 'name'
        GameObject prefab = Resources.Load<GameObject>(name);

        if (prefab == null)
        {
            Debug.LogWarning($"LỖI: Không tìm thấy món đồ '{name}'. Bạn đã bỏ nó vào thư mục Resources chưa?");
        }

        return prefab;
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
            return "Dữ liệu lỗi (Corrupted)";
        }
    }

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
        GameSaveManager instance = UnityEngine.Object.FindFirstObjectByType<GameSaveManager>();
        SaveData data = new SaveData();
        data.realTimeSaved = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        data.money = PlayerMoneyDisplay.CurrentMoney;

        DayNightCycle dnc = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc != null)
        {
            data.day = dnc.CurrentDay; data.month = dnc.CurrentMonth; data.weekDay = (int)dnc.CurrentWeekDay;
            data.timeOfDay = dnc.CurrentTimeOfDay;
            data.gameTimeInfo = $"{data.day}/{data.month} - {data.money} VND";
        }

        MinhThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<MinhThirdPersonController>();
        if (player != null)
        {
            data.posX = player.transform.position.x; data.posY = player.transform.position.y; data.posZ = player.transform.position.z;
            data.rotX = player.transform.rotation.x; data.rotY = player.transform.rotation.y;
            data.rotZ = player.transform.rotation.z; data.rotW = player.transform.rotation.w;
        }

        BusSchoolAttendanceController bus = UnityEngine.Object.FindFirstObjectByType<BusSchoolAttendanceController>();
        if (bus != null) bus.GetAttendanceData(out data.absenceCount, out data.failedSubjectCount, out data.attendanceDayKey, out data.attendedToday, out data.processedAbsenceToday);

        FishTankWaterQuality tank = UnityEngine.Object.FindFirstObjectByType<FishTankWaterQuality>();
        if (tank != null)
        {
            tank.GetWaterData(out data.cleanliness, out data.oxygen, out data.chlorine, out data.aeratorOn);
            data.placedObjects = tank.GetAllPlacedObjects();
        }

        SimpleInventory inv = UnityEngine.Object.FindFirstObjectByType<SimpleInventory>();
        if (inv != null) data.inventorySlots = inv.GetInventoryData();

        HouseLightSwitchController light = UnityEngine.Object.FindFirstObjectByType<HouseLightSwitchController>();
        if (light != null) data.houseLightsOn = light.LightsOn;

        data.doorStates = new List<DoorStateData>();
        DoorInteraction[] doors = UnityEngine.Object.FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);
        foreach (var d in doors) data.doorStates.Add(new DoorStateData { doorId = d.gameObject.name, isOpen = d.IsOpen });

        data.worldItems = new List<PlacedObjectData>();
        InventoryPickupItem[] allPickups = UnityEngine.Object.FindObjectsByType<InventoryPickupItem>(FindObjectsSortMode.None);
        Bounds tankBounds = tank != null && tank.GetComponentInChildren<Collider>() != null ? tank.GetComponentInChildren<Collider>().bounds : new Bounds();

        foreach (var pickup in allPickups)
        {
            if (player != null && pickup.transform.root == player.transform.root) continue;
            if (tank != null && tankBounds.Contains(pickup.transform.position)) continue;

            if (IsStaticFurniture(pickup.ItemName)) continue;

            // Những thứ còn lại chính là đồ rớt ngoài sàn -> LƯU LẠI
            PlacedObjectData wData = new PlacedObjectData();
            wData.itemName = pickup.ItemName;
            wData.posX = pickup.transform.position.x; wData.posY = pickup.transform.position.y; wData.posZ = pickup.transform.position.z;
            wData.rotX = pickup.transform.rotation.x; wData.rotY = pickup.transform.rotation.y;
            wData.rotZ = pickup.transform.rotation.z; wData.rotW = pickup.transform.rotation.w;

            Renderer r = pickup.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Color c = r.material.HasProperty("_BaseColor") ? r.material.GetColor("_BaseColor") : r.material.color;
                wData.colorR = c.r; wData.colorG = c.g; wData.colorB = c.b; wData.colorA = c.a;
            }
            data.worldItems.Add(wData);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(slot), json);
        Debug.Log($"Đã lưu toàn bộ Game vào Slot {slot}!");
    }

    public static void LoadGame(int slot)
    {
        if (!HasSave(slot)) return;
        GameSaveManager instance = UnityEngine.Object.FindFirstObjectByType<GameSaveManager>();

        string json = File.ReadAllText(GetSavePath(slot));
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        PlayerMoneyDisplay.SetMoney(data.money);

        DayNightCycle dnc = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dnc != null) dnc.SetDateTime(data.day, data.month, (DayNightCycle.WeekDay)data.weekDay, data.timeOfDay);

        MinhThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<MinhThirdPersonController>();
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
            player.transform.rotation = new Quaternion(data.rotX, data.rotY, data.rotZ, data.rotW);
            if (cc != null) cc.enabled = true;
        }

        BusSchoolAttendanceController bus = UnityEngine.Object.FindFirstObjectByType<BusSchoolAttendanceController>();
        if (bus != null) bus.SetAttendanceData(data.absenceCount, data.failedSubjectCount, data.attendanceDayKey, data.attendedToday, data.processedAbsenceToday);

        FishTankWaterQuality tank = UnityEngine.Object.FindFirstObjectByType<FishTankWaterQuality>();
        if (tank != null)
        {
            tank.SetWaterData(data.cleanliness, data.oxygen, data.chlorine, data.aeratorOn);
            tank.ClearAndRestorePlacedObjects(data.placedObjects, instance);
        }

        SimpleInventory inv = UnityEngine.Object.FindFirstObjectByType<SimpleInventory>();
        if (inv != null) inv.SetInventoryData(data.inventorySlots, instance);

        HouseLightSwitchController light = UnityEngine.Object.FindFirstObjectByType<HouseLightSwitchController>();
        if (light != null) light.SetLightState(data.houseLightsOn);

        DoorInteraction[] doors = UnityEngine.Object.FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);
        // Tạo một danh sách các cửa "chưa được xử lý"
        List<DoorInteraction> availableDoors = new List<DoorInteraction>(doors);

        foreach (var dData in data.doorStates)
        {
            for (int i = 0; i < availableDoors.Count; i++)
            {
                // Tìm cánh cửa có tên khớp với dữ liệu save
                if (availableDoors[i].gameObject.name == dData.doorId)
                {
                    // Cập nhật trạng thái Đóng/Mở
                    availableDoors[i].SetOpenState(dData.isOpen);

                    // QUAN TRỌNG NHẤT: Xóa cánh cửa này khỏi danh sách chờ 
                    // để lần lặp sau không bao giờ bị nhận diện nhầm vào nó nữa!
                    availableDoors.RemoveAt(i);
                    break; // Thoát vòng lặp con để đọc dòng data tiếp theo
                }
            }
        }

        InventoryPickupItem[] allPickups = UnityEngine.Object.FindObjectsByType<InventoryPickupItem>(FindObjectsSortMode.None);
        Bounds tankBounds = tank != null && tank.GetComponentInChildren<Collider>() != null ? tank.GetComponentInChildren<Collider>().bounds : new Bounds();

        // 1. Xóa sạch sành sanh mọi đồ vật mặc định đang rớt trên sàn của Scene mới
        foreach (var pickup in allPickups)
        {
            if (player != null && pickup.transform.root == player.transform.root) continue;
            if (tank != null && tankBounds.Contains(pickup.transform.position)) continue;

            if (IsStaticFurniture(pickup.ItemName)) continue;

            UnityEngine.Object.Destroy(pickup.gameObject);
        }

        // 2. Spawn lại những món đồ rớt trên sàn từ file Save
        if (data.worldItems != null)
        {
            foreach (var wData in data.worldItems)
            {
                GameObject prefab = instance.GetPrefabByName(wData.itemName);
                Vector3 pos = new Vector3(wData.posX, wData.posY, wData.posZ);
                Quaternion rot = new Quaternion(wData.rotX, wData.rotY, wData.rotZ, wData.rotW);

                GameObject obj = prefab != null ? Instantiate(prefab, pos, rot) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (prefab == null) { obj.transform.position = pos; obj.transform.rotation = rot; obj.transform.localScale = Vector3.one * 0.35f; }
                obj.name = wData.itemName;
                obj.SetActive(true);

                Renderer r = obj.GetComponentInChildren<Renderer>();
                Color c = new Color(wData.colorR, wData.colorG, wData.colorB, wData.colorA);
                if (r != null) { if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c); else r.material.color = c; }

                InventoryPickupItem pickup = obj.GetComponent<InventoryPickupItem>();
                if (pickup == null) pickup = obj.AddComponent<InventoryPickupItem>();
                pickup.Configure(wData.itemName, c, prefab);
                pickup.EnableDroppedPhysics(); // Bật trọng lực để nó rớt xuống sàn tự nhiên
            }
        }

        Debug.Log($"Đã tải toàn bộ Game từ Slot {slot}!");
    }

    private static bool IsStaticFurniture(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLower();
        return lower.Contains("bed") || lower.Contains("table") ||
               lower.Contains("chair") || lower.Contains("closet");
    }
}