using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Panel liệt kê save trong persistentDataPath; chọn một dòng để LoadGame.</summary>
public class MainMenuSaveListPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentParent;
    [SerializeField] private SaveSlotRowUI rowPrefab;
    [SerializeField] private TMP_Text emptyHintText;
    [SerializeField] private Button closeButton;

    bool _scrollContentLayoutPrepared;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        if (panelRoot != null && panelRoot.activeSelf)
        {
            panelRoot.SetActive(false);
        }

        PrepareScrollContentLayoutIfNeeded();
    }

    /// <summary>
    /// ScrollRect.Content cần xếp dọc các hàng; prefab neo (0,1) giống nhau nên nhiều hàng sẽ chồng lên nhau nếu không có layout.
    /// </summary>
    void PrepareScrollContentLayoutIfNeeded()
    {
        if (_scrollContentLayoutPrepared || contentParent == null)
        {
            return;
        }

        _scrollContentLayoutPrepared = true;

        RectTransform rt = contentParent as RectTransform;
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vl = rt.GetComponent<VerticalLayoutGroup>();
        if (vl == null)
        {
            vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vl.spacing = 8f;
        vl.padding = new RectOffset(12, 12, 12, 12);
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;

        ContentSizeFitter fit = rt.GetComponent<ContentSizeFitter>();
        if (fit == null)
        {
            fit = rt.gameObject.AddComponent<ContentSizeFitter>();
        }

        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (emptyHintText != null)
        {
            LayoutElement hintRow = emptyHintText.gameObject.GetComponent<LayoutElement>();
            if (hintRow == null)
            {
                hintRow = emptyHintText.gameObject.AddComponent<LayoutElement>();
            }

            hintRow.ignoreLayout = true;
        }
    }

    public void Show()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshList();
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void RefreshList()
    {
        PrepareScrollContentLayoutIfNeeded();

        if (contentParent == null)
        {
            Debug.LogWarning("MainMenuSaveListPanel: assign contentParent.");
            return;
        }

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform c = contentParent.GetChild(i);
            if (c.GetComponent<SaveSlotRowUI>() != null)
            {
                Destroy(c.gameObject);
            }
        }

        bool hasPrefab = rowPrefab != null;

        List<GameSaveIo.SaveSlotListEntry> list = GameSaveIo.EnumerateSortedByNewest();

        if (emptyHintText != null)
        {
            emptyHintText.gameObject.SetActive(list.Count == 0);
        }

        if (!hasPrefab)
        {
            Debug.LogWarning("MainMenuSaveListPanel: assign rowPrefab (SaveSlotRowUI).");
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            GameSaveIo.SaveSlotListEntry entry = list[i];
            SaveSlotRowUI row = Instantiate(rowPrefab, contentParent);
            row.Bind(entry, OnPickPath, OnDeletePath);
            row.gameObject.SetActive(true);
        }

        if (contentParent is RectTransform contentRt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }
    }

    void OnPickPath(string absolutePath)
    {
        Hide();
        GameSaveService.ResolveOrCreate().LoadGameFromAbsolutePath(absolutePath);
    }

    void OnDeletePath(string absolutePath)
    {
        if (GameSaveIo.TryDeleteAbsolutePath(absolutePath))
        {
            RefreshList();
        }
    }
}
