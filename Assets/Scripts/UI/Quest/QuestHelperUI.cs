using System.Text;
using TMPro;
using UnityEngine;

public class QuestHelperUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI questText;

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated += Refresh;
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
        }
    }

    public void Refresh()
    {
        if (questText == null) return;

        var qm = QuestManager.Instance;
        if (qm == null)
        {
            questText.text = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        var quests = qm.ActiveQuests;

        if (quests == null || quests.Count == 0)
        {
            sb.Append("No active quests");
            questText.text = sb.ToString();
            return;
        }

        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (q == null) continue;

            bool completed = qm.IsCompleted(q);
            sb.Append(completed ? "[DONE] " : "[TODO] ");
            sb.AppendLine(q.title);

            if (!string.IsNullOrWhiteSpace(q.description))
            {
                sb.AppendLine("- " + q.description);
            }

            if (q.objectives != null)
            {
                for (int j = 0; j < q.objectives.Count; j++)
                {
                    var o = q.objectives[j];
                    if (o == null) continue;

                    if (o.type == QuestObjectiveType.CollectItem)
                    {
                        int have = InventorySystem.Instance != null ? InventorySystem.Instance.GetAmount(o.item) : 0;
                        string itemName = o.item != null ? o.item.itemName : "(missing item)";
                        sb.AppendLine($"  * Collect {itemName}: {have}/{o.amount}");
                    }
                    else if (o.type == QuestObjectiveType.ReachStatValue)
                    {
                        sb.AppendLine($"  * Reach {o.stat}: {o.targetValue}");
                    }
                }
            }

            if (i < quests.Count - 1)
            {
                sb.AppendLine();
            }
        }

        questText.text = sb.ToString();
    }
}
