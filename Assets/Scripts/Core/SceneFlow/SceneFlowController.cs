using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowController : MonoBehaviour
{
    public static SceneFlowController Instance;

    [Header("Scenes")]
    [SerializeField] private string bootstrapSceneName = "00_Bootstrap";
    [SerializeField] private string mainMenuSceneName = "10_MainMenu";
    [SerializeField] private string gameplayUISceneName = "70_UIGamePlay";
    [SerializeField] private bool loadGameplayUIAdditive = true;

    [Header("Debug")]
    [SerializeField] private bool logFlow = true;

    public string BootstrapSceneName => bootstrapSceneName;
    public string MainMenuSceneName => mainMenuSceneName;

    private string pendingSpawnId = "Default";
    private bool isLoading;

    /// <summary>True when the active scene is the flow entry (bootstrap) or main menu — no gameplay HUD/phone chrome should show.</summary>
    public bool IsMenuOrBootstrapActive()
    {
        string n = SceneManager.GetActiveScene().name;
        return n == mainMenuSceneName || n == bootstrapSceneName;
    }

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

    private void Start()
    {
        if (logFlow)
        {
            Debug.Log($"SceneFlowController: start scene='{SceneManager.GetActiveScene().name}' bootstrap='{bootstrapSceneName}' menu='{mainMenuSceneName}' ui='{gameplayUISceneName}'");
        }

        if (SceneManager.GetActiveScene().name == bootstrapSceneName)
        {
            LoadMainMenu();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void LoadMainMenu()
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(mainMenuSceneName, "FromGame"));
    }

    public void LoadGameplayScene(string sceneName, string spawnId)
    {
        if (isLoading) return;
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        StartCoroutine(LoadSceneRoutine(sceneName, spawnId));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, string spawnId)
    {
        isLoading = true;
        pendingSpawnId = string.IsNullOrWhiteSpace(spawnId) ? "Default" : spawnId;

        ScreenFader fader = FindAnyObjectByType<ScreenFader>();
        if (fader != null)
        {
            fader.Fade(0.2f, 0.05f, 0.2f);
            yield return new WaitForSeconds(0.25f);
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        if (loadGameplayUIAdditive && !string.IsNullOrWhiteSpace(gameplayUISceneName) && sceneName != mainMenuSceneName)
        {
            var uiScene = SceneManager.GetSceneByName(gameplayUISceneName);
            if (!uiScene.IsValid() || !uiScene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(gameplayUISceneName, LoadSceneMode.Additive);
            }
        }

        isLoading = false;

        if (!string.Equals(sceneName, mainMenuSceneName, System.StringComparison.Ordinal))
        {
            GameplayGuideLetter guide = GameplayGuideLetter.Instance;
            if (guide == null)
            {
                guide = FindAnyObjectByType<GameplayGuideLetter>();
            }

            guide?.ScheduleDeferredShowAfterGameplayLoad();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplayUISceneName) return;

        StartCoroutine(ApplySpawnNextFrame());
    }

    private IEnumerator ApplySpawnNextFrame()
    {
        yield return null;

        GameSaveService saveSvc = GameSaveService.Instance != null ? GameSaveService.Instance : GameSaveService.ResolveOrCreate();
        saveSvc.TryConsumePendingAfterSceneApplied(out _);

        var player = PersistentPlayer.Instance;
        if (player == null)
        {
            Debug.LogWarning("SceneFlowController: PersistentPlayer.Instance is null. Place Player in 00_Bootstrap and add PersistentPlayer.");
            yield break;
        }

        bool isMenu = SceneManager.GetActiveScene().name == mainMenuSceneName;
        player.gameObject.SetActive(!isMenu);
        if (isMenu) yield break;

        Vector3 spawnPos = player.transform.position;
        bool hasResumeTeleport = GameResumeContext.TryConsumePendingTeleport(out Vector3 resumePos);

        if (!hasResumeTeleport)
        {
            SpawnPoint[] points = FindObjectsByType<SpawnPoint>();
            SpawnPoint chosen = null;
            SpawnPoint fallbackDefault = null;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;

                if (points[i].IsDefault)
                {
                    fallbackDefault = points[i];
                }

                if (!string.IsNullOrWhiteSpace(pendingSpawnId) && points[i].SpawnId == pendingSpawnId)
                {
                    chosen = points[i];
                    break;
                }
            }

            if (chosen == null)
            {
                chosen = fallbackDefault;
            }

            if (chosen != null)
            {
                spawnPos = chosen.transform.position;
            }
        }
        else
        {
            spawnPos = resumePos;
        }

        player.TeleportTo(spawnPos, resetVelocity: true);

        CameraFollow camFollow = null;
        if (Camera.main != null)
        {
            camFollow = Camera.main.GetComponent<CameraFollow>();
        }

        if (camFollow != null)
        {
            camFollow.isSnapping = true;
        }
    }
}