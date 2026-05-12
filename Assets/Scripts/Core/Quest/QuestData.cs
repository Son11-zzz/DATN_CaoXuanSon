using System.Collections.Generic;
using UnityEngine;

public enum QuestObjectiveType
{
    CollectItem,
    ReachStatValue,
    TalkToNpc,
    CompleteLessonQuiz
}

[System.Serializable]
public class QuestObjective
{
    public QuestObjectiveType type;

    [Header("CollectItem")]
    public ItemData item;
    public int amount = 1;

    [Header("ReachStatValue")]
    public StatType stat;
    public float targetValue;

    [Header("TalkToNpc")]
    public string npcId;

    [Header("CompleteLessonQuiz")]
    public int lessonQuizSemester = 1;

    [Min(1)] public int lessonQuizCalendarDay = 1;

    public LessonQuizStudyContext lessonQuizContext = LessonQuizStudyContext.EveningHome;

    public bool IsValid()
    {
        if (type == QuestObjectiveType.CollectItem)
        {
            return item != null && amount > 0;
        }

        if (type == QuestObjectiveType.TalkToNpc)
        {
            return !string.IsNullOrWhiteSpace(npcId);
        }

        if (type == QuestObjectiveType.CompleteLessonQuiz)
        {
            return lessonQuizSemester >= 1 && lessonQuizCalendarDay >= 1;
        }

        return targetValue > 0;
    }
}

public enum StatType
{
    GPA,
    Stress,
    Money,
    Health,
    Energy,
    Social,
    Skill
}

[CreateAssetMenu(fileName = "QuestData", menuName = "Scriptable Objects/QuestData")]
public class QuestData : ScriptableObject
{
    public string questId;
    public string title;

    [TextArea]
    public string description;

    public List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("Rewards")]
    public float rewardMoney;
    public float rewardGpa;
    public float rewardStress;
    public float rewardHealth;
    public float rewardEnergy;
    public float rewardSocial;
    public float rewardSkill;

    [Header("Behavior")]
    public bool autoCompleteWhenReady;

    [Header("Unlock")]
    public List<QuestData> unlockQuests = new List<QuestData>();

    public string GetId()
    {
        if (!string.IsNullOrWhiteSpace(questId)) return questId;
        return name;
    }
}
