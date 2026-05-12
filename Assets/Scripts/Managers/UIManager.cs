using UnityEngine;

public class UIManager : MonoBehaviour
{
    private void Start()
    {
        EventManager.Instance.OnStatChanged += UpdateUI;
    }

    void UpdateUI()
    {
        Debug.Log("UI Updated");
    }
}
