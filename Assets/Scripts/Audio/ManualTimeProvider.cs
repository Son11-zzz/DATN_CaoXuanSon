using System;
using UnityEngine;

public class ManualTimeProvider : MonoBehaviour, IGameTimeProvider
{
    [SerializeField, Range(0, 23)] private int currentHour = 8;

    public int CurrentHour => currentHour;

    public event Action TimeChanged;

    public void SetHour(int hour)
    {
        int clamped = Mathf.Clamp(hour, 0, 23);
        if (currentHour == clamped) return;
        currentHour = clamped;
        TimeChanged?.Invoke();
    }

    private void OnValidate()
    {
        currentHour = Mathf.Clamp(currentHour, 0, 23);
    }
}
