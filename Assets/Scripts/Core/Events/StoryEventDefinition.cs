using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryEvent", menuName = "Scriptable Objects/StoryEvent")]
public class StoryEventDefinition : ScriptableObject
{
    [Header("When")]
    [Min(1)] public int semester = 1;
    [Min(1)] public int dayInSemester = 1;

    [Tooltip("If several events use the same semester + day, the highest priority is chosen.")]
    public int schedulePriority;

    [Tooltip("Title shown in UI and letters. If empty, the asset name is used.")]
    public string eventDisplayName;

    [Header("Behavior")]
    public bool lockAutoTimeUntilComplete = true;
    public bool resumeAutoTimeWhenComplete = true;

    [Tooltip("If enabled, the bed interaction will be blocked until the event is completed.")]
    public bool blockSleepUntilComplete;

    [Tooltip("If enabled, ending the day (sleeping) while the event is incomplete will fail the event.")]
    public bool failEventIfDayEndsIncomplete = true;

    [Tooltip("If enabled, daily class attendance penalties are skipped for this day (e.g. weekends).")]
    public bool skipClassAttendanceToday;

    [Header("Late Penalty")]
    public bool applyLatePenalty;
    [Min(0)] public int lateHour = 7;
    [Tooltip("Only applies the penalty if this objective is not completed yet.")]
    public string lateObjectiveId;
    [Min(0f)] public float lateEnergyPenalty;
    [Min(0f)] public float lateStressPenalty;

    [Header("Late Dialogue")]
    public DialogueData lateDialogue;
    [TextArea] public string lateMessage = "Bạn đi học muộn.";
    public string lateOkText = "Được";

    [Header("Miss / Deadline")]
    [Tooltip("If enabled and the objective is not completed by missHour, the event fails (day can continue).")]
    public bool failIfMissed;
    [Min(0)] public int missHour = 12;
    [Tooltip("Fail only if this objective is not completed yet. If empty, fail regardless of objectives.")]
    public string missObjectiveId;
    [Min(0f)] public float missEnergyPenalty;
    [Min(0f)] public float missStressPenalty;
    [Min(0f)] public float missGpaPenalty;
    public DialogueData missedDialogue;

    [Header("Dialogue")]
    public DialogueData startDialogue;
    public DialogueData completedDialogue;

    [Header("Start Letter")]
    public bool showLetterOnStart;
    public string letterTitle;
    [TextArea] public string letterBody;
    public bool includeObjectivesInLetter = true;
    public string letterCloseText = "Được";

    [Header("Objectives")]
    public List<StoryObjective> objectives = new List<StoryObjective>();

    [Header("Quest integration")]
    [Tooltip("Quests accepted automatically when this story day begins (journal). Class study quests may also be auto-assigned by LessonStudyQuestAutoAssigner.")]
    public List<QuestData> autoAcceptQuestsOnStart = new List<QuestData>();

    [Tooltip("When a quest completes, optionally complete a story objective id (for checklist / story progress).")]
    public List<QuestStoryObjectiveLink> questCompletionToObjective = new List<QuestStoryObjectiveLink>();

    public bool Matches(int currentSemester, int currentDayInSemester)
    {
        return semester == currentSemester && dayInSemester == currentDayInSemester;
    }

    public string GetDisplayTitle()
    {
        return string.IsNullOrWhiteSpace(eventDisplayName) ? name : eventDisplayName.Trim();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        eventDisplayName = string.IsNullOrWhiteSpace(eventDisplayName) ? eventDisplayName : eventDisplayName.Trim();
        lateObjectiveId = string.IsNullOrWhiteSpace(lateObjectiveId) ? lateObjectiveId : lateObjectiveId.Trim();
        missObjectiveId = string.IsNullOrWhiteSpace(missObjectiveId) ? missObjectiveId : missObjectiveId.Trim();

        if (objectives != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < objectives.Count; i++)
            {
                var o = objectives[i];
                if (o == null) continue;

                if (!string.IsNullOrEmpty(o.id))
                {
                    o.id = o.id.Trim();
                    if (!seen.Add(o.id))
                    {
                        Debug.LogWarning($"StoryEventDefinition '{name}': duplicate objective id '{o.id}'.", this);
                    }
                }

                if (!string.IsNullOrEmpty(o.unlockAfterObjectiveId))
                {
                    o.unlockAfterObjectiveId = o.unlockAfterObjectiveId.Trim();
                }

                if (!string.IsNullOrEmpty(o.completeObjectiveWhenNpcFirstTalkId))
                {
                    o.completeObjectiveWhenNpcFirstTalkId = o.completeObjectiveWhenNpcFirstTalkId.Trim();
                }
            }
        }

        if (questCompletionToObjective != null)
        {
            foreach (var link in questCompletionToObjective)
            {
                if (link == null) continue;
                if (!string.IsNullOrEmpty(link.storyObjectiveId))
                {
                    link.storyObjectiveId = link.storyObjectiveId.Trim();
                }
            }
        }
    }
#endif
}

[Serializable]
public class QuestStoryObjectiveLink
{
    public QuestData quest;
    public string storyObjectiveId;
}

[Serializable]
public class StoryObjective
{
    public string id;
    [TextArea] public string description;

    [Tooltip("Objective will only be available after this objective id is completed (optional).")]
    public string unlockAfterObjectiveId;

    [Tooltip("If true, this objective is not required for the event to be considered complete. Players can still finish it for bonus rewards.")]
    public bool optional;

    [Tooltip("When this NPC id receives first-time talk registration (QuestManager), complete this objective if prerequisites allow.")]
    public string completeObjectiveWhenNpcFirstTalkId;
}
