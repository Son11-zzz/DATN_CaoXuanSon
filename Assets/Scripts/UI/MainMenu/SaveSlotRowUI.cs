using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hàng trong danh sách file save (bind từ MainMenuSaveListPanel).</summary>
public class SaveSlotRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private float preferredListRowHeight = 132f;

    string _absolutePath;

    void Awake()
    {
        if (selectButton == null)
        {
            selectButton = GetComponentInChildren<Button>(true);
        }

        EnsureRowMeasuresForVerticalList();
    }

    /// <summary>VerticalLayoutGroup trên ScrollRect.Content cần chiều cao ô; tránh chồng nhiều hàng chồng tại một tọa độ.</summary>
    void EnsureRowMeasuresForVerticalList()
    {
        LayoutElement rowLE = GetComponent<LayoutElement>();
        if (rowLE == null)
        {
            rowLE = gameObject.AddComponent<LayoutElement>();
        }

        rowLE.minHeight = preferredListRowHeight;
        rowLE.preferredHeight = preferredListRowHeight;
    }

    public void Bind(GameSaveIo.SaveSlotListEntry entry, Action<string> onPick, Action<string> onDelete)
    {
        if (titleText != null)
        {
            titleText.text = entry.TitleLine ?? string.Empty;
        }

        if (subtitleText != null)
        {
            subtitleText.text = entry.SubLine ?? string.Empty;
            subtitleText.enableWordWrapping = true;
            subtitleText.overflowMode = TextOverflowModes.Truncate;
        }

        _absolutePath = entry.FullPath ?? string.Empty;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_absolutePath))
                {
                    onPick?.Invoke(_absolutePath);
                }
            });
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_absolutePath))
                {
                    onDelete?.Invoke(_absolutePath);
                }
            });
        }
    }
}
