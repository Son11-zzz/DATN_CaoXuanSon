using UnityEngine;

/// <summary>Tu dong nhan 2 quest (on nha + lop) theo Ngay hoc ky hien tai; can co quest asset trong QuestManager.allQuests.
/// </summary>
public class LessonStudyQuestAutoAssigner : MonoBehaviour
{
    [SerializeField] private int semesterFilter = 1;

    private bool timeSubscribed;

    private void OnEnable()
    {
        TrySubscribeTime();
        AssignForToday();
    }

    private void OnDisable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= AssignForToday;
        }

        timeSubscribed = false;
    }

    private void Update()
    {
        TrySubscribeTime();
    }

    private void TrySubscribeTime()
    {
        if (timeSubscribed || GameTimeManager.Instance == null) return;

        GameTimeManager.Instance.OnTimeChanged -= AssignForToday;
        GameTimeManager.Instance.OnTimeChanged += AssignForToday;
        timeSubscribed = true;
    }

    private void AssignForToday()
    {
        if (QuestManager.Instance == null || GameTimeManager.Instance == null) return;
        if (GameTimeManager.Instance.Semester != semesterFilter) return;

        int day = GameTimeManager.Instance.DayInSemester;
        string idEve = $"study_eve_sem1_{day:D2}";
        string idCls = $"study_cls_sem1_{day:D2}";

        AcceptIfExists(idEve);
        AcceptIfExists(idCls);
    }

    private static void AcceptIfExists(string stableId)
    {
        var q = QuestManager.Instance.Persist_FindQuestByStableId(stableId);
        if (q == null || QuestManager.Instance.IsAccepted(q)) return;
        QuestManager.Instance.AcceptQuest(q);
    }
}
