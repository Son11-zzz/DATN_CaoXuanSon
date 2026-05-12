using UnityEngine;

/// <summary>
/// Đặt trên một GameObject để khi tương tác, mở ChoiceEventData.
/// Có thể yêu cầu điều kiện ngày/semester.
/// </summary>
public class ChoiceEventInteractable : InteractableBase
{
    [Header("Choice Event")]
    [SerializeField] private ChoiceEventData choiceEvent;

    [Header("Gating")]
    [SerializeField] private bool requireDay;
    [Min(1)] [SerializeField] private int semester = 1;
    [Min(1)] [SerializeField] private int dayInSemester = 1;

    [SerializeField, TextArea] private string notAvailableText = "Chưa đến lúc.";
    [SerializeField, TextArea] private string alreadyDoneText = "Bạn đã quyết định rồi.";

    public override void Interact()
    {
        if (choiceEvent == null)
        {
            Debug.LogWarning($"ChoiceEventInteractable '{name}': choiceEvent not assigned.");
            return;
        }

        if (requireDay && GameTimeManager.Instance != null)
        {
            var tm = GameTimeManager.Instance;
            if (tm.Semester != semester || tm.DayInSemester != dayInSemester)
            {
                ShowInfo(notAvailableText);
                return;
            }
        }

        if (choiceEvent.onceOnly && ChoiceEventRuntime.HasFired(choiceEvent))
        {
            ShowInfo(alreadyDoneText);
            return;
        }

        ChoiceEventRuntime.Present(choiceEvent, null);
    }

    private static void ShowInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var ds = DialogueSystem.Instance;
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
            Object.Destroy(data);
            return true;
        });
    }

}
