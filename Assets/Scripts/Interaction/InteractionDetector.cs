using UnityEngine;
using System.Collections.Generic;

public class InteractionDetector : MonoBehaviour
{
    private List<IInteractable> interactables = new List<IInteractable>();

    private IInteractable current;
    private IInteractable previous;

    [SerializeField] private InteractionUI interactionUI;

    // thêm: khoảng cách tối đa để hiện UI / focus
    [SerializeField] private float maxInteractDistance = 1.5f;

    string lastText = "";

    private void OnEnable()
    {
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnDisable()
    {
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    void HandleDialogueEnded()
    {
        // Khi vừa thoát hội thoại: mất focus và ẩn UI
        ClearCurrent();
        interactionUI.Hide();
        lastText = "";
    }

    private void Update()
    {
        if (DialogueSystem.Instance != null && DialogueSystem.Instance.isDialogueActive)
        {
            ClearCurrent();
            interactionUI.Hide();
            return;
        }

        UpdateClosest();
        HandleFocus();
        HandleUI();

        if (Input.GetKeyDown(KeyCode.E) && current != null)
        {
            current.Interact();
        }

        if (interactables.Count == 0)
        {
            current = null;
        }
    }

    void UpdateClosest()
    {
        float minDist = Mathf.Infinity;
        IInteractable closest = null;

        foreach (var i in interactables)
        {
            if (i == null) continue;
            MonoBehaviour mono = i as MonoBehaviour;
            if (mono == null) continue;

            float dist = Vector2.Distance(transform.position, mono.transform.position);

            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        // nếu gần nhất mà vẫn xa hơn maxInteractDistance => coi như không có gì
        if (closest != null)
        {
            MonoBehaviour mono = closest as MonoBehaviour;
            if (mono != null)
            {
                float dist = Vector2.Distance(transform.position, mono.transform.position);
                if (dist > maxInteractDistance)
                {
                    closest = null;
                }
            }
        }

        current = closest;
        interactables.RemoveAll(i => i == null);
    }

    void HandleFocus()
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

    void HandleUI()
    {
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

    private void OnTriggerEnter2D(Collider2D col)
    {
        var interact = col.GetComponent<IInteractable>();
        if (interact != null && !interactables.Contains(interact))
        {
            interactables.Add(interact);
        }
    }

    void ClearCurrent()
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

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Interactable"))
        {
            var interact = col.GetComponent<IInteractable>();
            if (interact != null)
            {
                interactables.Remove(interact);
            }
        }
    }
}