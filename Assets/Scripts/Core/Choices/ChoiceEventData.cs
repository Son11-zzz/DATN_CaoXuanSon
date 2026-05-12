using System.Collections.Generic;
using UnityEngine;

public enum ChoiceTone
{
    Neutral,
    Good,
    Bad
}

[System.Serializable]
public class ChoiceOption
{
    public string label = "Option";

    [TextArea] public string flavorText;

    public ChoiceTone tone = ChoiceTone.Neutral;

    [Tooltip("Diem bad/good flag (0 = khong ghi nhan).")]
    public int tonePoints = 1;

    [Header("Stat Delta")]
    public float gpaDelta;
    public float stressDelta;
    public float moneyDelta;
    public float healthDelta;
    public float energyDelta;
    public float socialDelta;
    public float skillDelta;

    [Header("Effects")]
    [Tooltip("Story objective se cho hoan thanh (neu thuoc active event).")]
    public string completeStoryObjectiveId;
    [Tooltip("Quest se duoc nhan khi chon.")]
    public QuestData acceptQuest;
    [Tooltip("Neu set khong rong, day la ID dung de danh dau da thuc hien choice.")]
    public string choiceFlagId;

    [Header("Ending")]
    public bool forceEnding;
    public EndingType forcedEnding = EndingType.None;
}

[CreateAssetMenu(fileName = "ChoiceEvent", menuName = "Scriptable Objects/ChoiceEvent")]
public class ChoiceEventData : ScriptableObject
{
    public string eventId;
    public string title = "Su kien";

    [TextArea(2, 6)] public string prompt;

    public List<ChoiceOption> options = new List<ChoiceOption>();

    [Tooltip("Chi cho phep kich hoat 1 lan.")]
    public bool onceOnly = true;
}
