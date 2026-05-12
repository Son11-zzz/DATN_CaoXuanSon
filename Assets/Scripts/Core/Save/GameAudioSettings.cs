using UnityEngine;

/// <summary>Simple master volume persisted with <see cref="PlayerPrefs"/> (works without AudioMixer).</summary>
public static class GameAudioSettings
{
    const string PrefKeyVolume = "SVSim_MasterVolumeLinear";

    /// <summary>0..1 passed to AudioListener.volume.</summary>
    public static float MasterVolumeLinear
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyVolume, AudioListener.volume > 0f ? AudioListener.volume : 1f));
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyVolume, v);
            PlayerPrefs.Save();
            AudioListener.volume = v;
        }
    }

    public static float MasterVolumeDbApprox => LinearToDb(MasterVolumeLinear);

    /// <summary>Delta applied in coarse steps (+/- buttons).</summary>
    public static void AdjustMasterVolumeDelta(float delta01)
    {
        MasterVolumeLinear = MasterVolumeLinear + delta01;
    }

    public static void ApplyStoredVolumeToAudioListenerIfNeeded()
    {
        AudioListener.volume = MasterVolumeLinear;
    }

    static float LinearToDb(float lin)
    {
        if (lin <= 0f) return -80f;
        return 20f * Mathf.Log10(lin);
    }
}
