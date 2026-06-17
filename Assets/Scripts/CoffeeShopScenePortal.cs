using System.Collections;
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
    [SerializeField] private string workSceneName = "CoffeeShopInteriorNIGHT";
    [SerializeField] private string workTransitionText = "4 tiếng sau ...";
    [SerializeField, Min(0f)] private float workHours = 4f;
    [SerializeField, Min(0)] private int hourlyPayVnd = 20000;
    [SerializeField, Min(0.1f)] private float workTransitionSeconds = 2f;

    private GameObject nearbyPlayer;
    private GUIStyle promptStyle;
    private GUIStyle transitionStyle;
    private bool isTransitioning;

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
        if (isTransitioning || nearbyPlayer == null || keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            return;
        }

        StartCoroutine(LoadSceneAfterOptionalWorkShift(nearbyPlayer));
    }

    private IEnumerator LoadSceneAfterOptionalWorkShift(GameObject player)
    {
        isTransitioning = true;

        if (ShouldRunWorkShift())
        {
            yield return new WaitForSecondsRealtime(workTransitionSeconds);

            DayNightCycle dayNightCycle = FindFirstObjectByType<DayNightCycle>();
            if (dayNightCycle != null)
            {
                dayNightCycle.SkipHours(workHours);
            }

            PlayerMoneyDisplay.AddMoney(Mathf.RoundToInt(workHours * hourlyPayVnd));
        }

        CoffeeShopSceneTransition.LoadScene(targetSceneName, targetSpawnPointId, player);
    }

    private bool ShouldRunWorkShift()
    {
        return !string.IsNullOrWhiteSpace(workSceneName) &&
               string.Equals(targetSceneName, workSceneName, System.StringComparison.OrdinalIgnoreCase) &&
               workHours > 0f &&
               hourlyPayVnd > 0;
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
        if (isTransitioning && ShouldRunWorkShift())
        {
            DrawWorkTransition();
            return;
        }

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

    private void DrawWorkTransition()
    {
        transitionStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.depth = -10000;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), workTransitionText, transitionStyle);
        GUI.depth = previousDepth;
    }
}
