using System.Collections;
using UnityEngine;

/// <summary>
/// Shows a one-shot gameplay guide letter after UI loads; persists <see cref="GameSaveFile.gameplayGuideShown"/>.
/// </summary>
public sealed class GameplayGuideLetter : MonoBehaviour
{
    public static GameplayGuideLetter Instance { get; private set; }

    [Header("Letter content")]
    [SerializeField] private string letterTitle = "Hướng dẫn chơi";

    [SerializeField, TextArea(4, 28)]
    private string letterBody =
        "- Di chuyển: phím WASD hoặc phím mũi tên.\n"
        + "- Mở túi: phím I. Đồ ăn/uống tiêu thụ: chọn một ô vật phẩm rồi nhấn U.\n"
        + "- Tạm dừng / menu: ESC (trong cửa hàng chỉ đóng cửa hàng).\n"
        + "- Điện thoại: phím P (lịch học, mục tiêu, sức khỏe).\n"
        + "- Theo thời khóa biểu và sự kiện trên HUD / điện thoại.";

    [SerializeField] private string closeLabel = "Đã hiểu";

    [Header("Timing")]
    [SerializeField] private int maxAttempts = 30;

    [SerializeField] private float retryIntervalRealtime = 0.25f;

    bool gameplayGuideDismissed;

    Coroutine deferredRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }

    public bool WasGameplayGuideDismissed() => gameplayGuideDismissed;

    public void Persist_ApplyFromSaveFlag(bool shown)
    {
        gameplayGuideDismissed = shown;
    }

    public void PrepareFresh_ResetGuideFlag()
    {
        gameplayGuideDismissed = false;
    }

    public void NotifyGuideDismissed()
    {
        gameplayGuideDismissed = true;
    }

    /// <summary>Call after additive gameplay scenes finished loading.</summary>
    public void ScheduleDeferredShowAfterGameplayLoad()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (deferredRoutine != null)
        {
            StopCoroutine(deferredRoutine);
            deferredRoutine = null;
        }

        deferredRoutine = StartCoroutine(RunDeferredLetterRoutine());
    }

    IEnumerator RunDeferredLetterRoutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        for (int i = 0; i < maxAttempts; i++)
        {
            if (gameplayGuideDismissed)
            {
                break;
            }

            if (!TryPresentGuideNow())
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, retryIntervalRealtime));
                continue;
            }

            break;
        }

        deferredRoutine = null;
    }

    bool TryPresentGuideNow()
    {
        if (gameplayGuideDismissed)
        {
            return true;
        }

        if (SceneFlowController.Instance != null && SceneFlowController.Instance.IsMenuOrBootstrapActive())
        {
            return false;
        }

        EventLetterUI letter = ResolveEventLetterUI();
        if (letter == null || letter.IsOpen)
        {
            return false;
        }

        letter.gameObject.SetActive(true);
        letter.ShowPlain(letterTitle, letterBody, closeLabel, NotifyGuideDismissed);

        return true;
    }

    static EventLetterUI ResolveEventLetterUI()
    {
        if (EventLetterUI.Instance != null)
        {
            return EventLetterUI.Instance;
        }

        EventLetterUI[] uis = FindObjectsByType<EventLetterUI>(FindObjectsInactive.Include);
        return uis != null && uis.Length > 0 ? uis[0] : null;
    }
}
