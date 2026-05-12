using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Theo dõi tiến trình semester: số buổi sáng/chiều đã đi học, số buổi đã nghỉ,
/// điểm các bài quiz/midterm/final, các lựa chọn rủi ro để tính ending cuối kỳ.
/// Nên đặt ở 00_Bootstrap bên cạnh <see cref="StoryEventManager"/> và <see cref="EndingManager"/>.
/// </summary>
public class SemesterProgressManager : MonoBehaviour
{
    public static SemesterProgressManager Instance;

    [Header("Thresholds")]
    [Tooltip("So buoi hoc (sang+chieu) da nghi toi da truoc khi bi dua roi (bao gom nguong: TotalMissed >=).")]
    [SerializeField] private int dropoutAfterMissedSessions = 5;

    [Tooltip("Nguong stress (>=) se kich hoat bad ending Kiet suc ngay lap tuc.")]
    [SerializeField] private float stressBreakdownThreshold = 95f;

    [Tooltip("So 'bad habit points' toi thieu de kich hoat nhanh bad habit ending.")]
    [SerializeField] private int badHabitPointsThreshold = 3;

    [Header("Grading")]
    [Tooltip("Trong so tiep cho GPA khi tinh ket qua hoc ky (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float gpaWeight = 0.55f;

    [Tooltip("Trong so diem quiz/midterm/final trong ket qua hoc ky (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float examWeight = 0.35f;

    [Tooltip("Trong so con lai cho attendance va social (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float attendanceWeight = 0.10f;

    [Header("Rank thresholds (0..1 score)")]
    [SerializeField] private float excellentScore = 0.85f;
    [SerializeField] private float goodScore = 0.70f;
    [SerializeField] private float averageScore = 0.50f;

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
    public int BadHabitPoints { get; private set; }
    public int GoodChoicePoints { get; private set; }

    public int TotalMissed => MorningsMissed + AfternoonsMissed;
    public int TotalAttended => MorningsAttended + AfternoonsAttended;

    private readonly HashSet<string> recordedChoiceIds = new HashSet<string>(StringComparer.Ordinal);

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

    public void RegisterBadChoice(string choiceId, int points = 1)
    {
        if (!string.IsNullOrWhiteSpace(choiceId) && !recordedChoiceIds.Add(choiceId)) return;
        BadHabitPoints += Mathf.Max(1, points);
        if (verboseLogging) Debug.Log($"SemesterProgress: Bad choice '{choiceId}' (+{points}). Total bad={BadHabitPoints}");
        Notify();
    }

    public void RegisterGoodChoice(string choiceId, int points = 1)
    {
        if (!string.IsNullOrWhiteSpace(choiceId) && !recordedChoiceIds.Add(choiceId)) return;
        GoodChoicePoints += Mathf.Max(1, points);
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
        BadHabitPoints = 0;
        GoodChoicePoints = 0;
        recordedChoiceIds.Clear();
    }

    public bool CheckAndTriggerStressBreakdown()
    {
        if (EndingManager.Instance == null) return false;
        if (StatManager.Instance == null) return false;

        if (StatManager.Instance.stress >= stressBreakdownThreshold)
        {
            EndingManager.Instance.TriggerEnding(EndingType.StressBreakdown);
            return true;
        }

        return false;
    }

    public bool CheckAndTriggerBadHabit()
    {
        if (EndingManager.Instance == null) return false;
        if (BadHabitPoints >= badHabitPointsThreshold)
        {
            EndingManager.Instance.TriggerEnding(EndingType.BadHabit);
            return true;
        }
        return false;
    }

    public bool TryTriggerDropout()
    {
        if (EndingManager.Instance == null) return false;
        if (TotalMissed >= dropoutAfterMissedSessions)
        {
            EndingManager.Instance.TriggerEnding(EndingType.Dropout);
            return true;
        }
        return false;
    }

    public EndingType EvaluateSemesterEnding()
    {
        if (TotalMissed >= dropoutAfterMissedSessions) return EndingType.Dropout;
        if (BadHabitPoints >= badHabitPointsThreshold) return EndingType.BadHabit;
        if (StatManager.Instance != null && StatManager.Instance.stress >= stressBreakdownThreshold) return EndingType.StressBreakdown;

        float score = CalculateSemesterScore01();
        if (score >= excellentScore) return EndingType.Excellent;
        if (score >= goodScore) return EndingType.Good;
        if (score >= averageScore) return EndingType.Average;
        return EndingType.AcademicFail;
    }

    public float CalculateSemesterScore01()
    {
        // Normalize GPA (giả sử GPA 0..4)
        float gpa01 = 0f;
        if (StatManager.Instance != null)
        {
            gpa01 = Mathf.Clamp01(StatManager.Instance.gpa / 4f);
        }

        float examAvg = 0f;
        int examCount = 0;
        if (QuizBestScore > 0f) { examAvg += QuizBestScore; examCount++; }
        if (MidtermScore > 0f) { examAvg += MidtermScore; examCount++; }
        if (FinalScore > 0f) { examAvg += FinalScore; examCount++; }
        if (examCount > 0) examAvg /= examCount;

        int totalSessions = TotalAttended + TotalMissed;
        float att01 = totalSessions <= 0 ? 0.5f : Mathf.Clamp01((float)TotalAttended / totalSessions);

        // Penalty stress
        float stressPenalty = 0f;
        if (StatManager.Instance != null)
        {
            float s = Mathf.Clamp(StatManager.Instance.stress, 0f, 100f);
            stressPenalty = (s / 100f) * 0.10f;
        }

        float totalWeight = Mathf.Max(0.0001f, gpaWeight + examWeight + attendanceWeight);
        float raw = gpa01 * gpaWeight + examAvg * examWeight + att01 * attendanceWeight;
        float score = Mathf.Clamp01(raw / totalWeight - stressPenalty);
        return score;
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
        BadHabitPoints = Mathf.Max(0, p.badHabitPoints);
        GoodChoicePoints = Mathf.Max(0, p.goodChoicePoints);

        recordedChoiceIds.Clear();
        if (p.recordedChoiceIdsOrdered != null)
        {
            for (int i = 0; i < p.recordedChoiceIdsOrdered.Length; i++)
            {
                string id = p.recordedChoiceIdsOrdered[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    recordedChoiceIds.Add(id.Trim());
                }
            }
        }

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
            badHabitPoints = BadHabitPoints,
            goodChoicePoints = GoodChoicePoints,
            recordedChoiceIdsOrdered = recordedChoiceIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
    }

    /// <summary>Wipes semester counters for Main Menu « New Game ».</summary>
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
        float score01 = CalculateSemesterScore01();
        string line1 = $"Kết quả học kỳ {semesterTracked}";
        string line2 = $"- Điểm danh: {TotalAttended}/{TotalAttended + TotalMissed} buổi, nghỉ {TotalMissed}";
        string line3 = $"- Muộn giờ: {LateCount}";
        string line4 = $"- GPA: {StatManager.Instance?.gpa:F2} / 4.00";
        string line5 = $"- Quiz tốt nhất: {QuizBestScore:P0}";
        string line6 = $"- Giữa kỳ: {MidtermScore:P0} | Cuối kỳ: {FinalScore:P0}";
        string line7 = $"- Tổng điểm học kỳ: {score01:P0}";
        string line8 = $"- Điểm lựa chọn xấu/tốt: {BadHabitPoints}/{GoodChoicePoints}";
        return $"{line1}\n{line2}\n{line3}\n{line4}\n{line5}\n{line6}\n{line7}\n{line8}";
    }

    private void Notify()
    {
        OnSemesterProgressChanged?.Invoke();
    }
}
