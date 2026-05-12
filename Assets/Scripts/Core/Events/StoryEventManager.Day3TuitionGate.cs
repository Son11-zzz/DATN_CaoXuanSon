using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ngày học kỳ 1 — ngày 3: giờ nghỉ trưa (12h–13h) đưa NV và GV (teacher3) vào CTSV, tạm dừng thời gian tự động cho đến khi nộp học phí.
/// </summary>
public partial class StoryEventManager
{
    [Header("Ngày 3 — nộp học phí tại CTSV (12h–13h)")]
    [SerializeField] private bool enableDay3TuitionGate = true;
    [SerializeField] private int day3TuitionSemester = 1;
    [SerializeField] private int day3TuitionDay = 3;
    [SerializeField, Range(0, 23)] private int day3TuitionWindowStartHour = 12;
    [SerializeField, Range(0, 24)] private int day3TuitionWindowEndHourExclusive = 13;
    [SerializeField, Min(0f)] private float day3TuitionFeeAmount = 2_000_000f;
    [Tooltip("SpawnPoint.spawnId (trong 21_SchoolArea object Spawn_CTSV hiện dùng id CTSV).")]
    [SerializeField] private string day3TuitionSpawnPointId = "CTSV";
    [SerializeField] private string day3TuitionSceneName = "21_SchoolArea";
    [SerializeField] private string day3TuitionTeacherNpcId = "npc_ThayGiaoBa";
    [SerializeField] private Vector2 day3TuitionTeacherOffsetFromSpawn = new Vector2(1.4f, 0f);
    [Tooltip("Mục tiêu story (Day03_HomeworkDay). Để trống = không gọi TryCompleteObjective.")]
    [SerializeField] private string day3TuitionStoryObjectiveId = "PayTuitionCTSV";
    [SerializeField, TextArea] private string day3TuitionDialogueIntro =
        "Phòng CTSV — hoàn tất đóng học phí kỳ này để tiếp tục buổi chiều.";
    [SerializeField, TextArea] private string day3TuitionTeacherLine =
        "Thầy: Em đến đúng lúc. Đây là học phí của kỳ này.";
    [SerializeField, TextArea] private string day3TuitionPlayerLine =
        "Dạ vâng, em sẽ đóng ngay.";
    [Tooltip("Nếu tiền đã bị hệ thống khác trừ, cho phép hoàn tất mà không yêu cầu đủ tiền.")]
    [SerializeField] private bool day3TuitionAllowCompleteWhenInsufficient = true;

    [Header("Dialogue assets (Unity)")]
    [Tooltip("Dòng chữ + lời thoại gốc; runtime sẽ nối thêm học phí / số tiền hiện có và gán lại nút nộp.")]
    [SerializeField] private DialogueData day3TuitionPromptDialogueAsset;
    [Tooltip("Hiện sau khi đóng học phí thành công (tuỳ chọn).")]
    [SerializeField] private DialogueData day3TuitionPaidThanksDialogueAsset;
    [Tooltip("Nếu có, sẽ chuyển node đến id này sau khi đóng học phí thành công.")]
    [SerializeField] private string day3TuitionPaidThanksNodeId = "Day03_CTSV_TuitionPaidThanks";

    [Header("HUD (ẩn khi đối thoại học phí)")]
    [SerializeField] private bool day3TuitionHideHudDuringDialogue = true;
    [SerializeField] private List<GameObject> day3TuitionHudRoots = new List<GameObject>();
    [SerializeField] private List<CanvasGroup> day3TuitionHudCanvasGroups = new List<CanvasGroup>();

    private bool day3TuitionFeePaid;
    private bool day3TuitionRelocationDoneForCalendarDay;
    private bool day3TuitionSuspendedAutoTick;
    private bool day3TuitionAutoTickBeforeSuspension = true;
    private DialogueData day3TuitionDialogueRuntime;
    private NpcSimpleMover2D day3TuitionTeacherMover;

    private bool day3TuitionDialogueOpen;
    private bool day3TuitionHudHidden;
    private Vector3 day3TuitionPlayerReturnPosition;
    private bool day3TuitionPlayerReturnCaptured;
    private Vector3 day3TuitionTeacherReturnPosition;
    private bool day3TuitionTeacherReturnCaptured;

    private readonly List<Day3HudRootState> day3TuitionHudRootStates = new List<Day3HudRootState>();
    private readonly List<Day3HudCanvasState> day3TuitionHudCanvasStates = new List<Day3HudCanvasState>();

