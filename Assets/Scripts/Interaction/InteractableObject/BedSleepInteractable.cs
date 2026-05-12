using System.Collections.Generic;
using UnityEngine;

public class BedSleepInteractable : InteractableBase
{
    [Header("Sleep")]
    [SerializeField] private int wakeHour = 8;
    [SerializeField] private float maxEnergy = 100f;

    [Tooltip("Giờ đi ngủ 20-21h: cộng năng lượng (không reset về max).")]
    [SerializeField] private float energyBonusBedtime20To22 = 50f;

    [Tooltip("Giờ đi ngủ 22-23h: cộng năng lượng. Từ 0h-19h (sau nửa đêm / quá khuya): 0.")]
    [SerializeField] private float energyBonusBedtime22To24 = 20f;
    [SerializeField] private float stressRelieveOnSleep = 8f;

    [Tooltip("Khong lay lai bang ngu neu = 0. HP chi qua tieu hao.")]
    [SerializeField] private float healthRestoreOnSleep = 0f;

    [Tooltip("Moi buoi DA nghi (TotalMissed) lam giam he so bot stress khi ngu.")]
    [SerializeField] private float missedSessionSleepStressReliefScalePerMiss = 0.12f;

    [Tooltip("Tran duoi bot stress khi ngu (gia tri nho = giam ich loi tam ly).")]
    [SerializeField, Range(0.05f, 1f)] private float missedSessionSleepStressReliefMinMultiplier = 0.2f;
    [SerializeField] private float maxHealth = 100f;

    [Header("Event Lock Message")]
    [SerializeField, TextArea] private string lockedText = "Bạn chưa thể đi ngủ lúc này.";

    [Header("End of day fade")]
    [SerializeField] private bool useScreenFade = true;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [Tooltip("Giữ màn hình đen (đã tối) trước khi chuyển ngày — tổng cảm giác nghỉ ~1–2s.")]
    [SerializeField] private float holdBlackBeforeDayAdvance = 0.9f;
    [SerializeField] private float holdBlackAfterDayAdvance = 0.1f;
    [SerializeField] private float fadeInDuration = 0.5f;

    private bool isSleepInProgress;
    private DialogueData sleepSummaryRuntime;
    private DialogueData sleepConfirmRuntime;

    public override void Interact()
    {
        if (isSleepInProgress) return;

        if (GameTimeManager.Instance == null)
        {
            Debug.LogWarning("BedSleepInteractable: GameTimeManager.Instance is null.");
            return;
        }

        if (StoryEventManager.Instance != null && StoryEventManager.Instance.IsEventActive && !StoryEventManager.Instance.IsActiveEventComplete)
        {
            var ev = StoryEventManager.Instance.ActiveEvent;
            if (ev != null && ev.blockSleepUntilComplete)
            {
                ShowLockedDialogue();
                return;
            }
        }

        if (StoryEventManager.Instance != null
            && StoryEventManager.Instance.TryGetSleepBlockMessageDueToEveningStudy(out string eveningStudyBlock))
        {
            ShowSimpleOkDialogue(eveningStudyBlock);
            return;
        }

        isSleepInProgress = true;
        ShowDailySummaryBeforeSleep();
    }

    private void ShowDailySummaryBeforeSleep()
    {
        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            BeginSleepTransition();
            return;
        }

        if (ds.IsDialogueActive)
        {
            isSleepInProgress = false;
            return;
        }

        CleanupRuntimeDialogues();

