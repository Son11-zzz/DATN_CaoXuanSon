using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("Available Quests")]
    [SerializeField] private List<QuestData> allQuests = new List<QuestData>();

    [Header("Active")]
    [SerializeField] private List<QuestData> activeQuests = new List<QuestData>();

    [Header("Auto Assign")]
    [SerializeField] private bool autoAssignFirstDayQuest = true;
    [SerializeField] private QuestData firstDayQuest;

    [Header("Debug")]
    [SerializeField] private bool resetFirstDayOnPlay;

    private readonly Dictionary<string, QuestProgress> progress = new Dictionary<string, QuestProgress>();

    private readonly HashSet<string> readyToTurnIn = new HashSet<string>();
    private readonly HashSet<string> firstTimeNpcTalked = new HashSet<string>();

    private bool firstDayQuestChecked;
    private bool timeSubscribed;

    public event Action OnQuestUpdated;
    public event Action<QuestData> OnQuestCompleted;
    /// <summary>Fired once per NPC id when <see cref="RegisterFirstTimeDialogueCompleted"/> first records that id.</summary>
    public event Action<string> OnFirstTimeNpcTalked;

    private bool subscribed;

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

        if (resetFirstDayOnPlay)
        {
            ResetFirstDayAssignment();
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        TrySubscribeTime();
        TryAutoAssignFirstDayQuest();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeTime();
    }

    private void Update()
    {
        // In case singletons spawn after QuestManager
        if (!subscribed)
        {
            TrySubscribe();
        }

        if (!timeSubscribed)
        {
            TrySubscribeTime();
        }

        if (!firstDayQuestChecked)
        {
            TryAutoAssignFirstDayQuest();
        }
    }

    private void TrySubscribeTime()
    {
        if (timeSubscribed) return;

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= TryAutoAssignFirstDayQuest;
            GameTimeManager.Instance.OnTimeChanged += TryAutoAssignFirstDayQuest;
            timeSubscribed = true;
        }
    }

    private void UnsubscribeTime()
    {
        if (!timeSubscribed) return;

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= TryAutoAssignFirstDayQuest;
        }

        timeSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        bool hasInv = InventorySystem.Instance != null;
        bool hasEvt = EventManager.Instance != null;

        if (hasInv)
        {
            InventorySystem.Instance.OnInventoryChanged -= Recalculate;
            InventorySystem.Instance.OnInventoryChanged += Recalculate;
        }

        if (hasEvt)
        {
            EventManager.Instance.OnStatChanged -= Recalculate;
            EventManager.Instance.OnStatChanged += Recalculate;
        }

        subscribed = hasInv || hasEvt;
    }

    private void Unsubscribe()
    {
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Recalculate;
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnStatChanged -= Recalculate;
        }

        subscribed = false;
    }

    public IReadOnlyList<QuestData> ActiveQuests => activeQuests;

    public bool IsAccepted(QuestData quest)
    {
        if (quest == null) return false;
        return progress.TryGetValue(quest.GetId(), out var p) && p.accepted;
    }

    public bool IsCompleted(QuestData quest)
    {
        if (quest == null) return false;
        return progress.TryGetValue(quest.GetId(), out var p) && p.completed;
    }

    public bool IsReadyToTurnIn(QuestData quest)
    {
        if (quest == null) return false;
        return readyToTurnIn.Contains(quest.GetId());
    }

    public void AcceptQuest(QuestData quest)
    {
        if (quest == null) return;

        Debug.Log($"QuestManager: AcceptQuest quest='{quest.GetId()}'");

        var id = quest.GetId();
        if (!progress.TryGetValue(id, out var p))
        {
            p = new QuestProgress { questId = id, accepted = true, completed = false };
            progress[id] = p;
        }
        else
        {
            p.accepted = true;
        }

        if (!activeQuests.Contains(quest))
        {
            activeQuests.Add(quest);
        }

        // Do not immediately move quest state during accept; objectives will be evaluated by Recalculate
        // when inventory/stats change.
        OnQuestUpdated?.Invoke();
    }

    public bool HasTalkedToNpc(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return false;
        npcId = npcId.Trim();
        if (firstTimeNpcTalked.Contains(npcId)) return true;
        string alias = NpcFirstMeetDialogueState.LegacyTalkAliasFor(npcId);
        return !string.IsNullOrEmpty(alias) && firstTimeNpcTalked.Contains(alias);
    }

    public void RegisterFirstTimeDialogueCompleted(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return;

        npcId = npcId.Trim();
        bool anyNew = firstTimeNpcTalked.Add(npcId);
        string alias = NpcFirstMeetDialogueState.LegacyTalkAliasFor(npcId);
        if (!string.IsNullOrEmpty(alias))
            anyNew |= firstTimeNpcTalked.Add(alias);

        if (anyNew)
        {
            OnFirstTimeNpcTalked?.Invoke(npcId);
            Recalculate();
        }
    }

    public void CompleteQuest(QuestData quest)
    {
        if (quest == null) return;

        Debug.Log($"QuestManager: CompleteQuest quest='{quest.GetId()}'");

        var id = quest.GetId();
        if (!progress.TryGetValue(id, out var p))
        {
            p = new QuestProgress { questId = id, accepted = true, completed = false };
            progress[id] = p;
        }

        // Ensure the quest is marked accepted on turn-in, even if the entry already exists.
        // (If it was created on AcceptQuest this is already true.)
        p.accepted = true;

        // If it was already completed earlier, don't re-consume objectives or re-apply rewards.
        if (p.completed) return;

        // Consume required items on turn-in
        ConsumeObjectives(quest);

        p.completed = true;

        readyToTurnIn.Remove(id);

        ApplyRewards(quest);
        UnlockNext(quest);
        OnQuestCompleted?.Invoke(quest);

        OnQuestUpdated?.Invoke();

        // keep it in active list (UI can show completed) or remove if you prefer
    }

    public void RemoveCompletedQuests()
    {
        if (activeQuests == null || activeQuests.Count == 0) return;

        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            var quest = activeQuests[i];
            if (quest == null) continue;

            if (IsCompleted(quest))
            {
                activeQuests.RemoveAt(i);
                readyToTurnIn.Remove(quest.GetId());
            }
        }

        OnQuestUpdated?.Invoke();
    }

    private void ConsumeObjectives(QuestData quest)
    {
        if (quest == null || quest.objectives == null) return;
        if (InventorySystem.Instance == null) return;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null) continue;
            if (obj.type != QuestObjectiveType.CollectItem) continue;
            if (obj.item == null) continue;

            int need = Mathf.Max(1, obj.amount);
            bool removed = InventorySystem.Instance.RemoveItem(obj.item, need);
            Debug.Log($"QuestManager: Turn-in consume '{obj.item.itemName}' x{need} removed={removed}");
        }
    }

    private void ApplyRewards(QuestData quest)
    {
        var sm = StatManager.Instance;
        if (sm == null) return;

        sm.money += quest.rewardMoney;
        sm.gpa += quest.rewardGpa;
        sm.stress += quest.rewardStress;

        // HP chỉ được hồi qua đồ ăn/uống (consumable). Cho phép sát thương âm về lore nếu cần.
        if (quest.rewardHealth < 0f)
        {
            sm.health = Mathf.Max(0f, sm.health + quest.rewardHealth);
        }

        sm.energy += quest.rewardEnergy;

        // if these stats don't exist in your StatManager yet, keep rewards at 0 or add fields
        sm.social += quest.rewardSocial;
        sm.skill += quest.rewardSkill;

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    private void UnlockNext(QuestData quest)
    {
        if (quest.unlockQuests == null) return;

        foreach (var q in quest.unlockQuests)
        {
            if (q == null) continue;
            // Make it available but don't auto-accept
            if (allQuests != null && !allQuests.Contains(q))
            {
                allQuests.Add(q);
            }
        }
    }

    public void Recalculate()
    {
        // Mark quests as ready-to-turn-in when objectives are met.
        for (int i = 0; i < activeQuests.Count; i++)
        {
            var quest = activeQuests[i];
            if (quest == null) continue;

            if (IsCompleted(quest))
            {
                continue;
            }

            if (IsQuestObjectivesMet(quest))
            {
                if (quest.autoCompleteWhenReady && !IsCompleted(quest))
                {
                    CompleteQuest(quest);
                    continue;
                }

                readyToTurnIn.Add(quest.GetId());
            }
            else
            {
                readyToTurnIn.Remove(quest.GetId());
            }
        }

        OnQuestUpdated?.Invoke();
    }

    private bool IsQuestObjectivesMet(QuestData quest)
    {
        if (quest.objectives == null || quest.objectives.Count == 0) return true;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null || !obj.IsValid()) return false;

            if (obj.type == QuestObjectiveType.CollectItem)
            {
                if (InventorySystem.Instance == null) return false;
                int have = InventorySystem.Instance.GetAmount(obj.item);
                if (have < obj.amount)
                {
                    var objName = obj.item != null ? obj.item.itemName : "<null>";
                    Debug.Log($"QuestManager: Objective not met quest='{quest.GetId()}' CollectItem='{objName}' need={obj.amount} have={have} (ItemDataRef='{obj.item}')");
                    return false;
                }
            }
            else if (obj.type == QuestObjectiveType.ReachStatValue)
            {
                if (!IsStatReached(obj.stat, obj.targetValue)) return false;
            }
            else if (obj.type == QuestObjectiveType.TalkToNpc)
            {
                if (!HasTalkedToNpc(obj.npcId)) return false;
            }
            else if (obj.type == QuestObjectiveType.CompleteLessonQuiz)
            {
                if (LessonQuizManager.Instance == null) return false;
                if (!LessonQuizManager.Instance.WasDeskQuizCompleted(
                        obj.lessonQuizSemester,
                        obj.lessonQuizCalendarDay,
                        obj.lessonQuizContext))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void TryAutoAssignFirstDayQuest()
    {
        if (!autoAssignFirstDayQuest || firstDayQuest == null) return;

        var time = GameTimeManager.Instance;
        if (time == null) return;

        if (time.Semester != 1 || time.DayInSemester != 1) return;

        string key = $"QuestManager.FirstDayAssigned.{firstDayQuest.GetId()}";
        bool alreadyAssigned = PlayerPrefs.GetInt(key, 0) == 1;

        if (!alreadyAssigned || !IsAccepted(firstDayQuest))
        {
            AcceptQuest(firstDayQuest);
        }

        if (!alreadyAssigned)
        {
            PlayerPrefs.SetInt(key, 1);
        }

        firstDayQuestChecked = true;
    }

    private void ResetFirstDayAssignment()
    {
        if (firstDayQuest == null) return;

        string key = $"QuestManager.FirstDayAssigned.{firstDayQuest.GetId()}";
        PlayerPrefs.DeleteKey(key);
        firstDayQuestChecked = false;
    }

    private bool IsStatReached(StatType stat, float target)
    {
        var sm = StatManager.Instance;
        if (sm == null) return false;

        switch (stat)
        {
            case StatType.GPA:
                return sm.gpa >= target;
            case StatType.Stress:
                return sm.stress >= target;
            case StatType.Money:
                return sm.money >= target;
            case StatType.Health:
                return sm.health >= target;
            case StatType.Energy:
                return sm.energy >= target;
            case StatType.Social:
                return sm.social >= target;
            case StatType.Skill:
                return sm.skill >= target;
            default:
                return false;
        }
    }
}
