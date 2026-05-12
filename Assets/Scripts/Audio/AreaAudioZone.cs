using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaAudioZone : MonoBehaviour
{
    [SerializeField] private AreaAudioProfile profile;

    private void OnValidate()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"AreaAudioZone '{name}' c?n Collider2D b?t IsTrigger. ?ã t? b?t.", this);
        }

        if (profile == null)
        {
            Debug.LogWarning($"AreaAudioZone '{name}' ch?a gán AreaAudioProfile.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("Không tìm th?y AudioManager trong scene.", this);
            return;
        }

        if (profile == null)
        {
            Debug.LogWarning($"AreaAudioZone '{name}' ch?a gán AreaAudioProfile.", this);
            return;
        }

        AudioManager.Instance.SetCurrentArea(profile);
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponent<PlayerController>() != null) return true;
        if (other.GetComponentInParent<PlayerController>() != null) return true;
        if (other.GetComponent<InteractionDetector>() != null) return true;
        if (other.GetComponentInParent<InteractionDetector>() != null) return true;
        return false;
    }
}
