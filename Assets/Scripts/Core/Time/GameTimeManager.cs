using System;
using UnityEngine;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    [Header("Timeline")]
    [SerializeField] private int totalDaysPerSemester = 30;

    [Header("Auto Time")]
    [SerializeField] private bool autoTick = true;
    [SerializeField] private float secondsPerGameHour = 5f;
    private float tickTimer;

    [Header("Current")]
    [SerializeField] private int semester = 1;
    [SerializeField] private int dayInSemester = 1;
    [SerializeField] private int hour = 8;

    public int Semester => semester;
    public int DayInSemester => dayInSemester;
    public int Hour => hour;

    public event Action OnTimeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!autoTick) return;
        if (secondsPerGameHour <= 0f) return;

        tickTimer += Time.deltaTime;
        while (tickTimer >= secondsPerGameHour)
        {
            tickTimer -= secondsPerGameHour;
            AdvanceHours(1);
        }
    }

    public void SetTime(int newSemester, int newDayInSemester, int newHour)
    {
        semester = Mathf.Max(1, newSemester);
        dayInSemester = Mathf.Clamp(newDayInSemester, 1, Mathf.Max(1, totalDaysPerSemester));
        hour = Mathf.Clamp(newHour, 0, 23);
        OnTimeChanged?.Invoke();
    }

    public void AdvanceHours(int deltaHours)
    {
        if (deltaHours <= 0) return;

        int newHour = hour + deltaHours;
        int daysToAdd = newHour / 24;
        newHour = newHour % 24;

        int newDay = dayInSemester + daysToAdd;
        int newSemester = semester;

        while (newDay > totalDaysPerSemester)
        {
            newDay -= totalDaysPerSemester;
            newSemester++;
        }

        SetTime(newSemester, newDay, newHour);
    }

    // Event-day progression: nh?y ??n ?úng ngày s? ki?n ti?p theo
    public void JumpToEventDay(int targetSemester, int targetDayInSemester, int startHour = 8)
    {
        SetTime(targetSemester, targetDayInSemester, startHour);
    }
}
