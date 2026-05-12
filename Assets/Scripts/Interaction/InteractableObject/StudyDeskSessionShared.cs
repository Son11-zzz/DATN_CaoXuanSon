using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared study / quiz-after-study logic for <see cref="StudyDeskInteractable"/> and <see cref="HomeComputerInteractable"/>.</summary>
public static class StudyDeskSessionShared
{
    private const float GpaMax = 4.0f;

    public static void ApplyTimeAdvanceForQuiz(LessonQuizData quiz)
    {
        if (quiz == null || GameTimeManager.Instance == null) return;
        int h = Mathf.Max(0, quiz.hoursToAdvance);
        if (h > 0)
        {
            GameTimeManager.Instance.AdvanceHours(h);
        }
    }

    public static void TryCompleteQuizObjective(LessonQuizData quiz, LessonQuizStudyContext deskCtx)
    {
        if (quiz == null || StoryEventManager.Instance == null) return;

        switch (quiz.kind)
        {
            case LessonQuizKind.Midterm:
                if (deskCtx == LessonQuizStudyContext.MorningClassDesk)
                {
                    StoryEventManager.Instance.TryCompleteObjective("MidtermExam", out _);
                }
                break;
            case LessonQuizKind.Final:
                if (deskCtx == LessonQuizStudyContext.MorningClassDesk)
                {
                    StoryEventManager.Instance.TryCompleteObjective("FinalExam", out _);
                }
                break;
            case LessonQuizKind.SmallQuiz:
            case LessonQuizKind.ClassLesson:
                if (deskCtx == LessonQuizStudyContext.MorningClassDesk)
                {
                    StoryEventManager.Instance.TryCompleteObjective("StudyMorning", out _);
                }
                break;
        }
    }

    public static void ApplyStudyStatTick(float gpaGain, float stressGain, float energyCost)
    {
        var stats = StatManager.Instance;
        if (stats == null) return;

        stats.gpa = Mathf.Clamp(stats.gpa + gpaGain, 0f, GpaMax);
        stats.stress += stressGain;
        stats.energy = Mathf.Max(0f, stats.energy - Mathf.Max(0f, energyCost));
    }

    public static void RegisterEveningStudyAfterSession(string sceneName, string storyObjectiveIdOnStudyComplete)
    {
        if (StoryEventManager.Instance == null) return;

        StoryEventManager.Instance.RegisterStudyAtDesk();
        StoryEventManager.Instance.RegisterEveningStudySessionAtDesk(sceneName);

        if (!string.IsNullOrWhiteSpace(storyObjectiveIdOnStudyComplete))
        {
            StoryEventManager.Instance.TryCompleteObjective(storyObjectiveIdOnStudyComplete.Trim(), out _);
        }
    }

    public static void NotifyStatsChanged()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    public static void AdvanceTimeAfterRoutineStudy(int hoursToAdvance)
    {
        if (GameTimeManager.Instance == null) return;

        int h = Mathf.Max(0, hoursToAdvance);
        if (h > 0)
        {
            GameTimeManager.Instance.AdvanceHours(h);
        }
        else
        {
            GameTimeManager.Instance.SetTime(
                GameTimeManager.Instance.Semester,
                GameTimeManager.Instance.DayInSemester,
                GameTimeManager.Instance.Hour);
        }
    }
}