    private struct Day3HudRootState
    {
        public GameObject root;
        public bool wasActive;
    }

    private struct Day3HudCanvasState
    {
        public CanvasGroup group;
        public float alpha;
        public bool blocksRaycasts;
        public bool interactable;
    }

    private void ResetDay3TuitionDailyTracking()
    {
        day3TuitionRelocationDoneForCalendarDay = false;
        day3TuitionDialogueOpen = false;
        RestoreDay3TuitionHud();
        ResetDay3TuitionReturnPositions();
        ReleaseDay3TuitionTeacherCutsceneHold();
        CleanupDay3TuitionRuntimeDialogue();
    }

    /// <summary>Xoá trạng thái cutscene học phí trước khi apply save (GameTimeManager giữ autoTick).</summary>
    private void PrepareDay3TuitionForLoadedSave()
    {
        day3TuitionDialogueOpen = false;
        RestoreDay3TuitionHud();
        ResetDay3TuitionReturnPositions();
        CleanupDay3TuitionRuntimeDialogue();
        ReleaseDay3TuitionTeacherCutsceneHold();
        day3TuitionRelocationDoneForCalendarDay = false;
        day3TuitionSuspendedAutoTick = false;
    }

    /// <summary>New Game: hoàn tác đóng băng thời gian / giữ NPC nếu đang kẹt.</summary>
    private void HardResetDay3TuitionForNewGame()
    {
        day3TuitionDialogueOpen = false;
        RestoreDay3TuitionHud();
        ResetDay3TuitionReturnPositions();
        CleanupDay3TuitionRuntimeDialogue();
        if (day3TuitionSuspendedAutoTick && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.SetAutoTickEnabled(day3TuitionAutoTickBeforeSuspension);
        }

        day3TuitionSuspendedAutoTick = false;
        ReleaseDay3TuitionTeacherCutsceneHold();
        day3TuitionRelocationDoneForCalendarDay = false;
    }

    private void CleanupDay3TuitionRuntimeDialogue()
    {
        if (day3TuitionDialogueRuntime == null) return;
        Destroy(day3TuitionDialogueRuntime);
        day3TuitionDialogueRuntime = null;
    }

    private void TryDay3TuitionGate()
    {
        if (!IsDay3TuitionWindowActive()) return;

        if (!day3TuitionRelocationDoneForCalendarDay)
        {
            ApplyDay3TuitionRelocationAndFreezeTime();
        }
    }

    public bool TryHandleDay3TuitionNpcInteraction(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return false;
        if (!IsDay3TuitionWindowActive()) return false;

        if (!day3TuitionRelocationDoneForCalendarDay)
        {
            ApplyDay3TuitionRelocationAndFreezeTime();
        }

        string targetNpcId = string.IsNullOrWhiteSpace(day3TuitionTeacherNpcId)
            ? string.Empty
            : day3TuitionTeacherNpcId.Trim();
        if (!string.Equals(npcId.Trim(), targetNpcId, StringComparison.OrdinalIgnoreCase)) return false;

        return TryShowDay3TuitionDialogueIfNeeded();
    }

    private bool IsDay3TuitionWindowActive()
    {
        if (!enableDay3TuitionGate) return false;
        if (day3TuitionFeePaid) return false;
        if (GameTimeManager.Instance == null) return false;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;
        int hour = GameTimeManager.Instance.Hour;

        if (sem != day3TuitionSemester || day != day3TuitionDay) return false;

        int w0 = Mathf.Clamp(day3TuitionWindowStartHour, 0, 23);
        int w1 = Mathf.Clamp(day3TuitionWindowEndHourExclusive, 0, 24);
        if (w1 <= w0) w1 = Mathf.Min(24, w0 + 1);

        if (hour < w0 || hour >= w1) return false;

        if (ActiveEvent != null && ActiveEvent.skipClassAttendanceToday) return false;

        EnsureDailyClassAttendanceState();

        if (!morningClassAttended) return false;

        string sceneWant = string.IsNullOrWhiteSpace(day3TuitionSceneName)
            ? "21_SchoolArea"
            : day3TuitionSceneName.Trim();

        return string.Equals(SceneManager.GetActiveScene().name, sceneWant, StringComparison.Ordinal);
    }

