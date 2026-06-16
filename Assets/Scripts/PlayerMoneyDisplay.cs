using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerMoneyDisplay : MonoBehaviour
{
    private const string ObjectName = "Player Money Display";

    [SerializeField, Min(0)] private int startingMoney = 10000;
    [SerializeField] private Vector2 screenOffset = new(20f, 20f);
    [SerializeField] private Vector2 displaySize = new(220f, 42f);

    private static PlayerMoneyDisplay instance;
    private GUIStyle moneyStyle;
    private int currentMoney;

    public static int CurrentMoney => instance != null ? instance.currentMoney : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    public static void AddMoney(int amount)
    {
        EnsureInstance();
        if (instance == null || amount <= 0)
        {
            return;
        }

        instance.currentMoney += amount;
    }

    public static bool TrySpendMoney(int amount)
    {
        EnsureInstance();
        if (instance == null || amount < 0 || instance.currentMoney < amount)
        {
            return false;
        }

        instance.currentMoney -= amount;
        return true;
    }

    public static bool CanAfford(int amount)
    {
        EnsureInstance();
        return instance != null && amount >= 0 && instance.currentMoney >= amount;
    }

    public static string FormatVnd(int amount)
    {
        return $"{amount.ToString("N0", CultureInfo.InvariantCulture)} VND";
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null || FindFirstObjectByType<PlayerMoneyDisplay>() != null)
        {
            return;
        }

        GameObject displayObject = new(ObjectName);
        displayObject.AddComponent<PlayerMoneyDisplay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        currentMoney = Mathf.Max(0, startingMoney);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        EnsureStyle();
        Rect rect = new(screenOffset.x, screenOffset.y, displaySize.x, displaySize.y);
        GUI.Box(rect, $"Tiền: {FormatVnd(currentMoney)}", moneyStyle);
    }

    private void EnsureStyle()
    {
        if (moneyStyle != null)
        {
            return;
        }

        moneyStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(16, 12, 8, 8)
        };
    }
}
