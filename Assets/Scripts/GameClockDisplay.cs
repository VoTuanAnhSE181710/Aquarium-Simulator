using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameClockDisplay : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private bool showPeriodLabel = true;
    [SerializeField] private Vector2 screenOffset = new(20f, 20f);
    [SerializeField] private Vector2 clockSize = new(180f, 48f);

    private GUIStyle clockStyle;

    // 1. Hàm này tự động chạy khi bật game, nhưng thay vì tạo đồng hồ luôn, nó chỉ ĐĂNG KÝ THEO DÕI việc load scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneListener()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 2. Hàm này sẽ được kích hoạt mỗi khi bất kỳ một Scene nào đó được load xong
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Nếu Scene vừa load là "SampleScene" và chưa có đồng hồ -> Tự động sinh ra đồng hồ
        if (scene.name == "SampleScene")
        {
            if (FindFirstObjectByType<GameClockDisplay>() == null)
            {
                GameObject clockObject = new("Game Clock Display");
                // LƯU Ý: Không còn DontDestroyOnLoad nữa, nên nó sẽ tự biến mất khi rời khỏi SampleScene
                clockObject.AddComponent<GameClockDisplay>();
            }
        }
    }

    private void Awake()
    {
        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        }
    }

    private void OnGUI()
    {
        // (Tùy chọn) Thêm dòng check PauseMenuManager ở đây nếu bạn muốn đồng hồ cũng tàng hình khi bấm Tạm Dừng game
        // if (PauseMenuManager.GameIsPaused) return;

        EnsureStyle();

        float timeOfDay = dayNightCycle != null
            ? dayNightCycle.CurrentTimeOfDay
            : Mathf.Repeat(Time.time / 60f, 24f);
        int totalMinutes = Mathf.FloorToInt(timeOfDay * 60f);
        int hour = (totalMinutes / 60) % 24;
        int minute = totalMinutes % 60;
        bool isNight = hour < 6 || hour >= 18;
        string label = showPeriodLabel
            ? $"{hour:00}:{minute:00} {(isNight ? "Night" : "Day")}"
            : $"{hour:00}:{minute:00}";

        Rect rect = new(
            Screen.width - clockSize.x - screenOffset.x,
            screenOffset.y,
            clockSize.x,
            clockSize.y);
        GUI.Box(rect, label, clockStyle);
    }

    private void EnsureStyle()
    {
        if (clockStyle != null)
        {
            return;
        }

        clockStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Color.white
            },
            padding = new RectOffset(10, 10, 8, 8)
        };
    }
}