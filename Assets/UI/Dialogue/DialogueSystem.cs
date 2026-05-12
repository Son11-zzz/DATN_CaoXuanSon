using UnityEngine;
using System.Collections.Generic;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance;
    public bool IsDialogueActive { get; private set; }

    [Header("Node System")]
    [SerializeField] private DialogueNodeDatabase nodeDatabase;

    private DialogueData currentDialogue;
    private int currentIndex;
    bool hasChoice = false;
    private InteractableObject currentSource;

    public DialogueUI dialogueUI;

    public event System.Action OnDialogueEnded;

    private System.Func<DialogueChoice, bool> externalChoiceHandler;

    private void Awake()
    {
        Instance = this;
        IsDialogueActive = false;
    }

    public bool HasChoice()
    {
        return hasChoice;
    }

    public void StartDialogue(DialogueData data, InteractableObject source = null)
    {
        StartDialogue(data, source, null);
    }

    public void StartDialogue(DialogueData data, InteractableObject source, System.Func<DialogueChoice, bool> choiceHandler)
    {
        if (data == null)
        {
            Debug.LogWarning("DialogueSystem: StartDialogue called with null DialogueData.");
            EndDialogue();
            return;
        }

        if (data.lines == null || data.lines.Count == 0)
        {
            Debug.LogWarning($"DialogueSystem: DialogueData '{data.name}' has no lines.");
            EndDialogue();
            return;
        }

        if (dialogueUI == null)
        {
            Debug.LogWarning("DialogueSystem: dialogueUI reference is not assigned.");
            EndDialogue();
            return;
        }

        currentDialogue = data;
        currentIndex = 0;
        currentSource = source;

        externalChoiceHandler = choiceHandler;

        IsDialogueActive = true;
        ShowLine();
    }

    void ShowLine()
    {
        if (currentDialogue == null || currentDialogue.lines == null || currentDialogue.lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        if (currentIndex < 0 || currentIndex >= currentDialogue.lines.Count)
        {
            EndDialogue();
            return;
        }

        var line = currentDialogue.lines[currentIndex];

        hasChoice = line.choices != null && line.choices.Count > 0;

        if (dialogueUI == null)
        {
            EndDialogue();
            return;
        }

        dialogueUI.Show(line.text);

        if (hasChoice)
        {
            dialogueUI.ShowChoices(line.choices);
        }
    }

    public void Next()
    {
        currentIndex++;

        if (currentIndex >= currentDialogue.lines.Count)
        {
            EndDialogue();
            return;
        }

        ShowLine();
    }

    public void EndDialogue()
    {
        if (dialogueUI != null)
        {
            dialogueUI.Hide();
        }
        currentDialogue = null;
        IsDialogueActive = false;
        externalChoiceHandler = null;

        // KHÔNG tự động DisableAfterLeave ở đây nữa
        // để sau khi lấy chìa khóa vẫn còn tương tác (emptyDialogue)

        if (OnDialogueEnded != null)
        {
            OnDialogueEnded.Invoke();
        }
    }

    public void Choose(DialogueChoice choice)
    {
        if (externalChoiceHandler != null)
        {
            bool handled = false;
            try
            {
                handled = externalChoiceHandler.Invoke(choice);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

            if (handled)
            {
                EndDialogue();
                return;
            }
        }

        Debug.Log($"DialogueSystem.Choose: choiceText='{choice?.choiceText}', type={choice?.type}, nextNode='{choice?.nextNode}', quest='{choice?.quest?.GetId()}'");

        if (choice != null && choice.type == ChoiceType.GoToNode)
        {
            if (!string.IsNullOrWhiteSpace(choice.nextNode) && nodeDatabase != null)
            {
                var next = nodeDatabase.GetNode(choice.nextNode);
                if (next != null)
                {
                    StartDialogue(next, currentSource);
                    return;
                }

                Debug.LogWarning($"DialogueSystem: GoToNode nextNode '{choice.nextNode}' not found in database.");
            }

            EndDialogue();
            return;
        }

        // Quest actions (can optionally branch to success/fail nodes)
        if (choice != null && (choice.type == ChoiceType.AcceptQuest || choice.type == ChoiceType.TurnInQuest))
        {
            bool success = HandleQuestChoice(choice);
            Debug.Log($"DialogueSystem: Quest choice handled success={success} type={choice.type} quest='{choice.quest?.GetId()}' ready={QuestManager.Instance?.IsReadyToTurnIn(choice.quest)} completed={QuestManager.Instance?.IsCompleted(choice.quest)}");
            string nodeId = success ? choice.nextNodeOnSuccess : choice.nextNodeOnFail;

            if (!string.IsNullOrWhiteSpace(nodeId) && nodeDatabase != null)
            {
                var next = nodeDatabase.GetNode(nodeId);
                if (next != null)
                {
                    StartDialogue(next, currentSource);
                    return;
                }

                Debug.LogWarning($"DialogueSystem: node '{nodeId}' not found in database.");
            }

            EndDialogue();
            return;
        }

        if (choice != null && !string.IsNullOrWhiteSpace(choice.nextNode))
        {
            DialogueData next = null;

            if (nodeDatabase != null)
            {
                next = nodeDatabase.GetNode(choice.nextNode);
            }

            if (next != null)
            {
                StartDialogue(next, currentSource);
                return;
            }

            Debug.LogWarning($"DialogueSystem: nextNode '{choice.nextNode}' not found in database.");
        }

        // Nhánh lấy item
        if (choice.type == ChoiceType.PickupItem)
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.AddItem(choice.rewardItem);
            }

            ClearItemFromSource();

            // Sau khi xử lý xong, thường sẽ kết thúc hội thoại
            EndDialogue();
            return;
        }

        // Nếu bạn vẫn dùng ChoiceType.LeaveLocket để "không cho tương tác nữa"
        if (choice.type == ChoiceType.LeaveLocket)
        {
            if (currentSource != null)
            {
                currentSource.DisableAfterLeave();
            }

            EndDialogue();
            return;
        }

        if (choice.type == ChoiceType.End)
        {
            EndDialogue();
            return;
        }

        // Continue sang câu kế
        Next();
    }

    private bool HandleQuestChoice(DialogueChoice choice)
    {
        if (choice == null || choice.quest == null) return false;
        if (QuestManager.Instance == null) return false;

        if (choice.type == ChoiceType.AcceptQuest)
        {
            Debug.Log($"DialogueSystem: AcceptQuest quest='{choice.quest.GetId()}' alreadyAccepted={QuestManager.Instance.IsAccepted(choice.quest)}");
            if (!QuestManager.Instance.IsAccepted(choice.quest))
            {
                QuestManager.Instance.AcceptQuest(choice.quest);
            }

            if (currentSource != null)
            {
                var mono = currentSource as MonoBehaviour;
                if (mono != null)
                {
                    mono.SendMessage("MarkQuestAccepted", SendMessageOptions.DontRequireReceiver);
                }
            }

            Debug.Log($"DialogueSystem: AcceptQuest done quest='{choice.quest.GetId()}' nowAccepted={QuestManager.Instance.IsAccepted(choice.quest)}");
            return true;
        }

        // Turn-in: require objectives met, then complete (rewards + unlock)
        if (choice.type == ChoiceType.TurnInQuest)
        {
            Debug.Log($"DialogueSystem: TurnInQuest quest='{choice.quest.GetId()}' accepted={QuestManager.Instance.IsAccepted(choice.quest)} completed={QuestManager.Instance.IsCompleted(choice.quest)}");
            if (!QuestManager.Instance.IsAccepted(choice.quest))
            {
                return false;
            }

            // ensure up-to-date objective check
            QuestManager.Instance.Recalculate();
            if (!QuestManager.Instance.IsReadyToTurnIn(choice.quest))
            {
                Debug.Log($"DialogueSystem: TurnInQuest quest='{choice.quest.GetId()}' not ready to turn in.");
                return false;
            }

            QuestManager.Instance.CompleteQuest(choice.quest);
            Debug.Log($"DialogueSystem: TurnInQuest completed quest='{choice.quest.GetId()}' nowCompleted={QuestManager.Instance.IsCompleted(choice.quest)}");
            return true;
        }

        return false;
    }

    void ClearItemFromSource()
    {
        if (currentSource != null)
        {
            currentSource.hasItem = false;
        }
    }
}