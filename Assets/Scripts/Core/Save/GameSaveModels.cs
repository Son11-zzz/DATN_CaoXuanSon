using System;
using UnityEngine;

/// <summary>JSON-friendly root object for persistent saves.</summary>
[Serializable]
public class GameSaveFile
{
    public int schemaVersion = 2;

    /// <summary>Thông tin hiển thị khi chọn slot (giờ thật / giờ game / tóm tắt tiến độ).</summary>
    public SaveSlotMeta meta;

    public string sceneName = string.Empty;

    /// <summary>If true, teleport uses saved world position rather than SpawnPoint ids.</summary>
    public bool useSavedWorldPosition = true;

    public SerVector3 playerPosition;

    /// <summary>Spawn id fallback when continuing without position data (legacy).</summary>
    public string spawnIdFallback = "Default";

    public TimePayload time;
    public StatPayload stats;
    public InventoryPayload[] inventory;
    public QuestPayload quest;
    public StoryPayload story;
    public PhonePayload phone;
    public LessonQuizPayload lessonQuiz;
    public SemesterProgressPayload semester;
    public EndingPayload ending;

    /// <summary>Linear master volume mirroring AudioListener (0–1).</summary>
    public float masterVolumeLinear = 1f;

    /// <summary>Once per campaign: gameplay guide letter was shown and closed.</summary>
    public bool gameplayGuideShown;
}

/// <summary>Metadata shown in Load list (stored inside each save JSON).</summary>
[Serializable]
public class SaveSlotMeta
{
    /// <summary>ISO 8601 (local).</summary>
    public string realWorldSavedAtIso = string.Empty;

    /// <summary>Ví dụ: 2026-05-03 14:30:00</summary>
    public string realWorldSavedAtDisplay = string.Empty;

    public string fileSafeNameHint = string.Empty;

    public string gameClockSummary = string.Empty;

    public string sceneNameDisplay = string.Empty;

    public string progressSummary = string.Empty;
}

[Serializable]
public struct SerVector3
{
    public float x;
    public float y;
    public float z;

    public static SerVector3 From(Vector3 v) => new SerVector3 { x = v.x, y = v.y, z = v.z };
    public readonly Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public class TimePayload
{
    public int semester = 1;
    public int dayInSemester = 1;
    public int hour = 8;
    public bool autoTick = true;
    public float secondsPerGameHour = 5f;
    public float tickTimer;
}

[Serializable]
public class StatPayload
{
    public int day = 1;
    public int time = 8;
    public float gpa;
    public float stress;
    public float money;
    public float health;
    public float energy;
    public float social;
    public float skill;
}

[Serializable]
public class InventoryPayload
{
    public string itemKey;
    public int amount;
}

[Serializable]
public class QuestProgressPayload
{
    public string questId;
    public bool accepted;
    public bool completed;
}

[Serializable]
public class QuestPayload
{
    public QuestProgressPayload[] progress;
    public string[] activeQuestIdsOrdered;
    public string[] readyToTurnInQuestIds;
    public string[] firstTimeNpcTalkedIds;
    public bool firstDayQuestPrefsAssigned;
}

[Serializable]
public class StoryPayload
{
    public string activeEventAssetName;
    public string[] completedObjectiveIds;

    public bool latePenaltyApplied;
    public bool missApplied;
    public bool lateDialoguePending;

    public bool startLetterShown;
    public bool startDialogueShown;

    public int classAttendanceSemester;
    public int classAttendanceDay;

    public bool morningClassAttended;
    public bool afternoonClassAttended;
    public bool morningMissPenaltyApplied;
    public bool afternoonMissPenaltyApplied;
    public bool morningStudyDone;
    public bool noStudyByNoonPenaltyApplied;
    public bool eveningStudyDone;
    public bool eveningStudyReminderShownTonight;
    public bool eveningStudyMissedReminderPending;

    public bool morningClassSoonReminderShown;
    public bool morningClassDueReminderShown;
    public bool afternoonClassSoonReminderShown;
    public bool afternoonClassDueReminderShown;

    public bool morningPostClassTeleportApplied;
    public bool afternoonPostClassTeleportApplied;
    public bool morningMissMoneyFineApplied;
    public bool day3TuitionFeePaid;

    public int summaryTrackedSemester;
    public int summaryTrackedDay;
    public int eventsCompletedToday;
    public int questsCompletedToday;

    public bool dailySummaryPending;
    public string dailySummaryText;

    public int suppressSummaryTransitionSemester;
    public int suppressSummaryTransitionDay;

    public string[] triggeredEventAssetNames;
    public int lastTrackedSemester = -1;
    public int lastTrackedDay = -1;
}

[Serializable]
public class PhonePayload
{
    public string[] deliveredMessageIdsOrdered;
    public string[] inboxMessageIdsOrdered;
    public int unreadCount;
}

[Serializable]
public class LessonQuizPayload
{
    public string[] completedEntryIdsOrdered;
}

[Serializable]
public class SemesterProgressPayload
{
    public int semesterTracked = -1;
    public int morningsAttended;
    public int afternoonsAttended;
    public int morningsMissed;
    public int afternoonsMissed;
    public int lateCount;
    public int studyAtDeskCount;

    public float quizBestScore;
    public float midtermScore;
    public float finalScore;
    public int badHabitPoints;
    public int goodChoicePoints;

    public string[] recordedChoiceIdsOrdered;
}

[Serializable]
public class EndingPayload
{
    public bool hasEnded;
    public int endingTypeOrdinal;
}
