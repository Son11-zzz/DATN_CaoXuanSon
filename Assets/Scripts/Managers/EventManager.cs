using System;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    public Action OnChoiceSelected;
    public Action OnStatChanged;

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

    public void TriggerChoice()
    {
        OnChoiceSelected?.Invoke();
    }

    public void NotifyStatChanged()
    {
        OnStatChanged?.Invoke();
    }
}
