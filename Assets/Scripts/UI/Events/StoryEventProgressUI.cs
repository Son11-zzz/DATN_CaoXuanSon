using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryEventProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI objectivesText;
    [SerializeField] private GameObject root;

    [Header("Integration")]
    [Tooltip("Nếu false, HUD sự kiện tắt: mục tiêu chỉ hiện trong Điện thoại (tab Mục tiêu). Bật true để dùng lại bảng giấy.")]
    [SerializeField] private bool showDedicatedEventHud;

    private void OnEnable()
    {
        if (!showDedicatedEventHud)
        {
            gameObject.SetActive(false);
            return;
        }

        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // In case this UI is enabled before StoryEventManager exists.
        Subscribe();
    }

    private void Subscribe()
    {
        if (StoryEventManager.Instance == null) return;

        StoryEventManager.Instance.OnProgressChanged -= Refresh;
        StoryEventManager.Instance.OnProgressChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (StoryEventManager.Instance == null) return;

        StoryEventManager.Instance.OnProgressChanged -= Refresh;
    }

    private void Refresh()
    {
        var mgr = StoryEventManager.Instance;
        if (mgr == null || mgr.ActiveEvent == null)
        {
            SetActive(false);
            return;
        }

        SetActive(true);

        var ev = mgr.ActiveEvent;

        if (titleText != null)
        {
            titleText.text = $"Sự kiện: {ev.GetDisplayTitle()}";
        }

        if (objectivesText != null)
        {
            objectivesText.text = BuildObjectivesText(ev, mgr);
            objectivesText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(objectivesText.rectTransform);
        }
    }

    private string BuildObjectivesText(StoryEventDefinition ev, StoryEventManager mgr)
    {
        if (ev == null || ev.objectives == null || ev.objectives.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder(256);
        for (int i = 0; i < ev.objectives.Count; i++)
        {
            var o = ev.objectives[i];
            if (o == null) continue;

            string id = string.IsNullOrWhiteSpace(o.id) ? string.Empty : o.id.Trim();
            bool locked = !string.IsNullOrWhiteSpace(o.unlockAfterObjectiveId)
                && !mgr.IsObjectiveComplete(o.unlockAfterObjectiveId.Trim());
            bool complete = !string.IsNullOrWhiteSpace(id) && mgr.IsObjectiveComplete(id);

            if (locked)
            {
                sb.Append("[...] ");
            }
            else if (complete)
            {
                sb.Append("[x] ");
            }
            else if (o.optional)
            {
                sb.Append("[~] ");
            }
            else
            {
                sb.Append("[ ] ");
            }

            if (!string.IsNullOrWhiteSpace(o.description))
            {
                sb.Append(o.description);
            }
            else if (!string.IsNullOrWhiteSpace(id))
            {
                sb.Append(id);
            }

            if (locked && !string.IsNullOrWhiteSpace(o.unlockAfterObjectiveId))
            {
                sb.Append(" (sau: ");
                sb.Append(o.unlockAfterObjectiveId.Trim());
                sb.Append(')');
            }

            if (i < ev.objectives.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private void SetActive(bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }
}
