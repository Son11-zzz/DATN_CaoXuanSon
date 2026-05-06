using UnityEngine;

public class QuestUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QuestPanelUI questPanel;

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private InventoryUI inventoryUI;

    private void Awake()
    {
        TryResolveQuestPanel();
        TryResolveInventoryUI();
    }

    private void OnEnable()
    {
        TryResolveQuestPanel();
        TryResolveInventoryUI();
    }

    private void Update()
    {
        TryResolveQuestPanel();
        TryResolveInventoryUI();

        if (questPanel == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            questPanel.Toggle();

            if (inventoryUI != null)
            {
                inventoryUI.HidePanel();
            }
        }
    }

    private void TryResolveQuestPanel()
    {
        if (questPanel != null) return;

        var panels = FindObjectsByType<QuestPanelUI>(FindObjectsInactive.Include);
        if (panels != null && panels.Length > 0)
        {
            questPanel = panels[0];
        }
    }

    private void TryResolveInventoryUI()
    {
        if (inventoryUI != null) return;
        inventoryUI = FindAnyObjectByType<InventoryUI>();
    }
}