    private void ApplyDay3TuitionRelocationAndFreezeTime()
    {
        if (string.IsNullOrWhiteSpace(day3TuitionSpawnPointId))
        {
            return;
        }

        if (!TryFindSpawnPointByIdAny(new[] { day3TuitionSpawnPointId.Trim(), "Spawn_CTSV", "CTSV" },
                out SpawnPoint spawn))
        {
            Debug.LogWarning(
                $"StoryEventManager: không tìm thấy SpawnPoint học phí (đã thử id: '{day3TuitionSpawnPointId}', Spawn_CTSV, CTSV). Kiểm tra spawnId trên object và/bật GameObject.");
            return;
        }

        if (PersistentPlayer.Instance != null)
        {
            CaptureDay3TuitionReturnPositions();
            PersistentPlayer.Instance.TeleportTo(spawn.transform.position);
        }

        ReleaseDay3TuitionTeacherCutsceneHold();
        if (TryFindNpcMoverForTuition(day3TuitionTeacherNpcId, out NpcSimpleMover2D mover))
        {
            day3TuitionTeacherMover = mover;
            if (!day3TuitionTeacherReturnCaptured)
            {
                day3TuitionTeacherReturnPosition = day3TuitionTeacherMover.transform.position;
                day3TuitionTeacherReturnCaptured = true;
            }
            day3TuitionTeacherMover.ExternalControl = true;
            Vector3 tpos = spawn.transform.position + (Vector3)day3TuitionTeacherOffsetFromSpawn;
            TeleportTransform2D(day3TuitionTeacherMover.transform, tpos);
        }

        if (!day3TuitionSuspendedAutoTick)
        {
            day3TuitionAutoTickBeforeSuspension = GameTimeManager.Instance.AutoTickEnabled;
            GameTimeManager.Instance.SetAutoTickEnabled(false);
            day3TuitionSuspendedAutoTick = true;
        }

        day3TuitionRelocationDoneForCalendarDay = true;
        OnProgressChanged?.Invoke();
    }

    private bool TryShowDay3TuitionDialogueIfNeeded()
    {
        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return false;
        if (StatManager.Instance == null) return false;

        CleanupDay3TuitionRuntimeDialogue();

        float fee = Mathf.Max(0f, day3TuitionFeeAmount);
        float money = StatManager.Instance.money;

        string narrativeSource = ResolveDay3TuitionNarrativeSourceForLine();
        day3TuitionDialogueRuntime = BuildDay3TuitionRuntimeDialogueData(narrativeSource, fee, money);
        if (day3TuitionDialogueRuntime == null || day3TuitionDialogueRuntime.lines == null
            || day3TuitionDialogueRuntime.lines.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < day3TuitionDialogueRuntime.lines.Count - 1; i++)
        {
            var line = day3TuitionDialogueRuntime.lines[i];
            if (line != null && line.choices != null && line.choices.Count > 0)
            {
                line.choices = null;
            }
        }

        DialogueLine choiceLine = day3TuitionDialogueRuntime.lines[day3TuitionDialogueRuntime.lines.Count - 1];
        bool goToThanksNode = !string.IsNullOrWhiteSpace(day3TuitionPaidThanksNodeId);
        if (choiceLine.choices == null || choiceLine.choices.Count == 0)
        {
            choiceLine.choices = new List<DialogueChoice>(1)
            {
                new DialogueChoice { choiceText = Day3TuitionPayChoiceCaption(fee) }
            };
        }
        else
        {
            choiceLine.choices[0].choiceText = Day3TuitionPayChoiceCaption(fee);
        }

        choiceLine.choices[0].type = goToThanksNode ? ChoiceType.GoToNode : ChoiceType.End;
        choiceLine.choices[0].nextNode = goToThanksNode ? day3TuitionPaidThanksNodeId.Trim() : null;

        string payCaption = choiceLine.choices[0].choiceText;

        day3TuitionDialogueOpen = true;
        HideDay3TuitionHud();

        ds.StartDialogue(day3TuitionDialogueRuntime, null, choice =>
        {
            if (choice == null || choice.choiceText != payCaption)
            {
                return true;
            }

            if (StatManager.Instance == null)
            {
                return true;
            }

            if (StatManager.Instance.money < fee)
            {
                if (!day3TuitionAllowCompleteWhenInsufficient)
                {
                    if (ds.dialogueUI != null)
                    {
                        float mNow = StatManager.Instance.money;
                        string body = ComposeDay3TuitionDialogueBody(fee, mNow, narrativeSource) + "\n\nChưa đủ tiền.";
                        ds.dialogueUI.Show(body);
                        ds.dialogueUI.ShowChoices(choiceLine.choices);
                    }

                    return false;
                }
            }

            if (StatManager.Instance.money >= fee)
            {
                StatManager.Instance.money = Mathf.Max(0f, StatManager.Instance.money - fee);
                if (EventManager.Instance != null)
                {
                    EventManager.Instance.NotifyStatChanged();
                }
            }

            day3TuitionFeePaid = true;
            ResetDay3TuitionReturnPositions();
            ReleaseDay3TuitionTeacherCutsceneHold();

            if (day3TuitionSuspendedAutoTick && GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.SetAutoTickEnabled(day3TuitionAutoTickBeforeSuspension);
                day3TuitionSuspendedAutoTick = false;
            }

            if (!string.IsNullOrWhiteSpace(day3TuitionStoryObjectiveId))
            {
                TryCompleteObjective(day3TuitionStoryObjectiveId.Trim(), out _);
            }

            OnProgressChanged?.Invoke();
            CleanupDay3TuitionRuntimeDialogue();

            if (!goToThanksNode && day3TuitionPaidThanksDialogueAsset != null)
            {
                StartCoroutine(CoShowDay3TuitionPaidThanksAfterClose());
            }

            return !goToThanksNode;
        });

        return true;
    }

