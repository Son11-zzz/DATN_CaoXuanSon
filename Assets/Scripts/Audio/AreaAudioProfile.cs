using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SVSimulator/Audio/Area Audio Profile", fileName = "AreaAudioProfile")]
public class AreaAudioProfile : ScriptableObject
{
    public string areaName = "Dormitory";
    public TimeOfDayAudioProfile[] timeProfiles;
    [Min(0f)] public float fadeDuration = 1.5f;
    public bool loopMusic = true;
    public bool useRandomAmbientClip = true;

    public bool TryGetProfileForHour(int hour, out TimeOfDayAudioProfile profile)
    {
        profile = null;
        if (timeProfiles == null || timeProfiles.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < timeProfiles.Length; i++)
        {
            TimeOfDayAudioProfile candidate = timeProfiles[i];
            if (candidate == null) continue;
            if (candidate.ContainsHour(hour))
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }

    public List<string> GetValidationWarnings()
    {
        List<string> warnings = new List<string>();
        if (timeProfiles == null || timeProfiles.Length == 0)
        {
            warnings.Add("Không có TimeOfDayAudioProfile nào trong area.");
            return warnings;
        }

        int[] coverage = new int[24];
        for (int i = 0; i < timeProfiles.Length; i++)
        {
            TimeOfDayAudioProfile profile = timeProfiles[i];
            if (profile == null)
            {
                warnings.Add($"Time profile index {i} b? thi?u.");
                continue;
            }

            for (int h = 0; h < 24; h++)
            {
                if (profile.ContainsHour(h))
                {
                    coverage[h] += 1;
                }
            }

            if (profile.startHour == profile.endHourExclusive)
            {
                warnings.Add($"{profile.name}: startHour trùng endHourExclusive (?ang bao ph? toàn b? ngày)." );
            }
        }

        List<int> missingHours = new List<int>();
        for (int h = 0; h < 24; h++)
        {
            if (coverage[h] > 1)
            {
                warnings.Add($"Ch?ng l?n time profile t?i gi? {h:00}.");
            }
            else if (coverage[h] == 0)
            {
                missingHours.Add(h);
            }
        }

        if (missingHours.Count > 0)
        {
            warnings.Add($"Thi?u time profile cho các gi?: {string.Join(", ", missingHours)}.");
        }

        return warnings;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);

        List<string> warnings = GetValidationWarnings();
        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning($"AreaAudioProfile '{name}': {warnings[i]}", this);
        }
    }
}
