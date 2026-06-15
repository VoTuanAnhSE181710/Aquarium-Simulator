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

        pendingSpawnPointId = spawnPointId;
        EnsurePersistentPlayer(player);
        EnsurePersistentCamera();
        SceneManager.LoadScene(sceneName);
    }

    private static void EnsurePersistentPlayer(GameObject player)
    {
        persistentPlayer = player.transform.root.gameObject;
        UnityEngine.Object.DontDestroyOnLoad(persistentPlayer);
    }

    private static void EnsurePersistentCamera()
    {
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
        if (persistentPlayer == null)
        {
            persistentPlayer = GameObject.Find(PlayerObjectName);
        }

        RemoveRuntimeDuplicates();
        RewireCamera();
        MovePlayerToPendingSpawn();
    }

    private static void RemoveRuntimeDuplicates()
    {
        if (persistentPlayer != null)
        {
            foreach (GameObject duplicate in GameObject.FindGameObjectsWithTag("Untagged")
                         .Where(candidate => candidate.name == PlayerObjectName && candidate != persistentPlayer))
            {
                UnityEngine.Object.Destroy(duplicate);
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

        ThirdPersonCameraFollow follow = persistentCamera.GetComponentInChildren<ThirdPersonCameraFollow>(true);
        if (follow != null)
        {
            follow.SetTarget(persistentPlayer.transform);
        }

        MinhThirdPersonController controller = persistentPlayer.GetComponent<MinhThirdPersonController>();
        if (controller != null)
        {
            controller.SetCameraTransform(persistentCamera.transform);
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

        CharacterController characterController = persistentPlayer.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        Transform playerTransform = persistentPlayer.transform;
        playerTransform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }

        pendingSpawnPointId = null;
    }
}
