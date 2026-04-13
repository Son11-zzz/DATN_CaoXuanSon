using UnityEngine;
using TMPro;

public class InteractionUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI actionText;

    private void Start()
    {
        Hide();
    }

    public void Show(string message)
    {
        Debug.Log("SHOW UI");
        panel.SetActive(true);
        actionText.text = message;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

        
}