    private void TryRestoreDay3TuitionAfterDialogue()
    {
        if (!day3TuitionDialogueOpen) return;

        var ds = ResolveDialogueSystem();
        if (ds != null && ds.IsDialogueActive)
        {
            return;
        }

        day3TuitionDialogueOpen = false;
        RestoreDay3TuitionHud();
    }

    private void CaptureDay3TuitionReturnPositions()
    {
        if (!day3TuitionPlayerReturnCaptured && PersistentPlayer.Instance != null)
        {
            day3TuitionPlayerReturnPosition = PersistentPlayer.Instance.transform.position;
            day3TuitionPlayerReturnCaptured = true;
        }
    }

    private void ResetDay3TuitionReturnPositions()
    {
        day3TuitionPlayerReturnCaptured = false;
        day3TuitionTeacherReturnCaptured = false;
    }

    private void RestoreDay3TuitionReturnPositions()
    {
        if (day3TuitionPlayerReturnCaptured && PersistentPlayer.Instance != null)
        {
            PersistentPlayer.Instance.TeleportTo(day3TuitionPlayerReturnPosition);
        }

        if (day3TuitionTeacherReturnCaptured && day3TuitionTeacherMover != null)
        {
            TeleportTransform2D(day3TuitionTeacherMover.transform, day3TuitionTeacherReturnPosition);
        }

        ResetDay3TuitionReturnPositions();
    }

