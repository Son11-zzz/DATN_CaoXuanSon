using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(0)]
public class QuestGiverNPC : InteractableBase
{
    [Header("Quest")]
    [SerializeField] private QuestData quest;

    [Header("Identity")]
    [Tooltip("Phải khớp characterId trên NpcCharacterProfile; dùng cho QuestObjective TalkToNpc và StoryObjective.completeObjectiveWhenNpcFirstTalkId.")]
    [SerializeField] private string npcId;

    [Tooltip("Hien thi khi tuong tac, vi du: 'Chuyen tro voi Lan'")]
    [SerializeField] private string displayName;

    [Header("Dialogue")]
    [SerializeField] private DialogueData firstTimeDialogue;
    [SerializeField] private DialogueData inProgressDialogue;
    [SerializeField] private DialogueData turnInDialogue;
    [SerializeField] private DialogueData defaultDialogue;
    [SerializeField] private DialogueData preAcceptDialogue;

    [Header("Debug")]
    [SerializeField] private bool resetFirstTimeOnPlay;

    private bool awaitingFirstTimeCompletion;
    private bool subscribed;

    protected override void Awake()
    {
        base.Awake();
        RebuildFirstTimeKey();

        if (resetFirstTimeOnPlay)
            NpcFirstMeetDialogueState.DeleteFirstTimeKey(ResolvedNpcId());
    }

    /// <summary>Goi tu <see cref="NpcCharacter"/> (chay truoc Awake cua component nho DefaultExecutionOrder).</summary>
    public void ApplyCharacterProfile(NpcCharacterProfile profile)
    {
        if (profile == null) return;

        npcId = profile.characterId;
        displayName = profile.displayName;
        quest = profile.quest;
        firstTimeDialogue = profile.firstTimeDialogue;
        inProgressDialogue = profile.inProgressDialogue;
        turnInDialogue = profile.turnInDialogue;
        defaultDialogue = profile.defaultDialogue;
        preAcceptDialogue = profile.preAcceptDialogue;
        RebuildFirstTimeKey();
    }

    private void RebuildFirstTimeKey()
    {
        var id = string.IsNullOrWhiteSpace(npcId) ? gameObject.name.Trim() : npcId.Trim();
        if (!string.IsNullOrWhiteSpace(id))
            NpcFirstMeetDialogueState.EnsureMigratedPlayerPrefs(id);
    }

    private void OnEnable()
    {
        subscribed = false;
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!subscribed)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        var ds = ResolveDialogueSystem();
        if (ds != null)
        {
            ds.OnDialogueEnded -= HandleDialogueEnded;
            ds.OnDialogueEnded += HandleDialogueEnded;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        var ds = ResolveDialogueSystem();
        if (ds != null)
        {
            ds.OnDialogueEnded -= HandleDialogueEnded;
        }

        subscribed = false;
    }

    private void HandleDialogueEnded()
    {
        if (!awaitingFirstTimeCompletion) return;

        awaitingFirstTimeCompletion = false;
        MarkFirstTimeConsumed();
    }

    private string ResolvedNpcId()
    {
        return string.IsNullOrWhiteSpace(npcId) ? gameObject.name.Trim() : npcId.Trim();
    }

    public override void Interact()
    {
        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            Debug.LogWarning($"QuestGiverNPC '{name}': không tìm thấy DialogueSystem trong scene đang tải.");
            return;
        }

        string rid = ResolvedNpcId();
        NpcFirstMeetDialogueState.SyncPlayerPrefsAndQuestManager(rid);
        bool offerFirstTime = NpcFirstMeetDialogueState.ShouldOfferFirstTimeIntro(rid, firstTimeDialogue);

        // NPC không gắn quest: first-time xong → các lần sau chỉ defaultDialogue
        if (quest == null)
        {
            if (offerFirstTime && firstTimeDialogue != null)
            {
                ds.StartDialogue(firstTimeDialogue, null);
                awaitingFirstTimeCompletion = true;
                return;
            }

            if (defaultDialogue != null)
                ds.StartDialogue(defaultDialogue, null);

            return;
        }

        if (QuestManager.Instance == null) return;

        bool completed = QuestManager.Instance.IsCompleted(quest);
        bool accepted = QuestManager.Instance.IsAccepted(quest);
        bool ready = QuestManager.Instance.IsReadyToTurnIn(quest);

        // NPC có quest: defaultDialogue chỉ sau khi quest done; trong lúc làm quest chỉ first / in-progress / turn-in
        if (!completed && offerFirstTime && firstTimeDialogue != null)
        {
            ds.StartDialogue(firstTimeDialogue, null);
            awaitingFirstTimeCompletion = true;
            return;
        }

        if (!accepted)
        {
            var dlg = preAcceptDialogue != null ? preAcceptDialogue : defaultDialogue;
            if (dlg != null)
                ds.StartDialogue(dlg, null);

            return;
        }

        if (completed)
        {
            if (defaultDialogue != null)
                ds.StartDialogue(defaultDialogue, null);

            return;
        }

        if (accepted && ready)
        {
            if (turnInDialogue != null)
                ds.StartDialogue(turnInDialogue, null);

            return;
        }

        if (accepted)
        {
            if (inProgressDialogue != null)
                ds.StartDialogue(inProgressDialogue, null);
            else
                Debug.LogWarning($"QuestGiverNPC '{name}': quest đang làm nhưng thiếu InProgressDialogue (không dùng DefaultDialogue trước khi quest xong).");

            return;
        }
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }

    public override string GetInteractText()
    {
        bool hasDialogue = firstTimeDialogue != null || defaultDialogue != null || preAcceptDialogue != null || inProgressDialogue != null || turnInDialogue != null;
        if (quest == null && !hasDialogue) return string.Empty;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return $"Trò chuyện: {displayName}";
        }

        return "Nhấn E";
    }

    public void MarkQuestAccepted()
    {
        MarkFirstTimeConsumed();
    }

    private void MarkFirstTimeConsumed()
    {
        NpcFirstMeetDialogueState.MarkFirstTimeConsumed(ResolvedNpcId());
    }
}
