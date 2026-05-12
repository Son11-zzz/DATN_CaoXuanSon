using UnityEngine;
using UnityEngine.UI;

/// <summary>In-game ESC pause sheet: Resume, Save, Exit, Settings (volume).</summary>
public class PauseMenuController : MonoBehaviour
{
    /// <summary>True while the modal pause UI is blocking simulation time.</summary>
    public static bool IsPaused { get; private set; }

    public static bool BlocksPlayerMovement => IsPaused;

    [Header("UI Roots")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject settingsPanel;

    [Header("Feedback (optional)")]
    [SerializeField] private GameObject savedFeedbackBadge;

    [Header("Audio Widgets")]
    [SerializeField] private Slider volumeSlider;

    [Header("Backdrop (fix click-through / blocking)")]
    [Tooltip(
        "Nếu ô này trống và Pause Root có parent mang Image full màn trong suốt, script sẽ tự tắt Raycast Target khi không pause để không chặn Letter/Dialogue.")]
    [SerializeField] private Graphic optionalFullscreenBackdropGraphic;

    float _storedTimeScale = 1f;

    /// <summary>Transparent Images with Raycast Target steal all clicks from UI underneath.</summary>
    void ResolveBackdropGraphic(out Graphic blocker)
    {
        blocker = optionalFullscreenBackdropGraphic;
        if (blocker != null)
        {
            return;
        }

        if (pauseRoot == null)
        {
            return;
        }

        Transform host = pauseRoot.transform.parent;
        if (host != null)
        {
            blocker = host.GetComponent<Graphic>();
        }
    }

    void ApplyBackdropBlockingPointerEvents(bool paused)
    {
        ResolveBackdropGraphic(out Graphic backdrop);
        if (backdrop != null)
        {
            backdrop.raycastTarget = paused;
        }
    }

    void Awake()
    {
        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (savedFeedbackBadge != null)
        {
            savedFeedbackBadge.SetActive(false);
        }

        HydrateSliderFromStoredVolume();
        ApplyBackdropBlockingPointerEvents(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        var shop = ShopUI.ResolveInstance();
        if (shop != null && shop.IsOpen)
        {
            shop.Hide();
            ShopUI.NotifyEscapeConsumedForShopClose();
            return;
        }

        if (ShopUI.WasEscapeConsumedClosingShopOnThisFrame())
        {
            return;
        }

        if (ShouldIgnorePauseEscape())
        {
            return;
        }

        TogglePause();
    }

    /// <summary>Chờ xử lý shop ở trên — chỉ bootstrap/menu chặn pause.</summary>
    static bool ShouldIgnorePauseEscape()
    {
        if (SceneFlowController.Instance == null)
        {
            return false;
        }

        return SceneFlowController.Instance.IsMenuOrBootstrapActive();
    }

    void HydrateSliderFromStoredVolume()
    {
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(GameAudioSettings.MasterVolumeLinear);
            volumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
            volumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        }
    }

    public void OpenPauseMenu()
    {
        if (IsPaused)
        {
            return;
        }

        PauseInternal();
    }

    /// <summary>
    /// Gán cho nút HUD &quot;Thoát&quot; khi muốn mở Pause (Resume / Lưu / Thoát game) thay vì về menu ngay.
    /// </summary>
    public void OnHudExitOpensPauseMenuClicked()
    {
        OpenPauseMenu();
    }

    /// <summary>Tìm PauseMenuController trong scene UI (kể cả inactive) và mở pause.</summary>
    public static void TryOpenPauseMenuFromHud()
    {
        var c = UnityEngine.Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        if (c == null)
        {
            Debug.LogWarning(
                "PauseMenuController: không có trong scene — thêm component vào scene 70_UIGamePlay, gán pauseRoot, rồi gán nút Thoát gọi OnHudExitOpensPauseMenuClicked hoặc TryOpenPauseMenuFromHud.");
            return;
        }

        c.OpenPauseMenu();
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            CloseSettingsPanelInternal();
            ResumeInternal();
        }
        else
        {
            PauseInternal();
        }
    }

    public void OnResumeClicked()
    {
        CloseSettingsPanelInternal();
        ResumeInternal();
    }

    void PauseInternal()
    {
        if (pauseRoot == null)
        {
            Debug.LogWarning("PauseMenuController: assign pauseRoot in Inspector.");
            return;
        }

        pauseRoot.SetActive(true);
        _storedTimeScale = Time.timeScale <= 0.0001f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;

        ApplyBackdropBlockingPointerEvents(true);

        HydrateSliderFromStoredVolume();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ResumeInternal()
    {
        if (!IsPaused && pauseRoot != null && pauseRoot.activeSelf == false)
        {
            return;
        }

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }

        CloseSettingsPanelInternal();

        Time.timeScale = Mathf.Approximately(_storedTimeScale, 0f) ? 1f : Mathf.Max(_storedTimeScale, 0.0001f);
        IsPaused = false;
        ApplyBackdropBlockingPointerEvents(false);
        StopSavedFeedbackTween();
    }

    public void OnSaveClicked()
    {
        GameSaveService save = GameSaveService.ResolveOrCreate();
        bool ok = save.CaptureActiveSessionToDisk(out string path);

        if (ok && !string.IsNullOrEmpty(path))
        {
            Debug.Log($"Game đã được lưu (file): {path}");
        }
        else
        {
            Debug.LogWarning("Lưu thất bại — xem Console.");
        }

        if (ok && savedFeedbackBadge != null)
        {
            savedFeedbackBadge.SetActive(true);
            Invoke(nameof(HideSavedFeedback), 2f);
        }
    }

    void HideSavedFeedback()
    {
        if (savedFeedbackBadge != null)
        {
            savedFeedbackBadge.SetActive(false);
        }
    }

    void StopSavedFeedbackTween()
    {
        CancelInvoke(nameof(HideSavedFeedback));
        HideSavedFeedback();
    }

    public void OnExitToMainMenuClicked()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        ApplyBackdropBlockingPointerEvents(false);
        GameResumeContext.Clear();
        GameSaveService.ResolveOrCreate().QueueApplyAfterGameplaySceneLoad(null);

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.LoadMainMenu();
        }
    }

    public void OnOpenSettingsClicked()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }

        HydrateSliderFromStoredVolume();
    }

    public void OnCloseSettingsClicked()
    {
        CloseSettingsPanelInternal();
    }

    void CloseSettingsPanelInternal()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    void OnMasterVolumeSliderChanged(float value)
    {
        GameAudioSettings.MasterVolumeLinear = value;
    }

    /// <summary>Dùng cho Button OnClick (không tham số — tránh Unity truyền 0 vào float).</summary>
    public void OnVolumeUpClicked()
    {
        AdjustVolumeStep(0.05f);
    }

    /// <summary>Dùng cho Button OnClick.</summary>
    public void OnVolumeDownClicked()
    {
        AdjustVolumeStep(-0.05f);
    }

    void AdjustVolumeStep(float delta01)
    {
        GameAudioSettings.AdjustMasterVolumeDelta(delta01);
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(GameAudioSettings.MasterVolumeLinear);
        }
    }

    public void OnVolumeIncreaseClicked(float step01 = 0.05f)
    {
        AdjustVolumeStep(step01);
    }

    public void OnVolumeDecreaseClicked(float step01 = 0.05f)
    {
        AdjustVolumeStep(-step01);
    }

    void OnDestroy()
    {
        ApplyBackdropBlockingPointerEvents(false);

        if (pauseRoot != null && pauseRoot.activeSelf)
        {
            Time.timeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            IsPaused = false;
        }
    }
}