    private void HideDay3TuitionHud()
    {
        if (!day3TuitionHideHudDuringDialogue || day3TuitionHudHidden) return;

        day3TuitionHudHidden = true;

        day3TuitionHudRootStates.Clear();
        for (int i = 0; i < day3TuitionHudRoots.Count; i++)
        {
            var root = day3TuitionHudRoots[i];
            if (root == null) continue;
            day3TuitionHudRootStates.Add(new Day3HudRootState { root = root, wasActive = root.activeSelf });
            root.SetActive(false);
        }

        day3TuitionHudCanvasStates.Clear();
        for (int i = 0; i < day3TuitionHudCanvasGroups.Count; i++)
        {
            var group = day3TuitionHudCanvasGroups[i];
            if (group == null) continue;

            day3TuitionHudCanvasStates.Add(new Day3HudCanvasState
            {
                group = group,
                alpha = group.alpha,
                blocksRaycasts = group.blocksRaycasts,
                interactable = group.interactable
            });

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    private void RestoreDay3TuitionHud()
    {
        if (!day3TuitionHudHidden) return;

        for (int i = 0; i < day3TuitionHudRootStates.Count; i++)
        {
            var state = day3TuitionHudRootStates[i];
            if (state.root == null) continue;
            state.root.SetActive(state.wasActive);
        }

        for (int i = 0; i < day3TuitionHudCanvasStates.Count; i++)
        {
            var state = day3TuitionHudCanvasStates[i];
            if (state.group == null) continue;
            state.group.alpha = state.alpha;
            state.group.blocksRaycasts = state.blocksRaycasts;
            state.group.interactable = state.interactable;
        }

        day3TuitionHudRootStates.Clear();
        day3TuitionHudCanvasStates.Clear();
        day3TuitionHudHidden = false;
    }

    private string ResolveDay3TuitionNarrativeSourceForLine()
    {
        if (day3TuitionPromptDialogueAsset != null
            && day3TuitionPromptDialogueAsset.lines != null
            && day3TuitionPromptDialogueAsset.lines.Count > 0
            && !string.IsNullOrWhiteSpace(day3TuitionPromptDialogueAsset.lines[0].text))
        {
            return day3TuitionPromptDialogueAsset.lines[0].text.Trim();
        }

        return string.IsNullOrWhiteSpace(day3TuitionDialogueIntro)
            ? "Phòng CTSV — đóng học phí để tiếp tục."
            : day3TuitionDialogueIntro.Trim();
    }

    private DialogueData BuildDay3TuitionRuntimeDialogueData(string narrativeSource, float fee, float money)
    {
        if (day3TuitionPromptDialogueAsset != null)
        {
            DialogueData copy = Instantiate(day3TuitionPromptDialogueAsset);
            if (copy.lines == null || copy.lines.Count == 0)
            {
                Destroy(copy);
                return BuildFallbackRuntimeDialogueData(narrativeSource, fee, money);
            }

            copy.lines[0].text = ComposeDay3TuitionDialogueBody(fee, money, narrativeSource);

            return copy;
        }

        return BuildFallbackRuntimeDialogueData(narrativeSource, fee, money);
    }

    private DialogueData BuildFallbackRuntimeDialogueData(string narrativeSource, float fee, float money)
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        string teacherLine = string.IsNullOrWhiteSpace(day3TuitionTeacherLine)
            ? "Thầy: Em đến đúng lúc. Đây là học phí của kỳ này."
            : day3TuitionTeacherLine.Trim();
        string playerLine = string.IsNullOrWhiteSpace(day3TuitionPlayerLine)
            ? "Dạ vâng, em sẽ đóng ngay."
            : day3TuitionPlayerLine.Trim();
        string intro = ComposeDay3TuitionDialogueBody(fee, money, narrativeSource);
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = teacherLine,
                choices = null
            },
            new DialogueLine
            {
                text = playerLine,
                choices = null
            },
            new DialogueLine
            {
                text = intro,
                choices = new List<DialogueChoice>(1)
                {
                    new DialogueChoice
                    {
                        choiceText = Day3TuitionPayChoiceCaption(fee),
                        type = ChoiceType.End
                    }
                }
            }
        };

        return data;
    }

    private IEnumerator CoShowDay3TuitionPaidThanksAfterClose()
    {
        yield return null;
        var ds = ResolveDialogueSystem();
        if (ds == null || day3TuitionPaidThanksDialogueAsset == null) yield break;

        float wait = 0f;
        while (ds.IsDialogueActive && wait < 3f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        DialogueData thanks = Instantiate(day3TuitionPaidThanksDialogueAsset);
        ds.StartDialogue(thanks, null, _ =>
        {
            Destroy(thanks);
            return true;
        });
    }

    private static string Day3TuitionPayChoiceCaption(float fee)
    {
        return $"Nộp học phí ({fee:0} đ)";
    }

    private static string ComposeDay3TuitionDialogueBody(float fee, float money, string narrativeSource)
    {
        string intro = string.IsNullOrWhiteSpace(narrativeSource)
            ? "Phòng CTSV — đóng học phí để tiếp tục."
            : narrativeSource.Trim();

        return $"{intro}\n\nHọc phí: {fee:0} đ\nHiện có: {money:0} đ";
    }

    private static void TeleportTransform2D(Transform t, Vector3 world)
    {
        if (t == null) return;
        t.position = world;
        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private static bool TryFindNpcMoverForTuition(string npcId, out NpcSimpleMover2D mover)
    {
        mover = null;
        if (string.IsNullOrWhiteSpace(npcId)) return false;

        string want = npcId.Trim();
        var givers = UnityEngine.Object.FindObjectsByType<QuestGiverNPC>(FindObjectsInactive.Include);
        for (int i = 0; i < givers.Length; i++)
        {
            QuestGiverNPC q = givers[i];
            if (q == null) continue;
            if (!string.Equals(q.GetResolvedNpcId(), want, StringComparison.Ordinal)) continue;

            mover = q.GetComponent<NpcSimpleMover2D>();
            if (mover == null) mover = q.GetComponentInChildren<NpcSimpleMover2D>(true);
            return mover != null;
        }

        return false;
    }

    private void ReleaseDay3TuitionTeacherCutsceneHold()
    {
        if (day3TuitionTeacherMover != null)
        {
            day3TuitionTeacherMover.ExternalControl = false;
            day3TuitionTeacherMover = null;
        }
    }
}
