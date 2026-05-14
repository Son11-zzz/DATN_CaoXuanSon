using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Theo dõi tiến trình semester: số buổi sáng/chiều đã đi học, số buổi đã nghỉ,
/// điểm các bài quiz/midterm/final, các lựa chọn rủi ro để tính ending cuối kỳ.
/// Nên đặt ở 00_Bootstrap bên cạnh <see cref="StoryEventManager"/> và <see cref="EndingManager"/>.
/// </summary>
/// <remarks>
/// <b>Setup ending “buộc thôi học” (Dropout) theo nghỉ:</b>
/// Trong Inspector → <c>Ending Milestones</c>, thêm hoặc chỉnh một dòng:
/// <c>enabled</c>, <c>ending = Dropout</c>, <c>priority</c> (số nhỏ = ưu tiên trước),
/// <c>minTotalMissed</c> = ngưỡng (ví dụ 8 = từ 8 buổi nghỉ trở lên; mỗi buổi sáng/chiều không vào lớp tính 1).
/// Muốn “hơn 8 buổi” (không tính đúng 8): đặt <c>minTotalMissed = 9</c>.
/// 1 ngày trường nghỉ cả sáng lẫn chiều = 2 trên <see cref="TotalMissed"/>.
/// <b>Setup ending stress:</b> một dòng <c>ending = StressBreakdown</c>, <c>minStress</c> (vd. 90 trên thang 0–100).
/// Dropout/Stress được gọi từ <see cref="RegisterMorningMissed"/> / <see cref="RegisterAfternoonMissed"/> và từ <c>StoryEventManager</c> mỗi giây.
/// Cần bật điểm danh: <c>StoryEventManager.enableDailyClassAttendance</c> thì mới cộng nghỉ.
/// </remarks>
public class SemesterProgressManager : MonoBehaviour
{
    public static SemesterProgressManager Instance;

    [Serializable]
    public class StatEndingMilestone
    {
        [Header("Rule")]
        [Tooltip("Tắt để bỏ qua milestone này.")]
        public bool enabled = true;
        [Tooltip("Loại ending khi mọi điều kiện dưới đây thỏa.")]
        public EndingType ending = EndingType.None;
        [Tooltip("Số nhỏ = xét trước. Khi nhiều milestone cùng loại (vd. nhiều Dropout), chọn bản có priority nhỏ nhất mà thỏa điều kiện.")]
        public int priority = 1;

        [Header("Stat ranges (max < 0 = ignore)")]
        [Tooltip("GPA tối thiểu (0–4). -1 không dùng field min: để 0 = không chặn dưới.")]
        public float minGpa = 0f;
        [Tooltip("GPA tối đa. **Đặt < 0 (thường -1) để bỏ qua.** Giá trị 0 nghĩa là GPA phải ≤ 0, không phải “ignore”.")]
        public float maxGpa = 4.0f;
        [Tooltip("Stress tối thiểu (thường 0–100). Ví dụ 90 = kích hoạt khi stress >= 90.")]
        public float minStress = 0f;
        [Tooltip("Stress tối đa. **-1 = bỏ qua.** 0 = stress phải ≤ 0 (gần như không bao giờ đúng nếu minStress > 0).")]
        public float maxStress = -1f;
        [Tooltip("Health tối thiểu. Đặt >= 0 để bật điều kiện.")]
        public float minHealth = 0f;
        [Tooltip("Health tối đa. Ví dụ 0 = health ≤ 0. **-1 = bỏ qua.** Không đặt minHealth > maxHealth.")]
        public float maxHealth = -1f;
        [Tooltip("Social tối thiểu.")]
        public float minSocial = 0f;
        [Tooltip("Social tối đa. **-1 = bỏ qua.** 0 = social phải ≤ 0.")]
        public float maxSocial = -1f;

        [Header("Progress ranges (-1 = ignore)")]
        [Tooltip(
            "Tổng buổi nghỉ tối thiểu (sáng + chiều). Mỗi buổi không vào lớp khi hết giờ +1. " +
            "Ví dụ 8 = Dropout từ 8 buổi nghỉ trở lên. 1 ngày nghỉ cả sáng và chiều = 2. Đặt -1 để bỏ qua.")]
        public int minTotalMissed = -1;
        [Tooltip("Tổng buổi nghỉ tối đa. **-1 = bỏ qua.** 0 = chỉ đúng khi TotalMissed = 0; không dùng 0 để “ignore” khi minTotalMissed > 0.")]
        public int maxTotalMissed = -1;
    }

