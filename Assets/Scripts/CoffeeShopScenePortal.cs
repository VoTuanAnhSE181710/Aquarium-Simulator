using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class CoffeeShopScenePortal : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "CoffeeShopInteriorNIGHT";
    [SerializeField] private string targetSpawnPointId = "CoffeeInteriorEntry";
    [SerializeField] private string promptText = "Press F to enter";
    [SerializeField] private string choiceTitle = "Coffee Shop";
    [SerializeField] private string enterShopText = "1. Vào shop";
    [SerializeField] private string workShiftText = "2. Làm việc";
    [SerializeField] private string closeChoiceText = "Esc để đóng";
    [SerializeField] private string playerObjectName = "Minh";
    [SerializeField] private float activationRadius = 4.5f;
    [SerializeField] private string workSceneName = "CoffeeShopInteriorNIGHT";
    [SerializeField] private string workTransitionText = "4 tiếng sau ...";
    [SerializeField, Min(0f)] private float workHours = 4f;
    [SerializeField, Min(0)] private int hourlyPayVnd = 20000;
    [SerializeField, Min(0.1f)] private float workTransitionSeconds = 2f;

    private GameObject nearbyPlayer;
    private GUIStyle promptStyle;
    private GUIStyle choiceStyle;
    private GUIStyle choiceButtonStyle;
    private GUIStyle choiceHintStyle;
    private GUIStyle transitionStyle;
    private bool isTransitioning;
    private bool isChoiceMenuOpen;

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
        if (isTransitioning || keyboard == null)
        {
            return;
        }

        if (nearbyPlayer == null)
        {
            isChoiceMenuOpen = false;
            return;
        }

        if (!ShouldOfferWorkShift())
        {
            if (keyboard.fKey.wasPressedThisFrame)
            {
                EnterShop();
            }

            return;
        }

        if (!isChoiceMenuOpen)
        {
            if (keyboard.fKey.wasPressedThisFrame)
            {
                isChoiceMenuOpen = true;
            }

            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            EnterShop();
        }
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            StartWorkShift();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame)
        {
            isChoiceMenuOpen = false;
        }
    }

    private void EnterShop()
    {
        isChoiceMenuOpen = false;
        CoffeeShopSceneTransition.LoadScene(targetSceneName, targetSpawnPointId, nearbyPlayer);
    }

    private void StartWorkShift()
    {
        isChoiceMenuOpen = false;
        StartCoroutine(RunWorkShift());
    }

    private IEnumerator RunWorkShift()
    {
        isTransitioning = true;
        yield return new WaitForSecondsRealtime(workTransitionSeconds);

        DayNightCycle dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        if (dayNightCycle != null)
        {
            dayNightCycle.SkipHours(workHours);
        }

        PlayerMoneyDisplay.AddMoney(Mathf.RoundToInt(workHours * hourlyPayVnd));
        isTransitioning = false;
    }

    private bool ShouldOfferWorkShift()
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
            isChoiceMenuOpen = false;
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
        if (isTransitioning && ShouldOfferWorkShift())
        {
            DrawWorkTransition();
            return;
        }

        if (nearbyPlayer == null || string.IsNullOrWhiteSpace(promptText))
        {
            return;
        }

        if (isChoiceMenuOpen && ShouldOfferWorkShift())
        {
            DrawChoiceMenu();
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

    private void DrawChoiceMenu()
    {
        EnsureChoiceStyles();

        const float width = 320f;
        const float height = 188f;
        Rect menuRect = new(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.Box(menuRect, choiceTitle, choiceStyle);

        Rect enterRect = new(menuRect.x + 24f, menuRect.y + 56f, width - 48f, 38f);
        Rect workRect = new(menuRect.x + 24f, menuRect.y + 102f, width - 48f, 38f);
        Rect hintRect = new(menuRect.x + 24f, menuRect.y + 146f, width - 48f, 24f);

        if (GUI.Button(enterRect, enterShopText, choiceButtonStyle))
        {
            EnterShop();
        }

        if (GUI.Button(workRect, workShiftText, choiceButtonStyle))
        {
            StartWorkShift();
        }

        GUI.Label(hintRect, closeChoiceText, choiceHintStyle);
    }

    private void EnsureChoiceStyles()
    {
        if (choiceStyle != null)
        {
            return;
        }

        choiceStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(18, 18, 16, 16)
        };

        choiceButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        choiceHintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = { textColor = Color.white }
        };
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
