using System;
using UnityEngine;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    [Header("Timeline")]
    [SerializeField] private int totalDaysPerSemester = 15;

    public int TotalDaysPerSemester => totalDaysPerSemester;

    [Header("Auto Time")]
    [SerializeField] private bool autoTick = true;
    [SerializeField] private float secondsPerGameHour = 5f;
    private float tickTimer;

    public bool AutoTickEnabled => autoTick;

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

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }

    public void SetAutoTickEnabled(bool enabled)
    {
        autoTick = enabled;
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

    /// <summary>Seconds of real time that must elapse before the in-game clock advances one hour.</summary>
    public float SecondsPerGameHourPublic => Mathf.Max(0.01f, secondsPerGameHour);

    /// <summary>Accumulator used when <see cref="AutoTickEnabled"/> is true (seconds).</summary>
    public float TickTimerSeconds => tickTimer;

    public void SetSecondsPerGameHour(float value)
    {
        secondsPerGameHour = value > 0f ? value : 5f;
    }

    public void SetTickTimerSeconds(float seconds)
    {
        tickTimer = Mathf.Max(0f, seconds);
    }

    public void SetTime(int newSemester, int newDayInSemester, int newHour, bool invokeCallbacks = true)
    {
        semester = Mathf.Max(1, newSemester);
        dayInSemester = Mathf.Clamp(newDayInSemester, 1, Mathf.Max(1, totalDaysPerSemester));
        hour = Mathf.Clamp(newHour, 0, 23);
        if (invokeCallbacks)
        {
            OnTimeChanged?.Invoke();
        }
    }

    public void RestoreFromSave(TimePayload payload, bool invokeTimeCallbacksOnceAfter)
    {
        if (payload == null) return;

        autoTick = payload.autoTick;
        secondsPerGameHour = payload.secondsPerGameHour > 0f ? payload.secondsPerGameHour : 5f;
        tickTimer = Mathf.Max(0f, payload.tickTimer);
        semester = Mathf.Max(1, payload.semester);
        dayInSemester = Mathf.Clamp(payload.dayInSemester, 1, Mathf.Max(1, totalDaysPerSemester));
        hour = Mathf.Clamp(payload.hour, 0, 23);

        if (invokeTimeCallbacksOnceAfter)
        {
            RaiseTimeChangedOnce();
        }
    }

    public void RaiseTimeChangedOnce()
    {
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

    // Event-day progression: nh?y ??n ?�ng ng�y s? ki?n ti?p theo
    public void JumpToEventDay(int targetSemester, int targetDayInSemester, int startHour = 8)
    {
        SetTime(targetSemester, targetDayInSemester, startHour);
    }
}
