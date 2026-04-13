using UnityEngine;

public class QuestUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QuestPanelUI questPanel;

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private void Awake()
    {
        if (questPanel == null)
        {
            questPanel = FindFirstObjectByType<QuestPanelUI>();
        }
    }

    private void Update()
    {
        if (questPanel == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            questPanel.Toggle();
        }
    }

    public void ShowQuestPanel()
    {
        if (questPanel == null) return;
        questPanel.Show();
    }

    public void HideQuestPanel()
    {
        if (questPanel == null) return;
        questPanel.Hide();
    }

    public void ToggleQuestPanel()
    {
        if (questPanel == null) return;
        questPanel.Toggle();
    }
}
