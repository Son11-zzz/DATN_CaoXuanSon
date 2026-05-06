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
        if (!canInteract) return;

        var ds = ResolveDialogueSystem();
        if (ds == null)
        {
            Debug.LogWarning($"InteractableObject '{name}': DialogueSystem not found in loaded scenes.");
            return;
        }

        if (hasItem)
        {
            if (dialogueData == null)
            {
                Debug.LogWarning($"InteractableObject '{name}': Missing dialogueData.");
                return;
            }

            ds.StartDialogue(dialogueData, this);
        }
        else
        {
            if (emptyDialogue == null)
            {
                Debug.LogWarning($"InteractableObject '{name}': Missing emptyDialogue.");
                return;
            }

            ds.StartDialogue(emptyDialogue, this);
        }
    }

    private static DialogueSystem ResolveDialogueSystem()
    {
        if (DialogueSystem.Instance != null) return DialogueSystem.Instance;

        var systems = FindObjectsByType<DialogueSystem>(FindObjectsInactive.Include);
        return systems != null && systems.Length > 0 ? systems[0] : null;
    }

    public override void OnFocus()
    {
        if (!canInteract) return;
        base.OnFocus();
    }

    public override string GetInteractText()
    {
        if (!canInteract) return string.Empty;
        return base.GetInteractText();
    }

    public void DisableAfterLeave()
    {
        canInteract = false;
        OnLoseFocus();
    }
}