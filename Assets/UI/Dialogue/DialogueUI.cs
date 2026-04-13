using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    [Header("Choices")]
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;
    public void Start()
    {
        panel.SetActive(false);
    }
    private void Update()
    {
        if (panel.activeSelf && Input.GetKeyDown(KeyCode.Space))
        {
            if (!DialogueSystem.Instance.HasChoice())
            {
                DialogueSystem.Instance.Next();
            }
        }
    }

    public void Show(string message)
    {
        panel.SetActive(true);
        text.text = message;

        ClearChoices();
    }

    public void ShowChoices(List<DialogueChoice> choices)
    {
        ClearChoices();

        foreach (var choice in choices)
        {
            var c = choice; // capture local

            GameObject btn = Instantiate(choiceButtonPrefab, choiceContainer);

            btn.GetComponentInChildren<TextMeshProUGUI>().text = c.choiceText;

            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                DialogueSystem.Instance.Choose(c);
            });
        }
    }
    void ClearChoices()
    {
        foreach (Transform child in choiceContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}