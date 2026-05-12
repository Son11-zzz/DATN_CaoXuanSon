public interface IInteractable
{
    void Interact();

    void OnFocus();
    void OnLoseFocus();

    string GetInteractText();
}
