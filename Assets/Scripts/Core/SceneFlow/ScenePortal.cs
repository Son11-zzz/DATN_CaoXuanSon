using UnityEngine;
using System.Collections.Generic;
using System.Text;


[RequireComponent(typeof(Collider2D))]
public class ScenePortal : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string destinationScene;
    [SerializeField] private string destinationSpawnId = "Default";

    [Header("Behavior")]
    [SerializeField] private bool requirePlayerTag = true;

    [Header("Requirements")]
    [SerializeField] private List<ItemData> requiredItems = new List<ItemData>();
    [SerializeField, TextArea] private string missingItemsText = "Bạn cần mua: {0}";
    [SerializeField] private string okText = "Được";
    [SerializeField] private bool completeObjectiveWhenRequirementsMet;
    [SerializeField] private string objectiveIdToComplete;
    [SerializeField] private bool showObjectiveCompletedDialogue;
    [SerializeField, TextArea] private string objectiveCompletedText = "Đã hoàn thành mục tiêu.";

    [Header("Confirm")]
    [SerializeField] private bool requireConfirm = true;
    [SerializeField, TextArea] private string confirmText = "Bạn có muốn di chuyển không?";
    [SerializeField] private string confirmYesText = "Có";
    [SerializeField] private string confirmNoText = "Không";

    [Header("Audio")]
    [SerializeField] private AudioClip portalSfx;

    [Header("Cooldown")]
    [SerializeField] private float portalCooldownSeconds = 1f;

    [Header("Story objective (optional)")]
    [Tooltip("Gọi lại TryComplete theo chu kỳ khi player đứng trong collider (tránh bỏ lỡ vì lúc Enter chưa xong bước trước, vd. GoHome).")]
    [SerializeField, Min(0.05f)] private float stayObjectiveRetryInterval = 0.4f;

    private float nextUseTime;
    private bool waitingForChoice;
    private float nextStayObjectiveTryTime;
    private bool waitingForExistingDialogueToConfirm;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requirePlayerTag && !other.CompareTag("Player")) return;
        if (SceneFlowController.Instance == null) return;
        if (waitingForChoice || waitingForExistingDialogueToConfirm) return;
        if (portalCooldownSeconds > 0f && Time.time < nextUseTime) return;

        if (!CheckItemRequirements(out string missingMessage))
        {
            ShowInfoDialogue(missingMessage);
            nextUseTime = Time.time + Mathf.Max(0f, portalCooldownSeconds);
            return;
        }

        bool completedObjectiveNow = TryCompleteObjectiveIfNeeded();
        ContinueAfterObjectiveCompletion(completedObjectiveNow);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (requirePlayerTag && !other.CompareTag("Player")) return;
        if (SceneFlowController.Instance == null) return;
        if (waitingForChoice || waitingForExistingDialogueToConfirm) return;
        if (Time.time < nextStayObjectiveTryTime) return;

        if (!CheckItemRequirements(out _)) return;

        if (!completeObjectiveWhenRequirementsMet || string.IsNullOrWhiteSpace(objectiveIdToComplete) || StoryEventManager.Instance == null)
        {
            return;
        }

        string oid = objectiveIdToComplete.Trim();
        if (StoryEventManager.Instance.IsObjectiveComplete(oid)) return;

        TryCompleteObjectiveIfNeeded();
        nextStayObjectiveTryTime = Time.time + Mathf.Max(0.05f, stayObjectiveRetryInterval);
    }

    private void UsePortal()
    {
        nextUseTime = Time.time + Mathf.Max(0f, portalCooldownSeconds);
        if (portalSfx != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(portalSfx);
        }
        SceneFlowController.Instance.LoadGameplayScene(destinationScene, destinationSpawnId);
    }

    private void ContinueAfterObjectiveCompletion(bool completedObjectiveNow)
    {
        var ds = ResolveDialogueSystem();

        if (completedObjectiveNow && ds != null && ds.IsDialogueActive)
        {
            waitingForExistingDialogueToConfirm = true;
            ds.OnDialogueEnded += HandleExistingDialogueEnded;
            return;
        }

        if (completedObjectiveNow && showObjectiveCompletedDialogue && ds != null && !ds.IsDialogueActive)
        {
            ShowObjectiveCompletedThenContinue(ds);
            return;
        }

        ShowConfirmOrUsePortal();
    }

    private void HandleExistingDialogueEnded()
    {
        var ds = ResolveDialogueSystem();
        if (ds != null)
        {
            ds.OnDialogueEnded -= HandleExistingDialogueEnded;
        }

        waitingForExistingDialogueToConfirm = false;
        ShowConfirmOrUsePortal();
    }

    private void ShowObjectiveCompletedThenContinue(DialogueSystem ds)
    {
        waitingForChoice = true;
        DialogueData completedData = CreateInfoDialogueData(
            string.IsNullOrWhiteSpace(objectiveCompletedText) ? "Da hoan thanh muc tieu." : objectiveCompletedText);

        ds.StartDialogue(completedData, null, _ =>
        {
            waitingForChoice = false;
            if (completedData != null)
            {
                Destroy(completedData);
            }

            ShowConfirmOrUsePortal();
            return true;
        });
    }

    private void ShowConfirmOrUsePortal()
    {
        if (!requireConfirm)
        {
            UsePortal();
            return;
        }

        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            Debug.LogWarning("ScenePortal: requireConfirm is enabled but DialogueSystem was not found. Add DialogueSystem (and its UI) to your gameplay UI scene.");
            return;
        }

        if (ds.IsDialogueActive)
        {
            waitingForExistingDialogueToConfirm = true;
            ds.OnDialogueEnded += HandleExistingDialogueEnded;
            return;
        }

        waitingForChoice = true;
        DialogueData confirmData = CreateConfirmDialogue();

        ds.StartDialogue(confirmData, null, choice =>
        {
            bool shouldTeleport = choice != null && choice.choiceText == confirmYesText;
            if (shouldTeleport)
            {
                UsePortal();
            }

            waitingForChoice = false;
            nextUseTime = Time.time + Mathf.Max(0f, portalCooldownSeconds);

            if (confirmData != null)
            {
                Destroy(confirmData);
            }

            return true;
        });
    }

    private bool TryCompleteObjectiveIfNeeded()
    {
        if (!completeObjectiveWhenRequirementsMet) return false;
        if (string.IsNullOrWhiteSpace(objectiveIdToComplete) || StoryEventManager.Instance == null) return false;

        return StoryEventManager.Instance.TryCompleteObjective(objectiveIdToComplete.Trim(), out _);
    }

    private bool CheckItemRequirements(out string message)
    {
        message = null;

        if (requiredItems == null || requiredItems.Count == 0) return true;
        if (InventorySystem.Instance == null)
        {
            message = "InventorySystem not found.";
            return false;
        }

        var missing = new List<string>();
        for (int i = 0; i < requiredItems.Count; i++)
        {
            var item = requiredItems[i];
            if (item == null) continue;

            if (InventorySystem.Instance.GetAmount(item) <= 0)
            {
                missing.Add(string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName);
            }
        }

        if (missing.Count == 0) return true;

        var sb = new StringBuilder();
        for (int i = 0; i < missing.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(missing[i]);
        }

        string listText = sb.ToString();
        message = string.IsNullOrWhiteSpace(missingItemsText) ? listText : string.Format(missingItemsText, listText);
        return false;
    }

    private void ShowInfoDialogue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var ds = ResolveDialogueSystem();
        if (ds == null || ds.IsDialogueActive) return;

        var data = CreateInfoDialogueData(text);

        ds.StartDialogue(data, null, _ =>
        {
            Destroy(data);
            return true;
        });
    }

    private DialogueData CreateInfoDialogueData(string text)
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

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }

    private DialogueData CreateConfirmDialogue()
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = confirmText,
                choices = new List<DialogueChoice>(2)
                {
                    new DialogueChoice { choiceText = confirmYesText, type = ChoiceType.End },
                    new DialogueChoice { choiceText = confirmNoText, type = ChoiceType.End }
                }
            }
        };

        return data;
    }
}
