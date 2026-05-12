using System.Collections.Generic;
using UnityEngine;

public enum LessonQuizKind
{
    ClassLesson,
    SmallQuiz,
    Midterm,
    Final
}

[System.Serializable]
public class LessonQuizQuestion
{
    [TextArea(1, 3)] public string prompt;
    public List<string> options = new List<string>();

    [Min(0)]
    [Tooltip("Chi so dap an dung trong options (0-based).")]
    public int correctIndex;

    [TextArea] public string explanation;
}

[CreateAssetMenu(fileName = "LessonQuiz", menuName = "Scriptable Objects/LessonQuiz")]
public class LessonQuizData : ScriptableObject
{
    public LessonQuizKind kind = LessonQuizKind.ClassLesson;
    public string title = "Bài học";
    [TextArea] public string intro;

    public List<LessonQuizQuestion> questions = new List<LessonQuizQuestion>();

    [Header("Rewards")]
    [Tooltip("GPA tang neu % dung >= this.")]
    [Range(0f, 1f)] public float passScore = 0.6f;

    [Tooltip("GPA cong khi dat pass score (per quiz).")]
    public float gpaOnPass = 0.15f;

    [Tooltip("GPA cong theo ti le % dung (neu dat pass).")]
    public float gpaPerCorrectPercent = 0.1f;

    [Tooltip("Skill tang khi dat pass score.")]
    public float skillOnPass = 5f;

    [Tooltip("Stress cong neu khong pass.")]
    public float stressOnFail = 5f;

    [Tooltip("Stress tang moi cau sai.")]
    public float stressPerWrong = 1f;

    [Header("Morning recap (lop + noi dung da on nua dem truoc)")]
    [Tooltip("Tru GPA cho moi cau tra loi sai (chi ap dung khi hoc buoi sang tai ban lop).")]
    public float morningRecapGpaPenaltyPerWrong = 0.08f;

    [Header("Time")]
    [Min(0)] public int hoursToAdvance = 1;
}
