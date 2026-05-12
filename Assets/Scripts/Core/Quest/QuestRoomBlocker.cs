using UnityEngine;

public class QuestRoomBlocker : MonoBehaviour
{
    [SerializeField] private QuestData questToUnlock;
    [SerializeField] private Collider2D blockerCollider;
    [SerializeField] private GameObject[] blockerObjects;
    [SerializeField] private DialogueData blockedDialogue;
    [SerializeField] private float dialogueCooldown = 1.5f;
    [SerializeField] private bool toggleChildColliders = true;
    [SerializeField] private bool disableGameObjectOnUnlock;

    private float lastDialogueTime = -999f;
    private bool subscribed;
    private bool hasBlockedState;
    private bool lastBlockedState;

    private void Awake()
    {
        if (blockerCollider == null)
        {
            blockerCollider = GetComponent<Collider2D>();
        }

        EnsureSensors();
    }

    private void Reset()
    {
        blockerCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        subscribed = false;
        TrySubscribe();

        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!subscribed)
        {
            TrySubscribe();
        }

        if (QuestManager.Instance != null && questToUnlock != null)
        {
            bool blocked = IsBlocked();
            if (!hasBlockedState || blocked != lastBlockedState)
            {
                Refresh();
            }
        }
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
            QuestManager.Instance.OnQuestUpdated += Refresh;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
        }

        subscribed = false;
    }

    private void Refresh()
    {
        bool block = IsBlocked();
        hasBlockedState = true;
        lastBlockedState = block;

        if (disableGameObjectOnUnlock && !block)
        {
            gameObject.SetActive(false);
            return;
        }

        if (blockerCollider != null)
        {
            blockerCollider.enabled = block;
        }
        else if (toggleChildColliders)
        {
            var colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = block;
            }
        }

        if (toggleChildColliders)
        {
            var parentColliders = GetComponentsInParent<Collider2D>(true);
            for (int i = 0; i < parentColliders.Length; i++)
            {
                if (parentColliders[i] != null && parentColliders[i].gameObject != gameObject)
                {
                    parentColliders[i].enabled = block;
                }
            }
        }

        if (blockerObjects != null)
        {
            for (int i = 0; i < blockerObjects.Length; i++)
            {
                if (blockerObjects[i] != null)
                {
                    blockerObjects[i].SetActive(block);
                    if (toggleChildColliders)
                    {
                        var colliders = blockerObjects[i].GetComponentsInChildren<Collider2D>(true);
                        for (int j = 0; j < colliders.Length; j++)
                        {
                            colliders[j].enabled = block;
                        }
                    }
                }
            }
        }
    }

    private bool IsBlocked()
    {
        if (QuestManager.Instance == null || questToUnlock == null) return true;
        return !QuestManager.Instance.IsCompleted(questToUnlock);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleContact(other);
    }

    public void HandleContact(Collider2D other)
    {
        TryShowBlockedDialogue(other);
    }

    private void TryShowBlockedDialogue(Collider2D other)
    {
        if (other == null) return;
        if (!IsPlayerCollider(other)) return;
        if (!IsBlocked()) return;
        if (blockedDialogue == null) return;
        if (DialogueSystem.Instance == null || DialogueSystem.Instance.IsDialogueActive) return;
        if (Time.time - lastDialogueTime < dialogueCooldown) return;

        lastDialogueTime = Time.time;
        DialogueSystem.Instance.StartDialogue(blockedDialogue, null);
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other.CompareTag("Player")) return true;
        return other.GetComponentInParent<PlayerController>() != null;
    }

    private void EnsureSensors()
    {
        var colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;

            var sensor = colliders[i].GetComponent<QuestRoomBlockerSensor>();
            if (sensor == null)
            {
                sensor = colliders[i].gameObject.AddComponent<QuestRoomBlockerSensor>();
            }

            sensor.SetOwner(this);
        }
    }
}

public class QuestRoomBlockerSensor : MonoBehaviour
{
    private QuestRoomBlocker owner;

    public void SetOwner(QuestRoomBlocker blocker)
    {
        owner = blocker;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.HandleContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        owner?.HandleContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        owner?.HandleContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        owner?.HandleContact(collision.collider);
    }
}
