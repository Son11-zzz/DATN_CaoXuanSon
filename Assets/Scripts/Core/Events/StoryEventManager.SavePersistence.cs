using System;
using System.Collections.Generic;
using System.Linq;

public partial class StoryEventManager
{
    /// <summary>Snapshot persisted fields for saves (runtime ScriptableInstances are intentionally dropped).</summary>
    public StoryPayload CaptureStoryPayload()
    {
        var p = new StoryPayload();

        if (ActiveEvent != null)
        {
            p.activeEventAssetName = ActiveEvent.name;
        }

        p.completedObjectiveIds = SafeOrdered(completedObjectiveIds);
        p.latePenaltyApplied = latePenaltyApplied;
        p.missApplied = missApplied;
        p.lateDialoguePending = lateDialoguePending;

        p.startLetterShown = startLetterShown;
        p.startDialogueShown = startDialogueShown;

        p.classAttendanceSemester = classAttendanceSemester;
        p.classAttendanceDay = classAttendanceDay;
        p.morningClassAttended = morningClassAttended;
        p.afternoonClassAttended = afternoonClassAttended;
        p.morningMissPenaltyApplied = morningMissPenaltyApplied;
        p.afternoonMissPenaltyApplied = afternoonMissPenaltyApplied;
        p.morningStudyDone = morningStudyDone;
        p.noStudyByNoonPenaltyApplied = noStudyByNoonPenaltyApplied;
        p.eveningStudyDone = eveningStudyDone;
        p.eveningStudyReminderShownTonight = eveningStudyReminderShownTonight;
        p.eveningStudyMissedReminderPending = eveningStudyMissedReminderPending;

        p.morningClassSoonReminderShown = morningClassSoonReminderShown;
        p.morningClassDueReminderShown = morningClassDueReminderShown;
        p.afternoonClassSoonReminderShown = afternoonClassSoonReminderShown;
        p.afternoonClassDueReminderShown = afternoonClassDueReminderShown;

        p.morningPostClassTeleportApplied = morningPostClassTeleportApplied;
        p.afternoonPostClassTeleportApplied = afternoonPostClassTeleportApplied;
        p.morningMissMoneyFineApplied = morningMissMoneyFineApplied;
        p.day3TuitionFeePaid = day3TuitionFeePaid;

        p.summaryTrackedSemester = summaryTrackedSemester;
        p.summaryTrackedDay = summaryTrackedDay;
        p.eventsCompletedToday = eventsCompletedToday;
        p.questsCompletedToday = questsCompletedToday;

        p.dailySummaryPending = dailySummaryPending;
        p.dailySummaryText = dailySummaryText;

        p.suppressSummaryTransitionSemester = suppressSummaryTransitionSemester;
        p.suppressSummaryTransitionDay = suppressSummaryTransitionDay;

        p.triggeredEventAssetNames = triggeredEventsToday
            .Where(e => e != null)
            .Select(e => e.name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        p.lastTrackedSemester = lastTrackedSemester;
        p.lastTrackedDay = lastTrackedDay;

        return p;
    }

    public void Persist_ApplyStoryPayload(StoryPayload p)
    {
        if (p == null)
        {
            ResetRuntimeStoryStateForSave();
            return;
        }

        StopDailySummaryAutoCloseRoutine();

        PrepareDay3TuitionForLoadedSave();

        Persist_SetActiveEvent(ResolveStoryAsset(p.activeEventAssetName));

        completedObjectiveIds.Clear();
        if (p.completedObjectiveIds != null)
        {
            for (int i = 0; i < p.completedObjectiveIds.Length; i++)
            {
                string id = p.completedObjectiveIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                completedObjectiveIds.Add(id.Trim());
            }
        }

        latePenaltyApplied = p.latePenaltyApplied;
        missApplied = p.missApplied;
        lateDialoguePending = p.lateDialoguePending;

        startLetterShown = p.startLetterShown;
        startDialogueShown = p.startDialogueShown;

        classAttendanceSemester = p.classAttendanceSemester;
        classAttendanceDay = p.classAttendanceDay;
        morningClassAttended = p.morningClassAttended;
        afternoonClassAttended = p.afternoonClassAttended;
        morningMissPenaltyApplied = p.morningMissPenaltyApplied;
        afternoonMissPenaltyApplied = p.afternoonMissPenaltyApplied;
        morningStudyDone = p.morningStudyDone;
        noStudyByNoonPenaltyApplied = p.noStudyByNoonPenaltyApplied;
        eveningStudyDone = p.eveningStudyDone;
        eveningStudyReminderShownTonight = p.eveningStudyReminderShownTonight;
        eveningStudyMissedReminderPending = p.eveningStudyMissedReminderPending;

        morningClassSoonReminderShown = p.morningClassSoonReminderShown;
        morningClassDueReminderShown = p.morningClassDueReminderShown;
        afternoonClassSoonReminderShown = p.afternoonClassSoonReminderShown;
        afternoonClassDueReminderShown = p.afternoonClassDueReminderShown;

        morningPostClassTeleportApplied = p.morningPostClassTeleportApplied;
        afternoonPostClassTeleportApplied = p.afternoonPostClassTeleportApplied;
        morningMissMoneyFineApplied = p.morningMissMoneyFineApplied;
        day3TuitionFeePaid = p.day3TuitionFeePaid;

        summaryTrackedSemester = p.summaryTrackedSemester;
        summaryTrackedDay = p.summaryTrackedDay;
        eventsCompletedToday = p.eventsCompletedToday;
        questsCompletedToday = p.questsCompletedToday;

        dailySummaryPending = p.dailySummaryPending;
        dailySummaryText = p.dailySummaryText;

        suppressSummaryTransitionSemester = p.suppressSummaryTransitionSemester;
        suppressSummaryTransitionDay = p.suppressSummaryTransitionDay;

        CleanupLateRuntimeDialogue();
        CleanupClassReminderRuntimeDialogue();
        CleanupEveningStudyReminderRuntimeDialogue();
        CleanupDailySummaryRuntimeDialogue();
        pendingFailedEventDialogue = null;

        triggeredEventsToday.Clear();
        if (p.triggeredEventAssetNames != null)
        {
            for (int i = 0; i < p.triggeredEventAssetNames.Length; i++)
            {
                var ev = ResolveStoryAsset(p.triggeredEventAssetNames[i]);
                if (ev != null)
                {
                    triggeredEventsToday.Add(ev);
                }
            }
        }

        lastTrackedSemester = p.lastTrackedSemester;
        lastTrackedDay = p.lastTrackedDay;

        OnProgressChanged?.Invoke();
    }

    /// <summary>Clears story runtime so New Game behaves like first boot for event/daily trackers.</summary>
    public void ResetRuntimeStoryStateForSave()
    {
        StopDailySummaryAutoCloseRoutine();
        CleanupLateRuntimeDialogue();
        CleanupClassReminderRuntimeDialogue();
        CleanupEveningStudyReminderRuntimeDialogue();
        CleanupDailySummaryRuntimeDialogue();

        HardResetDay3TuitionForNewGame();

        Persist_SetActiveEvent(null);
        completedObjectiveIds.Clear();
        latePenaltyApplied = false;
        missApplied = false;
        lateDialoguePending = false;
        lateDialogueSource = null;
        startLetterShown = false;
        startDialogueShown = false;

        classAttendanceSemester = -1;
        classAttendanceDay = -1;
        morningClassAttended = false;
        afternoonClassAttended = false;
        morningMissPenaltyApplied = false;
        afternoonMissPenaltyApplied = false;
        morningStudyDone = false;
        noStudyByNoonPenaltyApplied = false;
        eveningStudyDone = false;
        eveningStudyReminderShownTonight = false;
        eveningStudyMissedReminderPending = false;
        morningClassSoonReminderShown = false;
        morningClassDueReminderShown = false;
        afternoonClassSoonReminderShown = false;
        afternoonClassDueReminderShown = false;
        morningPostClassTeleportApplied = false;
        afternoonPostClassTeleportApplied = false;
        morningMissMoneyFineApplied = false;
        day3TuitionFeePaid = false;

        summaryTrackedSemester = -1;
        summaryTrackedDay = -1;
        eventsCompletedToday = 0;
        questsCompletedToday = 0;
        dailySummaryPending = false;
        dailySummaryText = null;
        suppressSummaryTransitionSemester = -1;
        suppressSummaryTransitionDay = -1;
        triggeredEventsToday.Clear();
        pendingFailedEventDialogue = null;

        lastTrackedSemester = -1;
        lastTrackedDay = -1;

        if (GameTimeManager.Instance != null)
        {
            InitializeDailyTracking();
            EnsureDailyClassAttendanceState();

            summaryTrackedSemester = GameTimeManager.Instance.Semester;
            summaryTrackedDay = GameTimeManager.Instance.DayInSemester;

            lastTrackedSemester = GameTimeManager.Instance.Semester;
            lastTrackedDay = GameTimeManager.Instance.DayInSemester;
            classAttendanceSemester = GameTimeManager.Instance.Semester;
            classAttendanceDay = GameTimeManager.Instance.DayInSemester;
        }

        OnProgressChanged?.Invoke();
    }

    private void Persist_SetActiveEvent(StoryEventDefinition ev)
    {
        ActiveEvent = ev;
    }

    private StoryEventDefinition ResolveStoryAsset(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName)) return null;

        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e != null && e.name == assetName)
            {
                return e;
            }
        }

        return null;
    }

    static string[] SafeOrdered(HashSet<string> set)
    {
        if (set == null || set.Count == 0) return Array.Empty<string>();
        return set
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
    }
}
