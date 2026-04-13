using UnityEngine;

public class InteractableObject : InteractableBase
{
    public DialogueData dialogueData;
    public DialogueData emptyDialogue;

    public bool hasItem = true;
    public ItemData itemInside;

    public bool canInteract = true;

    public override void Interact()
    {
        if (!canInteract)
        {
            return;
        }

        if (hasItem)
        {
            DialogueSystem.Instance.StartDialogue(dialogueData, this);
        }
        else
        {
            DialogueSystem.Instance.StartDialogue(emptyDialogue);
        }
    }

    public override void OnFocus()
    {
        if (!canInteract)
        {
            return;
        }

        base.OnFocus();
    }

    public override string GetInteractText()
    {
        if (!canInteract)
        {
            return string.Empty;
        }

        return base.GetInteractText();
    }

    public void DisableAfterLeave()
    {
        canInteract = false;
        OnLoseFocus();
    }
}