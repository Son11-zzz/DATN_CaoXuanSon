using UnityEngine;

public class CameraZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        Bounds bounds = col.bounds;

        cam.SetBounds(bounds);

        Debug.Log("Entered zone: " + gameObject.name);
    }
}
