using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiển thị một ChoiceEventData qua DialogueSystem và áp dụng kết quả.
/// </summary>
public static class ChoiceEventRuntime
{
    private static readonly HashSet<string> firedOnce = new HashSet<string>(StringComparer.Ordinal);

    public static bool HasFired(ChoiceEventData data)
    {
        if (data == null) return false;
        if (string.IsNullOrWhiteSpace(data.eventId)) return false;
        return firedOnce.Contains(data.eventId.Trim());
    }

    public static void Present(ChoiceEventData data, Action<ChoiceOption> onChosen = null)
    {
        if (data == null) return;

        if (data.onceOnly && !string.IsNullOrWhiteSpace(data.eventId) && firedOnce.Contains(data.eventId.Trim()))
        {
            onChosen?.Invoke(null);
            return;
        }

        var ds = DialogueSystem.Instance;
        if (ds == null)
        {
            var list = UnityEngine.Object.FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
            if (list != null && list.Length > 0) ds = list[0];
        }

        if (ds == null || ds.IsDialogueActive)
        {
            onChosen?.Invoke(null);
            return;
        }

        var runtime = ScriptableObject.CreateInstance<DialogueData>();
        runtime.lines = new List<DialogueLine>();

        string intro = string.IsNullOrWhiteSpace(data.title) ? data.prompt : $"[{data.title}]\n{data.prompt}";
        var choices = new List<DialogueChoice>(data.options.Count);
        foreach (var opt in data.options)
        {
            if (opt == null) continue;
            choices.Add(new DialogueChoice
            {
                choiceText = opt.label,
                type = ChoiceType.End
            });
        }

        runtime.lines.Add(new DialogueLine { text = intro, choices = choices });

        ds.StartDialogue(runtime, null, selectedChoice =>
        {
            ChoiceOption matched = null;
            if (selectedChoice != null)
            {
                for (int i = 0; i < data.options.Count; i++)
                {
                    var o = data.options[i];
                    if (o == null) continue;
                    if (o.label == selectedChoice.choiceText)
                    {
                        matched = o;
                        break;
                    }
                }
            }

            if (matched != null)
            {
                ApplyChoice(data, matched);
            }

            if (data.onceOnly && !string.IsNullOrWhiteSpace(data.eventId))
            {
                firedOnce.Add(data.eventId.Trim());
            }

            UnityEngine.Object.Destroy(runtime);
            onChosen?.Invoke(matched);
            return true;
        });
    }

    private static void ApplyChoice(ChoiceEventData data, ChoiceOption option)
    {
        if (option == null) return;

        var sm = StatManager.Instance;
        if (sm != null)
        {
            sm.gpa = Mathf.Clamp(sm.gpa + option.gpaDelta, 0f, 4.0f);
            sm.stress = Mathf.Max(0f, sm.stress + option.stressDelta);
            sm.money = Mathf.Max(0f, sm.money + option.moneyDelta);
            if (option.healthDelta < 0f)
            {
                sm.health = Mathf.Max(0f, sm.health + option.healthDelta);
            }

            sm.energy = Mathf.Max(0f, sm.energy + option.energyDelta);
            sm.social = Mathf.Max(0f, sm.social + option.socialDelta);
            sm.skill = Mathf.Max(0f, sm.skill + option.skillDelta);
        }

        // Choice tone is no longer used for semester ending evaluation.

        if (!string.IsNullOrWhiteSpace(option.completeStoryObjectiveId) && StoryEventManager.Instance != null)
        {
            StoryEventManager.Instance.TryCompleteObjective(option.completeStoryObjectiveId.Trim(), out _);
        }

        if (option.acceptQuest != null && QuestManager.Instance != null)
        {
            if (!QuestManager.Instance.IsAccepted(option.acceptQuest))
            {
                QuestManager.Instance.AcceptQuest(option.acceptQuest);
            }
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }

        if (option.forceEnding && option.forcedEnding != EndingType.None && EndingManager.Instance != null)
        {
            EndingManager.Instance.TriggerEnding(option.forcedEnding);
            return;
        }

        if (SemesterProgressManager.Instance != null)
        {
            SemesterProgressManager.Instance.CheckAndTriggerStressBreakdown();
        }
    }
}
