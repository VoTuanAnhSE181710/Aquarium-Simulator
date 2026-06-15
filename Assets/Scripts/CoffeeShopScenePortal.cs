using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class CoffeeShopScenePortal : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "CoffeeShopInteriorNIGHT";
    [SerializeField] private string targetSpawnPointId = "CoffeeInteriorEntry";
    [SerializeField] private string promptText = "Press F to enter";
    [SerializeField] private string playerObjectName = "Minh";
    [SerializeField] private float activationRadius = 4.5f;

    private GameObject nearbyPlayer;
    private GUIStyle promptStyle;

    private void Reset()
    {
        Collider portalCollider = GetComponent<Collider>();
        portalCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider portalCollider = GetComponent<Collider>();
        portalCollider.isTrigger = true;
    }

    private void Update()
    {
        UpdateNearbyPlayerByDistance();

        Keyboard keyboard = Keyboard.current;
        if (nearbyPlayer == null || keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            return;
        }

        CoffeeShopSceneTransition.LoadScene(targetSceneName, targetSpawnPointId, nearbyPlayer);
    }

    private void UpdateNearbyPlayerByDistance()
    {
        GameObject player = nearbyPlayer;
        if (player == null)
        {
            MinhThirdPersonController controller = FindFirstObjectByType<MinhThirdPersonController>();
            player = controller != null ? controller.gameObject : null;
        }

        if (!IsPlayer(player))
        {
            nearbyPlayer = null;
            return;
        }

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        nearbyPlayer = offset.sqrMagnitude <= activationRadius * activationRadius ? player : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject candidate = other.transform.root.gameObject;
        if (IsPlayer(candidate))
        {
            nearbyPlayer = candidate;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject candidate = other.transform.root.gameObject;
        if (candidate == nearbyPlayer)
        {
            nearbyPlayer = null;
        }
    }

    private bool IsPlayer(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return candidate.name == playerObjectName || candidate.GetComponent<MinhThirdPersonController>() != null;
    }

    private void OnGUI()
    {
        if (nearbyPlayer == null || string.IsNullOrWhiteSpace(promptText))
        {
            return;
        }

        promptStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(16, 16, 10, 10)
        };

        const float width = 260f;
        const float height = 42f;
        Rect promptRect = new(
            (Screen.width - width) * 0.5f,
            Screen.height - 160f,
            width,
            height);
        GUI.Box(promptRect, promptText, promptStyle);
    }
}
