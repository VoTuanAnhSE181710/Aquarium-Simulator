using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoffeeShopSceneTransition
{
    private const string PlayerObjectName = "Minh";
    private static string pendingSpawnPointId;
    private static GameObject persistentPlayer;
    private static GameObject persistentCamera;
    private static GameTimeSnapshot savedGameTime;
    private static float pendingSkipHours;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void LoadScene(string sceneName, string spawnPointId, GameObject player)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || player == null)
        {
            return;
        }

        CaptureGameTime();
        pendingSpawnPointId = spawnPointId;
        EnsurePersistentPlayer(player);
        EnsurePersistentCamera();
        SceneManager.LoadScene(sceneName);
    }

    public static void QueueTimeSkip(float hours)
    {
        if (hours > 0f)
        {
            pendingSkipHours += hours;
        }
    }

    public static GameObject GetActivePlayer()
    {
        ResolvePersistentPlayer();
        return persistentPlayer;
    }

    private static void EnsurePersistentPlayer(GameObject player)
    {
        if (persistentPlayer != null &&
            persistentPlayer.GetComponentInChildren<MinhThirdPersonController>(true) != null)
        {
            UnityEngine.Object.DontDestroyOnLoad(persistentPlayer);
            return;
        }

        MinhThirdPersonController incomingController = player.GetComponentInParent<MinhThirdPersonController>();
        persistentPlayer = incomingController != null
            ? incomingController.transform.root.gameObject
            : player.transform.root.gameObject;
        UnityEngine.Object.DontDestroyOnLoad(persistentPlayer);
    }

    private static void EnsurePersistentCamera()
    {
        if (persistentCamera != null)
        {
            UnityEngine.Object.DontDestroyOnLoad(persistentCamera);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            ThirdPersonCameraFollow followCamera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCameraFollow>();
            mainCamera = followCamera != null ? followCamera.GetComponent<Camera>() : null;
        }

        if (mainCamera == null)
        {
            return;
        }

        persistentCamera = mainCamera.transform.root.gameObject;
        UnityEngine.Object.DontDestroyOnLoad(persistentCamera);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolvePersistentPlayer();
        RemoveRuntimeDuplicates();
        RewireCamera();
        MovePlayerToPendingSpawn();
        RestoreGameTime();
    }

    private static void CaptureGameTime()
    {
        DayNightCycle dayNightCycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dayNightCycle == null)
        {
            return;
        }

        savedGameTime = new GameTimeSnapshot(
            dayNightCycle.CurrentDay,
            dayNightCycle.CurrentMonth,
            dayNightCycle.CurrentWeekDay,
            dayNightCycle.CurrentTimeOfDay,
            true);
    }

    private static void RestoreGameTime()
    {
        DayNightCycle dayNightCycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        if (dayNightCycle == null || !savedGameTime.HasValue)
        {
            return;
        }

        dayNightCycle.SetDateTime(
            savedGameTime.Day,
            savedGameTime.Month,
            savedGameTime.WeekDay,
            savedGameTime.TimeOfDay);

        if (pendingSkipHours > 0f)
        {
            dayNightCycle.SkipHours(pendingSkipHours);

            GameSaveManager.SaveGame(0);

            CaptureGameTime();
            pendingSkipHours = 0f;
        }
    }

    private static void RemoveRuntimeDuplicates()
    {
        if (persistentPlayer != null)
        {
            MinhThirdPersonController[] playerControllers = UnityEngine.Object.FindObjectsByType<MinhThirdPersonController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MinhThirdPersonController controller in playerControllers)
            {
                GameObject candidateRoot = controller.transform.root.gameObject;
                if (candidateRoot != persistentPlayer)
                {
                    UnityEngine.Object.Destroy(candidateRoot);
                }
            }
        }

        if (persistentCamera == null)
        {
            return;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Camera camera in cameras)
        {
            GameObject cameraRoot = camera.transform.root.gameObject;
            if (cameraRoot != persistentCamera)
            {
                UnityEngine.Object.Destroy(cameraRoot);
            }
        }
    }

    private static void RewireCamera()
    {
        if (persistentPlayer == null || persistentCamera == null)
        {
            return;
        }

        MinhThirdPersonController controller = persistentPlayer.GetComponentInChildren<MinhThirdPersonController>(true);
        Transform playerTarget = controller != null ? controller.transform : persistentPlayer.transform;

        ThirdPersonCameraFollow follow = persistentCamera.GetComponentInChildren<ThirdPersonCameraFollow>(true);
        if (follow != null)
        {
            follow.SetTarget(playerTarget);
        }

        MouseLock[] mouseLocks = UnityEngine.Object.FindObjectsByType<MouseLock>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MouseLock mouseLock in mouseLocks)
        {
            mouseLock.SetPlayerBody(playerTarget);
        }

        if (controller != null)
        {
            controller.SetCameraTransform(persistentCamera.transform);
        }
    }

    private static void ResolvePersistentPlayer()
    {
        if (persistentPlayer != null &&
            persistentPlayer.GetComponentInChildren<MinhThirdPersonController>(true) != null)
        {
            return;
        }

        MinhThirdPersonController controller = UnityEngine.Object
            .FindObjectsByType<MinhThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();
        if (controller != null)
        {
            persistentPlayer = controller.transform.root.gameObject;
            return;
        }

        GameObject namedPlayer = GameObject.Find(PlayerObjectName);
        if (namedPlayer != null)
        {
            persistentPlayer = namedPlayer.transform.root.gameObject;
        }
    }

    private static void MovePlayerToPendingSpawn()
    {
        if (persistentPlayer == null || string.IsNullOrWhiteSpace(pendingSpawnPointId))
        {
            return;
        }

        CoffeeShopSpawnPoint spawnPoint = UnityEngine.Object
            .FindObjectsByType<CoffeeShopSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => string.Equals(
                candidate.SpawnPointId,
                pendingSpawnPointId,
                StringComparison.OrdinalIgnoreCase));

        if (spawnPoint == null)
        {
            return;
        }

        CharacterController characterController = persistentPlayer.GetComponentInChildren<CharacterController>(true);
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        Transform playerTransform = characterController != null ? characterController.transform : persistentPlayer.transform;
        playerTransform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }

        pendingSpawnPointId = null;
    }

    private readonly struct GameTimeSnapshot
    {
        public GameTimeSnapshot(
            int day,
            int month,
            DayNightCycle.WeekDay weekDay,
            float timeOfDay,
            bool hasValue)
        {
            Day = day;
            Month = month;
            WeekDay = weekDay;
            TimeOfDay = timeOfDay;
            HasValue = hasValue;
        }

        public int Day { get; }
        public int Month { get; }
        public DayNightCycle.WeekDay WeekDay { get; }
        public float TimeOfDay { get; }
        public bool HasValue { get; }
    }
}
