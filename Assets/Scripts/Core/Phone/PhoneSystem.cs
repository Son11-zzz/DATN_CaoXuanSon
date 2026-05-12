using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý kho tin nhắn điện thoại và lịch học.
/// Mỗi tin nhắn chỉ xuất hiện 1 lần khi đạt điều kiện (ngày + giờ), nhận thông báo "tin moi".
/// </summary>
public class PhoneSystem : MonoBehaviour
{
    public static PhoneSystem Instance;

    [Header("Messages")]
    [SerializeField] private List<PhoneMessageData> messages = new List<PhoneMessageData>();

    [Header("Class Schedule")]
    [SerializeField] private List<ClassScheduleEntry> schedule = new List<ClassScheduleEntry>();

    [Tooltip("Dung khi khong co ClassScheduleEntry rieng cho ngay do (vd. ngay 1-5 hoc chuan).")]
    [SerializeField] private ClassScheduleEntry templateWeekday;

    [Tooltip("Dung cho ngay nghi cuoi tuan khi khong co muc rieng trong list.")]
    [SerializeField] private ClassScheduleEntry templateWeekend;

    [Tooltip("Cac ngay trong ky duoc coi la cuoi tuan (dung templateWeekend). De trong = mac dinh 6,7,13,14 cho ky 15 ngay.")]
    [SerializeField] private int[] weekendDays = { 6, 7, 13, 14 };

    private readonly HashSet<string> deliveredIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<PhoneMessageData> inbox = new List<PhoneMessageData>();

    public IReadOnlyList<PhoneMessageData> Inbox => inbox;
    public int UnreadCount { get; private set; }

    public event Action<PhoneMessageData> OnMessageReceived;
    public event Action OnInboxChanged;

