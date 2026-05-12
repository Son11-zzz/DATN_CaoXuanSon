using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string streetSceneName = "22_PlayerHouse";
    [SerializeField] private string streetSpawnId = "Default";

    [Header("Load game (danh sách file save)")]
    [SerializeField] private MainMenuSaveListPanel loadSavePanel;

    private void Awake()
    {
        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();
        GameResumeContext.Clear();

        GameSaveService save = GameSaveService.ResolveOrCreate();
        save.QueueApplyAfterGameplaySceneLoad(null);
    }

    public void NewGame()
    {
        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();
        GameResumeContext.Clear();

        GameSaveService save = GameSaveService.ResolveOrCreate();
        save.PrepareFreshCampaign();

        if (SceneFlowController.Instance == null)
        {
            return;
        }

        SceneFlowController.Instance.LoadGameplayScene(streetSceneName, streetSpawnId);
    }

    public void ContinueGame()
    {
        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();

        if (!GameSaveIo.SaveExists())
        {
            Debug.LogWarning("MainMenuController: không có save — bắt đầu game mới.");
            NewGame();
            return;
        }

        GameSaveService save = GameSaveService.ResolveOrCreate();
        save.ContinueFromPersistedSave();
    }

    /// <summary>Gắn OnClick của nút « Load » / « Tiếp tục có chọn file » trong Main Menu.</summary>
    public void OpenLoadSavePanel()
    {
        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();

        if (loadSavePanel != null)
        {
            loadSavePanel.Show();
            return;
        }

        Debug.LogWarning("MainMenuController: assign loadSavePanel để hiện danh sách save.");
        ContinueGame();
    }

    public void Quit()
    {
        Application.Quit();
    }
}
