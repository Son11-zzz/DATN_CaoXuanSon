using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class DialogueUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    [Header("Canvas Scaling")]
    [SerializeField] private bool enforceCanvasScaler = true;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

    [Header("Choices")]
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;

    private void Awake()
    {
        EnsureEventSystem();
        ConfigureCanvasScaler();
    }

    public void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    private void ConfigureCanvasScaler()
    {
        if (!enforceCanvasScaler)
        {
            return;
        }

        Canvas canvas = null;
        if (panel != null)
        {
            canvas = panel.GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            return;
        }

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (referenceResolution.x > 0f && referenceResolution.y > 0f)
        {
            scaler.referenceResolution = referenceResolution;
        }

        scaler.matchWidthOrHeight = matchWidthOrHeight;
    }

    private void Update()
    {
        var ds = DialogueSystem.Instance;
        if (panel == null || ds == null)
        {
            return;
        }

        if (!panel.activeSelf || !Input.GetKeyDown(KeyCode.Space))
        {
            return;
        }

        if (!ds.HasChoice())
        {
            ds.Next();
        }
    }

    public void Show(string message)
    {
        if (panel == null || text == null)
        {
            return;
        }

        panel.SetActive(true);
        text.text = message;
        if (text != null)
        {
            text.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
        }

        ClearChoices();
    }

    public void ShowChoices(List<DialogueChoice> choices)
    {
        if (choices == null || choiceContainer == null || choiceButtonPrefab == null)
        {
            return;
        }

        ClearChoices();

        foreach (var choice in choices)
        {
            var c = choice; // capture local

            GameObject btn = Instantiate(choiceButtonPrefab, choiceContainer);

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = c.choiceText;
            }

            var button = btn.GetComponent<Button>();
            if (button == null)
            {
                button = btn.GetComponentInChildren<Button>(true);
            }

            if (button == null)
            {
                Debug.LogWarning("DialogueUI: choiceButtonPrefab is missing a Button component.");
                continue;
            }

            button.onClick.AddListener(() =>
            {
                if (DialogueSystem.Instance != null)
                {
                    DialogueSystem.Instance.Choose(c);
                }
            });
        }
    }
    void ClearChoices()
    {
        if (choiceContainer == null)
        {
            return;
        }

        foreach (Transform child in choiceContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void Hide()
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(false);
    }
}