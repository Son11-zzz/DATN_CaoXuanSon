using TMPro;
using UnityEngine;

public class InteractionUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI actionText;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        if (actionText == null)
        {
            actionText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void Start()
    {
        Hide();
    }

    public void Show(string message)
    {
        if (panel == null || actionText == null) return;

        panel.SetActive(true);
        actionText.text = message;
    }

    public void Hide()
    {
        if (panel == null) return;
        panel.SetActive(false);
    }
}