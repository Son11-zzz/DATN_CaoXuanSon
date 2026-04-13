public class NPC : InteractableBase
{
    public DialogueData dialogueData;

    public override void Interact()
    {
        DialogueSystem.Instance.StartDialogue(dialogueData);
    }

    public override string GetInteractText()
    {
        return "Talk";
    }
}