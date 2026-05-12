using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI quiz runtime, tự dựng layout nếu không có prefab.
/// Gọi <see cref="Show(LessonQuizData, Action{float})"/> để bắt đầu.
/// </summary>
public class LessonQuizUI : MonoBehaviour
{
    public static LessonQuizUI Instance;

    [Header("Optional explicit refs")]
    [SerializeField] private Canvas canvasRef;

    private RectTransform panel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI feedbackText;
    private readonly List<Button> optionButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> optionTexts = new List<TextMeshProUGUI>();
    private Button nextButton;

    private Canvas _sortingOwnedCanvas;
    private int _savedCanvasSortOrder = int.MinValue;
    private bool _savedCanvasOverrideSorting;

    private LessonQuizData current;
    private int currentIndex;
    private int correctCount;
    private Action<float, int> onCompleted;

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

        BuildUIIfNeeded();
        SetVisible(false);
    }

    public void Show(LessonQuizData data, Action<float> legacyCallbackOnlyScore)
        => Show(data, (score01, _) => legacyCallbackOnlyScore?.Invoke(score01));

    public void Show(LessonQuizData data, Action<float, int> callback)
    {
        if (data == null || data.questions == null || data.questions.Count == 0)
        {
            callback?.Invoke(0f, 0);
            return;
        }

        BuildUIIfNeeded();
        current = data;
        onCompleted = callback;
        currentIndex = 0;
        correctCount = 0;

        titleText.text = string.IsNullOrWhiteSpace(data.title) ? "Bài học" : data.title;
        feedbackText.text = string.IsNullOrWhiteSpace(data.intro) ? "" : data.intro;
        SetVisible(true);
        ShowQuestion();
    }

    private void SetVisible(bool visible)
    {
        if (panel == null) return;

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;

        if (canvas != null)
        {
            if (visible)
            {
                if (_savedCanvasSortOrder == int.MinValue)
                {
                    _savedCanvasSortOrder = canvas.sortingOrder;
                    _savedCanvasOverrideSorting = canvas.overrideSorting;
                    _sortingOwnedCanvas = canvas;
                }

                canvas.overrideSorting = true;
                const int modalSortOrder = 20000;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, modalSortOrder);
            }
            else
            {
                if (_sortingOwnedCanvas != null && ReferenceEquals(canvas, _sortingOwnedCanvas)
                    && _savedCanvasSortOrder != int.MinValue)
                {
                    canvas.sortingOrder = _savedCanvasSortOrder;
                    canvas.overrideSorting = _savedCanvasOverrideSorting;
                    _savedCanvasSortOrder = int.MinValue;
                    _sortingOwnedCanvas = null;
                }
            }
        }

        panel.gameObject.SetActive(visible);
    }

    private void ShowQuestion()
    {
        if (current == null || current.questions == null || currentIndex >= current.questions.Count)
        {
            FinishQuiz();
            return;
        }

        var q = current.questions[currentIndex];
        promptText.text = $"Câu {currentIndex + 1}/{current.questions.Count}: {q.prompt}";

        for (int i = 0; i < optionButtons.Count; i++)
        {
            bool hasOption = q.options != null && i < q.options.Count;
            optionButtons[i].gameObject.SetActive(hasOption);
            if (hasOption)
            {
                optionTexts[i].text = q.options[i];
                optionButtons[i].interactable = true;
            }
        }

        nextButton.gameObject.SetActive(false);
        feedbackText.text = "";
    }

    private void OnOptionSelected(int idx)
    {
        if (current == null) return;
        if (currentIndex < 0 || currentIndex >= current.questions.Count) return;

        var q = current.questions[currentIndex];
        bool correct = idx == q.correctIndex;
        if (correct) correctCount++;

        for (int i = 0; i < optionButtons.Count; i++)
        {
            optionButtons[i].interactable = false;
        }

        feedbackText.text = correct
            ? "Đúng!"
            : $"Sai. Đáp án đúng: {(q.options != null && q.correctIndex < q.options.Count ? q.options[q.correctIndex] : "")}";
        if (!string.IsNullOrWhiteSpace(q.explanation))
        {
            feedbackText.text += $"\n{q.explanation}";
        }

        nextButton.gameObject.SetActive(true);
    }

    private void OnNextClicked()
    {
        currentIndex++;
        if (currentIndex >= current.questions.Count)
        {
            FinishQuiz();
            return;
        }
        ShowQuestion();
    }

    private void FinishQuiz()
    {
        int total = current != null && current.questions != null ? current.questions.Count : 0;
        float score = total > 0 ? (float)correctCount / total : 0f;
        int wrong = Mathf.Max(0, total - correctCount);
        var cb = onCompleted;
        onCompleted = null;
        SetVisible(false);
        cb?.Invoke(score, wrong);
    }

    private void BuildUIIfNeeded()
    {
        if (panel != null) return;

        var canvas = canvasRef;
        if (canvas == null)
        {
            var canvasGo = new GameObject("LessonQuizCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }
        var canvasRt = canvas.GetComponent<RectTransform>();

        // Backdrop + panel
        var panelGo = new GameObject("QuizPanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasRt, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(900f, 680f);

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.97f);

        // Title
        titleText = CreateText(panel, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        titleText.rectTransform.sizeDelta = new Vector2(-40f, 50f);
        titleText.alignment = TextAlignmentOptions.Midline;
        titleText.fontSize = 32f;
        titleText.color = Color.white;
        titleText.text = "Bài học";

        // Prompt
        promptText = CreateText(panel, "Prompt", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        promptText.rectTransform.anchoredPosition = new Vector2(0f, -76f);
        promptText.rectTransform.sizeDelta = new Vector2(-60f, 140f);
        promptText.alignment = TextAlignmentOptions.TopLeft;
        promptText.fontSize = 22f;
        promptText.enableWordWrapping = true;
        promptText.color = Color.white;

        // Options stack
        var optionsGo = new GameObject("Options", typeof(RectTransform));
        optionsGo.transform.SetParent(panel, false);
        var optRt = optionsGo.GetComponent<RectTransform>();
        optRt.anchorMin = new Vector2(0f, 0.5f);
        optRt.anchorMax = new Vector2(1f, 0.5f);
        optRt.pivot = new Vector2(0.5f, 0.5f);
        optRt.anchoredPosition = new Vector2(0f, -40f);
        optRt.sizeDelta = new Vector2(-60f, 260f);

        var layout = optionsGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        for (int i = 0; i < 4; i++)
        {
            int capture = i;
            var btn = CreateButton(optionsGo.transform, $"Option {i + 1}", () => OnOptionSelected(capture));
            var btnRt = btn.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(0f, 52f);
            optionButtons.Add(btn);
            optionTexts.Add(btn.GetComponentInChildren<TextMeshProUGUI>());
        }

        // Feedback
        feedbackText = CreateText(panel, "Feedback", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        feedbackText.rectTransform.anchoredPosition = new Vector2(0f, 100f);
        feedbackText.rectTransform.sizeDelta = new Vector2(-60f, 90f);
        feedbackText.alignment = TextAlignmentOptions.TopLeft;
        feedbackText.fontSize = 20f;
        feedbackText.enableWordWrapping = true;
        feedbackText.color = new Color(1f, 1f, 0.65f, 1f);

        // Next button
        nextButton = CreateButton(panel, "Tiếp tục", OnNextClicked);
        var nextRt = nextButton.GetComponent<RectTransform>();
        nextRt.anchorMin = new Vector2(0.5f, 0f);
        nextRt.anchorMax = new Vector2(0.5f, 0f);
        nextRt.pivot = new Vector2(0.5f, 0f);
        nextRt.anchoredPosition = new Vector2(0f, 20f);
        nextRt.sizeDelta = new Vector2(220f, 60f);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        return tmp;
    }

    private Button CreateButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(10, 4);
        txtRt.offsetMax = new Vector2(-10, -4);
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSize = 22f;

        return btn;
    }
}
