using UnityEngine;

public class Door : InteractableBase
{
    public override void Interact()
    {
        Debug.Log("Door opened");
    }

    public override string GetInteractText()
    {
        return "Open (E)";
    }
}
