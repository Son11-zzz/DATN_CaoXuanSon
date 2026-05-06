using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestPanelUI : MonoBehaviour
{
    private const string ScrollRootName = "QuestBodyScroll";

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Visibility (Optional)")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool useCanvasGroupIfAvailable = true;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Scroll (auto)")]
    [Tooltip("Nếu ScrollRect chưa gán, tự bọc BodyQuestText trong Viewport + ScrollRect + mask.")]
    [SerializeField] private bool autoWireScrollViewWhenMissing = true;

    [Header("Behavior")]
    [SerializeField] private bool showCompletedInBody = false;
    [SerializeField] private bool showOnlyFirstQuest = true;
    [SerializeField] private bool resetScrollOnRefresh = true;

    private string lastTitle = "Nhiệm vụ";
    private string lastBody = "Không có nhiệm vụ đang làm";

    private bool subscribed = false;
    private bool isVisible;

    [Header("Debug")]
    [SerializeField] private bool logToggle = false;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null && panelRoot != null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        TryWireScrollStructure();

        SetVisible(false);
    }

    private void OnEnable()
    {
        TryWireScrollStructure();
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        TrySubscribe();
    }

    public void Toggle()
    {
        SetVisible(!isVisible);

        if (logToggle && panelRoot != null)
        {
            Debug.Log($"QuestPanelUI: Toggle '{panelRoot.name}' visible={isVisible}", panelRoot);
        }

        if (isVisible)
        {
            Refresh();
        }
    }

    public bool IsVisible()
    {
        return isVisible;
    }

    public void Show()
    {
        SetVisible(true);
        Refresh();
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (panelRoot == null) return;

        bool canUseCanvasGroup = useCanvasGroupIfAvailable && canvasGroup != null;

        if (canUseCanvasGroup)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;

            if (!panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            return;
        }

        panelRoot.SetActive(visible);
    }

    #region Event Subscription

    private void TrySubscribe()
    {
        if (subscribed) return;

        bool didSubscribe = false;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
            QuestManager.Instance.OnQuestUpdated += Refresh;
            didSubscribe = true;
        }

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Refresh;
            InventorySystem.Instance.OnInventoryChanged += Refresh;
            didSubscribe = true;
        }

        subscribed = didSubscribe;
    }

    private void Unsubscribe()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
        }

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Refresh;
        }

        subscribed = false;
    }

    #endregion

    #region Scroll setup

    void TryWireScrollStructure()
    {
        Transform rootTr = panelRoot != null ? panelRoot.transform : transform;

        TryResolveAssignedScrollRect();
        if (scrollRect != null && scrollRect.content != null && bodyText != null &&
            bodyText.transform.IsChildOf(scrollRect.content))
        {
            EnsureScrollContentSizesContent(scrollRect.content);
            SyncVerticalLayoutOnScrollContent(scrollRect.content);
            NormalizeBodyAnchorsForScrollContent(bodyText.rectTransform, scrollRect.content);
            EnsureBodyTextSizingComponents(bodyText);
            FinalizeBuiltScroll(scrollRect);
            return;
        }

        if (!autoWireScrollViewWhenMissing || bodyText == null)
        {
            return;
        }

        Transform existing = rootTr.Find(ScrollRootName);
        if (existing != null)
        {
            scrollRect = existing.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                EnsureScrollContentSizesContent(scrollRect.content);
                SyncVerticalLayoutOnScrollContent(scrollRect.content);
                NormalizeBodyAnchorsForScrollContent(bodyText.rectTransform, scrollRect.content);
                EnsureBodyTextSizingComponents(bodyText);
                FinalizeBuiltScroll(scrollRect);
                return;
            }
        }

        RectTransform bodyRt = bodyText.rectTransform;
        RectTransform parentRt = bodyRt.parent as RectTransform;
        if (parentRt == null)
        {
            return;
        }

        int sibling = bodyRt.GetSiblingIndex();

        var scrollGo = new GameObject(ScrollRootName, typeof(RectTransform));
        var scrollRootRt = scrollGo.GetComponent<RectTransform>();
        scrollGo.transform.SetParent(parentRt, false);
        scrollRootRt.SetSiblingIndex(sibling);
        scrollRootRt.anchorMin = bodyRt.anchorMin;
        scrollRootRt.anchorMax = bodyRt.anchorMax;
        scrollRootRt.pivot = bodyRt.pivot;
        scrollRootRt.anchoredPosition = bodyRt.anchoredPosition;
        scrollRootRt.sizeDelta = bodyRt.sizeDelta;
        scrollRootRt.localScale = bodyRt.localScale;

        var scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.015f);
        scrollBg.raycastTarget = true;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.inertia = true;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;

        viewportGo.AddComponent<RectMask2D>();
        var vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = Color.clear;
        vpImg.raycastTarget = false;

        scrollRect.viewport = vpRt;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var ctRt = contentGo.GetComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0f, 1f);
        ctRt.anchorMax = new Vector2(1f, 1f);
        ctRt.pivot = new Vector2(0.5f, 1f);
        ctRt.anchoredPosition = Vector2.zero;
        ctRt.sizeDelta = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 6, 12);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        scrollRect.content = ctRt;

        EnsureScrollContentSizesContent(ctRt);

        bodyRt.SetParent(ctRt, false);
        NormalizeBodyAnchorsForScrollContent(bodyRt, ctRt);
        EnsureBodyTextSizingComponents(bodyText);

        FinalizeBuiltScroll(scrollRect);
    }

    static void NormalizeBodyAnchorsForScrollContent(RectTransform bodyRt, RectTransform content)
    {
        if (bodyRt == null || content == null || bodyRt.parent != content)
        {
            return;
        }

        bodyRt.anchorMin = new Vector2(0f, 1f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.anchoredPosition = Vector2.zero;
        bodyRt.sizeDelta = new Vector2(0f, 0f);
    }

    /// <summary>
    /// Without this, Content height can stay 0 and TMP lines stack (overlap) inside ScrollRect.
    /// </summary>
    static void EnsureScrollContentSizesContent(RectTransform content)
    {
        if (content == null) return;

        var csf = content.gameObject.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = content.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = content.gameObject.AddComponent<LayoutElement>();
        le.minWidth = -1f;
        le.minHeight = -1f;
        le.preferredWidth = -1f;
        le.preferredHeight = -1f;
        le.flexibleWidth = 1f;
        le.flexibleHeight = -1f;
    }

    static void EnsureBodyTextSizingComponents(TextMeshProUGUI tmp)
    {
        if (tmp == null)
        {
            return;
        }

        var le = tmp.gameObject.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = tmp.gameObject.AddComponent<LayoutElement>();
        }

        le.flexibleWidth = 1f;
        le.minHeight = -1f;

        var csf = tmp.gameObject.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = tmp.gameObject.AddComponent<ContentSizeFitter>();
        }

        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = true;
        tmp.lineSpacing = 2f;
    }

    static void SyncVerticalLayoutOnScrollContent(RectTransform content)
    {
        if (content == null) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;
        vlg.spacing = 4f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    void TryResolveAssignedScrollRect()
    {
        if (scrollRect != null)
        {
            return;
        }

        if (panelRoot != null)
        {
            scrollRect = panelRoot.GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect != null)
        {
            FinalizeBuiltScroll(scrollRect);
        }
    }

    static void FinalizeBuiltScroll(ScrollRect sr)
    {
        if (sr == null)
        {
            return;
        }

        if (sr.viewport == null)
        {
            var vp = sr.GetComponentInChildren<RectMask2D>(true);
            if (vp != null)
            {
                sr.viewport = vp.GetComponent<RectTransform>();
            }
        }

        if (sr.viewport != null && sr.content != null && sr.viewport.GetComponent<Image>() == null)
        {
            var im = sr.viewport.gameObject.AddComponent<Image>();
            im.color = Color.clear;
            im.raycastTarget = false;
        }
    }

    void RefreshBodyScrollLayoutAfterText()
    {
        if (scrollRect == null || bodyText == null || scrollRect.content == null)
        {
            return;
        }

        var vp = scrollRect.viewport;
        if (vp == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float vw = vp.rect.width;
        if (vw < 40f && vp.rect.size.x > 0f)
        {
            vw = vp.rect.size.x;
        }

        if (vw < 80f)
        {
            vw = 360f;
        }

        var le = bodyText.GetComponent<LayoutElement>();
        if (le != null)
        {
            float inner = Mathf.Max(40f, vw - 28f);
            le.minWidth = inner;
            le.preferredWidth = inner;
            le.flexibleWidth = 1f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        Canvas.ForceUpdateCanvases();
    }

    #endregion

    #region UI Refresh

    public void Refresh()
    {
        _ = showCompletedInBody;
        var qm = QuestManager.Instance;

        if (qm == null)
        {
            SetTexts(lastTitle, lastBody);
            return;
        }

        var quests = qm.ActiveQuests;

        if (quests == null || quests.Count == 0)
        {
            SetTexts("Nhiệm vụ", "Không có nhiệm vụ đang làm.");
            RefreshBodyScrollLayoutAfterText();
            FinishScrollRefresh();
            return;
        }

        int count = showOnlyFirstQuest ? Mathf.Min(1, quests.Count) : quests.Count;

        string headerTitle = lastTitle;

        if (quests[0] != null)
        {
            headerTitle = string.IsNullOrWhiteSpace(quests[0].title)
                ? quests[0].GetId()
                : quests[0].title;
        }

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < count; i++)
        {
            var q = quests[i];
            if (q == null) continue;

            bool completed = qm.IsCompleted(q);

            if (showOnlyFirstQuest)
            {
                sb.AppendLine(completed ? "[XONG]" : "[ĐANG]");
            }
            else
            {
                sb.Append(completed ? "[XONG] " : "[ĐANG] ");
                sb.AppendLine(string.IsNullOrWhiteSpace(q.title)
                    ? q.GetId()
                    : q.title);
            }

            if (!string.IsNullOrWhiteSpace(q.description))
            {
                sb.AppendLine(q.description);
            }

            if (q.objectives != null)
            {
                foreach (var o in q.objectives)
                {
                    if (o == null || !o.IsValid()) continue;

                    switch (o.type)
                    {
                        case QuestObjectiveType.CollectItem:
                            int have = InventorySystem.Instance != null
                                ? InventorySystem.Instance.GetAmount(o.item)
                                : 0;

                            int need = Mathf.Max(1, o.amount);
                            string itemName = o.item != null
                                ? o.item.itemName
                                : "(thiếu vật phẩm)";

                            sb.AppendLine($"- {itemName}: {have}/{need}");
                            break;

                        case QuestObjectiveType.ReachStatValue:
                            sb.AppendLine(
                                $"- {QuestUiLocalization.StatLabelVi(o.stat)} ≥ {o.targetValue}");
                            break;

                        case QuestObjectiveType.TalkToNpc:
                            bool talked = qm.HasTalkedToNpc(o.npcId);
                            string npcLabel = string.IsNullOrWhiteSpace(o.npcId)
                                ? "(thiếu nhân vật)"
                                : o.npcId;
                            sb.AppendLine($"- Trò chuyện với {npcLabel}: {(talked ? 1 : 0)}/1");
                            break;
                    }
                }
            }

            if (i < count - 1)
            {
                sb.AppendLine();
            }
        }

        string body = sb.Length > 0
            ? sb.ToString().TrimEnd()
            : "Không có nhiệm vụ đang làm";

        SetTexts(headerTitle, body);
        RefreshBodyScrollLayoutAfterText();
        FinishScrollRefresh();
    }

    void FinishScrollRefresh()
    {
        if (!resetScrollOnRefresh || scrollRect == null)
        {
            return;
        }

        scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    private void SetTexts(string title, string body)
    {
        lastTitle = title;
        lastBody = body;

        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;
    }

    public void RemoveCompletedQuests()
    {
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.RemoveCompletedQuests();
        Refresh();
    }

    public void ResetQuestHUD()
    {
        RemoveCompletedQuests();
    }

    #endregion
}
