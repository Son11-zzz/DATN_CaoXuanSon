using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AudioTriggerZone : MonoBehaviour
{
    [SerializeField] private AudioClip sfxClip;
    [SerializeField] private bool requirePlayerTag = true;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requirePlayerTag && !other.CompareTag("Player")) return;
        if (sfxClip == null) return;
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.PlaySfx(sfxClip);
    }
}
