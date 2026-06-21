using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    public static bool IsLoading = true;

    [Header("UI Settings")]
    public CanvasGroup loadingScreenGroup;
    public Image progressBarFill;

    [Header("Timers")]
    [Tooltip("Thời gian mờ dần (tính bằng giây)")]
    public float fadeDuration = 1.5f;
    [Tooltip("Thời gian load giả lập khi vừa mở game (Boot time)")]
    public float startupLoadTime = 2.0f;
    [Tooltip("Thời gian load giả lập khi chuyển Scene")]
    public float transitionFakeTime = 2.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // THAY ĐỔI: Thay vì Fade mờ luôn, ta gọi quá trình Load khởi tạo
        StartCoroutine(InitialStartupLoad());
    }

    // --- QUÁ TRÌNH LOAD LÚC VỪA MỞ GAME ---
    private IEnumerator InitialStartupLoad()
    {
        IsLoading = true;
        loadingScreenGroup.gameObject.SetActive(true);
        loadingScreenGroup.alpha = 1f; // Đảm bảo màn hình xanh đậm đặc

        if (progressBarFill != null) progressBarFill.fillAmount = 0f;

        float timer = 0f;

        // Chạy thanh Loading giả lập lúc khởi động
        while (timer < startupLoadTime)
        {
            timer += Time.deltaTime;
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = timer / startupLoadTime;
            }
            yield return null;
        }

        // Đảm bảo đầy 100% và chờ 1 nhịp ngắn
        if (progressBarFill != null) progressBarFill.fillAmount = 1f;
        yield return new WaitForSeconds(0.2f);

        // Sau khi load khởi tạo xong thì mới mờ dần màn hình
        yield return StartCoroutine(FadeInToGame());
    }

    // --- QUÁ TRÌNH LOAD KHI CHUYỂN SCENE ---
    public void LoadScene(string sceneName)
    {
        StartCoroutine(TransitionToScene(sceneName));
    }

    private IEnumerator TransitionToScene(string sceneName)
    {
        IsLoading = true;
        loadingScreenGroup.gameObject.SetActive(true);

        if (progressBarFill != null) progressBarFill.fillAmount = 0f;

        // 1. TỪ TỪ HIỆN MÀN HÌNH XANH
        float timer = 0f;
        while (timer < fadeDuration)
        {
            loadingScreenGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        loadingScreenGroup.alpha = 1f;

        // 2. LOAD SCENE VỚI THỜI GIAN ÉP BUỘC
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        float loadTimer = 0f;

        while (loadTimer < transitionFakeTime || asyncLoad.progress < 0.9f)
        {
            loadTimer += Time.deltaTime;

            float timeProgress = loadTimer / transitionFakeTime;
            float realProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = Mathf.Min(timeProgress, realProgress);
            }

            yield return null;
        }

        if (progressBarFill != null) progressBarFill.fillAmount = 1f;
        yield return new WaitForSeconds(0.2f);

        // 3. KÍCH HOẠT SCENE MỚI
        asyncLoad.allowSceneActivation = true;
        yield return null;

        // 4. MỜ DẦN MÀN HÌNH XANH ĐỂ HIỆN GAME
        yield return StartCoroutine(FadeInToGame());
    }

    // --- HIỆU ỨNG MỜ DẦN ---
    private IEnumerator FadeInToGame()
    {
        loadingScreenGroup.gameObject.SetActive(true);
        loadingScreenGroup.alpha = 1f;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            loadingScreenGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        loadingScreenGroup.alpha = 0f;
        loadingScreenGroup.gameObject.SetActive(false);

        IsLoading = false;
    }
}