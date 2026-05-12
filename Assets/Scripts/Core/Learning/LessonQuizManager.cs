using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lịch quiz theo ngày + ngữ cảnh (ôn nhà buổi tối / kiểm tra lại buổi sáng ở lớp).
/// </summary>
public class LessonQuizManager : MonoBehaviour
{
    public static LessonQuizManager Instance;

    [Serializable]
    public class QuizSchedule
    {
        [Min(1)] public int semester = 1;
        [Min(1)] public int dayInSemester = 1;
        public LessonQuizData data;

        /// <summary>Mỗi ngày chỉ một lần học đầu (ôn tối / kiểm tra sáng tách completion key).</summary>
        public bool onlyFirstStudyOfDay = true;

        /// <summary>
        /// <b>Tối nhà (<see cref="LessonQuizStudyContext.EveningHome"/>)</b> và <b>lớp sáng
        /// (<see cref="LessonQuizStudyContext.MorningClassDesk"/>)</b> có thể dùng hai <see cref="LessonQuizData"/> khác nhau
        /// (ôn nhà vui nhẹ, trên lớp khó). Hai entry cùng ngày khác studyContext và data.<br/>
        /// Sinh asset + lịch Bootstrap: Unity menu <c>SVSimulator/Học kỳ/Sinh bộ Quiz 15 ngày (vui + khó)</c>.
        /// </summary>
        public LessonQuizStudyContext studyContext = LessonQuizStudyContext.DefaultLegacy;
    }

    [Header("Schedule")]
    [SerializeField] private List<QuizSchedule> entries = new List<QuizSchedule>();

    [Header("Desk detection")]
    [SerializeField] private string schoolGameplaySceneName = "21_SchoolArea";

    [Header("Runtime")]
    [SerializeField] private LessonQuizUI uiRef;

    private readonly HashSet<string> completedEntryIds = new HashSet<string>(StringComparer.Ordinal);

    public event Action<LessonQuizData, float> OnQuizCompleted;

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

    /// <summary>Chan hoc tai thoi diem hop le: nha buoi toi sau gio hoc, hoac lop trong tiet hoc.</summary>
    public bool TryResolveStudyDeskQuizContext(out LessonQuizStudyContext ctx)
    {
        ctx = LessonQuizStudyContext.DefaultLegacy;
        string scene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;

        if (StoryEventManager.Instance != null &&
            StoryEventManager.Instance.LessonQuiz_IsEveningHomeDeskContext(scene))
        {
            ctx = LessonQuizStudyContext.EveningHome;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(schoolGameplaySceneName)
            && string.Equals(scene, schoolGameplaySceneName.Trim(), StringComparison.OrdinalIgnoreCase)
            && StoryEventManager.Instance != null
            && StoryEventManager.Instance.TryGetCurrentClassEndHour(out _))
        {
            ctx = LessonQuizStudyContext.MorningClassDesk;
            return true;
        }

        return false;
    }

    public bool TryGetQuizForStudyDesk(out LessonQuizData quiz)
    {
        quiz = null;
        if (!TryResolveStudyDeskQuizContext(out var ctx)) return false;

        return TryGetQuizForSchedule(ctx, out quiz);
    }

    /// <summary>Loc quiz theo boi canh hoc (toi nha vs sang lop); bo qua dong da hoan tat.</summary>
    public bool TryGetQuizForSchedule(LessonQuizStudyContext deskContext, out LessonQuizData quiz)
    {
        quiz = null;
        if (GameTimeManager.Instance == null) return false;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.data == null) continue;
            if (e.semester != sem || e.dayInSemester != day) continue;
            if (e.studyContext != deskContext) continue;

            string id = BuildCompletionKey(sem, day, e.studyContext, e.data.name);
            if (e.onlyFirstStudyOfDay && completedEntryIds.Contains(id)) continue;

