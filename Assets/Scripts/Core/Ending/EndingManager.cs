using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều phối việc hiển thị kết thúc (ending) cho semester.
/// Chỉ nên tồn tại duy nhất một instance; thường đặt trong 00_Bootstrap.
/// </summary>
public class EndingManager : MonoBehaviour
{
    public static EndingManager Instance;

    [Header("Titles")]
    [SerializeField] private string excellentTitle = "Kết thúc tốt đẹp";
    [SerializeField] private string goodTitle = "Kết thúc khá";
    [SerializeField] private string averageTitle = "Kết thúc trung bình";
    [SerializeField] private string academicFailTitle = "Học tập thất bại";
    [SerializeField] private string dropoutTitle = "Buộc thôi học";
    [SerializeField] private string stressTitle = "Kiệt sức tinh thần";
    [SerializeField] private string badHabitTitle = "Sa ngã";
    [SerializeField] private string hospitalizedTitle = "Bệnh nặng";

    [Header("Ending Letters")]
    [SerializeField] private bool useLetterForExcellent = true;
    [SerializeField] private string excellentLetterTitleOverride = "";
    [SerializeField, TextArea] private string excellentLetterBodyOverride = "Bạn đã có một kỳ học xuất sắc, xứng danh sinh viên giỏi.";
    [SerializeField] private string excellentLetterCloseText = "Được";

    [SerializeField] private bool useLetterForGood = true;
    [SerializeField] private string goodLetterTitleOverride = "";
    [SerializeField, TextArea] private string goodLetterBodyOverride = "Bạn đã có một kỳ học khá, cố gắng giữ phong độ này.";
    [SerializeField] private string goodLetterCloseText = "Được";

    [SerializeField] private bool useLetterForAverage = true;
    [SerializeField] private string averageLetterTitleOverride = "";
    [SerializeField, TextArea] private string averageLetterBodyOverride = "Bạn đã qua kỳ học với kết quả trung bình, cần cải thiện thêm.";
    [SerializeField] private string averageLetterCloseText = "Được";

    [SerializeField] private bool useLetterForDropout = true;
    [SerializeField] private string dropoutLetterTitleOverride = "";
    [SerializeField, TextArea] private string dropoutLetterBodyOverride = "Bạn đã nghỉ quá nhiều buổi học. Nhà trường buộc thôi học.";
    [SerializeField] private string dropoutLetterCloseText = "Rời khỏi trường";

    [SerializeField] private bool useLetterForStress = true;
    [SerializeField] private string stressLetterTitleOverride = "";
    [SerializeField, TextArea] private string stressLetterBodyOverride = "Áp lực quá lớn khiến bạn suy sụp. Kỳ học kết thúc sớm.";
    [SerializeField] private string stressLetterCloseText = "Được";

    [SerializeField] private bool useLetterForHospitalized = true;
    [SerializeField] private string hospitalizedLetterTitleOverride = "";
    [SerializeField, TextArea] private string hospitalizedLetterBodyOverride = "Sức khỏe của bạn đã xuống mức nguy hiểm và bạn không có đủ tiền để chữa trị. Nhân vật chính đã phải nghỉ học vì bệnh nặng.";
    [SerializeField] private string hospitalizedLetterCloseText = "Kết thúc";

    [SerializeField] private float endingCreditsDelayRealtime = 0.5f;

    [Header("Runtime")]
    [SerializeField] private bool disableAutoTickOnEnding = true;
    [Tooltip("Thời gian chờ (giây, unscaled) dialogue khác đóng trước khi hiện ending dạng dialogue. Tăng nếu ending không hiện khi vẫn có hội thoại bận.")]
    [SerializeField] private float endingDialogueClearWaitSeconds = 30f;

    [Header("Exit flow")]
    [SerializeField] private bool exitToMainMenuOnDropout = true;
    [SerializeField] private bool exitToMainMenuOnStressBreakdown = true;
    [SerializeField] private bool exitToMainMenuOnHospitalized = true;

    private float storedTimeScale = 1f;

    public bool HasEnded { get; private set; }
    public EndingType CurrentEnding { get; private set; } = EndingType.None;

    public event Action<EndingType> OnEndingTriggered;

    private void Awake()
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

