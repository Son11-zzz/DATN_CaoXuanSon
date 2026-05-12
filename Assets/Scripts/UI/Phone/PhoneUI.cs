using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum PhoneContentTab
{
    Messages,
    Schedule,
    Objectives,
    Health
}

/// <summary>
/// Phone UI: có thể tự dựng runtime, hoặc bạn dựng hết trong scene/prefab (70_UIGamePlay).
/// Chế độ "ứng dụng": màn hình chính (lưới icon) + từng app là một GameObject riêng.
/// </summary>
public class PhoneUI : MonoBehaviour
{
    public static PhoneUI Instance;

    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.P;

    [Header("Visibility")]
    [Tooltip("Hide the whole phone canvas on bootstrap / main menu so it does not overlap other UI.")]
    [SerializeField] private bool hideOutsideGameplay = true;

    [Header("Custom layout — core refs")]
    [Tooltip("Canvas chứa toàn bộ phone (Screen Space Overlay). Nếu để trống, script tìm Canvas trên phoneFrameRoot hoặc tự tạo.")]
    [SerializeField] private Canvas canvasRef;

    [Tooltip("Khung điện thoại: cả khối UI bật/tắt khi bấm P (viền máy + nội dung).")]
    [FormerlySerializedAs("panelRef")]
    [SerializeField] private RectTransform phoneFrameRoot;

    [Tooltip("Ô hint góc màn hình [P] Điện thoại + tóm tắt (để trống nếu không cần).")]
    [SerializeField] private TextMeshProUGUI toolbarBadgeRef;

    [Header("Custom layout — single body (không dùng app riêng)")]
    [Tooltip("Chỉ dùng khi KHÔNG cấu hình Custom App Screens: một TMP duy nhất đổi nội dung theo tab (bạn tự gắn nút đổi tab gọi OpenApp).")]
    [FormerlySerializedAs("bodyTextRef")]
    [SerializeField] private TextMeshProUGUI singleBodyTextRef;

    [Header("Custom layout — màn hình chính + từng app")]
    [Tooltip("Màn hình chính: lưới icon ứng dụng. Khi mở app, ẩn Home; nút Back gọi GoHome().")]
    [SerializeField] private GameObject homeScreenRoot;

    [SerializeField] private PhoneAppScreen[] customAppScreens;

    [Header("Custom layout — runtime bounds")]
    [Tooltip("Kích thước cố định của khung phone khi dùng layout dựng sẵn trong scene.")]
    [SerializeField] private Vector2 customPhoneFrameSize = new Vector2(600f, 900f);

    [Header("Layout (auto-generated UI only)")]
    [SerializeField] private Vector2 toolbarAnchorMin = new Vector2(0f, 0f);
    [SerializeField] private Vector2 toolbarAnchorMax = new Vector2(0f, 0f);
    [SerializeField] private Vector2 toolbarPivot = new Vector2(0f, 0f);
    [SerializeField] private Vector2 toolbarAnchoredPosition = new Vector2(24f, 24f);
    [SerializeField] private Vector2 toolbarSizeDelta = new Vector2(280f, 64f);

    private PhoneContentTab currentTab = PhoneContentTab.Messages;

    private bool isOpen;
    private GameObject autoRoot;
    private Canvas effectiveCanvas;

    private RectTransform panel;
    private TextMeshProUGUI body;
    private TextMeshProUGUI title;
    private Button messagesTabBtn;
    private Button scheduleTabBtn;
    private Button objectivesTabBtn;
    private Button closeBtn;
    private TextMeshProUGUI toolbarBadge;
    private RectTransform toolbarBadgeRect;

    private PhoneAppScreen runtimeHealthScreen;
    private bool runtimeHealthSetupDone;

    private GameObject runtimeHealthHomeIcon;

    private bool gameTimeBadgeHooked;

    private bool UsesCustomAppScreens =>
        customAppScreens != null && customAppScreens.Length > 0 &&
        Array.Exists(customAppScreens, s => s != null && s.appRoot != null && s.contentText != null);

