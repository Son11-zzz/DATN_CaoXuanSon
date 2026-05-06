using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    protected SpriteRenderer sr;

    protected virtual void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>(true);
    }

    public virtual void Interact()
    {
        Debug.Log("Interact with " + gameObject.name);
    }

    public virtual void OnFocus()
    {
        if (sr != null)
            sr.color = Color.yellow;
    }

    public virtual void OnLoseFocus()
    {
        if (sr != null)
            sr.color = Color.white;
    }

    public virtual string GetInteractText()
    {
        return "Nhấn E";
    }
}
