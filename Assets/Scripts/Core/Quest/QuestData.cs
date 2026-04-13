using System.Collections.Generic;
using UnityEngine;

public enum QuestObjectiveType
{
    CollectItem,
    ReachStatValue
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

    public bool IsValid()
    {
        if (type == QuestObjectiveType.CollectItem)
        {
            return item != null && amount > 0;
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

    [Header("Unlock")]
    public List<QuestData> unlockQuests = new List<QuestData>();

    public string GetId()
    {
        if (!string.IsNullOrWhiteSpace(questId)) return questId;
        return name;
    }
}
