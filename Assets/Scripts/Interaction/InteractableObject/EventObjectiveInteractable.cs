using UnityEngine;


public class EventObjectiveInteractable : InteractableBase
{
    [Header("Event Objective")]
    [SerializeField] private string objectiveId;

    [Header("Feedback")]
    [SerializeField, TextArea] private string notAvailableText = "Bạn không thể làm việc này ngay lúc này.";

    [Header("Requirements")]
    [SerializeField] private bool requireMinHour;
    [SerializeField] private int minHour = 13;
    [SerializeField, TextArea] private string tooEarlyText = "Chưa đến giờ.";
    [SerializeField] private string phaseAction;

    [Header("On Complete: Time Skip")]
    [SerializeField] private bool fadeAndSkipTimeOnComplete;
    [SerializeField] private int targetHour = 12;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("Optional")]
    [SerializeField] private DialogueData onCompleteDialogue;

    public override void Interact()
    {
        if (StoryEventManager.Instance == null)
        {
            Debug.LogWarning("EventObjectiveInteractable: StoryEventManager.Instance is null.");
            return;
        }

        string oid = string.IsNullOrWhiteSpace(objectiveId) ? objectiveId : objectiveId.Trim();
        if (string.IsNullOrWhiteSpace(oid))
        {
            Debug.LogWarning($"EventObjectiveInteractable '{name}': objectiveId is empty.");
            return;
        }

        if (requireMinHour && GameTimeManager.Instance != null && GameTimeManager.Instance.Hour < minHour)
        {
            ShowInfoDialogue(tooEarlyText);
            return;
        }

        bool completed = StoryEventManager.Instance.TryCompleteObjective(oid, out string reason);
        if (!completed)
        {
            Debug.Log($"EventObjectiveInteractable '{name}': cannot complete objective '{oid}'. Reason='{reason}' ActiveEvent='{StoryEventManager.Instance.ActiveEvent?.name}'.");
            ShowInfoDialogue(string.IsNullOrWhiteSpace(reason) ? notAvailableText : reason);
            return;
        }

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

        if (onCompleteDialogue != null)
        {
            var ds = ResolveDialogueSystem();
            if (ds != null && !ds.IsDialogueActive)
            {
                ds.StartDialogue(onCompleteDialogue, null);
            }
        }

        TriggerPhaseAction();
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

        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.lines = new System.Collections.Generic.List<DialogueLine>(1)
        {
            new DialogueLine
            {
                text = text,
                choices = new System.Collections.Generic.List<DialogueChoice>(1)
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

    private static ScreenFader ResolveScreenFader()
    {
        var faders = Object.FindObjectsByType<ScreenFader>(FindObjectsInactive.Include);
        return faders != null && faders.Length > 0 ? faders[0] : null;
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
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
}
