using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class HouseLightSwitchController : MonoBehaviour
{
    private const string ControllerName = "House Light Switch Controller";
    private const string CharacterHouseName = "CharacterHouse (1)";

    [SerializeField] private bool lightsStartOn = true;
    [SerializeField] private float interactionDistance = 2.2f;
    [SerializeField] private string promptText = "Press L to toggle house lights";

    private readonly List<Light> houseLights = new();
    private Transform player;
    private Transform lightSwitch;
    private GUIStyle promptStyle;
    private bool lightsOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene" ||
            FindFirstObjectByType<HouseLightSwitchController>() != null)
        {
            return;
        }

        GameObject controllerObject = new(ControllerName);
        controllerObject.AddComponent<HouseLightSwitchController>();
    }

    private void Awake()
    {
        lightsOn = lightsStartOn;
        BuildHouseLights();
        ApplyLightState();
        FindPlayer();
    }

    private void Update()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.lKey.wasPressedThisFrame && IsPlayerNearSwitch())
        {
            lightsOn = !lightsOn;
            ApplyLightState();
        }
    }

    private void BuildHouseLights()
    {
        Transform houseRoot = FindHouseRoot();
        Bounds houseBounds = GetHouseBounds(houseRoot);
        CollectExistingHouseLights(houseRoot, houseBounds);
        CreateSwitchNearDoor(houseBounds);
    }

    private void CollectExistingHouseLights(Transform houseRoot, Bounds houseBounds)
    {
        houseLights.Clear();
        Bounds expandedHouseBounds = houseBounds;
        expandedHouseBounds.Expand(new Vector3(1.5f, 1f, 1.5f));

        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light sceneLight in sceneLights)
        {
            if (!CanControlLight(sceneLight, houseRoot, expandedHouseBounds))
            {
                continue;
            }

            ConfigureControlledLight(sceneLight);
            houseLights.Add(sceneLight);
        }

        if (houseLights.Count == 0)
        {
            Light closestSpotLight = FindClosestIndoorSpotLight(sceneLights, houseBounds);
            if (closestSpotLight != null)
            {
                ConfigureControlledLight(closestSpotLight);
                houseLights.Add(closestSpotLight);
            }
        }
    }

    private static bool CanControlLight(Light sceneLight, Transform houseRoot, Bounds houseBounds)
    {
        if (sceneLight == null ||
            sceneLight.type == LightType.Directional ||
            IsOutdoorLightName(sceneLight.name))
        {
            return false;
        }

        Transform lightTransform = sceneLight.transform;
        return houseBounds.Contains(lightTransform.position) ||
               (houseRoot != null && lightTransform.IsChildOf(houseRoot));
    }

    private static bool IsOutdoorLightName(string lightName)
    {
        return lightName.StartsWith("Street", System.StringComparison.OrdinalIgnoreCase) ||
               lightName.StartsWith("Road", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureControlledLight(Light sceneLight)
    {
        sceneLight.shadows = LightShadows.Soft;
        sceneLight.shadowStrength = Mathf.Max(sceneLight.shadowStrength, 0.8f);
        sceneLight.shadowBias = Mathf.Min(sceneLight.shadowBias, 0.035f);
        sceneLight.shadowNormalBias = Mathf.Min(sceneLight.shadowNormalBias, 0.35f);
    }

    private static Light FindClosestIndoorSpotLight(Light[] sceneLights, Bounds houseBounds)
    {
        Light closestLight = null;
        float bestDistance = float.MaxValue;
        float maxDistance = Mathf.Max(houseBounds.size.magnitude, 8f);

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight == null ||
                sceneLight.type != LightType.Spot ||
                IsOutdoorLightName(sceneLight.name))
            {
                continue;
            }

            float distance = (sceneLight.transform.position - houseBounds.center).sqrMagnitude;
            if (distance >= bestDistance || distance > maxDistance * maxDistance)
            {
                continue;
            }

            bestDistance = distance;
            closestLight = sceneLight;
        }

        return closestLight;
    }

    private Transform FindHouseRoot()
    {
        GameObject characterHouse = GameObject.Find(CharacterHouseName);
        if (characterHouse != null)
        {
            return characterHouse.transform;
        }

        Transform bed = FindTransformByNamePrefix("Bed");
        return bed != null ? bed.root : null;
    }

    private Bounds GetHouseBounds(Transform houseRoot)
    {
        Renderer[] renderers = houseRoot != null
            ? houseRoot.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, new Vector3(8f, 4f, 8f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private Transform FindTransformByNamePrefix(string objectNamePrefix)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name.StartsWith(objectNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private void CreateSwitchNearDoor(Bounds houseBounds)
    {
        Transform door = FindDoorTransform();
        Vector3 switchPosition = door != null
            ? door.position + door.right * 0.45f + Vector3.up * 1.2f
            : new Vector3(houseBounds.min.x + 0.12f, houseBounds.min.y + 1.35f, houseBounds.center.z);

        GameObject switchObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        switchObject.name = "House Light Switch - Press L";
        switchObject.transform.SetParent(transform, true);
        switchObject.transform.position = switchPosition;
        switchObject.transform.rotation = door != null ? Quaternion.LookRotation(door.forward, Vector3.up) : Quaternion.identity;
        switchObject.transform.localScale = new Vector3(0.08f, 0.45f, 0.28f);

        Renderer renderer = switchObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.92f, 0.9f, 0.82f);
            renderer.sharedMaterial = material;
        }

        lightSwitch = switchObject.transform;
    }

    private Transform FindDoorTransform()
    {
        Transform houseRoot = FindHouseRoot();
        if (houseRoot != null)
        {
            Transform door = houseRoot.Find("Floors/Door Main") ??
                             FindChildByName(houseRoot, "Door Main") ??
                             FindChildByName(houseRoot, "Door");
            if (door != null)
            {
                return door;
            }
        }

        return FindTransformByNamePrefix("WallDoor") ?? FindTransformByNamePrefix("Door Main");
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void ApplyLightState()
    {
        foreach (Light houseLight in houseLights)
        {
            if (houseLight != null)
            {
                houseLight.enabled = lightsOn;
            }
        }
    }

    private bool IsPlayerNearSwitch()
    {
        if (player == null || lightSwitch == null)
        {
            return false;
        }

        return (player.position - lightSwitch.position).sqrMagnitude <= interactionDistance * interactionDistance;
    }

    private void FindPlayer()
    {
        GameObject minh = GameObject.Find("Minh");
        if (minh != null)
        {
            player = minh.transform;
        }
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused || !IsPlayerNearSwitch())
        {
            return;
        }

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                padding = new RectOffset(16, 16, 10, 10)
            };
        }

        const float width = 300f;
        const float height = 42f;
        Rect promptRect = new((Screen.width - width) * 0.5f, Screen.height - 190f, width, height);
        GUI.Box(promptRect, promptText, promptStyle);
    }

    // --- THÊM PHẦN NÀY VÀO CUỐI FILE ---
    public bool LightsOn => lightsOn;

    public void SetLightState(bool on)
    {
        lightsOn = on;
        ApplyLightState();
    }
}
