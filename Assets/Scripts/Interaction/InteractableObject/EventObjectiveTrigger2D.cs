using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class EventObjectiveTrigger2D : MonoBehaviour
{
    [Header("Event Objective")]
    [SerializeField] private string objectiveId;

    [Header("Time Window (optional)")]
    [SerializeField] private bool requireTimeWindow;
    [SerializeField] private int startHour = 7;
    [SerializeField] private int endHourExclusive = 9;
    [SerializeField, TextArea] private string tooEarlyText = "Bạn đến quá sớm.";
    [SerializeField, TextArea] private string tooLateText = "Bạn đi học muộn.";

    [SerializeField] private string phaseAction;

    [Header("On Complete: Time Skip (optional)")]
    [SerializeField] private bool fadeAndSkipTimeOnComplete;
    [SerializeField] private int targetHour = 12;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("Behavior")]
    [SerializeField] private bool oneShot = true;

    [Header("Player position (optional)")]
    [Tooltip("Sau khi hoàn thành objective (TryCompleteObjective), đưa nhân vật tới điểm này — vd. vào trong phòng học.")]
    [SerializeField] private Transform teleportPlayerToOnObjectiveComplete;
    [SerializeField] private bool teleportUsesPersistentPlayer = true;

    [Header("Daily Class Attendance")]
    [SerializeField] private bool registerAttendClassOnEnter;
    [SerializeField] private bool showDialogueOnAttendClass = true;
    [SerializeField] private bool showDialogueWhenClassBlocked = true;
    [SerializeField, TextArea] private string attendClassSuccessText = "Điểm danh thành công.";

    [Header("Dialogue")]
    [SerializeField] private bool showDialogueOnFail = true;
    [SerializeField] private string okText = "Được";

    private bool fired;
    private Collider2D cachedCollider;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        EnsureClassGateIsTriggerOnly();
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// Giữ collider luôn là trigger. Trước đây code tắt isTrigger khi ngoài giờ vào lớp → collider thành tường,
    /// chặn cả đi ra trong giờ nghỉ trưa (CanEnterClassAreaNow = false giữa buổi sáng và chiều).
    /// </summary>
    private void EnsureClassGateIsTriggerOnly()
    {
        if (!registerAttendClassOnEnter) return;

        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        if (cachedCollider == null) return;

        if (!cachedCollider.isTrigger)
        {
            cachedCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (!TryRegisterAttendClass(showBlockedMessage: true))
        {
            return;
        }

        if (oneShot && fired) return;

        if (StoryEventManager.Instance == null) return;
        if (string.IsNullOrWhiteSpace(objectiveId)) return;

        bool isLate;
        if (!CheckTimeWindow(out string failMessage, out isLate))
        {
            if (showDialogueOnFail)
            {
                ShowInfoDialogue(failMessage);
            }

            return;
        }

        // If the player is late, still allow completing the objective, but apply late penalty now.
        if (isLate)
        {
            StoryEventManager.Instance.ApplyLatePenaltyNow();
        }

        bool ok = StoryEventManager.Instance.TryCompleteObjective(objectiveId.Trim(), out string reason);
        if (!ok)
        {
            if (showDialogueOnFail)
            {
                ShowInfoDialogue(string.IsNullOrWhiteSpace(reason) ? "Hiện tại không thể làm điều đó." : reason);
            }

            return;
        }

        fired = true;

        TryTeleportPlayerAfterObjective(other);

        if (fadeAndSkipTimeOnComplete)
        {
            var fader = ResolveScreenFader();
            if (fader != null)
            {
                fader.FadeWithMidAction(SkipToTargetHour, fadeOutDuration, 0f, holdDuration, fadeInDuration);
            }
            else
            {
                SkipToTargetHour();
            }
        }

        TriggerPhaseAction();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        TryRegisterAttendClass(showBlockedMessage: false);
    }

    private bool TryRegisterAttendClass(bool showBlockedMessage)
    {
        if (!registerAttendClassOnEnter) return true;
        if (StoryEventManager.Instance == null) return true;

        bool registered = StoryEventManager.Instance.RegisterAttendClass(out string statusText, out var result);
        if (!registered)
        {
            bool blocked = result == StoryEventManager.AttendClassResult.TooEarly
                || result == StoryEventManager.AttendClassResult.BreakTime
                || result == StoryEventManager.AttendClassResult.Ended;

            if (blocked && showBlockedMessage && showDialogueWhenClassBlocked)
            {
                ShowInfoDialogue(statusText);
            }

            return !blocked;
        }

        if (!showDialogueOnAttendClass) return true;
        string text = string.IsNullOrWhiteSpace(attendClassSuccessText) ? statusText : attendClassSuccessText;
        ShowInfoDialogue(text);
        return true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        EnsureClassGateIsTriggerOnly();
    }

    void TryTeleportPlayerAfterObjective(Collider2D playerCollider)
    {
        if (teleportPlayerToOnObjectiveComplete == null)
        {
            return;
        }

        Vector3 destination = teleportPlayerToOnObjectiveComplete.position;

        if (teleportUsesPersistentPlayer && PersistentPlayer.Instance != null)
        {
            PersistentPlayer.Instance.TeleportTo(destination);
            return;
        }

        if (playerCollider != null)
        {
            Transform root = playerCollider.transform.root;
            root.position = destination;
            if (root.TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void TriggerPhaseAction()
    {
        if (string.IsNullOrEmpty(phaseAction)) return;

        switch (phaseAction)
        {
            case "NOON":
                EventPhaseHelper.ToNoon();
                break;

            case "AFTERNOON":
                EventPhaseHelper.ToAfternoon();
                break;

            case "EVENING":
                EventPhaseHelper.ToEvening();
                break;

            case "NIGHT":
                EventPhaseHelper.ToNight();
                break;
        }
    }
    private bool CheckTimeWindow(out string failMessage, out bool isLate)
    {
        failMessage = null;
        isLate = false;

        if (!requireTimeWindow) return true;
        if (GameTimeManager.Instance == null) return true;

        int h = GameTimeManager.Instance.Hour;
        int start = Mathf.Clamp(startHour, 0, 23);
        int end = Mathf.Clamp(endHourExclusive, 0, 24);
        if (end <= start) end = start + 1;

        if (h < start)
        {
            failMessage = tooEarlyText;
            return false;
        }

        if (h >= end)
        {
            // Late is allowed: we will complete the objective but apply a late penalty.
            isLate = true;
        }

        return true;
    }

    private void SkipToTargetHour()
    {
        if (GameTimeManager.Instance == null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;
        int h = Mathf.Clamp(targetHour, 0, 23);
        GameTimeManager.Instance.SetTime(sem, day, h);
    }

    private void ShowInfoDialogue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        string ok = string.IsNullOrWhiteSpace(okText) ? "Được" : okText;

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new System.Collections.Generic.List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = new System.Collections.Generic.List<DialogueChoice>(1)
                {
                    new DialogueChoice { choiceText = ok, type = ChoiceType.End }
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
