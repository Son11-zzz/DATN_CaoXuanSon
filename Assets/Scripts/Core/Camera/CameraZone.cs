using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CameraZone : MonoBehaviour
{
    private BoxCollider2D col;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        // Nếu player spawn sẵn trong zone thì set bounds ngay
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        if (col != null && col.OverlapPoint(player.transform.position))
        {
            ApplyBounds();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        ApplyBounds();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Hữu ích nếu spawn/teleport không kích hoạt Enter đúng lúc
        if (!other.CompareTag("Player")) return;
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (Camera.main == null) return;

        var camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow == null) return;

        if (col == null)
        {
            col = GetComponent<BoxCollider2D>();
            if (col == null) return;
        }

        camFollow.SetBounds(col.bounds);
    }
}
