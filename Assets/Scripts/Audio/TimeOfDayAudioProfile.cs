using UnityEngine;

[CreateAssetMenu(menuName = "SVSimulator/Audio/Time Of Day Audio Profile", fileName = "TimeOfDayAudioProfile")]
public class TimeOfDayAudioProfile : ScriptableObject
{
    public string timeRangeName = "Morning";
    [Range(0, 23)] public int startHour = 6;
    [Range(0, 24)] public int endHourExclusive = 12;

    [Header("Clips")]
    public AudioClip musicClip;
    public AudioClip[] ambientClips;

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 1f;

    public bool ContainsHour(int hour)
    {
        int h = Mathf.Clamp(hour, 0, 23);
        int start = Mathf.Clamp(startHour, 0, 23);
        int end = Mathf.Clamp(endHourExclusive, 0, 24);

        if (start == end)
        {
            return true;
        }

        if (start < end)
        {
            return h >= start && h < end;
        }

        return h >= start || h < end;
    }

    private void OnValidate()
    {
        startHour = Mathf.Clamp(startHour, 0, 23);
        endHourExclusive = Mathf.Clamp(endHourExclusive, 0, 24);
        musicVolume = Mathf.Clamp01(musicVolume);
        ambientVolume = Mathf.Clamp01(ambientVolume);
    }
}