            quiz = e.data;
            return true;
        }

        return false;
    }

    /// <summary>Legacy: dong dau tien trong ngay (context Default).</summary>
    public bool TryGetQuizForToday(out LessonQuizData quiz)
    {
        return TryGetQuizForSchedule(LessonQuizStudyContext.DefaultLegacy, out quiz);
    }

    public static string BuildCompletionKey(int semester, int dayInSemester, LessonQuizStudyContext ctx, string dataAssetName)
    {
        string n = string.IsNullOrWhiteSpace(dataAssetName) ? "null" : dataAssetName.Trim();
        int c = (int)ctx;
        return $"{semester}_{dayInSemester}_{c}_{n}";
    }

    /// <returns>Đã có log hoàn thành quiz cho slot lịch này (dùng cho quest).</returns>
    public bool WasDeskQuizCompleted(int semester, int dayInSemester, LessonQuizStudyContext context)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.data == null) continue;
            if (e.semester != semester || e.dayInSemester != dayInSemester) continue;
            if (e.studyContext != context) continue;

            string id = BuildCompletionKey(semester, dayInSemester, e.studyContext, e.data.name);
            if (completedEntryIds.Contains(id)) return true;
        }

        return false;
    }

    public bool TryStartQuiz(LessonQuizData data, Action<float> onCompleted)
    {
        if (!TryResolveStudyDeskQuizContext(out var inferred))
        {
            inferred = LessonQuizStudyContext.DefaultLegacy;
        }

        return TryStartQuiz(data, inferred, (s, w) => onCompleted?.Invoke(s));
    }

    public bool TryStartQuiz(LessonQuizData data, LessonQuizStudyContext completionContext, Action<float, int> onCompleted)
    {
        if (data == null) return false;
        if (data.questions == null || data.questions.Count == 0) return false;

        var ui = ResolveUI();
        if (ui == null) return false;

        int sem = GameTimeManager.Instance != null ? GameTimeManager.Instance.Semester : 1;
        int day = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 1;
        string id = BuildCompletionKey(sem, day, completionContext, data.name);

        ui.Show(data, (score01, wrongCount) =>
        {
            completedEntryIds.Add(id);
            onCompleted?.Invoke(score01, wrongCount);
            OnQuizCompleted?.Invoke(data, score01);
            ApplyRewardsAndStats(data, score01, wrongCount, completionContext);

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.Recalculate();
            }
        });

        return true;
    }

    private void ApplyRewardsAndStats(LessonQuizData data, float score01, int wrongCount, LessonQuizStudyContext ctx)
    {
        if (StatManager.Instance == null) return;

        bool passed = score01 >= Mathf.Clamp01(data.passScore);
        wrongCount = Mathf.Clamp(wrongCount, 0, data.questions != null ? data.questions.Count : 0);

        bool isExam = data.kind == LessonQuizKind.Midterm || data.kind == LessonQuizKind.Final;

        if (isExam && ctx == LessonQuizStudyContext.EveningHome)
        {
            if (passed)
            {
                StatManager.Instance.skill += Mathf.Max(0f, data.skillOnPass) * 0.25f;
            }
        }
        else if (!isExam && ctx == LessonQuizStudyContext.EveningHome)
        {
            if (passed)
            {
                StatManager.Instance.skill += Mathf.Max(0f, data.skillOnPass) * 0.35f;
            }
        }
        else if (isExam && ctx == LessonQuizStudyContext.MorningClassDesk)
        {
            float pen = Mathf.Max(0f, data.morningRecapGpaPenaltyPerWrong);
            if (wrongCount > 0 && pen > 0f)
            {
                StatManager.Instance.gpa = Mathf.Max(0f, StatManager.Instance.gpa - pen * wrongCount);
            }

            if (passed)
            {
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaOnPass);
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaPerCorrectPercent) * score01;
                StatManager.Instance.skill += Mathf.Max(0f, data.skillOnPass);
            }
            else
            {
                StatManager.Instance.stress += Mathf.Max(0f, data.stressOnFail);
            }

            if (wrongCount > 0 && data.stressPerWrong > 0f)
            {
                StatManager.Instance.stress += wrongCount * data.stressPerWrong;
            }

            ProgressExamResults(data, score01, ctx);
        }
        else if (!isExam && ctx == LessonQuizStudyContext.MorningClassDesk)
        {
            float pen = Mathf.Max(0f, data.morningRecapGpaPenaltyPerWrong);
            if (wrongCount > 0 && pen > 0f)
            {
                StatManager.Instance.gpa = Mathf.Max(0f, StatManager.Instance.gpa - pen * wrongCount);
            }

            if (passed)
            {
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaOnPass);
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaPerCorrectPercent) * score01;
                StatManager.Instance.skill += Mathf.Max(0f, data.skillOnPass);
            }
            else
            {
                StatManager.Instance.stress += Mathf.Max(0f, data.stressOnFail);
            }

            float sw = Mathf.Max(0f, data.stressPerWrong);
            if (wrongCount > 0 && sw > 0f)
            {
                StatManager.Instance.stress += wrongCount * sw;
            }

            ProgressDailyQuiz(score01);
        }
        else
        {
            if (passed)
            {
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaOnPass);
                StatManager.Instance.gpa += Mathf.Max(0f, data.gpaPerCorrectPercent) * score01;
                StatManager.Instance.skill += Mathf.Max(0f, data.skillOnPass);
            }
            else
            {
                StatManager.Instance.stress += Mathf.Max(0f, data.stressOnFail);
            }

            if (wrongCount > 0 && data.stressPerWrong > 0f)
            {
                StatManager.Instance.stress += wrongCount * data.stressPerWrong;
            }

            if (data.kind == LessonQuizKind.Midterm || data.kind == LessonQuizKind.Final)
            {
                ProgressExamResults(data, score01, LessonQuizStudyContext.DefaultLegacy);
            }
            else
            {
                ProgressDailyQuiz(score01);
            }
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    private static void ProgressExamResults(LessonQuizData data, float score01, LessonQuizStudyContext ctx)
    {
        if (SemesterProgressManager.Instance == null) return;

        if (ctx == LessonQuizStudyContext.EveningHome)
        {
            return;
        }

        switch (data.kind)
        {
            case LessonQuizKind.Midterm:
                SemesterProgressManager.Instance.RegisterMidtermResult(score01);
                break;
            case LessonQuizKind.Final:
                SemesterProgressManager.Instance.RegisterFinalResult(score01);
                break;
            default:
                SemesterProgressManager.Instance.RegisterQuizResult(score01);
                break;
        }
    }

    private static void ProgressDailyQuiz(float score01)
    {
        if (SemesterProgressManager.Instance == null) return;
        SemesterProgressManager.Instance.RegisterQuizResult(score01);
    }

    private LessonQuizUI ResolveUI()
    {
        if (uiRef != null) return uiRef;
        if (LessonQuizUI.Instance != null)
        {
            uiRef = LessonQuizUI.Instance;
            return uiRef;
        }

        var list = UnityEngine.Object.FindObjectsByType<LessonQuizUI>(FindObjectsInactive.Include);
        if (list != null && list.Length > 0)
        {
            uiRef = list[0];
            return uiRef;
        }

        var go = new GameObject("LessonQuizUI_Auto");
        uiRef = go.AddComponent<LessonQuizUI>();
        return uiRef;
    }

    public string[] Persist_CaptureCompletedQuizEntryIds()
    {
        return completedEntryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    public void Persist_RestoreCompletedQuizEntries(string[] idsOrdered)
    {
        completedEntryIds.Clear();
        if (idsOrdered == null) return;

        for (int i = 0; i < idsOrdered.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(idsOrdered[i]))
            {
                completedEntryIds.Add(idsOrdered[i].Trim());
            }
        }
    }

    public void Persist_ResetCompletedQuizzesForSave()
    {
        completedEntryIds.Clear();
    }
}
