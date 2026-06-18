using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class CoffeeShopWorkStation : MonoBehaviour
{
    [SerializeField] private string promptText = "B\u1ea5m F \u0111\u1ec3 l\u00e0m vi\u1ec7c";
    [SerializeField] private string transitionText = "6 ti\u1ebfng sau ...";
    [SerializeField] private string playerObjectName = "Minh";
    [SerializeField] private float activationRadius = 2f;
    [SerializeField, Min(0f)] private float workHours = 6f;
    [SerializeField, Min(0)] private int hourlyPayVnd = 20000;
    [SerializeField, Min(0.1f)] private float transitionSeconds = 2f;

    private GameObject nearbyPlayer;
    private GUIStyle promptStyle;
    private GUIStyle transitionStyle;
    private bool isWorking;

    private void Reset()
    {
        Collider stationCollider = GetComponent<Collider>();
        stationCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider stationCollider = GetComponent<Collider>();
        stationCollider.isTrigger = true;
    }

    private void Update()
    {
        UpdateNearbyPlayerByDistance();

        Keyboard keyboard = Keyboard.current;
        if (isWorking || nearbyPlayer == null || keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            return;
        }

        StartCoroutine(RunWorkShift());
    }

    private IEnumerator RunWorkShift()
    {
        isWorking = true;
        yield return new WaitForSecondsRealtime(transitionSeconds);

        CoffeeShopSceneTransition.QueueTimeSkip(workHours);
        PlayerMoneyDisplay.AddMoney(Mathf.RoundToInt(workHours * hourlyPayVnd));
        isWorking = false;
    }

    private void UpdateNearbyPlayerByDistance()
    {
        GameObject player = CoffeeShopSceneTransition.GetActivePlayer();
        if (player == null)
        {
            player = nearbyPlayer;
        }

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

        return candidate.name == playerObjectName ||
               candidate.GetComponentInChildren<MinhThirdPersonController>(true) != null;
    }

    private void OnGUI()
    {
        if (isWorking)
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

        const float width = 280f;
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

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), transitionText, transitionStyle);
        GUI.depth = previousDepth;
    }
}
