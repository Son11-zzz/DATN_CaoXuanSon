using UnityEngine;

[CreateAssetMenu(fileName = "ClassScheduleEntry", menuName = "Scriptable Objects/ClassScheduleEntry")]
public class ClassScheduleEntry : ScriptableObject
{
    [Min(1)] public int semester = 1;
    [Min(1)] public int dayInSemester = 1;

    [Tooltip("Tieu de lich (vd. 'Lap trinh C#').")]
    public string title;

    [TextArea]
    public string description;

    [Tooltip("Yeu cau chuan bi truoc khi vao lop (it dung cho UI).")]
    [TextArea] public string prepareHint;

    public int morningStartHour = 9;
    public int morningEndHour = 12;
    public int afternoonStartHour = 13;
    public int afternoonEndHour = 16;
}
