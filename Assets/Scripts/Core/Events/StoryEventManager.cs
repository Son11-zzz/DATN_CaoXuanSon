using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public partial class StoryEventManager : MonoBehaviour
{
    public enum AttendClassResult
    {
        None,
        TooEarly,
        BreakTime,
        Ended,
        AlreadyAttendedMorning,
        AlreadyAttendedAfternoon,
        AttendedMorningOnTime,
        AttendedMorningLate,
        AttendedAfternoonOnTime,
        AttendedAfternoonLate
    }

    public static StoryEventManager Instance;

    [Header("Config")]
    [SerializeField] private List<StoryEventDefinition> events = new List<StoryEventDefinition>();

    [Header("Story hooks (optional)")]
    [Tooltip("When afternoon class attendance is registered, try to complete this objective if it belongs to the active event.")]
    [SerializeField] private string objectiveIdOnAfternoonClassRegistered;

    public StoryEventDefinition ActiveEvent { get; private set; }
    public bool IsEventActive => ActiveEvent != null;
    public bool IsActiveEventComplete => ActiveEvent != null && IsEventComplete(ActiveEvent);

    public event Action<StoryEventDefinition> OnEventStarted;
    public event Action<StoryEventDefinition> OnEventCompleted;
    public event Action OnProgressChanged;
    public event Action<string> OnObjectiveCompleted;

    private readonly HashSet<string> completedObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
    private bool latePenaltyApplied;
    private bool missApplied;

    private bool lateDialoguePending;
    private DialogueData lateDialogueRuntime;
    private StoryEventDefinition lateDialogueSource;

    private bool startLetterShown;
    private bool startDialogueShown;

    [Header("Daily Class Attendance")]
    [SerializeField] private bool enableDailyClassAttendance = true;
    [SerializeField] private int morningStartHour = 9;
    [SerializeField] private int morningEndHourExclusive = 12;
    [SerializeField] private int afternoonStartHour = 13;
    [SerializeField] private int afternoonEndHourExclusive = 16;
    [SerializeField] private float lateEnergyPenalty = 5f;
    [SerializeField] private float lateStressPenalty = 5f;
    [SerializeField] private float missEnergyPenalty = 12f;
    [SerializeField] private float missStressPenalty = 12f;

    [Tooltip("Them stress khi bo hoc: moi buoi DA nghi tu truoc (TotalMissed hien tai) cong them ap luc (co tran).")]
    [SerializeField] private float missExtraStressPerPriorMissSession = 2f;

    [Tooltip("Gioi han cong tu missExtraStressPerPriorMissSession (ke ca 0 tat).")]
    [SerializeField] private float missExtraStressFromPriorSessionsCap = 14f;
    [SerializeField] private float missGpaPenalty = 0.1f;
    [SerializeField] private float noStudyByNoonGpaPenalty = 0.1f;
    [SerializeField, Min(0)] private int classReminderHoursBeforeStart = 1;
    [SerializeField, TextArea] private string morningClassSoonReminderText =
        "Sắp đến giờ học buổi sáng. Hãy vào lớp đúng giờ.";
    [SerializeField, TextArea] private string morningClassDueReminderText =
        "Đã đến giờ học buổi sáng. Hãy vào lớp.";
    [SerializeField, TextArea] private string afternoonClassSoonReminderText =
        "Sắp đến giờ học buổi chiều. Hãy vào lớp đúng giờ.";
    [SerializeField, TextArea] private string afternoonClassDueReminderText =
        "Đã đến giờ học buổi chiều. Hãy vào lớp.";

    [Header("Evening study at home")]
    [Tooltip("Sau buổi chi\u1EC1u c\xF3 \u0111i h\u1ECDc: ph\u1EA3i \xF4n t\u1EADp \u1EDF b\xE0n trong nh\xE0 tr\u01B0\u1EDBc khi ng\u1EE7.")]
    [SerializeField] private bool requireEveningHomeStudyBeforeSleep = true;
    [SerializeField] private string eveningStudyHomeSceneName = "22_PlayerHouse";
    [SerializeField, Min(0)] private int eveningStudyReminderStartHour = 17;
    [SerializeField, TextArea] private string eveningStudyReminderText =
        "Buổi tối ở nhà: hãy ngồi bàn học để ôn tập trước khi nghỉ.";
    [SerializeField, TextArea] private string eveningStudyBlockedSleepText =
        "Bạn cần ngồi bàn học ôn tập tại nhà trước khi được đi ngủ.";
    [SerializeField, TextArea] private string eveningStudyMissedCarryOverText =
        "Hôm qua bạn chưa ôn tập buổi tối. Hãy ôn đủ mỗi tối tại bàn học trong nhà.";

    [Header("Sau buổi học sáng (teleport)")]
    [SerializeField] private bool morningPostClassTeleportEnabled = true;
    [SerializeField] private string morningPostClassTeleportSceneName = "21_SchoolArea";
    [Tooltip("SpawnPoint.spawnId trong scene trường.")]
    [SerializeField] private string morningPostClassSpawnPointId = "AfterMorningClass";

    [Header("Sau buổi học chiều (teleport)")]
    [SerializeField] private bool afternoonPostClassTeleportEnabled = true;
    [SerializeField] private string afternoonPostClassTeleportSceneName = "21_SchoolArea";
    [Tooltip("Mặc định cùng điểm sau buổi sáng (Spawn_AfterMorningClass → spawnId AfterMorningClass).")]
    [SerializeField] private string afternoonPostClassSpawnPointId = "AfterMorningClass";

    [Header("Bỏ học sáng — phạt tiền buổi tối")]
    [SerializeField] private bool morningMissMoneyFineEnabled = true;
    [SerializeField, Range(0, 23)] private int morningMissMoneyFineStartHour = 18;
    [SerializeField, Min(0f)] private float morningMissMoneyFineAmount = 1_000_000f;

    private int classAttendanceSemester = -1;
    private int classAttendanceDay = -1;
    private bool morningClassAttended;
    private bool afternoonClassAttended;
    private bool morningMissPenaltyApplied;
    private bool afternoonMissPenaltyApplied;
    private bool morningStudyDone;
    private bool noStudyByNoonPenaltyApplied;
    private bool morningClassSoonReminderShown;
    private bool morningClassDueReminderShown;
    private bool afternoonClassSoonReminderShown;
    private bool afternoonClassDueReminderShown;
    private DialogueData classReminderRuntime;

    private bool morningPostClassTeleportApplied;
    private bool afternoonPostClassTeleportApplied;
    private bool morningMissMoneyFineApplied;

    private bool eveningStudyDone;
    private bool eveningStudyReminderShownTonight;
    private bool eveningStudyMissedReminderPending;
    private DialogueData eveningStudyReminderRuntime;

    private int summaryTrackedSemester = -1;
    private int summaryTrackedDay = -1;
    private int eventsCompletedToday;
    private int questsCompletedToday;
    private bool dailySummaryPending;
    private string dailySummaryText;
    private DialogueData dailySummaryRuntime;
    [SerializeField] private float dailySummaryAutoCloseSeconds = 4f;
    private Coroutine dailySummaryAutoCloseRoutine;
    private bool questCompletionSubscribed;
    private bool questNpcTalkSubscribed;
    private int suppressSummaryTransitionSemester = -1;
    private int suppressSummaryTransitionDay = -1;
    // Dialogue thất bại sự kiện: xếp hàng nếu lúc fail vẫn đang mở dialogue khác (vd. xác nhận ngủ).
    private DialogueData pendingFailedEventDialogue;

    [Header("Day 6 Arcade Event")]
    public bool day6EventTriggered;
    public bool day6AcceptedArcadeInvite;
    public bool forceSleepTonight;
    public bool wakeUpLateAfterArcade;
    public bool lateWakePenaltyApplied;
    [SerializeField] private GameObject leToanThangPrefab; // Gán trong Inspector
    [SerializeField] private string day6ArcadeNpcId = "npc_LeToanThang";
    [SerializeField] private string day6ArcadeNpcSpawnId;
    [SerializeField] private DialogueData day6ArcadeInviteDialogue;
    [SerializeField] private DialogueData day6ArcadeAcceptDialogue;
    [SerializeField] private DialogueData day6ArcadeDeclineDialogue;
    [SerializeField] private string day6ArcadeAcceptChoiceText = "Đi luôn!";
    [SerializeField] private string day6ArcadeDeclineChoiceText = "Thôi, mình còn việc khác.";
    private GameObject day6ArcadeNpcInstance;
    private string lastDay6DebugMessage;

    // Prevent the same event from being started again later in the same day.
    private readonly HashSet<StoryEventDefinition> triggeredEventsToday = new HashSet<StoryEventDefinition>();
    private int lastTrackedSemester = -1;
    private int lastTrackedDay = -1;

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
            GameTimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        }

        TrySubscribeQuestCompletion();
        TrySubscribeNpcTalk();
    }

    private void OnDisable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }

        TryUnsubscribeQuestCompletion();
        TryUnsubscribeNpcTalk();
        StopDailySummaryAutoCloseRoutine();
        CleanupClassReminderRuntimeDialogue();
        CleanupEveningStudyReminderRuntimeDialogue();
        CleanupDay3TuitionRuntimeDialogue();
    }

    private void Start()
    {
        InitializeDailyTracking();
        TryStartEventForCurrentTime();
    }

    private void HandleTimeChanged()
    {
        ProcessDailyTransition();
        TryStartEventForCurrentTime();
    }

    private void TryStartEventForCurrentTime()
    {
        if (GameTimeManager.Instance == null) return;
        if (ActiveEvent != null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;

        if (sem != lastTrackedSemester || day != lastTrackedDay)
        {
            triggeredEventsToday.Clear();
            lastTrackedSemester = sem;
            lastTrackedDay = day;
        }

        StoryEventDefinition match = FindEventForDay(sem, day);
        if (match == null) return;
        if (triggeredEventsToday.Contains(match)) return;

        StartEvent(match);
    }

    /// <summary>
    /// One calendar day should map to at most one story event. If multiple assets share the same day,
    /// the highest <see cref="StoryEventDefinition.schedulePriority"/> wins (then first in the list).
    /// </summary>
    private StoryEventDefinition FindEventForDay(int sem, int day)
    {
        StoryEventDefinition best = null;
        int bestPriority = int.MinValue;
        int bestIndex = int.MaxValue;
        int matchCount = 0;

        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e == null || !e.Matches(sem, day)) continue;

            matchCount++;
            int p = e.schedulePriority;
            if (best == null || p > bestPriority || (p == bestPriority && i < bestIndex))
            {
                best = e;
                bestPriority = p;
                bestIndex = i;
            }
        }

        if (matchCount > 1)
        {
            Debug.LogWarning(
                $"StoryEventManager: {matchCount} events target semester {sem} day {day}. Using '{best?.name}' (priority {bestPriority}). Remove duplicates or set schedulePriority.",
                this);
        }

        return best;
    }

    private void StartEvent(StoryEventDefinition ev)
    {
        ActiveEvent = ev;
        if (ev != null)
        {
            triggeredEventsToday.Add(ev);
        }
        completedObjectiveIds.Clear();
        latePenaltyApplied = false;
        missApplied = false;
        lateDialoguePending = false;
        lateDialogueSource = null;
        CleanupLateRuntimeDialogue();
        startLetterShown = false;
        startDialogueShown = false;

        if (ev != null && ev.lockAutoTimeUntilComplete && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(false);
        }

        ApplyAutoAcceptQuests(ev);
        SyncQuestMappedObjectivesForAlreadyCompletedQuests(ev);

        // Try to show the start letter immediately if the UI scene is already loaded.
        // If it's not yet loaded, we'll retry in Update().
        TryShowStartIntro();

        OnEventStarted?.Invoke(ev);
        OnProgressChanged?.Invoke();
    }

    private void TryShowStartIntro()
    {
        if (ActiveEvent == null) return;

        // 1) Prefer letter
        if (!startLetterShown && ActiveEvent.showLetterOnStart)
        {
            if (TryShowStartLetter(ActiveEvent))
            {
                startLetterShown = true;
                return;
            }
        }

        // 2) Fallback to dialogue
        if (!startDialogueShown && ActiveEvent.startDialogue != null)
        {
            var ds = ResolveDialogueSystem();
            if (ds != null && !ds.IsDialogueActive)
            {
                startDialogueShown = true;
                ds.StartDialogue(ActiveEvent.startDialogue, null);
            }
        }
    }

    private static bool TryShowStartLetter(StoryEventDefinition ev)
    {
        if (ev == null) return false;
        if (!ev.showLetterOnStart) return false;

        var ui = ResolveEventLetterUI();
        if (ui == null || ui.IsOpen) return false;

        ui.gameObject.SetActive(true);
        ui.Show(ev, () =>
        {
            if (ev.startDialogue != null)
            {
                var ds = ResolveDialogueSystem();
                if (ds != null && !ds.IsDialogueActive)
                {
                    ds.StartDialogue(ev.startDialogue, null);
                    Instance?.MarkStartDialogueLaunchedFromLetter();
                }
            }
        });

        return true;
    }

    private void MarkStartDialogueLaunchedFromLetter()
    {
        startDialogueShown = true;
    }

    private static EventLetterUI ResolveEventLetterUI()
    {
        if (EventLetterUI.Instance != null) return EventLetterUI.Instance;

        var uis = UnityEngine.Object.FindObjectsByType<EventLetterUI>(FindObjectsInactive.Include);
        return uis != null && uis.Length > 0 ? uis[0] : null;
    }

    public bool TryCompleteObjective(string objectiveId)
    {
        return TryCompleteObjective(objectiveId, out _);
    }

    public bool TryCompleteObjective(string objectiveId, out string reason)
    {
        reason = null;

        if (objectiveId != null)
        {
            objectiveId = objectiveId.Trim();
        }

        if (!CanCompleteObjective(objectiveId, out reason))
        {
            return false;
        }

        if (!completedObjectiveIds.Add(objectiveId))
        {
            reason = "Mục tiêu đã hoàn thành trước đó.";
            return false;
        }

        OnObjectiveCompleted?.Invoke(objectiveId);
        OnProgressChanged?.Invoke();

        if (IsEventComplete(ActiveEvent))
        {
            CompleteActiveEvent();
        }

        return true;
    }

    public bool CanCompleteObjective(string objectiveId, out string reason)
    {
        reason = null;

        if (objectiveId != null)
        {
            objectiveId = objectiveId.Trim();
        }

        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            reason = "Chưa có mã mục tiêu.";
            return false;
        }

        if (ActiveEvent == null)
        {
            reason = "Không có sự kiện đang diễn ra.";
            return false;
        }

        if (!HasObjective(ActiveEvent, objectiveId))
        {
            reason = $"Mục tiêu '{objectiveId}' không thuộc sự kiện hiện tại '{ActiveEvent.name}'. Đang có: {BuildObjectiveIdList(ActiveEvent)}";
            return false;
        }

        if (!TryGetObjectivePrerequisite(ActiveEvent, objectiveId, out string prerequisiteId))
        {
            // objective not found (shouldn't happen because HasObjective checked)
            return true;
        }

        if (!string.IsNullOrWhiteSpace(prerequisiteId) && !IsObjectiveComplete(prerequisiteId))
        {
            reason = $"Bị khóa. Hoàn thành '{prerequisiteId}' trước.";
            return false;
        }

        return true;
    }

    private static string BuildObjectiveIdList(StoryEventDefinition ev)
    {
        if (ev == null || ev.objectives == null || ev.objectives.Count == 0) return "(không có)";

        var ids = new List<string>(ev.objectives.Count);
        for (int i = 0; i < ev.objectives.Count; i++)
        {
            var o = ev.objectives[i];
            if (o == null) continue;
            if (string.IsNullOrWhiteSpace(o.id)) continue;
            ids.Add(o.id.Trim());
        }

        if (ids.Count == 0) return "(không có)";
        return string.Join(", ", ids);
    }

    private static bool TryGetObjectivePrerequisite(StoryEventDefinition ev, string objectiveId, out string prerequisiteId)
    {
        prerequisiteId = null;
        if (ev == null || ev.objectives == null) return false;

        string needle = string.IsNullOrWhiteSpace(objectiveId) ? objectiveId : objectiveId.Trim();

        for (int i = 0; i < ev.objectives.Count; i++)
        {
            var o = ev.objectives[i];
            if (o == null) continue;
            if (string.IsNullOrWhiteSpace(o.id)) continue;
            if (!string.Equals(o.id.Trim(), needle, StringComparison.Ordinal)) continue;

            prerequisiteId = string.IsNullOrWhiteSpace(o.unlockAfterObjectiveId)
                ? null
                : o.unlockAfterObjectiveId.Trim();
            return true;
        }

        return false;
    }

    public bool IsObjectiveComplete(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId)) return false;
        return completedObjectiveIds.Contains(objectiveId.Trim());
    }

    private void CompleteActiveEvent()
    {
        var ev = ActiveEvent;

        if (ev != null && ev.resumeAutoTimeWhenComplete && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(true);
        }

        ActiveEvent = null;

        if (ev != null && ev.completedDialogue != null)
        {
            var ds = ResolveDialogueSystem();
            if (ds != null && !ds.IsDialogueActive)
            {
                ds.StartDialogue(ev.completedDialogue, null);
            }
        }

        eventsCompletedToday++;

        OnEventCompleted?.Invoke(ev);
        OnProgressChanged?.Invoke();
    }

    private void Update()
    {
        // Retry showing the letter/dialogue after UI scenes are loaded.
        // This is important on boot because gameplay UI is often loaded additively a frame later.
        TryShowStartIntro();

        TryShowLateDialogueIfPending();
        
        TryTriggerDay6ArcadeEvent();
        TryApplyDay7WakeUpPenalty();

        TryApplyLatePenalty();
        TryFailByMissDeadline();
        TryProcessDailyClassAttendance();
        TryMorningPostClassTeleport();
        TryDay3TuitionGate();
        TryRestoreDay3TuitionAfterDialogue();
        TryAfternoonPostClassTeleport();
        TryApplyMorningMissEveningMoneyFine();
        TryApplyNoStudyByNoonPenalty();
        TryShowEveningStudyMissedReminderIfPending();
        TryShowClassAttendanceReminder();
        TryShowEveningStudyReminder();
        TryShowDailySummaryIfPending();
        TryShowPendingFailedEventDialogue();

        TryTriggerStressBreakdownEnding();

        if (!questCompletionSubscribed)
        {
            TrySubscribeQuestCompletion();
        }

        if (!questNpcTalkSubscribed)
        {
            TrySubscribeNpcTalk();
        }
    }

    private float stressBreakdownCheckCooldown;

    private void TryTriggerDay6ArcadeEvent()
    {
        if (GameTimeManager.Instance == null)
        {
            Debug.LogWarning("StoryEventManager: GameTimeManager missing for day 6 arcade event.", this);
            return;
        }
        if (day6EventTriggered)
        {
            LogDay6DebugOnce("StoryEventManager: Day 6 arcade already triggered.", this);
            return;
        }
        if (GameTimeManager.Instance.DayInSemester != 6)
        {
            LogDay6DebugOnce($"StoryEventManager: Day 6 arcade gated (day={GameTimeManager.Instance.DayInSemester}).", this);
            return;
        }
        if (GameTimeManager.Instance.Hour < 9)
        {
            LogDay6DebugOnce($"StoryEventManager: Day 6 arcade gated (hour={GameTimeManager.Instance.Hour}).", this);
            return;
        }
        
        // Kích hoạt khi vừa bước ra khỏi nhà hoặc đang ở bên ngoài
        string currentScene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        if (IsScenePlayerHome(currentScene))
        {
            LogDay6DebugOnce($"StoryEventManager: Day 6 arcade gated (scene='{currentScene}', player still at home).", this);
            return; // Chỉ kích hoạt khi đã ra khỏi nhà!
        }
        
        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive)
        {
            LogDay6DebugOnce($"StoryEventManager: Day 6 arcade gated (dialogue system {(ds == null ? "missing" : "busy")}).", this);
            return; // Nếu đang chạy hội thoại khác thì đợi
        }

        day6EventTriggered = true; // Chuyển cờ trigger xuống dưới để không bị bỏ lỡ
        lastDay6DebugMessage = null;
        Debug.Log($"StoryEventManager: Day 6 arcade event triggered in scene '{currentScene}'.", this);

        if (day6ArcadeNpcInstance == null)
        {
            day6ArcadeNpcInstance = ResolveDay6ArcadeNpcInstance();
        }

        if (day6ArcadeNpcInstance != null)
        {
            ForceDay6NpcVisibleAndControlled(day6ArcadeNpcInstance);
        }

        if (string.IsNullOrWhiteSpace(day6ArcadeNpcSpawnId))
        {
            Debug.LogWarning("StoryEventManager: Missing spawn id for day 6 arcade event.", this);
        }
        else if (TryFindSpawnPointById(day6ArcadeNpcSpawnId.Trim(), out SpawnPoint spawn))
        {
            if (day6ArcadeNpcInstance != null)
            {
                day6ArcadeNpcInstance.transform.position = spawn.transform.position;
            }
            else if (leToanThangPrefab != null)
            {
                day6ArcadeNpcInstance = Instantiate(
                    leToanThangPrefab,
                    spawn.transform.position,
                    spawn.transform.rotation,
                    spawn.transform);
                ForceDay6NpcVisibleAndControlled(day6ArcadeNpcInstance);
            }
            else
            {
                Debug.LogWarning("StoryEventManager: Missing NPC prefab for day 6 arcade event.", this);
            }
        }
        else
        {
            Debug.LogWarning(
                $"StoryEventManager: SpawnPoint '{day6ArcadeNpcSpawnId}' not found in scene '{SceneManager.GetActiveScene().name}'.",
                this);
        }

        bool createdRuntimeDialogue = false;
        var data = day6ArcadeInviteDialogue;
        if (data == null)
        {
            createdRuntimeDialogue = true;
            data = ScriptableObject.CreateInstance<DialogueData>();
            data.lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    text = "Ê Sơn, hôm nay không có lịch học căng thẳng đâu. Đi chơi điện tử với tớ không? - Lê Toàn Thắng",
                    choices = new List<DialogueChoice>
                    {
                        new DialogueChoice { choiceText = day6ArcadeAcceptChoiceText, type = ChoiceType.End },
                        new DialogueChoice { choiceText = day6ArcadeDeclineChoiceText, type = ChoiceType.End }
                    }
                }
            };
        }

        DialogueChoice acceptChoice = null;
        DialogueChoice declineChoice = null;
        if (data.lines != null && data.lines.Count > 0)
        {
            var line = data.lines[0];
            if (line?.choices != null)
            {
                if (line.choices.Count > 0)
                {
                    acceptChoice = line.choices[0];
                    if (acceptChoice != null && string.IsNullOrWhiteSpace(acceptChoice.choiceText))
                    {
                        acceptChoice.choiceText = day6ArcadeAcceptChoiceText;
                    }
                }

                if (line.choices.Count > 1)
                {
                    declineChoice = line.choices[1];
                    if (declineChoice != null && string.IsNullOrWhiteSpace(declineChoice.choiceText))
                    {
                        declineChoice.choiceText = day6ArcadeDeclineChoiceText;
                    }
                }
            }
        }

        ds.StartDialogue(data, null, choice =>
        {
            if (createdRuntimeDialogue)
            {
                Destroy(data);
            }

            bool matchedAccept = choice != null && (choice == acceptChoice
                || string.Equals(choice.choiceText, day6ArcadeAcceptChoiceText, StringComparison.Ordinal));
            bool matchedDecline = choice != null && (choice == declineChoice
                || string.Equals(choice.choiceText, day6ArcadeDeclineChoiceText, StringComparison.Ordinal));

            if (matchedAccept) // Đồng ý
            {
                day6AcceptedArcadeInvite = true;
                if (day6ArcadeAcceptDialogue != null)
                {
                    ds.StartDialogue(day6ArcadeAcceptDialogue, null);
                }
                var fader = FindObjectOfType<ScreenFader>();
                if (fader != null)
                {
                    fader.FadeWithMidAction(() => {
                        GameTimeManager.Instance.SetTime(GameTimeManager.Instance.Semester, GameTimeManager.Instance.DayInSemester, 23);
                        if (StatManager.Instance != null)
                        {
                            StatManager.Instance.energy = Mathf.Max(0, StatManager.Instance.energy - 70f);
                            StatManager.Instance.stress = Mathf.Min(100f, StatManager.Instance.stress + 20f); // Adjust for health/stress
                        }
                        HomeComputerInteractable.GlobalBlockForArcadeEvent = true;
                        forceSleepTonight = true;
                        wakeUpLateAfterArcade = true;
                    }, 1.5f, 2f, 1.5f, 1.5f, null);
                }
            }
            else if (matchedDecline)
            {
                if (day6ArcadeDeclineDialogue != null)
                {
                    ds.StartDialogue(day6ArcadeDeclineDialogue, null);
                }
            }
            return true;
        });
    }

    private void TryApplyDay7WakeUpPenalty()
    {
        if (!wakeUpLateAfterArcade || lateWakePenaltyApplied) return;
        if (GameTimeManager.Instance == null || GameTimeManager.Instance.DayInSemester != 7) return;
        if (GameTimeManager.Instance.Hour < 13) // Vừa mới thức dậy
        {
            var ds = ResolveDialogueSystem();
            if (ds == null || ds.IsDialogueActive) return; // Đợi dialogue khác kết thúc mới phạt

            GameTimeManager.Instance.SetTime(GameTimeManager.Instance.Semester, GameTimeManager.Instance.DayInSemester, 13); // Thức dậy lúc 13h
            if (StatManager.Instance != null)
            {
                StatManager.Instance.gpa = Mathf.Max(0, StatManager.Instance.gpa - 0.3f);
            }
            lateWakePenaltyApplied = true;
            HomeComputerInteractable.GlobalBlockForArcadeEvent = false;

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    text = "Bạn đã ngủ quên và bỏ lỡ bài kiểm tra sáng nay.",
                    choices = new List<DialogueChoice>
                    {
                        new DialogueChoice { choiceText = "Trời ơi...", type = ChoiceType.End }
                    }
                }
            };
            ds.StartDialogue(data, null, _ => { Destroy(data); return true; });
        }
    }

    private void TryTriggerStressBreakdownEnding()
    {
        if (EndingManager.Instance == null || EndingManager.Instance.HasEnded) return;
        if (SemesterProgressManager.Instance == null) return;

        stressBreakdownCheckCooldown -= Time.deltaTime;
        if (stressBreakdownCheckCooldown > 0f) return;
        stressBreakdownCheckCooldown = 1f;

        SemesterProgressManager.Instance.CheckAndTriggerStressBreakdown();
    }

    private void LogDay6DebugOnce(string message, UnityEngine.Object context)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (string.Equals(lastDay6DebugMessage, message, StringComparison.Ordinal)) return;
        lastDay6DebugMessage = message;
        Debug.Log(message, context);
    }

    private GameObject ResolveDay6ArcadeNpcInstance()
    {
        if (string.IsNullOrWhiteSpace(day6ArcadeNpcId))
        {
            return null;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        GameObject fallback = null;

        var questNpcs = UnityEngine.Object.FindObjectsByType<QuestGiverNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < questNpcs.Length; i++)
        {
            var npc = questNpcs[i];
            if (npc == null) continue;
            if (string.Equals(npc.GetResolvedNpcId(), day6ArcadeNpcId, StringComparison.OrdinalIgnoreCase))
            {
                if (IsRuntimeNpcCandidate(npc.gameObject, activeScene))
                {
                    return npc.gameObject;
                }

                if (fallback == null)
                {
                    fallback = npc.gameObject;
                }
            }
        }

        var characters = UnityEngine.Object.FindObjectsByType<NpcCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            var npc = characters[i];
            if (npc == null || npc.Profile == null) continue;
            if (string.Equals(npc.Profile.characterId, day6ArcadeNpcId, StringComparison.OrdinalIgnoreCase))
            {
                if (IsRuntimeNpcCandidate(npc.gameObject, activeScene))
                {
                    return npc.gameObject;
                }

                if (fallback == null)
                {
                    fallback = npc.gameObject;
                }
            }
        }

        return fallback;
    }

    private static bool IsRuntimeNpcCandidate(GameObject npc, string activeScene)
    {
        if (npc == null) return false;
        var scene = npc.scene;
        if (!scene.IsValid()) return false;
        if (string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal)) return true;
        return !string.Equals(scene.name, activeScene, StringComparison.OrdinalIgnoreCase);
    }

    private static void ForceDay6NpcVisibleAndControlled(GameObject npc)
    {
        if (npc == null) return;
        npc.SetActive(true);

        var presence = npc.GetComponentInChildren<NpcWorldPresence2D>(true);
        if (presence != null)
        {
            presence.SetWorldHidden(false);
        }
        else
        {
            foreach (var col in npc.GetComponentsInChildren<Collider2D>(true))
            {
                if (col != null) col.enabled = true;
            }

            foreach (var sr in npc.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                sr.enabled = true;
                var c = sr.color;
                if (c.a < 1f) sr.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        var mover = npc.GetComponent<NpcSimpleMover2D>();
        if (mover != null)
        {
            mover.ExternalControl = true;
        }
    }

    public bool TryGetCurrentClassEndHour(out int classEndHour)
    {
        classEndHour = -1;

        if (!enableDailyClassAttendance) return false;
        if (GameTimeManager.Instance == null) return false;

        int hour = GameTimeManager.Instance.Hour;
        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart) morningEnd = morningStart + 1;

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart) afternoonEnd = afternoonStart + 1;

        if (hour >= morningStart && hour < morningEnd)
        {
            classEndHour = morningEnd;
            return true;
        }

        if (hour >= afternoonStart && hour < afternoonEnd)
        {
            classEndHour = afternoonEnd;
            return true;
        }

        return false;
    }

    public bool CanEnterClassAreaNow(out string statusText, out AttendClassResult result)
    {
        statusText = null;
        result = AttendClassResult.None;

        if (!enableDailyClassAttendance) return true;
        if (GameTimeManager.Instance == null) return true;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday)
        {
            result = AttendClassResult.Ended;
            statusText = "Hôm nay không có tiết học.";
            return false;
        }

        int hour = GameTimeManager.Instance.Hour;
        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart) morningEnd = morningStart + 1;

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart) afternoonEnd = afternoonStart + 1;

        if (hour < morningStart)
        {
            result = AttendClassResult.TooEarly;
            statusText = "Chưa đến giờ vào lớp.";
            return false;
        }

        if (hour >= morningEnd && hour < afternoonStart)
        {
            result = AttendClassResult.BreakTime;
            statusText = "Đang là giờ nghỉ trưa. Hãy quay lại lúc 13 giờ.";
            return false;
        }

        if (hour >= afternoonEnd)
        {
            result = AttendClassResult.Ended;
            statusText = "Đã hết giờ học hôm nay.";
            return false;
        }

        return true;
    }

    public bool HasAttendedClassToday
    {
        get
        {
            EnsureDailyClassAttendanceState();
            return morningClassAttended || afternoonClassAttended;
        }
    }

    public bool RegisterAttendClass()
    {
        return RegisterAttendClass(out _);
    }

    public bool RegisterAttendClass(out string statusText)
    {
        return RegisterAttendClass(out statusText, out _);
    }

    public bool RegisterAttendClass(out string statusText, out AttendClassResult result)
    {
        statusText = null;
        result = AttendClassResult.None;

        if (!enableDailyClassAttendance) return false;
        if (GameTimeManager.Instance == null) return false;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday)
        {
            result = AttendClassResult.Ended;
            statusText = "Hôm nay không có tiết học.";
            return false;
        }

        EnsureDailyClassAttendanceState();

        int hour = GameTimeManager.Instance.Hour;

        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart) morningEnd = morningStart + 1;

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart) afternoonEnd = afternoonStart + 1;

        if (hour < morningStart)
        {
            result = AttendClassResult.TooEarly;
            statusText = "Chưa đến giờ vào lớp.";
            return false;
        }

        if (hour >= morningEnd && hour < afternoonStart)
        {
            result = AttendClassResult.BreakTime;
            statusText = "Đang là giờ nghỉ giữa buổi.";
            return false;
        }

        if (hour >= afternoonEnd)
        {
            result = AttendClassResult.Ended;
            statusText = "Đã hết giờ học hôm nay.";
            return false;
        }

        if (hour >= morningStart && hour < morningEnd)
        {
            if (morningClassAttended)
            {
                result = AttendClassResult.AlreadyAttendedMorning;
            statusText = "Bạn đã vào lớp buổi sáng.";
                return false;
            }

            morningClassAttended = true;
            bool morningLate = hour > morningStart;
            if (morningLate)
            {
                ApplyDailyClassPenalty(isMiss: false);
                result = AttendClassResult.AttendedMorningLate;
            }
            else
            {
                result = AttendClassResult.AttendedMorningOnTime;
            }

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterMorningAttended(morningLate);
            }

            statusText = "Đã vào lớp buổi sáng.";
            return true;
        }

        if (hour >= afternoonStart && hour < afternoonEnd)
        {
            if (afternoonClassAttended)
            {
                result = AttendClassResult.AlreadyAttendedAfternoon;
                statusText = "Bạn đã vào lớp buổi chiều.";
                return false;
            }

            afternoonClassAttended = true;
            bool afternoonLate = hour > afternoonStart;
            if (afternoonLate)
            {
                ApplyDailyClassPenalty(isMiss: false);
                result = AttendClassResult.AttendedAfternoonLate;
            }
            else
            {
                result = AttendClassResult.AttendedAfternoonOnTime;
            }

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterAfternoonAttended(afternoonLate);
            }

            TryCompleteObjectiveOnAfternoonClassHook();

            statusText = "Đã vào lớp buổi chiều.";
            return true;
        }

        return false;
    }

    private void TryCompleteObjectiveOnAfternoonClassHook()
    {
        if (string.IsNullOrWhiteSpace(objectiveIdOnAfternoonClassRegistered)) return;
        TryCompleteObjective(objectiveIdOnAfternoonClassRegistered.Trim(), out _);
    }

    /// <summary>Chốt buổi sáng/chiều đã nghỉ trước khi đổi ngày (vd. ngủ rất sớm — giờ không vượt morningEnd).</summary>
    public void FinalizeClassAttendanceForEndOfDay()
    {
        if (!enableDailyClassAttendance) return;
        if (GameTimeManager.Instance == null) return;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return;

        EnsureDailyClassAttendanceState();

        if (!morningClassAttended && !morningMissPenaltyApplied)
        {
            morningMissPenaltyApplied = true;
            ApplyDailyClassPenalty(isMiss: true);

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterMorningMissed();
            }
        }

        if (!afternoonClassAttended && !afternoonMissPenaltyApplied)
        {
            afternoonMissPenaltyApplied = true;
            ApplyDailyClassPenalty(isMiss: true);

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterAfternoonMissed();
            }
        }

        ApplyMorningMissMoneyFineIfDue(endOfDayFinalize: true);
    }

    private void TryProcessDailyClassAttendance()
    {
        if (!enableDailyClassAttendance) return;
        if (GameTimeManager.Instance == null) return;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return;

        EnsureDailyClassAttendanceState();

        int hour = GameTimeManager.Instance.Hour;

        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart) morningEnd = morningStart + 1;

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart) afternoonEnd = afternoonStart + 1;

        if (!morningClassAttended && !morningMissPenaltyApplied && hour >= morningEnd)
        {
            morningMissPenaltyApplied = true;
            ApplyDailyClassPenalty(isMiss: true);

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterMorningMissed();
            }
        }

        if (!afternoonClassAttended && !afternoonMissPenaltyApplied && hour >= afternoonEnd)
        {
            afternoonMissPenaltyApplied = true;
            ApplyDailyClassPenalty(isMiss: true);

            if (SemesterProgressManager.Instance != null)
            {
                SemesterProgressManager.Instance.RegisterAfternoonMissed();
            }
        }
    }

    private void TryMorningPostClassTeleport()
    {
        if (!morningPostClassTeleportEnabled)
        {
            return;
        }

        if (!enableDailyClassAttendance)
        {
            return;
        }

        if (GameTimeManager.Instance == null)
        {
            return;
        }

        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday)
        {
            return;
        }

        EnsureDailyClassAttendanceState();

        if (morningPostClassTeleportApplied)
        {
            return;
        }

        if (!morningClassAttended)
        {
            return;
        }

        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart)
        {
            morningEnd = morningStart + 1;
        }

        if (GameTimeManager.Instance.Hour < morningEnd)
        {
            return;
        }

        string sceneWant = string.IsNullOrWhiteSpace(morningPostClassTeleportSceneName)
            ? "21_SchoolArea"
            : morningPostClassTeleportSceneName.Trim();

        if (!string.Equals(SceneManager.GetActiveScene().name, sceneWant, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(morningPostClassSpawnPointId))
        {
            return;
        }

        if (!TryFindSpawnPointById(morningPostClassSpawnPointId.Trim(), out SpawnPoint spawn))
        {
            return;
        }

        if (PersistentPlayer.Instance == null)
        {
            return;
        }

        PersistentPlayer.Instance.TeleportTo(spawn.transform.position);
        morningPostClassTeleportApplied = true;
        OnProgressChanged?.Invoke();
    }

    private void TryAfternoonPostClassTeleport()
    {
        if (!afternoonPostClassTeleportEnabled)
        {
            return;
        }

        if (!enableDailyClassAttendance)
        {
            return;
        }

        if (GameTimeManager.Instance == null)
        {
            return;
        }

        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday)
        {
            return;
        }

        EnsureDailyClassAttendanceState();

        if (afternoonPostClassTeleportApplied)
        {
            return;
        }

        if (!afternoonClassAttended)
        {
            return;
        }

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart)
        {
            afternoonEnd = afternoonStart + 1;
        }

        if (GameTimeManager.Instance.Hour < afternoonEnd)
        {
            return;
        }

        string sceneWant = string.IsNullOrWhiteSpace(afternoonPostClassTeleportSceneName)
            ? "21_SchoolArea"
            : afternoonPostClassTeleportSceneName.Trim();

        if (!string.Equals(SceneManager.GetActiveScene().name, sceneWant, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(afternoonPostClassSpawnPointId))
        {
            return;
        }

        if (!TryFindSpawnPointById(afternoonPostClassSpawnPointId.Trim(), out SpawnPoint spawn))
        {
            return;
        }

        if (PersistentPlayer.Instance == null)
        {
            return;
        }

        PersistentPlayer.Instance.TeleportTo(spawn.transform.position);
        afternoonPostClassTeleportApplied = true;
        OnProgressChanged?.Invoke();
    }

    private void TryApplyMorningMissEveningMoneyFine()
    {
        if (GameTimeManager.Instance == null)
        {
            return;
        }

        int fineHour = Mathf.Clamp(morningMissMoneyFineStartHour, 0, 23);
        if (GameTimeManager.Instance.Hour < fineHour)
        {
            return;
        }

        ApplyMorningMissMoneyFineIfDue(endOfDayFinalize: false);
    }

    /// <param name="endOfDayFinalize">
    /// True khi kết thúc ngày (ngủ): trừ tiền dù chưa tới giờ phạt buổi tối để không bỏ sót.
    /// </param>
    private void ApplyMorningMissMoneyFineIfDue(bool endOfDayFinalize)
    {
        if (!morningMissMoneyFineEnabled)
        {
            return;
        }

        if (!enableDailyClassAttendance)
        {
            return;
        }

        if (StatManager.Instance == null)
        {
            return;
        }

        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday)
        {
            return;
        }

        EnsureDailyClassAttendanceState();

        if (morningMissMoneyFineApplied)
        {
            return;
        }

        if (morningClassAttended)
        {
            return;
        }

        float amt = Mathf.Max(0f, morningMissMoneyFineAmount);
        morningMissMoneyFineApplied = true;

        if (amt > 0f)
        {
            StatManager.Instance.money = Mathf.Max(0f, StatManager.Instance.money - amt);
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }

        OnProgressChanged?.Invoke();
    }

    private static bool TryFindSpawnPointById(string spawnId, out SpawnPoint spawn)
    {
        spawn = null;
        if (string.IsNullOrWhiteSpace(spawnId))
        {
            return false;
        }

        SpawnPoint[] points = UnityEngine.Object.FindObjectsByType<SpawnPoint>(
            FindObjectsInactive.Include);

        for (int i = 0; i < points.Length; i++)
        {
            SpawnPoint p = points[i];
            if (p == null)
            {
                continue;
            }

            if (!string.Equals(p.SpawnId, spawnId.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            spawn = p;
            return true;
        }

        return false;
    }

    private static bool TryFindSpawnPointByIdAny(string[] spawnIdsInOrder, out SpawnPoint spawn)
    {
        spawn = null;
        if (spawnIdsInOrder == null || spawnIdsInOrder.Length == 0) return false;

        for (int s = 0; s < spawnIdsInOrder.Length; s++)
        {
            string raw = spawnIdsInOrder[s];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            if (TryFindSpawnPointById(raw.Trim(), out spawn))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureDailyClassAttendanceState()
    {
        if (GameTimeManager.Instance == null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;
        if (sem == classAttendanceSemester && day == classAttendanceDay) return;

        classAttendanceSemester = sem;
        classAttendanceDay = day;
        morningClassAttended = false;
        afternoonClassAttended = false;
        morningMissPenaltyApplied = false;
        afternoonMissPenaltyApplied = false;
        morningStudyDone = false;
        noStudyByNoonPenaltyApplied = false;
        eveningStudyDone = false;
        eveningStudyReminderShownTonight = false;
        morningClassSoonReminderShown = false;
        morningClassDueReminderShown = false;
        afternoonClassSoonReminderShown = false;
        afternoonClassDueReminderShown = false;
        morningPostClassTeleportApplied = false;
        afternoonPostClassTeleportApplied = false;
        morningMissMoneyFineApplied = false;

        ResetDay3TuitionDailyTracking();
    }

    public bool TryGetSleepBlockMessageDueToEveningStudy(out string message)
    {
        message = null;

        if (!enableDailyClassAttendance || !requireEveningHomeStudyBeforeSleep) return false;
        if (GameTimeManager.Instance == null) return false;

        EnsureDailyClassAttendanceState();

        string scene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        int afternoonEndExclusive = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);

        if (!(morningClassAttended || afternoonClassAttended)) return false;
        if (eveningStudyDone) return false;
        if (!IsScenePlayerHome(scene)) return false;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return false;

        if (GameTimeManager.Instance.Hour < afternoonEndExclusive) return false;

        message = eveningStudyBlockedSleepText;
        return !string.IsNullOrWhiteSpace(message);
    }

    /// <summary>Cho StudyDeskInteractable mo khoa ban hoc khi het gio hoc neu dang buoi toi tai nha.</summary>
    public bool ShouldBypassActiveClassSessionRequirementForDesk(string activeGameplaySceneName)
    {
        if (!enableDailyClassAttendance || !requireEveningHomeStudyBeforeSleep) return false;
        EnsureDailyClassAttendanceState();
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return false;
        if (!(morningClassAttended || afternoonClassAttended)) return false;
        return IsEveningHomeStudyWindowForScene(activeGameplaySceneName);
    }

    /// <summary>Đang trong cửa sổ ôn bài nhà sau giờ học (quiz buổi tối) — không phụ thuộc chặn ngủ.</summary>
    public bool LessonQuiz_IsEveningHomeDeskContext(string activeGameplaySceneName)
    {
        if (GameTimeManager.Instance == null) return false;

        if (!enableDailyClassAttendance)
        {
            return IsEveningHomeStudyWindowForScene(activeGameplaySceneName);
        }

        EnsureDailyClassAttendanceState();
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return false;
        if (!(morningClassAttended || afternoonClassAttended)) return false;

        return IsEveningHomeStudyWindowForScene(activeGameplaySceneName);
    }

    public void RegisterEveningStudySessionAtDesk(string activeGameplaySceneName)
    {
        if (!enableDailyClassAttendance || !requireEveningHomeStudyBeforeSleep) return;
        if (GameTimeManager.Instance == null) return;

        EnsureDailyClassAttendanceState();
        if (!IsEveningHomeStudyWindowForScene(activeGameplaySceneName)) return;
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return;
        if (!(morningClassAttended || afternoonClassAttended)) return;

        eveningStudyDone = true;

        if (SemesterProgressManager.Instance != null)
        {
            SemesterProgressManager.Instance.RegisterStudyAtDesk();
        }

        OnProgressChanged?.Invoke();
    }

    private bool IsEveningHomeStudyWindowForScene(string sceneName)
    {
        if (GameTimeManager.Instance == null) return false;

        string scene = sceneName?.Trim() ?? string.Empty;
        if (!IsScenePlayerHome(scene)) return false;

        int afternoonEndExclusive = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        return GameTimeManager.Instance.Hour >= afternoonEndExclusive;
    }

    private bool IsScenePlayerHome(string trimmedSceneName)
    {
        if (string.IsNullOrWhiteSpace(eveningStudyHomeSceneName)) return false;
        return string.Equals(
            trimmedSceneName,
            eveningStudyHomeSceneName.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    public void RegisterStudyAtDesk()
    {
        if (!enableDailyClassAttendance) return;
        if (GameTimeManager.Instance == null) return;

        EnsureDailyClassAttendanceState();

        int hour = GameTimeManager.Instance.Hour;
        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        if (!morningClassAttended) return;
        if (hour < morningStart) return;
        if (hour >= afternoonStart) return;

        morningStudyDone = true;

        if (SemesterProgressManager.Instance != null)
        {
            SemesterProgressManager.Instance.RegisterStudyAtDesk();
        }
    }

    private void TryApplyNoStudyByNoonPenalty()
    {
        if (!enableDailyClassAttendance) return;
        if (GameTimeManager.Instance == null) return;
        if (StatManager.Instance == null) return;

        EnsureDailyClassAttendanceState();
        if (noStudyByNoonPenaltyApplied) return;

        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= 0) morningEnd = 1;

        if (GameTimeManager.Instance.Hour < morningEnd) return;
        if (!morningClassAttended) return;
        if (morningStudyDone) return;

        float gpaPenalty = Mathf.Max(0f, noStudyByNoonGpaPenalty);
        if (gpaPenalty > 0f)
        {
            StatManager.Instance.gpa = Mathf.Max(0f, StatManager.Instance.gpa - gpaPenalty);
        }

        noStudyByNoonPenaltyApplied = true;

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    private void TryShowClassAttendanceReminder()
    {
        if (!enableDailyClassAttendance) return;
        if (GameTimeManager.Instance == null) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        EnsureDailyClassAttendanceState();

        int hour = GameTimeManager.Instance.Hour;
        int remindBefore = Mathf.Max(0, classReminderHoursBeforeStart);
        int morningStart = Mathf.Clamp(morningStartHour, 0, 23);
        int morningEnd = Mathf.Clamp(morningEndHourExclusive, 1, 24);
        if (morningEnd <= morningStart) morningEnd = morningStart + 1;

        int afternoonStart = Mathf.Clamp(afternoonStartHour, 0, 23);
        int afternoonEnd = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        if (afternoonEnd <= afternoonStart) afternoonEnd = afternoonStart + 1;

        if (!morningClassAttended && !morningClassSoonReminderShown && hour >= morningStart - remindBefore && hour < morningStart)
        {
            morningClassSoonReminderShown = ShowClassReminder(morningClassSoonReminderText);
            return;
        }

        if (!morningClassAttended && !morningClassDueReminderShown && hour >= morningStart && hour < morningEnd)
        {
            morningClassDueReminderShown = ShowClassReminder(morningClassDueReminderText);
            return;
        }

        if (!afternoonClassAttended && !afternoonClassSoonReminderShown && hour >= afternoonStart - remindBefore && hour < afternoonStart)
        {
            afternoonClassSoonReminderShown = ShowClassReminder(afternoonClassSoonReminderText);
            return;
        }

        if (!afternoonClassAttended && !afternoonClassDueReminderShown && hour >= afternoonStart && hour < afternoonEnd)
        {
            afternoonClassDueReminderShown = ShowClassReminder(afternoonClassDueReminderText);
        }
    }

    private bool ShowClassReminder(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return false;

        CleanupClassReminderRuntimeDialogue();
        classReminderRuntime = CreateOneLineDialogue(text, "Được");
        ds.StartDialogue(classReminderRuntime, null, _ =>
        {
            CleanupClassReminderRuntimeDialogue();
            return true;
        });

        return true;
    }

    private void CleanupClassReminderRuntimeDialogue()
    {
        if (classReminderRuntime == null) return;
        Destroy(classReminderRuntime);
        classReminderRuntime = null;
    }

    private bool ShouldQueueEveningStudyMissedReminderForDepartedDay(bool morningAttendedDepartedDay, bool afternoonAttendedDepartedDay)
    {
        if (!enableDailyClassAttendance || !requireEveningHomeStudyBeforeSleep) return false;
        if (!(morningAttendedDepartedDay || afternoonAttendedDepartedDay)) return false;
        return !eveningStudyDone;
    }

    private void TryShowEveningStudyReminder()
    {
        if (!enableDailyClassAttendance || !requireEveningHomeStudyBeforeSleep) return;
        if (GameTimeManager.Instance == null) return;
        if (eveningStudyReminderShownTonight || eveningStudyDone) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        EnsureDailyClassAttendanceState();
        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return;
        if (!(morningClassAttended || afternoonClassAttended)) return;

        string scene = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        if (!IsScenePlayerHome(scene)) return;

        int hour = GameTimeManager.Instance.Hour;
        int afternoonEndExclusive = Mathf.Clamp(afternoonEndHourExclusive, 1, 24);
        int remindStart = Mathf.Clamp(eveningStudyReminderStartHour, 0, 23);

        if (hour < Mathf.Max(afternoonEndExclusive, remindStart)) return;
        if (string.IsNullOrWhiteSpace(eveningStudyReminderText)) return;

        CleanupEveningStudyReminderRuntimeDialogue();
        eveningStudyReminderRuntime = CreateOneLineDialogue(eveningStudyReminderText, "Được");
        ds.StartDialogue(eveningStudyReminderRuntime, null, _ =>
        {
            CleanupEveningStudyReminderRuntimeDialogue();
            eveningStudyReminderShownTonight = true;
            return true;
        });
    }

    private void TryShowEveningStudyMissedReminderIfPending()
    {
        if (!eveningStudyMissedReminderPending) return;
        if (string.IsNullOrWhiteSpace(eveningStudyMissedCarryOverText)) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        CleanupEveningStudyReminderRuntimeDialogue();
        eveningStudyReminderRuntime = CreateOneLineDialogue(eveningStudyMissedCarryOverText, "Được");
        ds.StartDialogue(eveningStudyReminderRuntime, null, _ =>
        {
            CleanupEveningStudyReminderRuntimeDialogue();
            eveningStudyMissedReminderPending = false;
            OnProgressChanged?.Invoke();
            return true;
        });
    }

    private void CleanupEveningStudyReminderRuntimeDialogue()
    {
        if (eveningStudyReminderRuntime == null) return;
        Destroy(eveningStudyReminderRuntime);
        eveningStudyReminderRuntime = null;
    }

    private void InitializeDailyTracking()
    {
        if (GameTimeManager.Instance == null) return;

        summaryTrackedSemester = GameTimeManager.Instance.Semester;
        summaryTrackedDay = GameTimeManager.Instance.DayInSemester;
        ResetDay6ArcadeEventStateIfBeforeDay6();
        EnsureDailyClassAttendanceState();
    }

    private void ProcessDailyTransition()
    {
        if (GameTimeManager.Instance == null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;

        if (summaryTrackedSemester < 0 || summaryTrackedDay < 0)
        {
            summaryTrackedSemester = sem;
            summaryTrackedDay = day;
            EnsureDailyClassAttendanceState();
            return;
        }

        if (sem == summaryTrackedSemester && day == summaryTrackedDay)
        {
            return;
        }

        bool suppressSummary = summaryTrackedSemester == suppressSummaryTransitionSemester
            && summaryTrackedDay == suppressSummaryTransitionDay;

        if (ShouldQueueEveningStudyMissedReminderForDepartedDay(morningClassAttended, afternoonClassAttended))
        {
            eveningStudyMissedReminderPending = true;
        }

        if (!suppressSummary)
        {
            QueueDailySummary(summaryTrackedSemester, summaryTrackedDay, morningClassAttended, afternoonClassAttended, eventsCompletedToday, questsCompletedToday);
        }
        else
        {
            dailySummaryPending = false;
            dailySummaryText = null;
            suppressSummaryTransitionSemester = -1;
            suppressSummaryTransitionDay = -1;
        }

        // Clean up a stale active event from a previous day. One calendar day = at most one
        // event in the current design. If the bed didn't fail it (failEventIfDayEndsIncomplete=false)
        // and the player advanced past the event's day, drop it so today's event can start.
        if (ActiveEvent != null && (ActiveEvent.semester != sem || ActiveEvent.dayInSemester != day))
        {
            var stale = ActiveEvent;
            if (stale.lockAutoTimeUntilComplete && GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.SetAutoTickEnabled(true);
            }

            ActiveEvent = null;
            completedObjectiveIds.Clear();
            triggeredEventsToday.Clear();
            lastTrackedSemester = sem;
            lastTrackedDay = day;
            startLetterShown = false;
            startDialogueShown = false;
            latePenaltyApplied = false;
            missApplied = false;
            lateDialoguePending = false;
            lateDialogueSource = null;
            CleanupLateRuntimeDialogue();

            OnProgressChanged?.Invoke();
        }

        summaryTrackedSemester = sem;
        summaryTrackedDay = day;
        ResetDay6ArcadeEventStateIfBeforeDay6();
        eventsCompletedToday = 0;
        questsCompletedToday = 0;

        classAttendanceSemester = sem;
        classAttendanceDay = day;
        morningClassAttended = false;
        afternoonClassAttended = false;
        morningMissPenaltyApplied = false;
        afternoonMissPenaltyApplied = false;
        morningStudyDone = false;
        noStudyByNoonPenaltyApplied = false;
        eveningStudyDone = false;
        eveningStudyReminderShownTonight = false;
        morningClassSoonReminderShown = false;
        morningClassDueReminderShown = false;
        afternoonClassSoonReminderShown = false;
        afternoonClassDueReminderShown = false;
        morningPostClassTeleportApplied = false;
        afternoonPostClassTeleportApplied = false;
        morningMissMoneyFineApplied = false;

        ResetDay3TuitionDailyTracking();
    }

    private void ResetDay6ArcadeEventStateIfBeforeDay6()
    {
        if (GameTimeManager.Instance == null) return;
        if (GameTimeManager.Instance.DayInSemester >= 6) return;

        day6EventTriggered = false;
        day6AcceptedArcadeInvite = false;
        forceSleepTonight = false;
        wakeUpLateAfterArcade = false;
        lateWakePenaltyApplied = false;
    }

    public string BuildCurrentDaySummaryText()
    {
        if (GameTimeManager.Instance == null) return null;

        EnsureDailyClassAttendanceState();

        return BuildDailySummaryText(
            GameTimeManager.Instance.Semester,
            GameTimeManager.Instance.DayInSemester,
            morningClassAttended,
            afternoonClassAttended,
            eventsCompletedToday,
            questsCompletedToday);
    }

    public void SuppressCurrentDaySummaryOnNextTransition()
    {
        if (GameTimeManager.Instance == null) return;

        suppressSummaryTransitionSemester = GameTimeManager.Instance.Semester;
        suppressSummaryTransitionDay = GameTimeManager.Instance.DayInSemester;
        dailySummaryPending = false;
        dailySummaryText = null;
    }

    private void QueueDailySummary(int semester, int day, bool morningAttended, bool afternoonAttended, int completedEvents, int completedQuests)
    {
        dailySummaryText = BuildDailySummaryText(semester, day, morningAttended, afternoonAttended, completedEvents, completedQuests);
        dailySummaryPending = true;
    }

    private static string BuildDailySummaryText(int semester, int day, bool morningAttended, bool afternoonAttended, int completedEvents, int completedQuests)
    {
        bool attendedEnough = morningAttended && afternoonAttended;
        return
            $"Tổng kết ngày {day} (HK {semester})\n" +
            $"- Điểm danh sáng: {(morningAttended ? "Có" : "Không")}\n" +
            $"- Điểm danh chiều: {(afternoonAttended ? "Có" : "Không")}\n" +
            $"- Đi học đầy đủ: {(attendedEnough ? "Có" : "Không")}\n" +
            $"- Sự kiện hoàn thành: {Mathf.Max(0, completedEvents)}\n" +
            $"- Nhiệm vụ hoàn thành: {Mathf.Max(0, completedQuests)}";
    }

    private void TryShowPendingFailedEventDialogue()
    {
        if (pendingFailedEventDialogue == null) return;

        var ds = ResolveDialogueSystem();
        if (ds == null) return;
        if (ds.IsDialogueActive) return;

        ds.StartDialogue(pendingFailedEventDialogue, null);
        pendingFailedEventDialogue = null;
    }

    private void TryShowDailySummaryIfPending()
    {
        if (!dailySummaryPending) return;
        if (string.IsNullOrWhiteSpace(dailySummaryText))
        {
            dailySummaryPending = false;
            return;
        }

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        CleanupDailySummaryRuntimeDialogue();
        dailySummaryRuntime = CreateOneLineDialogueNoChoice(dailySummaryText);

        ds.StartDialogue(dailySummaryRuntime, null);

        dailySummaryPending = false;
        dailySummaryText = null;

        if (dailySummaryAutoCloseSeconds > 0f)
        {
            StopDailySummaryAutoCloseRoutine();
            dailySummaryAutoCloseRoutine = StartCoroutine(AutoCloseDailySummary(dailySummaryAutoCloseSeconds));
        }
    }

    private void CleanupDailySummaryRuntimeDialogue()
    {
        if (dailySummaryRuntime != null)
        {
            Destroy(dailySummaryRuntime);
            dailySummaryRuntime = null;
        }
    }

    private IEnumerator AutoCloseDailySummary(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, seconds));

        var ds = ResolveDialogueSystem();
        if (ds != null && ds.IsDialogueActive)
        {
            ds.EndDialogue();
        }

        CleanupDailySummaryRuntimeDialogue();
        dailySummaryAutoCloseRoutine = null;
    }

    private void StopDailySummaryAutoCloseRoutine()
    {
        if (dailySummaryAutoCloseRoutine != null)
        {
            StopCoroutine(dailySummaryAutoCloseRoutine);
            dailySummaryAutoCloseRoutine = null;
        }
    }

    private void TrySubscribeQuestCompletion()
    {
        if (questCompletionSubscribed) return;
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        questCompletionSubscribed = true;
    }

    private static DialogueData CreateOneLineDialogueNoChoice(string text)
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = null
            }
        };

        return data;
    }

    private void TryUnsubscribeQuestCompletion()
    {
        if (!questCompletionSubscribed) return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }

        questCompletionSubscribed = false;
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (quest == null) return;
        questsCompletedToday++;

        var ev = ActiveEvent;
        if (ev == null) return;
        if (ev.questCompletionToObjective == null || ev.questCompletionToObjective.Count == 0) return;

        string questId = quest.GetId();
        for (int i = 0; i < ev.questCompletionToObjective.Count; i++)
        {
            var link = ev.questCompletionToObjective[i];
            if (link?.quest == null) continue;
            if (!string.Equals(link.quest.GetId(), questId, StringComparison.Ordinal)) continue;
            if (string.IsNullOrWhiteSpace(link.storyObjectiveId)) continue;
            TryCompleteObjective(link.storyObjectiveId.Trim());
        }
    }

    private void ApplyAutoAcceptQuests(StoryEventDefinition ev)
    {
        if (ev?.autoAcceptQuestsOnStart == null || ev.autoAcceptQuestsOnStart.Count == 0) return;
        if (QuestManager.Instance == null) return;

        for (int i = 0; i < ev.autoAcceptQuestsOnStart.Count; i++)
        {
            var q = ev.autoAcceptQuestsOnStart[i];
            if (q == null) continue;
            if (QuestManager.Instance.IsCompleted(q)) continue;
            QuestManager.Instance.AcceptQuest(q);
        }
    }

    private void SyncQuestMappedObjectivesForAlreadyCompletedQuests(StoryEventDefinition ev)
    {
        if (ev?.questCompletionToObjective == null || ev.questCompletionToObjective.Count == 0) return;
        if (QuestManager.Instance == null) return;

        for (int i = 0; i < ev.questCompletionToObjective.Count; i++)
        {
            var link = ev.questCompletionToObjective[i];
            if (link?.quest == null || string.IsNullOrWhiteSpace(link.storyObjectiveId)) continue;
            if (!QuestManager.Instance.IsCompleted(link.quest)) continue;
            TryCompleteObjective(link.storyObjectiveId.Trim());
        }
    }

    private void TrySubscribeNpcTalk()
    {
        if (questNpcTalkSubscribed) return;
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnFirstTimeNpcTalked -= HandleFirstTimeNpcTalked;
        QuestManager.Instance.OnFirstTimeNpcTalked += HandleFirstTimeNpcTalked;
        questNpcTalkSubscribed = true;
    }

    private void TryUnsubscribeNpcTalk()
    {
        if (!questNpcTalkSubscribed) return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnFirstTimeNpcTalked -= HandleFirstTimeNpcTalked;
        }

        questNpcTalkSubscribed = false;
    }

    private void HandleFirstTimeNpcTalked(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return;

        var ev = ActiveEvent;
        if (ev?.objectives == null || ev.objectives.Count == 0) return;

        string id = npcId.Trim();
        for (int i = 0; i < ev.objectives.Count; i++)
        {
            var o = ev.objectives[i];
            if (o == null) continue;
            if (string.IsNullOrWhiteSpace(o.completeObjectiveWhenNpcFirstTalkId)) continue;
            if (!string.Equals(o.completeObjectiveWhenNpcFirstTalkId.Trim(), id, StringComparison.Ordinal)) continue;
            if (string.IsNullOrWhiteSpace(o.id)) continue;
            TryCompleteObjective(o.id.Trim());
        }
    }

    private void ApplyDailyClassPenalty(bool isMiss)
    {
        if (StatManager.Instance == null) return;

        float energyPenalty = isMiss ? Mathf.Max(0f, missEnergyPenalty) : Mathf.Max(0f, lateEnergyPenalty);
        float stressPenalty = isMiss ? Mathf.Max(0f, missStressPenalty) : Mathf.Max(0f, lateStressPenalty);
        float gpaPenalty = isMiss ? Mathf.Max(0f, missGpaPenalty) : 0f;

        if (isMiss && stressPenalty > 0f && SemesterProgressManager.Instance != null)
        {
            int priorMissed = Mathf.Max(0, SemesterProgressManager.Instance.TotalMissed);
            float bonus = Mathf.Min(
                Mathf.Max(0f, missExtraStressFromPriorSessionsCap),
                Mathf.Max(0f, missExtraStressPerPriorMissSession) * priorMissed);
            stressPenalty += bonus;
        }

        if (energyPenalty > 0f)
        {
            StatManager.Instance.energy = Mathf.Max(0f, StatManager.Instance.energy - energyPenalty);
        }

        if (stressPenalty > 0f)
        {
            StatManager.Instance.stress += stressPenalty;
        }

        if (gpaPenalty > 0f)
        {
            StatManager.Instance.gpa = Mathf.Max(0f, StatManager.Instance.gpa - gpaPenalty);
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    private void TryApplyLatePenalty()
    {
        if (latePenaltyApplied) return;
        if (ActiveEvent == null) return;
        if (!ActiveEvent.applyLatePenalty) return;

        if (GameTimeManager.Instance == null) return;
        if (StatManager.Instance == null) return;

        if (!string.IsNullOrWhiteSpace(ActiveEvent.lateObjectiveId) && IsObjectiveComplete(ActiveEvent.lateObjectiveId))
        {
            latePenaltyApplied = true;
            return;
        }

        if (GameTimeManager.Instance.Hour < ActiveEvent.lateHour) return;

        ApplyLatePenaltyInternal();
    }

    public bool ApplyLatePenaltyNow()
    {
        if (latePenaltyApplied) return false;
        if (ActiveEvent == null) return false;
        if (!ActiveEvent.applyLatePenalty) return false;
        if (StatManager.Instance == null) return false;

        ApplyLatePenaltyInternal();
        return true;
    }

    private void ApplyLatePenaltyInternal()
    {
        if (ActiveEvent == null) return;
        if (StatManager.Instance == null) return;

        lateDialogueSource = ActiveEvent;

        float energyPenalty = Mathf.Max(0f, ActiveEvent.lateEnergyPenalty);
        float stressPenalty = Mathf.Max(0f, ActiveEvent.lateStressPenalty);

        if (energyPenalty > 0f)
        {
            StatManager.Instance.energy = Mathf.Max(0f, StatManager.Instance.energy - energyPenalty);
        }

        if (stressPenalty > 0f)
        {
            StatManager.Instance.stress += stressPenalty;
        }

        latePenaltyApplied = true;

        // Show a one-time late notification.
        lateDialoguePending = true;

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    private void TryShowLateDialogueIfPending()
    {
        if (!lateDialoguePending) return;
        var source = ActiveEvent != null ? ActiveEvent : lateDialogueSource;
        if (source == null)
        {
            lateDialoguePending = false;
            CleanupLateRuntimeDialogue();
            return;
        }

        var ds = ResolveDialogueSystem();
        if (ds == null) return;
        if (ds.IsDialogueActive) return;

        DialogueData dataToShow = source.lateDialogue;
        if (dataToShow == null)
        {
            if (lateDialogueRuntime == null)
            {
                string ok = string.IsNullOrWhiteSpace(source.lateOkText) ? "Được" : source.lateOkText;
                string msg = string.IsNullOrWhiteSpace(source.lateMessage)
                    ? "Bạn đi học muộn."
                    : source.lateMessage;
                lateDialogueRuntime = CreateOneLineDialogue(msg, ok);
            }

            dataToShow = lateDialogueRuntime;
        }

        // Use external handler to cleanup the runtime dialogue when closed.
        ds.StartDialogue(dataToShow, null, _ =>
        {
            lateDialoguePending = false;
            lateDialogueSource = null;
            CleanupLateRuntimeDialogue();
            return true;
        });
    }

    private static DialogueData CreateOneLineDialogue(string text, string okText)
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = new List<DialogueChoice>(1)
                {
                    new DialogueChoice { choiceText = okText, type = ChoiceType.End }
                }
            }
        };

        return data;
    }

    private void CleanupLateRuntimeDialogue()
    {
        if (lateDialogueRuntime != null)
        {
            Destroy(lateDialogueRuntime);
            lateDialogueRuntime = null;
        }
    }

    private void TryFailByMissDeadline()
    {
        if (missApplied) return;
        if (ActiveEvent == null) return;
        if (!ActiveEvent.failIfMissed) return;

        if (GameTimeManager.Instance == null) return;

        if (GameTimeManager.Instance.Hour < ActiveEvent.missHour) return;

        if (!string.IsNullOrWhiteSpace(ActiveEvent.missObjectiveId) && IsObjectiveComplete(ActiveEvent.missObjectiveId))
        {
            missApplied = true;
            return;
        }

        FailActiveEventInternal(ActiveEvent);
        missApplied = true;
    }

    public bool FailActiveEvent()
    {
        if (ActiveEvent == null) return false;
        FailActiveEventInternal(ActiveEvent);
        missApplied = true;
        return true;
    }

    private void FailActiveEventInternal(StoryEventDefinition ev)
    {
        if (ev == null) return;

        if (StatManager.Instance != null)
        {
            float energyPenalty = Mathf.Max(0f, ev.missEnergyPenalty);
            float stressPenalty = Mathf.Max(0f, ev.missStressPenalty);
            float gpaPenalty = Mathf.Max(0f, ev.missGpaPenalty);

            if (energyPenalty > 0f)
            {
                StatManager.Instance.energy = Mathf.Max(0f, StatManager.Instance.energy - energyPenalty);
            }

            if (stressPenalty > 0f)
            {
                StatManager.Instance.stress += stressPenalty;
            }

            if (gpaPenalty > 0f)
            {
                StatManager.Instance.gpa = Mathf.Max(0f, StatManager.Instance.gpa - gpaPenalty);
            }
        }

        if (ev.resumeAutoTimeWhenComplete && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(true);
        }

        ActiveEvent = null;

        if (ev.missedDialogue != null)
        {
            var ds = ResolveDialogueSystem();
            if (ds != null && !ds.IsDialogueActive)
            {
                ds.StartDialogue(ev.missedDialogue, null);
            }
            else
            {
                pendingFailedEventDialogue = ev.missedDialogue;
            }
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }

        OnEventCompleted?.Invoke(ev);
        OnProgressChanged?.Invoke();
    }

    private static bool HasObjective(StoryEventDefinition ev, string objectiveId)
    {
        if (ev == null || ev.objectives == null) return false;
        if (string.IsNullOrWhiteSpace(objectiveId)) return false;

        string id = objectiveId.Trim();

        for (int i = 0; i < ev.objectives.Count; i++)
        {
            if (ev.objectives[i] == null) continue;
            if (string.IsNullOrWhiteSpace(ev.objectives[i].id)) continue;
            if (string.Equals(ev.objectives[i].id.Trim(), id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEventComplete(StoryEventDefinition ev)
    {
        if (ev == null || ev.objectives == null || ev.objectives.Count == 0) return true;

        for (int i = 0; i < ev.objectives.Count; i++)
        {
            var o = ev.objectives[i];
            if (o == null) continue;

            // Optional objectives never block completion. Players can still finish them
            // for bonus rewards, but the event auto-completes once all required ones are done.
            if (o.optional) continue;

            if (string.IsNullOrWhiteSpace(o.id)) continue;
            if (!completedObjectiveIds.Contains(o.id.Trim())) return false;
        }

        return true;
    }

    public bool IsObjectiveOptional(string objectiveId)
    {
        if (ActiveEvent == null) return false;
        if (string.IsNullOrWhiteSpace(objectiveId)) return false;
        if (ActiveEvent.objectives == null) return false;

        string id = objectiveId.Trim();
        for (int i = 0; i < ActiveEvent.objectives.Count; i++)
        {
            var o = ActiveEvent.objectives[i];
            if (o == null) continue;
            if (string.IsNullOrWhiteSpace(o.id)) continue;
            if (string.Equals(o.id.Trim(), id, StringComparison.Ordinal))
            {
                return o.optional;
            }
        }

        return false;
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = UnityEngine.Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }
}
