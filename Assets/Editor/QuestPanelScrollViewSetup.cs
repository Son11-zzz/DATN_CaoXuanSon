#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bakes a ScrollRect + mask around <c>BodyQuestText</c> on the main UI Canvas prefab
/// (matches runtime wiring in <see cref="QuestPanelUI"/>).
/// </summary>
public static class QuestPanelScrollViewSetup
{
    const string CanvasPrefabPath = "Assets/Prefabs/UI/Canvas.prefab";

    /// <summary>Non-interactive entry for CI / batch: <c>-executeMethod QuestPanelScrollViewSetup.BakeQuestPanelScrollForBatch</c></summary>
    public static void BakeQuestPanelScrollForBatch()
    {
        BakeIntoCanvasPrefab();
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/SVSimulator/Setup Quest Panel Scroll View")]
    public static void SetupFromMenu()
    {
        BakeIntoCanvasPrefab();
    }

    static void BakeIntoCanvasPrefab()
    {
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            Transform questPanel = FindDeep(root.transform, "QuestPanel");
            if (questPanel == null)
            {
                Debug.LogError("QuestPanelScrollViewSetup: QuestPanel not found under Canvas prefab.");
                return;
            }

            if (questPanel.Find("QuestBodyScroll") != null)
            {
                Debug.Log("QuestPanelScrollViewSetup: QuestBodyScroll already present — no changes.");
                return;
            }

            Transform body = FindDeep(questPanel, "BodyQuestText");
            if (body == null)
            {
                Debug.LogError("QuestPanelScrollViewSetup: BodyQuestText not found.");
                return;
            }

            var bodyRt = body.GetComponent<RectTransform>();
            int bodySibling = bodyRt.GetSiblingIndex();

            var scrollGo = new GameObject("QuestBodyScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(questPanel, false);
            scrollGo.transform.SetSiblingIndex(bodySibling);

            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = bodyRt.anchorMin;
            scrollRt.anchorMax = bodyRt.anchorMax;
            scrollRt.pivot = bodyRt.pivot;
            scrollRt.anchoredPosition = bodyRt.anchoredPosition;
            scrollRt.sizeDelta = bodyRt.sizeDelta;
            scrollRt.localScale = bodyRt.localScale;

            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.015f);
            scrollBg.raycastTarget = true;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.inertia = true;

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

            scroll.viewport = vpRt;

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

            scroll.content = ctRt;

            var contentCsf = contentGo.AddComponent<ContentSizeFitter>();
            contentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bodyRt.SetParent(ctRt, false);
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = new Vector2(0f, 0f);

            var tmp = body.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                EnsureBodyTextSizingComponents(tmp);
            }

            var panelUi = questPanel.GetComponent<QuestPanelUI>();
            if (panelUi != null)
            {
                var so = new SerializedObject(panelUi);
                so.FindProperty("scrollRect").objectReferenceValue = scroll;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            Debug.Log("QuestPanelScrollViewSetup: baked QuestBodyScroll into Canvas.prefab.");
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsureBodyTextSizingComponents(TextMeshProUGUI text)
    {
        var le = text.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = text.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minHeight = -1f;

        var csf = text.gameObject.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = text.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableWordWrapping = true;
        text.lineSpacing = 2f;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var c = FindDeep(t.GetChild(i), name);
            if (c != null) return c;
        }
        return null;
    }
}
#endif
