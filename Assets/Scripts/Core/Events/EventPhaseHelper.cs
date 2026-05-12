using UnityEngine;

public static class EventPhaseHelper
{
    public static void ToNoon()
    {
        SetHour(12);
    }

    public static void ToAfternoon()
    {
        SetHour(13);
    }

    public static void ToEvening()
    {
        SetHour(19); // 🔥 critical for lighting
    }

    public static void ToNight()
    {
        SetHour(22);
    }

    private static void SetHour(int hour)
    {
        if (GameTimeManager.Instance == null) return;

        int sem = GameTimeManager.Instance.Semester;
        int day = GameTimeManager.Instance.DayInSemester;

        GameTimeManager.Instance.SetTime(sem, day, hour);
    }
}   