    private bool gameTimeHooked;

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
        TrySubscribeGameTime();
    }

    private void Update()
    {
        // GameTimeManager can spawn later than Bootstrap; subscribe once it exists so messages + badges stay in sync.
        TrySubscribeGameTime();
    }

    private void OnDisable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }

        gameTimeHooked = false;
    }

    private void Start()
    {
        TrySubscribeGameTime();
    }

    private void TrySubscribeGameTime()
    {
        if (gameTimeHooked)
        {
            return;
        }

        if (GameTimeManager.Instance == null)
        {
            return;
        }

        GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        GameTimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        gameTimeHooked = true;
        HandleTimeChanged();
    }

    private void HandleTimeChanged()
    {
        if (GameTimeManager.Instance == null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;
        int hr = GameTimeManager.Instance.Hour;

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg == null) continue;

            string id = msg.GetId();
            if (deliveredIds.Contains(id)) continue;
            if (!msg.IsAvailableNow(sem, day, hr)) continue;

            deliveredIds.Add(id);
            inbox.Add(msg);
            UnreadCount++;
            OnMessageReceived?.Invoke(msg);
            TryTeleportForMessage(msg);
            OnInboxChanged?.Invoke();
        }
    }

    /// <summary>
    /// Tin nhắn có thể gắn SpawnPoint.spawnId trong cùng scene (vd. nhảy vào Phòng CTSV trong 21_SchoolArea).
    /// </summary>
    private static void TryTeleportForMessage(PhoneMessageData msg)
    {
        if (msg == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.teleportSpawnPointId))
        {
            return;
        }

        string targetId = msg.teleportSpawnPointId.Trim();

        if (!string.IsNullOrWhiteSpace(msg.teleportOnlyIfSceneNamed))
        {
            string wantScene = msg.teleportOnlyIfSceneNamed.Trim();
            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.Equals(activeScene, wantScene, StringComparison.Ordinal))
            {
                return;
            }
        }

        SpawnPoint[] points = UnityEngine.Object.FindObjectsByType<SpawnPoint>(
            FindObjectsInactive.Exclude);

        SpawnPoint picked = null;
        for (int i = 0; i < points.Length; i++)
        {
            SpawnPoint p = points[i];
            if (p == null)
            {
                continue;
            }

            if (string.Equals(p.SpawnId, targetId, StringComparison.Ordinal))
            {
                picked = p;
                break;
            }
        }

        if (picked == null)
        {
            return;
        }

        if (PersistentPlayer.Instance != null)
        {
            PersistentPlayer.Instance.TeleportTo(picked.transform.position);
        }
    }

    public void MarkAllAsRead()
    {
        if (UnreadCount == 0) return;
        UnreadCount = 0;
        OnInboxChanged?.Invoke();
    }

    public ClassScheduleEntry GetTodaySchedule()
    {
        if (GameTimeManager.Instance == null) return null;
        return GetScheduleForDay(GameTimeManager.Instance.Semester, GameTimeManager.Instance.DayInSemester);
    }

    /// <summary>
    /// Lich cho mot ngay: uu tien muc trong list (dung semester + day), sau do template ngay thuong / cuoi tuan.
    /// Ngay nghi gia dinh trong ky 15 ngay: 6-7 va 13-14 (co the thay bang asset rieng trong list).
    /// </summary>
    public ClassScheduleEntry GetScheduleForDay(int semester, int dayInSemester)
    {
        for (int i = 0; i < schedule.Count; i++)
        {
            var s = schedule[i];
            if (s == null) continue;
            if (s.semester == semester && s.dayInSemester == dayInSemester) return s;
        }

        if (IsWeekendDay(dayInSemester))
        {
            return templateWeekend;
        }

        return templateWeekday;
    }

    private bool IsWeekendDay(int dayInSemester)
    {
        if (weekendDays != null && weekendDays.Length > 0)
        {
            for (int i = 0; i < weekendDays.Length; i++)
            {
                if (weekendDays[i] == dayInSemester) return true;
            }

            return false;
        }

        return (dayInSemester >= 6 && dayInSemester <= 7)
               || (dayInSemester >= 13 && dayInSemester <= 14);
    }

    public IReadOnlyList<ClassScheduleEntry> ScheduleEntries => schedule;

    public PhonePayload Persist_CapturePhone()
    {
        var payload = new PhonePayload();

        payload.deliveredMessageIdsOrdered = deliveredIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var inboxOrdered = new List<string>(inbox.Count);
        for (int i = 0; i < inbox.Count; i++)
        {
            if (inbox[i] != null)
            {
                inboxOrdered.Add(inbox[i].GetId());
            }
        }

        payload.inboxMessageIdsOrdered = inboxOrdered.ToArray();
        payload.unreadCount = UnreadCount;
        return payload;
    }

    public void Persist_ApplyPhone(PhonePayload p)
    {
        deliveredIds.Clear();
        inbox.Clear();
        UnreadCount = 0;

        if (p == null)
        {
            OnInboxChanged?.Invoke();
            return;
        }

        if (p.deliveredMessageIdsOrdered != null)
        {
            for (int i = 0; i < p.deliveredMessageIdsOrdered.Length; i++)
            {
                string id = p.deliveredMessageIdsOrdered[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    deliveredIds.Add(id.Trim());
                }
            }
        }

        if (p.inboxMessageIdsOrdered != null)
        {
            for (int i = 0; i < p.inboxMessageIdsOrdered.Length; i++)
            {
                PhoneMessageData msg = Persist_FindPhoneMessageById(p.inboxMessageIdsOrdered[i]);
                if (msg != null && !inbox.Contains(msg))
                {
                    inbox.Add(msg);
                }
            }
        }

        UnreadCount = inbox.Count > 0 ? Mathf.Clamp(p.unreadCount, 0, inbox.Count) : 0;
        OnInboxChanged?.Invoke();
    }

    PhoneMessageData Persist_FindPhoneMessageById(string needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return null;

        string id = needle.Trim();

        for (int i = 0; i < messages.Count; i++)
        {
            PhoneMessageData m = messages[i];
            if (m != null && m.GetId() == id)
            {
                return m;
            }
        }

        return null;
    }

    public void Persist_ResetPhoneForSave()
    {
        deliveredIds.Clear();
        inbox.Clear();
        UnreadCount = 0;
        OnInboxChanged?.Invoke();
    }
}
