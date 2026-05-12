using UnityEngine;
using UnityEngine.SceneManagement;

public class StudyDeskInteractable : InteractableBase
{
    [Header("Study")]
    [SerializeField] private float gpaGain = 0.1f;
    [SerializeField] private float stressGain;
    [SerializeField] private float energyCost = 1f;
    [SerializeField] private int hoursToAdvance = 1;

    [Header("Requirement")]
    [SerializeField] private bool requireClassAttendanceToday = true;
    [SerializeField] private bool requireActiveClassSession = true;
    [SerializeField] private bool requireObjectiveCompletion;
    [SerializeField] private string requiredObjectiveId = "AttendClass";
    [SerializeField, TextArea] private string unavailableText = "Bạn cần vào lớp đúng giờ học trước khi có thể sử dụng bàn học.";

    [Header("Fade")]
    [SerializeField] private bool fadeOnStudy = true;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("Class session end")]
    [SerializeField] private bool movePlayerOutWhenClassSessionEnds = true;
    [SerializeField] private Vector2 classSessionEndExitOffset = new Vector2(0f, -4f);

    [Header("Story event (optional)")]
    [Tooltip("If set, completing a study session tries to mark this story objective complete.")]
    [SerializeField] private string storyObjectiveIdOnStudyComplete;

    private bool isStudyActionRunning;

    public override void Interact()
    {
        if (isStudyActionRunning) return;

        if (!CanUseDesk())
        {
            ShowInfoDialogue(unavailableText);
            return;
        }

        // If a quiz is scheduled for this desk channel (toi nha / sang lop), open it first.
        if (LessonQuizManager.Instance != null
            && LessonQuizManager.Instance.TryResolveStudyDeskQuizContext(out var deskCtx)
            && LessonQuizManager.Instance.TryGetQuizForSchedule(deskCtx, out var quiz)
            && quiz != null)
        {
            isStudyActionRunning = true;
            LessonQuizManager.Instance.TryStartQuiz(quiz, deskCtx, (_, _) =>
            {
                StudyDeskSessionShared.ApplyTimeAdvanceForQuiz(quiz);
                ApplyStudyEffects();
                StudyDeskSessionShared.TryCompleteQuizObjective(quiz, deskCtx);
            });
            return;
        }

        if (fadeOnStudy)
        {
            var fader = ResolveScreenFader();
            if (fader != null)
            {
                isStudyActionRunning = true;
                fader.FadeWithMidAction(ApplyStudyEffects, fadeOutDuration, 0f, holdDuration, fadeInDuration);
                return;
            }
        }

        ApplyStudyEffects();
    }

    private void ApplyStudyEffects()
    {
        isStudyActionRunning = true;

        StudyDeskSessionShared.ApplyStudyStatTick(gpaGain, stressGain, energyCost);

        if (GameTimeManager.Instance != null)
        {
            bool skippedToSessionEnd = false;
            if (StoryEventManager.Instance != null
                && StoryEventManager.Instance.TryGetCurrentClassEndHour(out int classEndHour)
                && classEndHour > GameTimeManager.Instance.Hour)
            {
                int beforeHour = GameTimeManager.Instance.Hour;
                GameTimeManager.Instance.SetTime(
                    GameTimeManager.Instance.Semester,
                    GameTimeManager.Instance.DayInSemester,
                    classEndHour);
                skippedToSessionEnd = true;

                if (classEndHour != beforeHour)
                {
                    MovePlayerOutAfterClassSession();
                }
            }

            if (!skippedToSessionEnd)
            {
                int h = Mathf.Max(0, hoursToAdvance);
                if (h > 0)
                {
                    GameTimeManager.Instance.AdvanceHours(h);
                }
                else
                {
                    // still notify UI
                    GameTimeManager.Instance.SetTime(GameTimeManager.Instance.Semester, GameTimeManager.Instance.DayInSemester, GameTimeManager.Instance.Hour);
                }
            }
        }

        StudyDeskSessionShared.RegisterEveningStudyAfterSession(
            SceneManager.GetActiveScene().name,
            storyObjectiveIdOnStudyComplete);

        StudyDeskSessionShared.NotifyStatsChanged();

        isStudyActionRunning = false;
    }

    public override string GetInteractText()
    {
        return CanUseDesk() ? "Nhấn E" : "Bạn cần vào lớp trước";
    }

    private bool CanUseDesk()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        bool bypassActiveSessionRequirement = StoryEventManager.Instance != null
            && StoryEventManager.Instance.ShouldBypassActiveClassSessionRequirementForDesk(sceneName);

        if (!bypassActiveSessionRequirement && requireActiveClassSession)
        {
            if (StoryEventManager.Instance == null) return false;
            if (!StoryEventManager.Instance.TryGetCurrentClassEndHour(out _)) return false;
        }

        if (requireClassAttendanceToday)
        {
            if (StoryEventManager.Instance == null) return false;
            if (!StoryEventManager.Instance.HasAttendedClassToday) return false;
        }

        if (!requireObjectiveCompletion) return true;
        if (string.IsNullOrWhiteSpace(requiredObjectiveId)) return true;
        if (StoryEventManager.Instance == null) return false;

        return StoryEventManager.Instance.IsObjectiveComplete(requiredObjectiveId.Trim());
    }

    private void MovePlayerOutAfterClassSession()
    {
        if (!movePlayerOutWhenClassSessionEnds) return;
        if (PersistentPlayer.Instance == null) return;

        Vector3 target = transform.position + (Vector3)classSessionEndExitOffset;
        PersistentPlayer.Instance.TeleportTo(target);
    }

    private void ShowInfoDialogue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new System.Collections.Generic.List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = new System.Collections.Generic.List<DialogueChoice>(1)
                {
                    new DialogueChoice { choiceText = "Được", type = ChoiceType.End }
                }
            }
        };

        ds.StartDialogue(data, null, _ =>
        {
            Destroy(data);
            return true;
        });
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }

    private static ScreenFader ResolveScreenFader()
    {
        var faders = Object.FindObjectsByType<ScreenFader>(FindObjectsInactive.Include);
        return faders != null && faders.Length > 0 ? faders[0] : null;
    }
}
