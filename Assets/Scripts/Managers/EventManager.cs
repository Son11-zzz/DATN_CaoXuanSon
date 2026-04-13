using System;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    public Action OnChoiceSelected;
    public Action OnStatChanged;

    private void Awake()
    {
        Instance = this;
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
