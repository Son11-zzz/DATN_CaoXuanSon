using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventLetterUI : MonoBehaviour
{
    public static EventLetterUI Instance;

    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI closeButtonText;

    private Action onClosed;

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel == null)
        {
            panel = gameObject;
        }

        WireCloseButton();
        HidePanelOnly();
        onClosed = null;
    }

    private void WireCloseButton()
    {
        if (closeButton == null)
        {
            closeButton = GetComponentInChildren<Button>(true);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Show(StoryEventDefinition ev, Action onClosed = null)
    {
        this.onClosed = onClosed;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        gameObject.SetActive(true);

        if (ev != null)
        {
            if (titleText != null)
            {
                string t = string.IsNullOrWhiteSpace(ev.letterTitle) ? ev.GetDisplayTitle() : ev.letterTitle;
                titleText.text = t;
            }

            if (bodyText != null)
            {
                bodyText.text = BuildBody(ev);
            }

            if (closeButtonText != null)
            {
                closeButtonText.text = string.IsNullOrWhiteSpace(ev.letterCloseText) ? "Được" : ev.letterCloseText;
            }
        }
    }

    /// <summary>Thư tự do tiêu đề/nội dung (tổng kết kỳ, hướng dẫn) không cần StoryEventDefinition.</summary>
    public void ShowPlain(string title, string body, string closeText = "Được", Action onClosedCallback = null)
    {
        onClosed = onClosedCallback;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        gameObject.SetActive(true);

        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
        }

        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
        }

        if (closeButtonText != null)
        {
            closeButtonText.text = string.IsNullOrWhiteSpace(closeText) ? "Được" : closeText;
        }

        WireCloseButton();
    }

    public void Close()
    {
        var cb = onClosed;
        onClosed = null;
        HidePanelOnly();
        cb?.Invoke();
    }

    void HidePanelOnly()
    {
        if (panel != null && panel != gameObject)
        {
            panel.SetActive(false);
        }
        else if (panel == gameObject)
        {
            gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        HidePanelOnly();
        onClosed = null;
    }

    private static string BuildBody(StoryEventDefinition ev)
    {
        if (ev == null) return string.Empty;

        var sb = new StringBuilder(512);

        if (!string.IsNullOrWhiteSpace(ev.letterBody))
        {
            sb.AppendLine(ev.letterBody.Trim());
        }

        if (ev.includeObjectivesInLetter && ev.objectives != null && ev.objectives.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();

            sb.AppendLine("Nhiệm vụ:");
            for (int i = 0; i < ev.objectives.Count; i++)
            {
                var o = ev.objectives[i];
                if (o == null) continue;

                string text = !string.IsNullOrWhiteSpace(o.description) ? o.description : o.id;
                if (string.IsNullOrWhiteSpace(text)) continue;

                sb.Append("- ");
                sb.AppendLine(text);
            }
        }

        return sb.ToString().Trim();
    }
}