    [Header("Ending rules (priority nhỏ trước — xem tooltip từng dòng)")]
    [Tooltip(
        "Mỗi phần tử: một rule. Chỉ cần mọi field có hiệu lực đều khớp là thỏa (AND trong cùng một dòng). " +
        "TryTriggerDropout / CheckAndTriggerStressBreakdown chỉ tìm milestone đúng EndingType.")]
    [SerializeField] private List<StatEndingMilestone> endingMilestones = new List<StatEndingMilestone>()
    {
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.StressBreakdown,
            priority = 2,
            minStress = 90f
        },
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.Dropout,
            priority = 3,
            minTotalMissed = 8
        },
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.Excellent,
            priority = 4,
            minGpa = 3.5f,
            maxGpa = 4.0f
        },
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.Good,
            priority = 5,
            minGpa = 3.0f,
            maxGpa = 3.5f
        },
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.Average,
            priority = 6,
            minGpa = 2.0f,
            maxGpa = 3.0f
        },
        new StatEndingMilestone
        {
            enabled = true,
            ending = EndingType.AcademicFail,
            priority = 7
        }
    };

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Semester runtime")]
    [SerializeField] private int semesterTracked = -1;

    public int MorningsAttended { get; private set; }
    public int AfternoonsAttended { get; private set; }
    public int MorningsMissed { get; private set; }
    public int AfternoonsMissed { get; private set; }
    public int LateCount { get; private set; }
    public int StudyAtDeskCount { get; private set; }

    public float QuizBestScore { get; private set; }
    public float MidtermScore { get; private set; }
    public float FinalScore { get; private set; }

    public int TotalMissed => MorningsMissed + AfternoonsMissed;
    public int TotalAttended => MorningsAttended + AfternoonsAttended;

    public event Action OnSemesterProgressChanged;

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

    private void OnEnable()
    {
        if (GameTimeManager.Instance != null)
        {
            semesterTracked = GameTimeManager.Instance.Semester;
        }
    }

    public void RegisterMorningAttended(bool late)
    {
        EnsureSemesterContext();
        MorningsAttended++;
        if (late) LateCount++;
        if (verboseLogging) Debug.Log($"SemesterProgress: Morning attended (late={late}). Total attended={TotalAttended}, missed={TotalMissed}");
        Notify();
    }

    public void RegisterAfternoonAttended(bool late)
    {
        EnsureSemesterContext();
        AfternoonsAttended++;
        if (late) LateCount++;
        if (verboseLogging) Debug.Log($"SemesterProgress: Afternoon attended (late={late}). Total attended={TotalAttended}, missed={TotalMissed}");
        Notify();
    }

    public void RegisterMorningMissed()
    {
        EnsureSemesterContext();
        MorningsMissed++;
        if (verboseLogging) Debug.Log($"SemesterProgress: Morning missed. Total missed={TotalMissed}");
        TryTriggerDropout();
        Notify();
    }

    public void RegisterAfternoonMissed()
    {
        EnsureSemesterContext();
        AfternoonsMissed++;
        if (verboseLogging) Debug.Log($"SemesterProgress: Afternoon missed. Total missed={TotalMissed}");
        TryTriggerDropout();
        Notify();
    }

    public void RegisterStudyAtDesk()
    {
        StudyAtDeskCount++;
        Notify();
    }

    /// <summary>
    /// Điểm số quiz (hoặc midterm/final) đã chuẩn hóa về [0..1].
    /// </summary>
    public void RegisterQuizResult(float normalizedScore01)
    {
        float s = Mathf.Clamp01(normalizedScore01);
        if (s > QuizBestScore) QuizBestScore = s;
        Notify();
    }

    public void RegisterMidtermResult(float normalizedScore01)
    {
        MidtermScore = Mathf.Clamp01(normalizedScore01);
        Notify();
    }

    public void RegisterFinalResult(float normalizedScore01)
    {
        FinalScore = Mathf.Clamp01(normalizedScore01);
        Notify();
    }

    private void EnsureSemesterContext()
    {
        if (GameTimeManager.Instance == null) return;

        int cur = GameTimeManager.Instance.Semester;
        if (semesterTracked < 0)
        {
            semesterTracked = cur;
            return;
        }

        if (cur != semesterTracked)
        {
            ResetForNewSemester(cur);
        }
    }

    public void ResetForNewSemester(int newSemester)
    {
        semesterTracked = newSemester;
        MorningsAttended = 0;
        AfternoonsAttended = 0;
        MorningsMissed = 0;
        AfternoonsMissed = 0;
        LateCount = 0;
        StudyAtDeskCount = 0;
        QuizBestScore = 0f;
        MidtermScore = 0f;
        FinalScore = 0f;
    }

    private bool MeetsMilestone(StatEndingMilestone m)
    {
        if (m == null || !m.enabled) return false;
        if (m.ending == EndingType.None) return false;

        var sm = StatManager.Instance;
        if (sm == null) return false;

        // Health=0 → bệnh viện/hospitalized được StatManager xử lý riêng.
        // Bỏ qua milestone Dropout dựa trên health (maxHealth >= 0) khi health <= 0.
        if (m.ending == EndingType.Dropout && m.maxHealth >= 0f && sm.health <= m.maxHealth)
        {
            return false;
        }

        if (m.minGpa >= 0f && sm.gpa < m.minGpa) return false;
        if (m.maxGpa >= 0f && sm.gpa > m.maxGpa) return false;

        if (m.minStress >= 0f && sm.stress < m.minStress) return false;
        if (m.maxStress >= 0f && sm.stress > m.maxStress) return false;

        if (m.minHealth >= 0f && sm.health < m.minHealth) return false;
        if (m.maxHealth >= 0f && sm.health > m.maxHealth) return false;

        if (m.minSocial >= 0f && sm.social < m.minSocial) return false;
        if (m.maxSocial >= 0f && sm.social > m.maxSocial) return false;

        if (m.minTotalMissed >= 0 && TotalMissed < m.minTotalMissed) return false;
        if (m.maxTotalMissed >= 0 && TotalMissed > m.maxTotalMissed) return false;

        return true;
    }

    private bool TryEvaluateEndingFromMilestones(out EndingType ending)
    {
        ending = EndingType.None;
        if (endingMilestones == null || endingMilestones.Count == 0) return false;

        StatEndingMilestone best = null;
        for (int i = 0; i < endingMilestones.Count; i++)
        {
            var m = endingMilestones[i];
            if (!MeetsMilestone(m)) continue;
            if (best == null || m.priority < best.priority)
            {
                best = m;
            }
        }

        if (best == null) return false;
        ending = best.ending;
        return ending != EndingType.None;
    }

    private bool TryEvaluateSpecificEnding(EndingType wanted)
    {
        if (wanted == EndingType.None) return false;
        if (endingMilestones == null || endingMilestones.Count == 0) return false;

        StatEndingMilestone best = null;
        for (int i = 0; i < endingMilestones.Count; i++)
        {
            var m = endingMilestones[i];
            if (m == null || !m.enabled) continue;
            if (m.ending != wanted) continue;
            if (!MeetsMilestone(m)) continue;

            if (best == null || m.priority < best.priority)
            {
                best = m;
            }
        }

        return best != null;
    }

    public bool CheckAndTriggerStressBreakdown()
    {
        if (EndingManager.Instance == null) return false;
        if (StatManager.Instance == null) return false;

        if (TryEvaluateSpecificEnding(EndingType.StressBreakdown))
        {
            EndingManager.Instance.TriggerEnding(EndingType.StressBreakdown);
            return true;
        }

        return false;
    }

    public bool TryTriggerDropout()
    {
        if (EndingManager.Instance == null) return false;

        // StatManager xử lý health=0 bằng hospital flow riêng — không trigger Dropout ở đây.
        if (StatManager.Instance != null && StatManager.Instance.IsHandlingHealthZero) return false;

        if (TryEvaluateSpecificEnding(EndingType.Dropout))
        {
            EndingManager.Instance.TriggerEnding(EndingType.Dropout);
            return true;
        }

        return false;
    }

    public EndingType EvaluateSemesterEnding()
    {
        if (TryEvaluateEndingFromMilestones(out EndingType ending))
        {
            return ending;
        }

        return EndingType.AcademicFail;
    }

    public void Persist_ApplySemester(SemesterProgressPayload p)
    {
        if (p == null) return;

        semesterTracked = p.semesterTracked;
        MorningsAttended = Mathf.Max(0, p.morningsAttended);
        AfternoonsAttended = Mathf.Max(0, p.afternoonsAttended);
        MorningsMissed = Mathf.Max(0, p.morningsMissed);
        AfternoonsMissed = Mathf.Max(0, p.afternoonsMissed);
        LateCount = Mathf.Max(0, p.lateCount);
        StudyAtDeskCount = Mathf.Max(0, p.studyAtDeskCount);

        QuizBestScore = Mathf.Clamp01(p.quizBestScore);
        MidtermScore = Mathf.Clamp01(p.midtermScore);
        FinalScore = Mathf.Clamp01(p.finalScore);

        Notify();
    }

    public SemesterProgressPayload Persist_CaptureSemester()
    {
        return new SemesterProgressPayload
        {
            semesterTracked = semesterTracked,
            morningsAttended = MorningsAttended,
            afternoonsAttended = AfternoonsAttended,
            morningsMissed = MorningsMissed,
            afternoonsMissed = AfternoonsMissed,
            lateCount = LateCount,
            studyAtDeskCount = StudyAtDeskCount,
            quizBestScore = QuizBestScore,
            midtermScore = MidtermScore,
            finalScore = FinalScore,
            badHabitPoints = 0,
            goodChoicePoints = 0,
            recordedChoiceIdsOrdered = null
        };
    }

    public void Persist_ResetSemesterForSave()
    {
        if (GameTimeManager.Instance != null)
        {
            ResetForNewSemester(GameTimeManager.Instance.Semester);
        }
        else
        {
            ResetForNewSemester(1);
        }
    }

    public string BuildSemesterSummaryText()
    {
        string line1 = $"Kết quả học kỳ {semesterTracked}";
        string line2 = $"- Điểm danh: {TotalAttended}/{TotalAttended + TotalMissed} buổi, nghỉ {TotalMissed}";
        string line3 = $"- Muộn giờ: {LateCount}";
        string line4 = $"- GPA: {StatManager.Instance?.gpa:F2} / 4.00";
        string line5 = $"- Quiz tốt nhất: {QuizBestScore:P0}";
        string line6 = $"- Giữa kỳ: {MidtermScore:P0} | Cuối kỳ: {FinalScore:P0}";
        return $"{line1}\n{line2}\n{line3}\n{line4}\n{line5}\n{line6}";
    }

    private void Notify()
    {
        OnSemesterProgressChanged?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (endingMilestones == null) return;

        for (int i = 0; i < endingMilestones.Count; i++)
        {
            var m = endingMilestones[i];
            if (m == null || !m.enabled || m.ending == EndingType.None) continue;

            if (m.maxStress >= 0f && m.minStress >= 0f && m.minStress > m.maxStress)
            {
                Debug.LogWarning(
                    $"SemesterProgressManager '{name}': Ending milestone #{i} ({m.ending}) có minStress ({m.minStress}) > maxStress ({m.maxStress}) — không bao giờ khớp. Đặt maxStress = -1 để bỏ trần.",
                    this);
            }

            if (m.maxTotalMissed >= 0 && m.minTotalMissed >= 0 && m.minTotalMissed > m.maxTotalMissed)
            {
                Debug.LogWarning(
                    $"SemesterProgressManager '{name}': Ending milestone #{i} ({m.ending}) có minTotalMissed ({m.minTotalMissed}) > maxTotalMissed ({m.maxTotalMissed}) — không bao giờ khớp. Đặt maxTotalMissed = -1 để bỏ trần.",
                    this);
            }

            if (m.maxHealth >= 0f && m.minHealth >= 0f && m.minHealth > m.maxHealth)
            {
                Debug.LogWarning(
                    $"SemesterProgressManager '{name}': Ending milestone #{i} ({m.ending}) có minHealth ({m.minHealth}) > maxHealth ({m.maxHealth}) — không bao giờ khớp.",
                    this);
            }

            if (m.maxGpa >= 0f && m.minGpa >= 0f && m.minGpa > m.maxGpa)
            {
                Debug.LogWarning(
                    $"SemesterProgressManager '{name}': Ending milestone #{i} ({m.ending}) có minGpa ({m.minGpa}) > maxGpa ({m.maxGpa}) — không bao giờ khớp.",
                    this);
            }
        }
    }
#endif
}
