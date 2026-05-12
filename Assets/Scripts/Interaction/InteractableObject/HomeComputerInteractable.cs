using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>Máy tính ở nhà: mở UI (gán trong Editor); nút gọi <see cref="TryStartEveningStudy"/>, <see cref="TryLoadMinigame"/>, <see cref="TryOpenBetting"/>.</summary>
public class HomeComputerInteractable : InteractableBase
{
    [Header("UI (thiết kế trong Editor)")]
    [Tooltip("Bật khi người chơi tương tác; chứa các nút Vào học / Chơi game / Cá cược.")]
    [SerializeField] private GameObject computerMenuRoot;

    [Header("Study (khớp StudyDeskInteractable ở nhà khi không có quiz)")]
    [SerializeField] private float gpaGain = 0.1f;
    [SerializeField] private float stressGain;
    [SerializeField] private float energyCost = 1f;
    [SerializeField] private int hoursToAdvance = 1;

    [Header("Story event (optional)")]
    [SerializeField] private string storyObjectiveIdOnStudyComplete;

    [Header("Minigame")]
    [SerializeField] private string minigameSceneName;

    [Header("Betting (mở khi đủ điều kiện)")]
    [SerializeField] private string unlockBettingObjectiveId;
    [SerializeField] private string bettingSceneName;
    [SerializeField, TextArea] private string bettingLockedMessage = "Chua mo khoa.";
    [SerializeField, TextArea] private string bettingNoSceneMessage = "Chua co scene.";

    [Header("Feedback")]
    [SerializeField, TextArea] private string noEveningStudyContextText = "Bay gio khong phai luc on bai tai nha.";
    [SerializeField, TextArea] private string noQuizScheduledText = "Khong co bai on cho hom nay.";

    [Header("Events")]
    [SerializeField] private UnityEvent onMenuOpened;
    [SerializeField] private UnityEvent onMenuClosed;

    private bool sessionBusy;

    public bool IsSessionBusy => sessionBusy;

    // Day 6 Arcade Event Flag
    public static bool GlobalBlockForArcadeEvent { get; set; } = false;

    private void SetComputerMenuUiLockedForQuiz(bool locked)
    {
        if (computerMenuRoot == null) return;
        var cg = computerMenuRoot.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = computerMenuRoot.AddComponent<CanvasGroup>();
        cg.interactable = !locked;
    }

    public override void Interact()
    {
        if (sessionBusy) return;

        if (GlobalBlockForArcadeEvent)
        {
            ShowOk("Bạn quá mệt và phải đi ngủ ngay.");
            return;
        }

        if (computerMenuRoot != null)
        {
            computerMenuRoot.SetActive(true);
        }

        onMenuOpened?.Invoke();
    }

    /// <summary>Gọi từ nút Đóng trên UI máy tính.</summary>
    public void CloseComputerMenu()
    {
        if (computerMenuRoot != null)
        {
            computerMenuRoot.SetActive(false);
        }

        onMenuClosed?.Invoke();
    }

    /// <summary>Nút "Vào học" — ôn buổi tối (cùng dữ liệu với lớp ngày mai).</summary>
    public void TryStartEveningStudy()
    {
        if (sessionBusy) return;

        if (LessonQuizManager.Instance == null || StoryEventManager.Instance == null)
        {
            ShowOk(noEveningStudyContextText);
            return;
        }

        string scene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        if (!StoryEventManager.Instance.LessonQuiz_IsEveningHomeDeskContext(scene))
        {
            ShowOk(noEveningStudyContextText);
            return;
        }

        var deskCtx = LessonQuizStudyContext.EveningHome;
        if (!LessonQuizManager.Instance.TryGetQuizForSchedule(deskCtx, out var quiz) || quiz == null)
        {
            ShowOk(noQuizScheduledText);
            return;
        }

        sessionBusy = true;
        // Giữ UI máy tính hiển thị; khóa click nút menu — quiz mở layer trên (LessonQuizUI).
        SetComputerMenuUiLockedForQuiz(true);

        if (!LessonQuizManager.Instance.TryStartQuiz(quiz, deskCtx, (_, _) =>
            {
                StudyDeskSessionShared.ApplyTimeAdvanceForQuiz(quiz);
                ApplyEveningStudyRoutineEffects();
                StudyDeskSessionShared.TryCompleteQuizObjective(quiz, deskCtx);
                StudyDeskSessionShared.NotifyStatsChanged();
                sessionBusy = false;
                SetComputerMenuUiLockedForQuiz(false);
            }))
        {
            sessionBusy = false;
            SetComputerMenuUiLockedForQuiz(false);
        }
    }

    /// <summary>Nút "Chơi game" — load scene minigame (thêm scene vào Build Settings).</summary>
    public void TryLoadMinigame()
    {
        if (sessionBusy) return;
        if (string.IsNullOrWhiteSpace(minigameSceneName))
        {
            ShowOk("Chua dat ten scene minigame.");
            return;
        }

        sessionBusy = true;
        SceneManager.LoadScene(minigameSceneName.Trim());
    }

    /// <summary>Nút cá cược — chỉ mở khi objective đã hoàn thành (Inspector).</summary>
    public void TryOpenBetting()
    {
        if (sessionBusy) return;

        if (!string.IsNullOrWhiteSpace(unlockBettingObjectiveId)
            && StoryEventManager.Instance != null
            && !StoryEventManager.Instance.IsObjectiveComplete(unlockBettingObjectiveId.Trim()))
        {
            ShowOk(string.IsNullOrWhiteSpace(bettingLockedMessage) ? "Chưa mở khóa." : bettingLockedMessage);
            return;
        }

        if (string.IsNullOrWhiteSpace(bettingSceneName))
        {
            ShowOk(string.IsNullOrWhiteSpace(bettingNoSceneMessage) ? "Chưa có scene." : bettingNoSceneMessage);
            return;
        }

        sessionBusy = true;
        SceneManager.LoadScene(bettingSceneName.Trim());
    }

    private void ApplyEveningStudyRoutineEffects()
    {
        StudyDeskSessionShared.ApplyStudyStatTick(gpaGain, stressGain, energyCost);
        StudyDeskSessionShared.AdvanceTimeAfterRoutineStudy(hoursToAdvance);

        string scene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        StudyDeskSessionShared.RegisterEveningStudyAfterSession(scene, storyObjectiveIdOnStudyComplete);
    }

    private static void ShowOk(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var ds = DialogueSystem.Instance;
        if (ds == null)
        {
            var systems = Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
            ds = systems != null && systems.Length > 0 ? systems[0] : null;
        }

        if (ds == null || ds.IsDialogueActive) return;

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text.Trim(),
                choices = new List<DialogueChoice>(1)
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
}
