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

    [Header("Choices")]
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;

    private void Awake()
    {
        EnsureEventSystem();
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