    public void TriggerEnding(EndingType ending)
    {
        if (HasEnded && ending == CurrentEnding) return;

        CurrentEnding = ending;
        HasEnded = true;

        if (disableAutoTickOnEnding && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(false);
        }

        if (TryShowEndingLetter(ending))
        {
            OnEndingTriggered?.Invoke(ending);
            return;
        }

        ShowEndingDialogue(ending);
        OnEndingTriggered?.Invoke(ending);
    }

    private bool ShouldExitToMainMenuAfterEnding(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Dropout:
                return exitToMainMenuOnDropout;
            case EndingType.StressBreakdown:
                return exitToMainMenuOnStressBreakdown;
            case EndingType.Hospitalized:
                return exitToMainMenuOnHospitalized;
            default:
                return false;
        }
    }

    private bool TryShowEndingLetter(EndingType ending)
    {
        if (!ShouldUseLetterForEnding(ending))
        {
            return false;
        }

        EventLetterUI letter = ResolveEventLetterUI();
        if (letter == null)
        {
            return false;
        }

        storedTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        Time.timeScale = 0f;

        ResolveEndingLetterContent(ending, out string title, out string body, out string closeText);

        letter.gameObject.SetActive(true);
        letter.ShowPlain(title, body, closeText, () =>
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (ShouldExitToMainMenuAfterEnding(ending))
            {
                StartCoroutine(RunEndingExitFlow());
            }
            else
            {
                RestoreTimeScaleAfterLetter();
            }
        });

        return true;
    }

    private void ShowEndingDialogue(EndingType ending)
    {
        string title = GetTitle(ending);
        string body = GetBody(ending);

        if (isActiveAndEnabled)
        {
            StartCoroutine(ShowEndingDialogueRoutine(ending, title, body));
        }

        Debug.Log($"EndingManager: Triggered ending '{ending}' - {title}: {body}");
    }

    private IEnumerator ShowEndingDialogueRoutine(EndingType ending, string title, string body)
    {
        // Wait until any existing dialogue closes so this one is not suppressed.
        DialogueSystem ds = ResolveDialogueSystem();
        float timeout = Mathf.Max(0.5f, endingDialogueClearWaitSeconds);
        while (ds == null || ds.IsDialogueActive)
        {
            yield return null;
            timeout -= Time.unscaledDeltaTime;
            if (timeout <= 0f)
            {
                Debug.LogWarning(
                    "EndingManager: Hết thời gian chờ dialogue đóng; ending không hiển thị được. " +
                    "Tăng Ending Dialogue Clear Wait Seconds trên EndingManager hoặc đảm bảo DialogueSystem không bị kẹt.",
                    this);
                yield break;
            }

            ds = ResolveDialogueSystem();
        }

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>
        {
            new DialogueLine
            {
                text = string.IsNullOrWhiteSpace(title) ? body : $"[{title}]\n{body}",
                choices = new List<DialogueChoice>(1)
                {
                    new DialogueChoice { choiceText = "Được", type = ChoiceType.End }
                }
            }
        };

        bool handled = false;
        ds.StartDialogue(data, null, _ =>
        {
            if (handled) return true;
            handled = true;

            Destroy(data);

            if (ShouldExitToMainMenuAfterEnding(ending) && isActiveAndEnabled)
            {
                StartCoroutine(RunEndingExitFlow());
            }

            return true;
        });
    }

    private IEnumerator RunEndingExitFlow()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(false);
        }

        if (endingCreditsDelayRealtime > 0f)
        {
            yield return new WaitForSecondsRealtime(endingCreditsDelayRealtime);
        }

        RestoreTimeScaleAfterLetter();

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.LoadMainMenu();
        }
    }

    private void RestoreTimeScaleAfterLetter()
    {
        Time.timeScale = Mathf.Approximately(storedTimeScale, 0f) ? 1f : Mathf.Max(storedTimeScale, 0.0001f);
    }

    public string GetTitle(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Excellent: return excellentTitle;
            case EndingType.Good: return goodTitle;
            case EndingType.Average: return averageTitle;
            case EndingType.AcademicFail: return academicFailTitle;
            case EndingType.Dropout: return dropoutTitle;
            case EndingType.StressBreakdown: return stressTitle;
            case EndingType.BadHabit: return badHabitTitle;
            case EndingType.Hospitalized: return hospitalizedTitle;
        }
        return "Kết thúc";
    }

    public string GetBody(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Excellent: return excellentLetterBodyOverride;
            case EndingType.Good: return goodLetterBodyOverride;
            case EndingType.Average: return averageLetterBodyOverride;
            case EndingType.Dropout: return dropoutLetterBodyOverride;
            case EndingType.StressBreakdown: return stressLetterBodyOverride;
            case EndingType.Hospitalized: return hospitalizedLetterBodyOverride;
        }
        return string.Empty;
    }

    private bool ShouldUseLetterForEnding(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Excellent: return useLetterForExcellent;
            case EndingType.Good: return useLetterForGood;
            case EndingType.Average: return useLetterForAverage;
            case EndingType.Dropout: return useLetterForDropout;
            case EndingType.StressBreakdown: return useLetterForStress;
            case EndingType.Hospitalized: return useLetterForHospitalized;
        }

        return false;
    }

    private void ResolveEndingLetterContent(EndingType ending, out string title, out string body, out string closeText)
    {
        title = GetTitle(ending);
        body = string.Empty;
        closeText = "Được";

        switch (ending)
        {
            case EndingType.Excellent:
                title = string.IsNullOrWhiteSpace(excellentLetterTitleOverride) ? title : excellentLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(excellentLetterBodyOverride) ? string.Empty : excellentLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(excellentLetterCloseText) ? closeText : excellentLetterCloseText;
                return;
            case EndingType.Good:
                title = string.IsNullOrWhiteSpace(goodLetterTitleOverride) ? title : goodLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(goodLetterBodyOverride) ? string.Empty : goodLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(goodLetterCloseText) ? closeText : goodLetterCloseText;
                return;
            case EndingType.Average:
                title = string.IsNullOrWhiteSpace(averageLetterTitleOverride) ? title : averageLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(averageLetterBodyOverride) ? string.Empty : averageLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(averageLetterCloseText) ? closeText : averageLetterCloseText;
                return;
            case EndingType.Dropout:
                title = string.IsNullOrWhiteSpace(dropoutLetterTitleOverride) ? title : dropoutLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(dropoutLetterBodyOverride) ? string.Empty : dropoutLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(dropoutLetterCloseText) ? closeText : dropoutLetterCloseText;
                return;
            case EndingType.StressBreakdown:
                title = string.IsNullOrWhiteSpace(stressLetterTitleOverride) ? title : stressLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(stressLetterBodyOverride) ? string.Empty : stressLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(stressLetterCloseText) ? closeText : stressLetterCloseText;
                return;
            case EndingType.Hospitalized:
                title = string.IsNullOrWhiteSpace(hospitalizedLetterTitleOverride) ? title : hospitalizedLetterTitleOverride;
                body = string.IsNullOrWhiteSpace(hospitalizedLetterBodyOverride) ? string.Empty : hospitalizedLetterBodyOverride;
                closeText = string.IsNullOrWhiteSpace(hospitalizedLetterCloseText) ? closeText : hospitalizedLetterCloseText;
                return;
        }
    }

    public void Persist_ResetEndingQuiet()
    {
        HasEnded = false;
        CurrentEnding = EndingType.None;
    }

    /// <summary>Restores meta-ending flags without spawning ending dialogue panels.</summary>
    public void Persist_ApplyEndingQuiet(EndingPayload p)
    {
        if (p == null || !p.hasEnded)
        {
            Persist_ResetEndingQuiet();
            return;
        }

        EndingType stored = NormalizeEndingOrdinal(p.endingTypeOrdinal);

        CurrentEnding = stored;
        HasEnded = stored != EndingType.None && p.hasEnded;

        if (!HasEnded)
        {
            CurrentEnding = EndingType.None;
            return;
        }

        if (disableAutoTickOnEnding && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(false);
        }
    }

    static EndingType NormalizeEndingOrdinal(int ordinal)
    {
        if (!System.Enum.IsDefined(typeof(EndingType), ordinal))
        {
            return EndingType.None;
        }

        return (EndingType)ordinal;
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = UnityEngine.Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }

    private static EventLetterUI ResolveEventLetterUI()
    {
        if (EventLetterUI.Instance != null)
        {
            return EventLetterUI.Instance;
        }

        EventLetterUI[] uis = FindObjectsByType<EventLetterUI>(FindObjectsInactive.Include);
        return uis != null && uis.Length > 0 ? uis[0] : null;
    }
}
