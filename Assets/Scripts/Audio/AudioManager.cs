using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Profiles")]
    [SerializeField] private AreaAudioProfile initialAreaProfile;

    [Header("Scene Profiles")]
    [SerializeField] private SceneAudioProfile[] sceneProfiles;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource uiSfxSource;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Bell SFX")]
    [SerializeField] private AudioClip classBellClip;
    [SerializeField] private int[] classBellHours = new[] { 9, 13 };
    [SerializeField] private string classBellSceneName = "21_SchoolArea";

    [Header("Time Provider (Optional)")]
    [SerializeField] private MonoBehaviour timeProviderComponent;

    private IGameTimeProvider timeProvider;
    private AreaAudioProfile currentAreaProfile;
    private TimeOfDayAudioProfile currentTimeProfile;
    private AudioSource activeMusicSource;
    private Coroutine crossfadeRoutine;
    private Coroutine ambientRoutine;
    private int lastKnownHour = -1;
    private int ambientLoopToken;
    private bool loggedMissingTimeProvider;
    private int lastBellHour = -1;

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

        EnsureSources();
        activeMusicSource = musicSourceA;
    }

    private void OnEnable()
    {
        ResolveTimeProvider();
        SubscribeTimeProvider();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        currentAreaProfile = initialAreaProfile;
        ApplySceneProfile(SceneManager.GetActiveScene().name, true);
        RefreshAudio();
    }

    private void OnDisable()
    {
        UnsubscribeTimeProvider();
        DisposeTimeProvider();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (timeProvider == null)
        {
            ResolveTimeProvider();
            SubscribeTimeProvider();
            if (timeProvider == null)
            {
                if (!loggedMissingTimeProvider)
                {
                    loggedMissingTimeProvider = true;
                }
                return;
            }
        }

        int hour = Mathf.Clamp(timeProvider.CurrentHour, 0, 23);
        if (hour != lastKnownHour)
        {
            lastKnownHour = hour;
            RefreshAudio();
            TryPlayClassBell(hour);
        }
    }

    public void SetCurrentArea(AreaAudioProfile profile)
    {
        if (profile == null)
        {
            Debug.LogWarning("AudioManager.SetCurrentArea nh?n profile null.");
            return;
        }

        if (currentAreaProfile == profile)
        {
            return;
        }

        currentAreaProfile = profile;
        RefreshAudio();
    }

    public void RefreshAudio()
    {
        if (currentAreaProfile == null)
        {
            Debug.LogWarning("AudioManager: Ch?a có AreaAudioProfile.");
            StopMusic();
            StopAmbient();
            return;
        }

        int hour = GetCurrentHour();
        lastKnownHour = hour;

        if (!currentAreaProfile.TryGetProfileForHour(hour, out TimeOfDayAudioProfile timeProfile))
        {
            Debug.LogWarning($"AudioManager: Không tìm th?y time profile cho gi? {hour:00} trong area '{currentAreaProfile.name}'.");
            StopMusic();
            StopAmbient();
            return;
        }

        currentTimeProfile = timeProfile;

        ApplyMusicProfile(timeProfile);
        ApplyAmbientProfile(timeProfile);
    }

    public void PlayUISound(AudioClip clip)
    {
        if (clip == null || uiSfxSource == null) return;
        uiSfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume));
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || uiSfxSource == null) return;
        uiSfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume));
    }

    public void StopMusic()
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        if (musicSourceA != null)
        {
            musicSourceA.Stop();
            musicSourceA.clip = null;
        }

        if (musicSourceB != null)
        {
            musicSourceB.Stop();
            musicSourceB.clip = null;
        }
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        RefreshAudio();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        if (uiSfxSource != null)
        {
            uiSfxSource.volume = sfxVolume;
        }

        ApplyAmbientVolume();
    }

    private void EnsureSources()
    {
        EnsureAudioSource(ref musicSourceA, "MusicSourceA");
        EnsureAudioSource(ref musicSourceB, "MusicSourceB");
        EnsureAudioSource(ref ambientSource, "AmbientSource");
        EnsureAudioSource(ref uiSfxSource, "UISfxSource");

        musicSourceA.loop = true;
        musicSourceB.loop = true;
        ambientSource.loop = true;
        uiSfxSource.loop = false;

        musicSourceA.playOnAwake = false;
        musicSourceB.playOnAwake = false;
        ambientSource.playOnAwake = false;
        uiSfxSource.playOnAwake = false;

        uiSfxSource.volume = Mathf.Clamp01(sfxVolume);
    }

    private void EnsureAudioSource(ref AudioSource source, string name)
    {
        if (source != null) return;
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        source = child.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
    }

    private int GetCurrentHour()
    {
        if (timeProvider != null)
        {
            return Mathf.Clamp(timeProvider.CurrentHour, 0, 23);
        }

        if (GameTimeManager.Instance != null)
        {
            return Mathf.Clamp(GameTimeManager.Instance.Hour, 0, 23);
        }

        if (StatManager.Instance != null)
        {
            return Mathf.Clamp(StatManager.Instance.time, 0, 23);
        }

        return 8;
    }

    private void ResolveTimeProvider()
    {
        if (timeProvider != null) return;

        if (timeProviderComponent != null)
        {
            timeProvider = timeProviderComponent as IGameTimeProvider;
            if (timeProvider == null)
            {
                Debug.LogWarning("AudioManager: Time Provider không implement IGameTimeProvider.");
            }
        }

        if (timeProvider == null && GameTimeManager.Instance != null)
        {
            timeProvider = new GameTimeManagerProvider(GameTimeManager.Instance);
        }

        if (timeProvider == null && StatManager.Instance != null)
        {
            timeProvider = new StatManagerProvider(StatManager.Instance);
        }

        if (timeProvider != null)
        {
            loggedMissingTimeProvider = false;
        }

        if (timeProvider == null)
        {
            if (!loggedMissingTimeProvider)
            {
                Debug.LogWarning("AudioManager: Không tìm th?y h? th?ng th?i gian. Hãy gán ManualTimeProvider ho?c IGameTimeProvider khác.");
                loggedMissingTimeProvider = true;
            }
        }
    }

    private void SubscribeTimeProvider()
    {
        if (timeProvider == null) return;
        timeProvider.TimeChanged += HandleTimeChanged;
    }

    private void UnsubscribeTimeProvider()
    {
        if (timeProvider == null) return;
        timeProvider.TimeChanged -= HandleTimeChanged;
    }

    private void DisposeTimeProvider()
    {
        if (timeProvider is GameTimeManagerProvider gameTimeProvider)
        {
            gameTimeProvider.Dispose();
        }

        timeProvider = null;
    }

    private void HandleTimeChanged()
    {
        RefreshAudio();
        TryPlayClassBell(GetCurrentHour());
    }

    private void TryPlayClassBell(int hour)
    {
        if (classBellClip == null || classBellHours == null || classBellHours.Length == 0) return;
        if (!IsBellSceneActive()) return;
        if (lastBellHour == hour) return;

        for (int i = 0; i < classBellHours.Length; i++)
        {
            if (classBellHours[i] != hour) continue;
            PlaySfx(classBellClip);
            lastBellHour = hour;
            return;
        }
    }

    private bool IsBellSceneActive()
    {
        if (string.IsNullOrWhiteSpace(classBellSceneName)) return true;
        Scene activeScene = SceneManager.GetActiveScene();
        return string.Equals(activeScene.name, classBellSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneProfile(scene.name, false);
    }

    private void ApplySceneProfile(string sceneName, bool allowFallback)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        if (sceneProfiles != null)
        {
            for (int i = 0; i < sceneProfiles.Length; i++)
            {
                SceneAudioProfile mapping = sceneProfiles[i];
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.sceneName)) continue;
                if (!string.Equals(mapping.sceneName, sceneName, StringComparison.OrdinalIgnoreCase)) continue;
                if (mapping.areaProfile == null)
                {
                    Debug.LogWarning($"AudioManager: Scene '{sceneName}' ch?a gán AreaAudioProfile.");
                    return;
                }

                SetCurrentArea(mapping.areaProfile);
                return;
            }
        }

        if (allowFallback && initialAreaProfile != null)
        {
            SetCurrentArea(initialAreaProfile);
            return;
        }

        if (allowFallback && TryGetAnyProfile(out AreaAudioProfile fallbackProfile))
        {
            SetCurrentArea(fallbackProfile);
            return;
        }

        Debug.LogWarning($"AudioManager: Không có c?u hình âm thanh cho scene '{sceneName}'.");
    }

    private bool TryGetAnyProfile(out AreaAudioProfile profile)
    {
        profile = null;
        if (sceneProfiles == null) return false;

        for (int i = 0; i < sceneProfiles.Length; i++)
        {
            SceneAudioProfile mapping = sceneProfiles[i];
            if (mapping == null || mapping.areaProfile == null) continue;
            profile = mapping.areaProfile;
            return true;
        }

        return false;
    }

    private void ApplyMusicProfile(TimeOfDayAudioProfile timeProfile)
    {
        if (timeProfile == null)
        {
            StopMusic();
            return;
        }

        AudioClip clip = timeProfile.musicClip;
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: Time profile '{timeProfile.name}' thi?u music clip.");
            StopMusic();
            return;
        }

        float targetVolume = Mathf.Clamp01(musicVolume) * Mathf.Clamp01(timeProfile.musicVolume);
        float fadeDuration = currentAreaProfile != null ? Mathf.Max(0f, currentAreaProfile.fadeDuration) : 0f;
        bool loop = currentAreaProfile == null || currentAreaProfile.loopMusic;

        if (activeMusicSource != null && activeMusicSource.clip == clip)
        {
            if (!activeMusicSource.isPlaying)
            {
                activeMusicSource.volume = 0f;
                activeMusicSource.loop = loop;
                activeMusicSource.Play();
                StartCrossfade(null, activeMusicSource, targetVolume, fadeDuration);
                return;
            }

            activeMusicSource.volume = targetVolume;
            activeMusicSource.loop = loop;
            return;
        }

        AudioSource nextSource = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
        if (nextSource == null)
        {
            nextSource = activeMusicSource;
        }

        nextSource.clip = clip;
        nextSource.loop = loop;
        nextSource.volume = 0f;
        nextSource.Play();

        StartCrossfade(activeMusicSource, nextSource, targetVolume, fadeDuration);
        activeMusicSource = nextSource;
    }

    private void StartCrossfade(AudioSource from, AudioSource to, float targetVolume, float duration)
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
        }

        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(from, to, targetVolume, duration));
    }

    private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float targetVolume, float duration)
    {
        float fromStart = from != null ? from.volume : 0f;
        float toStart = to != null ? to.volume : 0f;
        float time = 0f;

        if (duration <= 0f)
        {
            if (from != null)
            {
                from.volume = 0f;
                from.Stop();
            }

            if (to != null)
            {
                to.volume = targetVolume;
            }

            yield break;
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            if (from != null)
            {
                from.volume = Mathf.Lerp(fromStart, 0f, t);
            }

            if (to != null)
            {
                to.volume = Mathf.Lerp(toStart, targetVolume, t);
            }

            yield return null;
        }

        if (from != null)
        {
            from.volume = 0f;
            from.Stop();
        }

        if (to != null)
        {
            to.volume = targetVolume;
        }
    }

    private void ApplyAmbientProfile(TimeOfDayAudioProfile timeProfile)
    {
        if (ambientSource == null) return;

        AudioClip[] ambientClips = timeProfile.ambientClips;
        if (ambientClips == null || ambientClips.Length == 0)
        {
            StopAmbient();
            return;
        }

        if (currentAreaProfile != null && currentAreaProfile.useRandomAmbientClip && ambientClips.Length > 1)
        {
            StartAmbientRandomLoop(ambientClips, timeProfile.ambientVolume);
            return;
        }

        AudioClip clip = ambientClips[0];
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: Ambient clip b? thi?u trong profile '{timeProfile.name}'.");
            StopAmbient();
            return;
        }

        StopAmbientLoop();

        if (ambientSource.clip == clip && ambientSource.isPlaying)
        {
            ApplyAmbientVolume(timeProfile.ambientVolume);
            ambientSource.loop = true;
            return;
        }

        ambientSource.clip = clip;
        ambientSource.loop = true;
        ApplyAmbientVolume(timeProfile.ambientVolume);
        ambientSource.Play();
    }

    private void StartAmbientRandomLoop(AudioClip[] clips, float profileVolume)
    {
        StopAmbientLoop();
        ambientLoopToken++;
        ambientRoutine = StartCoroutine(AmbientRandomLoop(clips, profileVolume, ambientLoopToken));
    }

    private IEnumerator AmbientRandomLoop(AudioClip[] clips, float profileVolume, int token)
    {
        if (ambientSource == null || clips == null || clips.Length == 0)
        {
            yield break;
        }

        ambientSource.loop = false;

        while (token == ambientLoopToken)
        {
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null)
            {
                Debug.LogWarning("AudioManager: Ambient clip b? thi?u.");
                yield return null;
                continue;
            }

            ambientSource.clip = clip;
            ApplyAmbientVolume(profileVolume);
            ambientSource.Play();

            yield return new WaitForSeconds(clip.length);
        }
    }

    private void ApplyAmbientVolume(float profileVolume)
    {
        if (ambientSource == null) return;
        ambientSource.volume = Mathf.Clamp01(sfxVolume) * Mathf.Clamp01(profileVolume);
    }

    private void ApplyAmbientVolume()
    {
        if (currentTimeProfile == null) return;
        ApplyAmbientVolume(currentTimeProfile.ambientVolume);
    }

    private void StopAmbient()
    {
        StopAmbientLoop();
        if (ambientSource == null) return;
        ambientSource.Stop();
        ambientSource.clip = null;
    }

    private void StopAmbientLoop()
    {
        if (ambientRoutine != null)
        {
            StopCoroutine(ambientRoutine);
            ambientRoutine = null;
        }
    }

    private class GameTimeManagerProvider : IGameTimeProvider
    {
        private readonly GameTimeManager manager;
        public event Action TimeChanged;

        public GameTimeManagerProvider(GameTimeManager manager)
        {
            this.manager = manager;
            if (this.manager != null)
            {
                this.manager.OnTimeChanged += HandleTimeChanged;
            }
        }

        public int CurrentHour => manager != null ? manager.Hour : 0;

        private void HandleTimeChanged()
        {
            TimeChanged?.Invoke();
        }

        public void Dispose()
        {
            if (manager != null)
            {
                manager.OnTimeChanged -= HandleTimeChanged;
            }
        }
    }

    private class StatManagerProvider : IGameTimeProvider
    {
        private readonly StatManager manager;
        public event Action TimeChanged;

        public StatManagerProvider(StatManager manager)
        {
            this.manager = manager;
        }

        public int CurrentHour => manager != null ? manager.time : 0;
    }

    [Serializable]
    private class SceneAudioProfile
    {
        public string sceneName;
        public AreaAudioProfile areaProfile;
    }
}
