using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class BusSchoolAttendanceController : MonoBehaviour
{
    private const string ControllerName = "Bus School Attendance Controller";
    private const string BusNamePrefix = "Vehicle_Bus";
    private const string BusStopNamePrefix = "Props_Bus Stop";

    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private float busStartHour = 6.5f;
    [SerializeField] private float busEndHour = 7f;
    [SerializeField] private float schoolReturnHour = 12f;
    [SerializeField, Min(0)] private int busFareVnd = 3000;
    [SerializeField] private int absencesToFailSubject = 4;
    [SerializeField] private int failedSubjectsToLose = 3;

    private DayNightCycle dayNightCycle;
    private Transform player;
    private Transform busTarget;
    private Collider[] busColliders = System.Array.Empty<Collider>();
    private GUIStyle promptStyle;
    private int attendanceDayKey = -1;
    private bool attendedToday;
    private bool processedAbsenceToday;
    private int absenceCountInCurrentSubject;
    private int failedSubjectCount;
    private bool gameOver;

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
            FindFirstObjectByType<BusSchoolAttendanceController>() != null)
        {
            return;
        }

        GameObject controllerObject = new(ControllerName);
        controllerObject.AddComponent<BusSchoolAttendanceController>();
    }

    private void Awake()
    {
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        FindPlayer();
        FindBusTarget();
        RefreshAttendanceDay();
    }

    private void Update()
    {
        if (PauseMenuManager.GameIsPaused || gameOver)
        {
            return;
        }

        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        }

        if (player == null)
        {
            FindPlayer();
        }

        if (busTarget == null)
        {
            FindBusTarget();
        }

        RefreshAttendanceDay();
        ProcessMissedBusIfNeeded();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame && CanRideBus())
        {
            RideBusToSchool();
        }
    }

    private void RefreshAttendanceDay()
    {
        if (dayNightCycle == null)
        {
            return;
        }

        int currentDayKey = dayNightCycle.CurrentMonth * 100 + dayNightCycle.CurrentDay;
        if (attendanceDayKey == currentDayKey)
        {
            return;
        }

        attendanceDayKey = currentDayKey;
        attendedToday = false;
        processedAbsenceToday = false;
    }

    private void ProcessMissedBusIfNeeded()
    {
        if (dayNightCycle == null ||
            !dayNightCycle.IsSchoolDay ||
            attendedToday ||
            processedAbsenceToday ||
            dayNightCycle.CurrentTimeOfDay < busEndHour)
        {
            return;
        }

        processedAbsenceToday = true;
        absenceCountInCurrentSubject++;
        if (absenceCountInCurrentSubject >= absencesToFailSubject)
        {
            absenceCountInCurrentSubject = 0;
            failedSubjectCount++;
            gameOver = failedSubjectCount >= failedSubjectsToLose;
        }
    }

    private bool CanRideBus()
    {
        return dayNightCycle != null &&
               dayNightCycle.IsSchoolDay &&
               dayNightCycle.CurrentTimeOfDay >= busStartHour &&
               dayNightCycle.CurrentTimeOfDay <= busEndHour &&
               !attendedToday &&
               IsPlayerNearBus();
    }

    private void RideBusToSchool()
    {
        if (!PlayerMoneyDisplay.TrySpendMoney(busFareVnd))
        {
            return;
        }

        attendedToday = true;
        processedAbsenceToday = true;
        dayNightCycle.SetTimeOfDay(schoolReturnHour);
    }

    private bool IsPlayerNearBus()
    {
        if (player == null || busTarget == null)
        {
            return false;
        }

        foreach (Collider busCollider in busColliders)
        {
            if (busCollider == null || !busCollider.enabled)
            {
                continue;
            }

            Vector3 closestPoint = GetClosestSupportedPoint(busCollider, player.position);
            if ((player.position - closestPoint).sqrMagnitude <= interactionDistance * interactionDistance)
            {
                return true;
            }
        }

        return (player.position - busTarget.position).sqrMagnitude <= interactionDistance * interactionDistance;
    }

    private static Vector3 GetClosestSupportedPoint(Collider collider, Vector3 position)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        if (collider is BoxCollider ||
            collider is SphereCollider ||
            collider is CapsuleCollider ||
            (meshCollider != null && meshCollider.convex))
        {
            return collider.ClosestPoint(position);
        }

        return collider.bounds.ClosestPoint(position);
    }

    private void FindPlayer()
    {
        GameObject minh = GameObject.Find("Minh");
        if (minh != null)
        {
            player = minh.transform;
        }
    }

    private void FindBusTarget()
    {
        busTarget = FindTransformByNamePrefix(BusNamePrefix) ?? FindTransformByNamePrefix(BusStopNamePrefix);
        busColliders = busTarget != null
            ? busTarget.GetComponentsInChildren<Collider>()
            : System.Array.Empty<Collider>();
    }

    private static Transform FindTransformByNamePrefix(string objectNamePrefix)
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

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        EnsurePromptStyle();

        if (gameOver)
        {
            DrawBox("Rớt 3 môn. Thua game.", Screen.height * 0.45f, 440f);
            return;
        }

        if (CanRideBus())
        {
            string fare = PlayerMoneyDisplay.FormatVnd(busFareVnd);
            string prompt = PlayerMoneyDisplay.CanAfford(busFareVnd)
                ? $"Bấm F vào xe bus để đi học - {fare}"
                : $"Không đủ tiền đi xe bus. Cần {fare}";
            DrawBox(prompt, Screen.height - 238f, 500f);
            return;
        }

        if (dayNightCycle != null && dayNightCycle.IsSchoolDay && IsPlayerNearBus())
        {
            string status = dayNightCycle.CurrentTimeOfDay < busStartHour
                ? "Xe bus chạy từ 06:30 đến 07:00"
                : attendedToday
                    ? "Hôm nay đã đi học"
                    : "Hôm nay đã nghỉ học";
            DrawBox(status, Screen.height - 238f, 360f);
        }

        DrawAttendanceStatus();
    }

    private void DrawAttendanceStatus()
    {
        string status =
            $"Nghỉ học: {absenceCountInCurrentSubject}/{absencesToFailSubject}   Rớt môn: {failedSubjectCount}/{failedSubjectsToLose}";
        Rect rect = new(20f, 82f, 430f, 36f);
        GUI.Box(rect, status, promptStyle);
    }

    private void DrawBox(string text, float y, float width)
    {
        Rect rect = new((Screen.width - width) * 0.5f, y, width, 42f);
        GUI.Box(rect, text, promptStyle);
    }

    private void EnsurePromptStyle()
    {
        if (promptStyle != null)
        {
            return;
        }

        promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(16, 16, 10, 10)
        };
    }
}