        string summaryText = StoryEventManager.Instance != null
            ? StoryEventManager.Instance.BuildCurrentDaySummaryText()
            : null;
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            summaryText = "Tổng kết ngày hôm nay.";
        }

        sleepSummaryRuntime = CreateOneLineDialogue(summaryText, choices: null);

        void HandleSummaryClosed()
        {
            ds.OnDialogueEnded -= HandleSummaryClosed;
            CleanupSummaryRuntimeDialogue();
            ShowSleepConfirmDialogue();
        }

        ds.OnDialogueEnded += HandleSummaryClosed;
        ds.StartDialogue(sleepSummaryRuntime, null);
    }

    private void ShowSleepConfirmDialogue()
    {
        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            BeginSleepTransition();
            return;
        }

        if (ds.IsDialogueActive)
        {
            isSleepInProgress = false;
            return;
        }

        var sleepChoice = new DialogueChoice { choiceText = "Ngủ", type = ChoiceType.End };
        var cancelChoice = new DialogueChoice { choiceText = "Hủy", type = ChoiceType.End };
        sleepConfirmRuntime = CreateOneLineDialogue(
            "Bạn có muốn đi ngủ và sang ngày mới không?",
            new List<DialogueChoice>(2) { sleepChoice, cancelChoice });

        ds.StartDialogue(sleepConfirmRuntime, null, choice =>
        {
            bool wantsSleep = ReferenceEquals(choice, sleepChoice)
                || (choice != null && choice.choiceText == sleepChoice.choiceText);

            CleanupConfirmRuntimeDialogue();

            if (wantsSleep)
            {
                BeginSleepTransition();
            }
            else
            {
                isSleepInProgress = false;
            }

            return true;
        });
    }

    private void BeginSleepTransition()
    {
        if (useScreenFade)
        {
            var fader = ResolveScreenFader();
            if (fader != null)
            {
                fader.FadeWithMidAction(
                    AdvanceToNextDay,
                    fadeOutDuration,
                    holdBlackBeforeDayAdvance,
                    holdBlackAfterDayAdvance,
                    fadeInDuration,
                    () => { isSleepInProgress = false; });
                return;
            }
        }

        AdvanceToNextDay();
        isSleepInProgress = false;
    }

    private void AdvanceToNextDay()
    {
        if (GameTimeManager.Instance == null) return;

        if (StoryEventManager.Instance != null && StoryEventManager.Instance.IsEventActive && !StoryEventManager.Instance.IsActiveEventComplete)
        {
            var ev = StoryEventManager.Instance.ActiveEvent;
            if (ev != null && ev.failEventIfDayEndsIncomplete)
            {
                StoryEventManager.Instance.FailActiveEvent();
            }
        }

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;

        RestoreStatsOnSleep();

        if (StoryEventManager.Instance != null)
        {
            StoryEventManager.Instance.SuppressCurrentDaySummaryOnNextTransition();
            StoryEventManager.Instance.FinalizeClassAttendanceForEndOfDay();
        }

        int lastDay = GameTimeManager.Instance.TotalDaysPerSemester;
        bool semesterEnds = day >= lastDay;

        if (semesterEnds)
        {
            // Day 15: không sang ngày tiếp, mà kích hoạt tổng kết + ending
            GameTimeManager.Instance.SetTime(sem, day, Mathf.Clamp(wakeHour, 0, 23));

            if (EventManager.Instance != null)
            {
                EventManager.Instance.NotifyStatChanged();
            }

            TriggerSemesterConclusion();
            return;
        }

        GameTimeManager.Instance.SetTime(sem, day + 1, Mathf.Clamp(wakeHour, 0, 23));

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }

        // Kiểm tra kiệt sức ngay sau khi tính toán lại stat
        if (SemesterProgressManager.Instance != null)
        {
            if (SemesterProgressManager.Instance.CheckAndTriggerStressBreakdown()) return;
        }
    }

    private void RestoreStatsOnSleep()
    {
        var sm = StatManager.Instance;
        if (sm == null) return;

        float cap = Mathf.Max(0f, maxEnergy);

        int bedtimeHour = GameTimeManager.Instance != null ? GameTimeManager.Instance.Hour : 0;
        float energyBonus = EvaluateSleepEnergyBonus(bedtimeHour);
        if (energyBonus > 0f)
        {
            if (cap > 0f)
            {
                sm.energy = Mathf.Min(cap, sm.energy + energyBonus);
            }
            else
            {
                sm.energy = sm.energy + energyBonus;
            }
        }

        if (cap > 0f && sm.energy > cap)
        {
            sm.energy = cap;
        }

        if (stressRelieveOnSleep > 0f)
        {
            float reliefMultiplier = 1f;
            if (missedSessionSleepStressReliefScalePerMiss > 0f && SemesterProgressManager.Instance != null)
            {
                int missed = Mathf.Max(0, SemesterProgressManager.Instance.TotalMissed);
                reliefMultiplier = Mathf.Max(
                    missedSessionSleepStressReliefMinMultiplier,
                    1f - missedSessionSleepStressReliefScalePerMiss * missed);
            }

            sm.stress = Mathf.Max(0f, sm.stress - stressRelieveOnSleep * reliefMultiplier);
        }

        if (healthRestoreOnSleep > 0f)
        {
            sm.health = Mathf.Min(Mathf.Max(0f, maxHealth), sm.health + healthRestoreOnSleep);
        }
    }

    /// <summary>Giờ trong ngày (0-23) tại thời điểm bắt đầu ngủ. 20-21: +bonus lớn; 22-23: +bonus nhỏ; 0-19: không cộng.</summary>
    float EvaluateSleepEnergyBonus(int hour)
    {
        if (hour >= 20 && hour < 22)
        {
            return Mathf.Max(0f, energyBonusBedtime20To22);
        }

        if (hour >= 22 && hour <= 23)
        {
            return Mathf.Max(0f, energyBonusBedtime22To24);
        }

        return 0f;
    }

    private void TriggerSemesterConclusion()
    {
        var ending = EndingType.None;
        if (SemesterProgressManager.Instance != null)
        {
            ending = SemesterProgressManager.Instance.EvaluateSemesterEnding();
        }
        else if (StatManager.Instance != null)
        {
            ending = StatManager.Instance.gpa >= 3.2f ? EndingType.Good : EndingType.Average;
        }

        string summary = SemesterProgressManager.Instance != null
            ? SemesterProgressManager.Instance.BuildSemesterSummaryText()
            : "Kết thúc học kỳ.";

        EventLetterUI letterUi = ResolveEventLetterUI();
        if (letterUi != null && !letterUi.IsOpen)
        {
            letterUi.gameObject.SetActive(true);
            letterUi.ShowPlain("Tổng kết học kỳ", summary, "Tiếp tục", () =>
            {
                if (EndingManager.Instance != null)
                {
                    EndingManager.Instance.TriggerEnding(ending);
                }
            });
            return;
        }

        var ds = ResolveDialogueSystem();
        if (ds != null)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    text = summary,
                    choices = new List<DialogueChoice>(1)
                    {
                        new DialogueChoice { choiceText = "Tiếp tục", type = ChoiceType.End }
                    }
                }
            };

            void HandleSummaryEnded()
            {
                ds.OnDialogueEnded -= HandleSummaryEnded;
                Destroy(data);
                if (EndingManager.Instance != null)
                {
                    EndingManager.Instance.TriggerEnding(ending);
                }
            }

            ds.OnDialogueEnded += HandleSummaryEnded;
            ds.StartDialogue(data, null, _ => true);
        }
        else if (EndingManager.Instance != null)
        {
            EndingManager.Instance.TriggerEnding(ending);
        }
    }

    private void ShowLockedDialogue()
    {
        ShowSimpleOkDialogue(lockedText);
    }

    private void ShowSimpleOkDialogue(string text)
    {
        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
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

    private static DialogueData CreateOneLineDialogue(string text, List<DialogueChoice> choices)
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = choices
            }
        };

        return data;
    }

    private void CleanupRuntimeDialogues()
    {
        CleanupSummaryRuntimeDialogue();
        CleanupConfirmRuntimeDialogue();
    }

    private void CleanupSummaryRuntimeDialogue()
    {
        if (sleepSummaryRuntime == null) return;
        Destroy(sleepSummaryRuntime);
        sleepSummaryRuntime = null;
    }

    private void CleanupConfirmRuntimeDialogue()
    {
        if (sleepConfirmRuntime == null) return;
        Destroy(sleepConfirmRuntime);
        sleepConfirmRuntime = null;
    }

    static EventLetterUI ResolveEventLetterUI()
    {
        if (EventLetterUI.Instance != null)
        {
            return EventLetterUI.Instance;
        }

        EventLetterUI[] uis = Object.FindObjectsByType<EventLetterUI>(FindObjectsInactive.Include);
        return uis != null && uis.Length > 0 ? uis[0] : null;
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