    private bool UsesSingleBodyFallback =>
        phoneFrameRoot != null && singleBodyTextRef != null && !UsesCustomAppScreens;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        EnsureUI();
        SubscribeEvents();
        SubscribeStoryProgress();
        SubscribeStatChanges();
        TrySubscribeGameTimeForBadge();
        SetOpen(false);
        RefreshBadge();
        ApplyGameplayVisibility();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        UnsubscribeEvents();
        UnsubscribeStoryProgress();
        UnsubscribeStatChanges();
        UnsubscribeGameTimeForBadge();
    }

    private void OnActiveSceneChanged(Scene a, Scene b)
    {
        ApplyGameplayVisibility();
    }

    public void ApplyGameplayVisibility()
    {
        EnsureUI();

        bool allow = !hideOutsideGameplay || !IsMenuOrBootstrapSceneActive();

        if (effectiveCanvas != null)
        {
            effectiveCanvas.enabled = allow;
        }

        if (!allow)
        {
            SetOpen(false);
        }
    }

    private static bool IsMenuOrBootstrapSceneActive()
    {
        if (SceneFlowController.Instance != null)
        {
            return SceneFlowController.Instance.IsMenuOrBootstrapActive();
        }

        string n = SceneManager.GetActiveScene().name;
        return n == "10_MainMenu" || n == "00_Bootstrap";
    }

    private void SubscribeEvents()
    {
        if (PhoneSystem.Instance != null)
        {
            PhoneSystem.Instance.OnInboxChanged -= OnPhoneInboxChanged;
            PhoneSystem.Instance.OnInboxChanged += OnPhoneInboxChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (PhoneSystem.Instance != null)
        {
            PhoneSystem.Instance.OnInboxChanged -= OnPhoneInboxChanged;
        }
    }

    private void OnPhoneInboxChanged()
    {
        RefreshBadge();
        if (isOpen && currentTab == PhoneContentTab.Messages)
        {
            RefreshBody();
        }
    }

    private void SubscribeStoryProgress()
    {
        if (StoryEventManager.Instance != null)
        {
            StoryEventManager.Instance.OnProgressChanged -= OnStoryProgressChanged;
            StoryEventManager.Instance.OnProgressChanged += OnStoryProgressChanged;
        }
    }

    private void UnsubscribeStoryProgress()
    {
        if (StoryEventManager.Instance != null)
        {
            StoryEventManager.Instance.OnProgressChanged -= OnStoryProgressChanged;
        }
    }

    private void OnStoryProgressChanged()
    {
        if (isOpen && currentTab == PhoneContentTab.Objectives)
        {
            RefreshBody();
        }
    }

    private void SubscribeStatChanges()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnStatChanged -= HandleStatsChangedExternally;
            EventManager.Instance.OnStatChanged += HandleStatsChangedExternally;
        }
    }

    private void UnsubscribeStatChanges()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnStatChanged -= HandleStatsChangedExternally;
        }
    }

    private void HandleStatsChangedExternally()
    {
        RefreshBadge();
        if (isOpen && currentTab == PhoneContentTab.Health)
        {
            RefreshBody();
        }
    }

    private void TrySubscribeGameTimeForBadge()
    {
        if (gameTimeBadgeHooked)
        {
            return;
        }

        if (GameTimeManager.Instance == null)
        {
            return;
        }

        GameTimeManager.Instance.OnTimeChanged -= HandleGameTimeChangedForBadge;
        GameTimeManager.Instance.OnTimeChanged += HandleGameTimeChangedForBadge;
        gameTimeBadgeHooked = true;
        RefreshBadge();
    }

    private void HandleGameTimeChangedForBadge()
    {
        RefreshBadge();
    }

    private void UnsubscribeGameTimeForBadge()
    {
        if (!gameTimeBadgeHooked)
        {
            return;
        }

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleGameTimeChangedForBadge;
        }

        gameTimeBadgeHooked = false;
    }

    private void Update()
    {
        TrySubscribeGameTimeForBadge();

        if (PhoneSystem.Instance != null && toolbarBadge == null)
        {
            SubscribeEvents();
            RefreshBadge();
        }

        if (StoryEventManager.Instance != null)
        {
            SubscribeStoryProgress();
        }

        if (EventManager.Instance != null)
        {
            SubscribeStatChanges();
        }

        if (Input.GetKeyDown(toggleKey) && (!hideOutsideGameplay || !IsMenuOrBootstrapSceneActive()))
        {
            SetOpen(!isOpen);
        }
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        if (panel != null)
        {
            panel.gameObject.SetActive(open);
        }

        if (open)
        {
            if (PhoneSystem.Instance != null)
            {
                PhoneSystem.Instance.MarkAllAsRead();
            }

            RefreshBadge();

            if (UsesCustomAppScreens)
            {
                if (homeScreenRoot != null)
                {
                    GoHome();
                }
                else
                {
                    OpenApp(PhoneContentTab.Messages);
                }
            }
            else
            {
                RefreshBody();
            }
        }
    }

    /// <summary>Màn hình chính (ứng dụng). Gắn vào nút Back trong từng app.</summary>
    public void GoHome()
    {
        if (!UsesCustomAppScreens)
        {
            return;
        }

        EnsureRuntimeHealthArtifacts();

        if (homeScreenRoot != null)
        {
            homeScreenRoot.SetActive(true);
            for (int i = 0; i < customAppScreens.Length; i++)
            {
                var s = customAppScreens[i];
                if (s != null && s.appRoot != null)
                {
                    s.appRoot.SetActive(false);
                }
            }

            if (runtimeHealthScreen != null && runtimeHealthScreen.appRoot != null)
            {
                runtimeHealthScreen.appRoot.SetActive(false);
            }

            return;
        }

        OpenApp(PhoneContentTab.Messages);
    }

    /// <summary>Mở một "ứng dụng". Gắn vào Button trên màn hình chính (hoặc dock).</summary>
    public void OpenApp(PhoneContentTab tab)
    {
        currentTab = tab;

        if (UsesCustomAppScreens)
        {
            EnsureRuntimeHealthArtifacts();

            if (homeScreenRoot != null)
            {
                homeScreenRoot.SetActive(false);
            }

            for (int i = 0; i < customAppScreens.Length; i++)
            {
                var s = customAppScreens[i];
                if (s == null || s.appRoot == null)
                {
                    continue;
                }

                bool on = s.tab == tab;
                s.appRoot.SetActive(on);
            }

            if (runtimeHealthScreen != null && runtimeHealthScreen.appRoot != null)
            {
                runtimeHealthScreen.appRoot.SetActive(tab == PhoneContentTab.Health);
            }
        }

        RefreshBody();
    }

    public void OpenMessagesApp() => OpenApp(PhoneContentTab.Messages);
    public void OpenScheduleApp() => OpenApp(PhoneContentTab.Schedule);
    public void OpenObjectivesApp() => OpenApp(PhoneContentTab.Objectives);
    public void OpenHealthApp() => OpenApp(PhoneContentTab.Health);

    private void EnsureUI()
    {
        if (phoneFrameRoot != null)
        {
            panel = phoneFrameRoot;
            toolbarBadge = toolbarBadgeRef;
            if (toolbarBadge != null)
            {
                toolbarBadgeRect = toolbarBadge.rectTransform;
            }

            effectiveCanvas = canvasRef != null ? canvasRef : phoneFrameRoot.GetComponentInParent<Canvas>();
            NormalizeCustomLayoutBounds();

            if (UsesSingleBodyFallback)
            {
                body = singleBodyTextRef;
            }
            else if (UsesCustomAppScreens)
            {
                body = null;
            }
            else
            {
                body = singleBodyTextRef;
            }

            EnsurePhoneCanvasInputWorks();
            return;
        }

        if (autoRoot != null)
        {
            return;
        }

        autoRoot = new GameObject("PhoneAutoUI");
        autoRoot.transform.SetParent(transform, false);

        var canvas = canvasRef;
        if (canvas == null)
        {
            var canvasGo = new GameObject("PhoneCanvas");
            canvasGo.transform.SetParent(autoRoot.transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 280;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        effectiveCanvas = canvas;
        EnsurePhoneCanvasInputWorks();
        var canvasRt = canvas.GetComponent<RectTransform>();

        var badgeGo = new GameObject("PhoneBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(canvasRt, false);
        toolbarBadgeRect = badgeGo.GetComponent<RectTransform>();
        toolbarBadgeRect.anchorMin = toolbarAnchorMin;
        toolbarBadgeRect.anchorMax = toolbarAnchorMax;
        toolbarBadgeRect.pivot = toolbarPivot;
        toolbarBadgeRect.anchoredPosition = toolbarAnchoredPosition;
        toolbarBadgeRect.sizeDelta = toolbarSizeDelta;

        var badgeBg = badgeGo.AddComponent<Image>();
        badgeBg.color = new Color(0f, 0f, 0f, 0.55f);

        var badgeTextGo = new GameObject("Text", typeof(RectTransform));
        badgeTextGo.transform.SetParent(toolbarBadgeRect, false);
        var badgeTextRt = badgeTextGo.GetComponent<RectTransform>();
        badgeTextRt.anchorMin = Vector2.zero;
        badgeTextRt.anchorMax = Vector2.one;
        badgeTextRt.offsetMin = new Vector2(10, 4);
        badgeTextRt.offsetMax = new Vector2(-10, -4);
        toolbarBadge = badgeTextGo.AddComponent<TextMeshProUGUI>();
        toolbarBadge.fontSize = 22f;
        toolbarBadge.alignment = TextAlignmentOptions.MidlineLeft;
        toolbarBadge.color = Color.white;
        toolbarBadge.text = "P: Điện thoại";

        var panelGo = new GameObject("PhonePanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasRt, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(720f, 820f);

        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(panel, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -12f);
        titleRt.sizeDelta = new Vector2(0f, 60f);
        title = titleGo.AddComponent<TextMeshProUGUI>();
        title.alignment = TextAlignmentOptions.Midline;
        title.fontSize = 34f;
        title.color = Color.white;
        title.text = "Điện thoại";

        var tabsGo = new GameObject("Tabs", typeof(RectTransform));
        tabsGo.transform.SetParent(panel, false);
        var tabsRt = tabsGo.GetComponent<RectTransform>();
        tabsRt.anchorMin = new Vector2(0f, 1f);
        tabsRt.anchorMax = new Vector2(1f, 1f);
        tabsRt.pivot = new Vector2(0.5f, 1f);
        tabsRt.anchoredPosition = new Vector2(0f, -84f);
        tabsRt.sizeDelta = new Vector2(-40f, 56f);

        var tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 10f;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;

        messagesTabBtn = CreateTab(tabsGo.transform, "Tin nhắn", () => OpenApp(PhoneContentTab.Messages));
        scheduleTabBtn = CreateTab(tabsGo.transform, "Lịch học", () => OpenApp(PhoneContentTab.Schedule));
        objectivesTabBtn = CreateTab(tabsGo.transform, "Mục tiêu", () => OpenApp(PhoneContentTab.Objectives));

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(panel, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.offsetMin = new Vector2(20f, 80f);
        bodyRt.offsetMax = new Vector2(-20f, -150f);
        body = bodyGo.AddComponent<TextMeshProUGUI>();
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSize = 22f;
        body.enableWordWrapping = true;
        body.color = Color.white;
        body.text = "";

        closeBtn = CreateButton(panel, "Đóng (P)", () => SetOpen(false));
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.sizeDelta = new Vector2(160f, 50f);
        closeRt.anchoredPosition = new Vector2(0f, 18f);

        panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Layout dựng trong Editor đôi khi thiếu GraphicRaycaster hoặc sortingOrder thấp hơn HUD khác → không bấm được app.
    /// </summary>
    private void EnsurePhoneCanvasInputWorks()
    {
        if (effectiveCanvas == null)
        {
            return;
        }

        if (effectiveCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
        {
            effectiveCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        const int minPhoneCanvasSortOrder = 280;
        if (effectiveCanvas.sortingOrder < minPhoneCanvasSortOrder)
        {
            effectiveCanvas.sortingOrder = minPhoneCanvasSortOrder;
        }
    }

    void ApplyPhoneAppLayout(RectTransform appRt)
    {
        if (appRt == null || phoneFrameRoot == null)
        {
            return;
        }

        appRt.SetParent(phoneFrameRoot, false);
        appRt.anchorMin = Vector2.zero;
        appRt.anchorMax = Vector2.one;
        appRt.pivot = new Vector2(0.5f, 0.5f);
        appRt.offsetMin = new Vector2(25f, 25f);
        appRt.offsetMax = new Vector2(-25f, -95f);
    }

    static Transform RecursiveFindFirstNamed(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform nested = RecursiveFindFirstNamed(root.GetChild(i), targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    static TextMeshProUGUI PickLikelyBodyText(GameObject cloneRoot)
    {
        if (cloneRoot == null)
        {
            return null;
        }

        var tmps = cloneRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI best = null;
        float bestArea = -1f;
        for (int i = 0; i < tmps.Length; i++)
        {
            var t = tmps[i];
            if (t == null)
            {
                continue;
            }

            Vector2 sz = t.rectTransform.rect.size;
            float area = Mathf.Abs(sz.x * sz.y);
            if (area > bestArea)
            {
                bestArea = area;
                best = t;
            }
        }

        return best;
    }

    void EnsureRuntimeHealthArtifacts()
    {
        if (runtimeHealthSetupDone)
        {
            return;
        }

        if (!UsesCustomAppScreens || phoneFrameRoot == null || customAppScreens == null)
        {
            runtimeHealthSetupDone = true;
            return;
        }

        if (HasConfiguredHealthScreen())
        {
            runtimeHealthSetupDone = true;
            return;
        }

        PhoneAppScreen template = null;
        for (int i = 0; i < customAppScreens.Length; i++)
        {
            var s = customAppScreens[i];
            if (s != null && s.tab == PhoneContentTab.Objectives && s.appRoot != null)
            {
                template = s;
                break;
            }
        }

        if (template?.appRoot == null)
        {
            runtimeHealthSetupDone = true;
            return;
        }

        GameObject clone = Instantiate(template.appRoot, phoneFrameRoot, false);
        clone.name = "AppHealthRuntime";
        clone.SetActive(false);

        if (clone.TryGetComponent(out RectTransform cloneRt))
        {
            ApplyPhoneAppLayout(cloneRt);
        }

        TextMeshProUGUI bodyTmp = PickLikelyBodyText(clone);
        runtimeHealthScreen = new PhoneAppScreen
        {
            tab = PhoneContentTab.Health,
            appRoot = clone,
            contentText = bodyTmp
        };

        TryAttachRuntimeHealthHomeButton();
        runtimeHealthSetupDone = true;
    }

    bool HasConfiguredHealthScreen()
    {
        for (int i = 0; i < customAppScreens.Length; i++)
        {
            var s = customAppScreens[i];
            if (s != null && s.tab == PhoneContentTab.Health && s.appRoot != null && s.contentText != null)
            {
                return true;
            }
        }

        return false;
    }

    void TryAttachRuntimeHealthHomeButton()
    {
        if (runtimeHealthHomeIcon != null || homeScreenRoot == null)
        {
            return;
        }

        Transform template = RecursiveFindFirstNamed(homeScreenRoot.transform, "BtnObjectives");
        if (template == null)
        {
            return;
        }

        GameObject go = Instantiate(template.gameObject, template.parent, false);
        go.name = "BtnHealthAuto";
        runtimeHealthHomeIcon = go;

        if (go.TryGetComponent(out RectTransform rt) && template.TryGetComponent(out RectTransform trt))
        {
            rt.anchorMin = trt.anchorMin;
            rt.anchorMax = trt.anchorMax;
            rt.pivot = trt.pivot;
            rt.sizeDelta = trt.sizeDelta;
            rt.anchoredPosition = trt.anchoredPosition + new Vector2(0f, -145f);
        }

        var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = "Sức khỏe";
        }

        if (go.TryGetComponent(out Button btn))
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OpenHealthApp);
        }
    }

    private void NormalizeCustomLayoutBounds()
    {
        phoneFrameRoot.anchorMin = new Vector2(0.5f, 0.5f);
        phoneFrameRoot.anchorMax = new Vector2(0.5f, 0.5f);
        phoneFrameRoot.pivot = new Vector2(0.5f, 0.5f);
        phoneFrameRoot.anchoredPosition = Vector2.zero;
        phoneFrameRoot.sizeDelta = customPhoneFrameSize;

        if (homeScreenRoot != null && homeScreenRoot.TryGetComponent(out RectTransform homeRt))
        {
            homeRt.SetParent(phoneFrameRoot, false);
            homeRt.anchorMin = new Vector2(0.5f, 0.5f);
            homeRt.anchorMax = new Vector2(0.5f, 0.5f);
            homeRt.pivot = new Vector2(0.5f, 0.5f);
            homeRt.anchoredPosition = Vector2.zero;
            homeRt.sizeDelta = new Vector2(customPhoneFrameSize.x - 50f, customPhoneFrameSize.y - 80f);
        }

        if (customAppScreens == null)
        {
            return;
        }

        for (int i = 0; i < customAppScreens.Length; i++)
        {
            var screen = customAppScreens[i];
            if (screen == null || screen.appRoot == null || !screen.appRoot.TryGetComponent(out RectTransform appRt))
            {
                continue;
            }

            ApplyPhoneAppLayout(appRt);
        }

        EnsureRuntimeHealthArtifacts();
        if (runtimeHealthScreen != null && runtimeHealthScreen.appRoot != null &&
            runtimeHealthScreen.appRoot.TryGetComponent(out RectTransform healthRt))
        {
            ApplyPhoneAppLayout(healthRt);
        }
    }

    private Button CreateTab(Transform parent, string label, Action onClick)
    {
        return CreateButton(parent, label, onClick);
    }

    private Button CreateButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.1f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSize = 22f;

        return btn;
    }

    private void RefreshBadge()
    {
        if (toolbarBadge == null)
        {
            return;
        }

        int unread = PhoneSystem.Instance != null ? PhoneSystem.Instance.UnreadCount : 0;
        string att = BuildAttendanceSnapshot();
        string newTag = unread > 0 ? $"[P] Điện thoại ({unread} tin mới)" : "[P] Điện thoại";
        toolbarBadge.text = $"{newTag}\n{att}";
    }

    private string BuildAttendanceSnapshot()
    {
        var tm = GameTimeManager.Instance;
        int d = tm != null ? tm.DayInSemester : 0;
        int s = tm != null ? tm.Semester : 0;
        int h = tm != null ? tm.Hour : 0;
        int missed = SemesterProgressManager.Instance != null ? SemesterProgressManager.Instance.TotalMissed : 0;
        return $"Ngày {d} · HK{s} — {h:00}h · Nghỉ {missed} buổi";
    }

    private void RefreshBody()
    {
        if (UsesCustomAppScreens)
        {
            EnsureRuntimeHealthArtifacts();

            for (int i = 0; i < customAppScreens.Length; i++)
            {
                var screen = customAppScreens[i];
                if (screen == null || screen.contentText == null || screen.tab != currentTab)
                {
                    continue;
                }

                screen.contentText.text = BuildTabPayload(currentTab);
                ApplyPhoneTextLayoutHint(screen.contentText);
            }

            if (runtimeHealthScreen != null && runtimeHealthScreen.contentText != null &&
                currentTab == PhoneContentTab.Health)
            {
                runtimeHealthScreen.contentText.text = BuildTabPayload(PhoneContentTab.Health);
                ApplyPhoneTextLayoutHint(runtimeHealthScreen.contentText);
            }

            return;
        }

        if (body == null)
        {
            return;
        }

        body.text = BuildTabPayload(currentTab);
        switch (currentTab)
        {
            case PhoneContentTab.Messages:
                if (title != null)
                {
                    title.text = "Tin nhắn";
                }

                break;
            case PhoneContentTab.Schedule:
                if (title != null)
                {
                    title.text = "Lịch học";
                }

                break;
            case PhoneContentTab.Objectives:
                if (title != null)
                {
                    title.text = "Mục tiêu";
                }

                break;
            case PhoneContentTab.Health:
                if (title != null)
                {
                    title.text = "Sức khỏe";
                }

                break;
        }
    }

    string BuildTabPayload(PhoneContentTab tab)
    {
        switch (tab)
        {
            case PhoneContentTab.Messages:
                return BuildMessagesTab();
            case PhoneContentTab.Schedule:
                return BuildScheduleTab();
            case PhoneContentTab.Objectives:
                return BuildObjectivesTab();
            case PhoneContentTab.Health:
                return BuildHealthTab();
            default:
                return string.Empty;
        }
    }

    static void ApplyPhoneTextLayoutHint(TextMeshProUGUI contentText)
    {
        if (contentText == null)
        {
            return;
        }

        contentText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        var textRt = contentText.rectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRt);
        if (textRt.parent is RectTransform parentRt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
        }
    }

    private string BuildHealthTab()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>Theo dõi sức khỏe</b>");

        var tm = GameTimeManager.Instance;
        if (tm != null)
        {
            sb.AppendLine($"Ngày {tm.DayInSemester} · HK {tm.Semester} — {tm.Hour:00}:00");
            sb.AppendLine();
        }

        if (StatManager.Instance != null)
        {
            StatManager sm = StatManager.Instance;
            sb.AppendLine($"Sinh lực (HP): {sm.health:0}");
            sb.AppendLine($"Căng thẳng: {sm.stress:0}");
            sb.AppendLine($"Năng lượng: {sm.energy:0}");
            sb.AppendLine($"GPA: {sm.gpa:F2}");
            sb.AppendLine($"Tiền: {sm.money:0}");
        }
        else
        {
            sb.AppendLine("StatManager chưa sẵn sàng.");
        }

        if (SemesterProgressManager.Instance != null)
        {
            var sp = SemesterProgressManager.Instance;
            sb.AppendLine();
            sb.AppendLine(
                $"Điểm danh: {sp.TotalAttended} buổi · Nghỉ {sp.TotalMissed} buổi · Muộn {sp.LateCount}");
        }

        sb.AppendLine();
        sb.AppendLine("HP tăng chủ yếu khi dùng đồ ăn/uống trong túi (I mở túi, chọn một món, U).");
        return sb.ToString();
    }

    private string BuildMessagesTab()
    {
        var sb = new StringBuilder();
        if (PhoneSystem.Instance == null || PhoneSystem.Instance.Inbox.Count == 0)
        {
            sb.AppendLine("Chưa có tin nhắn.");
            return sb.ToString();
        }

        for (int i = PhoneSystem.Instance.Inbox.Count - 1; i >= 0; i--)
        {
            var msg = PhoneSystem.Instance.Inbox[i];
            if (msg == null)
            {
                continue;
            }

            sb.AppendLine($"[{msg.sender}] Ngày {msg.dayInSemester} · HK {msg.semester}");
            sb.AppendLine(msg.content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildScheduleTab()
    {
        var sb = new StringBuilder();
        if (PhoneSystem.Instance == null)
        {
            sb.AppendLine("PhoneSystem chưa sẵn sàng.");
            return sb.ToString();
        }

        var tm = GameTimeManager.Instance;
        int sem = tm != null ? tm.Semester : 1;
        int todayDay = tm != null ? tm.DayInSemester : 1;

        var today = PhoneSystem.Instance.GetTodaySchedule();
        if (today != null)
        {
            sb.AppendLine("<b>Hôm nay</b>");
            sb.AppendLine($"Ngày {todayDay} · HK {sem} — {today.title}");
            if (!string.IsNullOrWhiteSpace(today.description))
            {
                sb.AppendLine($"  {today.description}");
            }

            bool hasClassHours = today.morningEndHour > today.morningStartHour
                                 || today.afternoonEndHour > today.afternoonStartHour;
            if (hasClassHours)
            {
                sb.AppendLine($"  Sáng {today.morningStartHour}:00 – {today.morningEndHour}:00 | Chiều {today.afternoonStartHour}:00 – {today.afternoonEndHour}:00");
            }
            else
            {
                sb.AppendLine("  Không có tiết học (nghỉ).");
            }

            if (!string.IsNullOrWhiteSpace(today.prepareHint))
            {
                sb.AppendLine($"  Chuẩn bị: {today.prepareHint}");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("<b>Hôm nay</b>");
            sb.AppendLine($"Chưa có dữ liệu lịch cho ngày {todayDay} · HK {sem}.");
            sb.AppendLine();
        }

        sb.AppendLine("<b>Toàn kỳ (theo ngày trong học kỳ)</b>");
        int maxDay = tm != null ? tm.TotalDaysPerSemester : 15;
        for (int d = 1; d <= maxDay; d++)
        {
            var e = PhoneSystem.Instance.GetScheduleForDay(sem, d);
            if (e == null)
            {
                sb.AppendLine($"- Ngày {d}: (chưa cấu hình)");
                continue;
            }

            sb.AppendLine($"- Ngày {d}: {e.title}");
        }

        return sb.ToString();
    }

    private string BuildObjectivesTab()
    {
        var sb = new StringBuilder();

        var tm = GameTimeManager.Instance;
        if (tm != null)
        {
            sb.AppendLine($"Ngày {tm.DayInSemester} · HK {tm.Semester} — {tm.Hour:00}:00");
        }

        if (StatManager.Instance != null)
        {
            sb.AppendLine($"GPA {StatManager.Instance.gpa:F2} · Căng thẳng {StatManager.Instance.stress:0} · Năng lượng {StatManager.Instance.energy:0}");
        }

        if (SemesterProgressManager.Instance != null)
        {
            int att = SemesterProgressManager.Instance.TotalAttended;
            int miss = SemesterProgressManager.Instance.TotalMissed;
            sb.AppendLine($"Điểm danh: {att} buổi, nghỉ {miss} buổi (muộn {SemesterProgressManager.Instance.LateCount})");
        }

        sb.AppendLine();
        sb.AppendLine("<b>Mục tiêu hiện tại</b>");
        var ev = StoryEventManager.Instance != null ? StoryEventManager.Instance.ActiveEvent : null;
        if (ev == null || ev.objectives == null || ev.objectives.Count == 0)
        {
            sb.AppendLine("Không có sự kiện đang diễn ra.");
        }
        else
        {
            for (int i = 0; i < ev.objectives.Count; i++)
            {
                var o = ev.objectives[i];
                if (o == null)
                {
                    continue;
                }

                bool done = StoryEventManager.Instance.IsObjectiveComplete(o.id);
                string mark = done ? "[x]" : "[ ]";
                if (o.optional && !done)
                {
                    mark = "[~]";
                }

                string desc = string.IsNullOrWhiteSpace(o.description) ? o.id : o.description;
                sb.AppendLine($"{mark} {desc}");
            }
        }

        return sb.ToString();
    }
}

[Serializable]
public class PhoneAppScreen
{
    public PhoneContentTab tab;

    [Tooltip("Root của app (Header + ScrollView + ...). Bật khi mở đúng tab; tắt khi về Home hoặc app khác.")]
    public GameObject appRoot;

    [Tooltip("TMP nhận nội dung động từ PhoneSystem / StoryEventManager.")]
    public TextMeshProUGUI contentText;
}
