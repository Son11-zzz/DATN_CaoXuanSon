using UnityEngine;

/// <summary>
/// NPC chỉ hội thoại (không có <see cref="QuestGiverNPC"/>).
/// First-time + default dùng chung <see cref="NpcFirstMeetDialogueState"/> với QuestGiver (không quest).
/// </summary>
public class NPC : InteractableBase
{
    [Tooltip("Hội thoại thường (sau khi đã làm quen lần đầu).")]
    public DialogueData dialogueData;

    [SerializeField] private DialogueData firstTimeDialogue;

    [SerializeField] private string bindingNpcId;

    private bool awaitingFirstTimeCompletion;
    private bool subscribed;

    /// <summary>Gọi từ <see cref="NpcCharacter"/> khi không có QuestGiver trên cùng GameObject.</summary>
    public void ApplyWorldProfileDialogues(string characterId, DialogueData firstMeet, DialogueData defaultDlg)
    {
        bindingNpcId = characterId != null ? characterId.Trim() : string.Empty;
        firstTimeDialogue = firstMeet;
        if (defaultDlg != null)
            dialogueData = defaultDlg;

        if (!string.IsNullOrWhiteSpace(bindingNpcId))
            NpcFirstMeetDialogueState.EnsureMigratedPlayerPrefs(bindingNpcId);
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
            TrySubscribe();
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
            ds.OnDialogueEnded -= HandleDialogueEnded;

        subscribed = false;
    }

    private void HandleDialogueEnded()
    {
        if (!awaitingFirstTimeCompletion) return;

        awaitingFirstTimeCompletion = false;
        if (!string.IsNullOrWhiteSpace(bindingNpcId))
            NpcFirstMeetDialogueState.MarkFirstTimeConsumed(bindingNpcId);
    }

    public override void Interact()
    {
        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            Debug.LogWarning($"NPC '{name}': DialogueSystem not found in loaded scenes.");
            return;
        }

        if (string.IsNullOrWhiteSpace(bindingNpcId))
        {
            if (dialogueData == null)
            {
                Debug.LogWarning($"NPC '{name}': dialogueData is null.");
                return;
            }

            ds.StartDialogue(dialogueData, null);
            return;
        }

        NpcFirstMeetDialogueState.SyncPlayerPrefsAndQuestManager(bindingNpcId);
        bool offerFirst = NpcFirstMeetDialogueState.ShouldOfferFirstTimeIntro(bindingNpcId, firstTimeDialogue);

        if (offerFirst && firstTimeDialogue != null)
        {
            ds.StartDialogue(firstTimeDialogue, null);
            awaitingFirstTimeCompletion = true;
            return;
        }

        if (dialogueData == null)
        {
            Debug.LogWarning($"NPC '{name}': dialogueData is null.");
            return;
        }

        ds.StartDialogue(dialogueData, null);
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }
}
