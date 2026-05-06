using System.Collections.Generic;
using UnityEngine;

public class InteractionDetector : MonoBehaviour
{
    private readonly List<IInteractable> interactables = new List<IInteractable>();

    private readonly Collider2D[] overlapResults = new Collider2D[32];

    private IInteractable current;
    private IInteractable previous;

    [Header("UI")]
    [SerializeField] private InteractionUI interactionUI;

    [Header("Detection")]
    [SerializeField] private float maxInteractDistance = 1.5f;
    [SerializeField] private LayerMask interactableLayers = ~0;

    private string lastText = "";

    private void Awake()
    {
        TryResolveInteractionUI();
    }

    private void OnEnable()
    {
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnded += HandleDialogueEnded;
        }

        TryResolveInteractionUI();
    }

    private void OnDisable()
    {
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    private void HandleDialogueEnded()
    {
        ClearCurrent();

        if (interactionUI != null)
        {
            interactionUI.Hide();
        }

        lastText = "";
    }

    private void Update()
    {
        TryResolveInteractionUI();

        var shop = ShopUI.ResolveInstance();
        if (shop != null && shop.IsOpen)
        {
            ClearCurrent();

            if (interactionUI != null)
            {
                interactionUI.Hide();
            }

            return;
        }

        if (EventLetterUI.Instance != null && EventLetterUI.Instance.IsOpen)
        {
            ClearCurrent();

            if (interactionUI != null)
            {
                interactionUI.Hide();
            }

            return;
        }

        if (DialogueSystem.Instance != null && DialogueSystem.Instance.IsDialogueActive)
        {
            ClearCurrent();

            if (interactionUI != null)
            {
                interactionUI.Hide();
            }

            return;
        }

        RefreshCandidates();
        UpdateClosest();
        HandleFocus();
        HandleUI();

        if (Input.GetKeyDown(KeyCode.E) && current != null)
        {
            current.Interact();
        }
    }

    private void TryResolveInteractionUI()
    {
        if (interactionUI != null) return;
        interactionUI = FindAnyObjectByType<InteractionUI>();
    }

    private void RefreshCandidates()
    {
        interactables.Clear();

        // Use OverlapCircle instead of the obsolete OverlapCircleNonAlloc
        Collider2D[] foundColliders = Physics2D.OverlapCircleAll(transform.position, maxInteractDistance, interactableLayers);
        int count = foundColliders.Length;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = foundColliders[i];
            if (col == null) continue;

            IInteractable interact = FindInteractable(col);
            if (interact == null) continue;
            if (!interactables.Contains(interact))
            {
                interactables.Add(interact);
            }
        }
    }

    private static IInteractable FindInteractable(Collider2D col)
    {
        if (col == null) return null;

        IInteractable interact = col.GetComponent<IInteractable>();
        if (interact != null) return interact;

        interact = col.GetComponentInParent<IInteractable>();
        if (interact != null) return interact;

        return col.GetComponentInChildren<IInteractable>();
    }

    private void UpdateClosest()
    {
        float minDist = Mathf.Infinity;
        IInteractable closest = null;

        for (int i = 0; i < interactables.Count; i++)
        {
            IInteractable it = interactables[i];
            if (it == null) continue;

            MonoBehaviour mono = it as MonoBehaviour;
            if (mono == null) continue;

            float dist = Vector2.Distance(transform.position, mono.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = it;
            }
        }

        current = closest;
    }

    private void HandleFocus()
    {
        if (current != previous)
        {
            if (previous != null)
            {
                previous.OnLoseFocus();
            }

            if (current != null)
            {
                current.OnFocus();
            }

            previous = current;
        }

        if (current == null && previous != null)
        {
            previous.OnLoseFocus();
            previous = null;
        }
    }

    private void HandleUI()
    {
        if (interactionUI == null) return;

        if (current != null)
        {
            string text = current.GetInteractText();

            if (text != lastText)
            {
                interactionUI.Show(text);
                lastText = text;
            }
        }
        else
        {
            interactionUI.Hide();
            lastText = "";
        }
    }

    private void ClearCurrent()
    {
        if (current != null)
        {
            current.OnLoseFocus();
            current = null;
        }

        if (previous != null)
        {
            previous.OnLoseFocus();
            previous = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxInteractDistance);
    }
}