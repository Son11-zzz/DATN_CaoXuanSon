using UnityEngine;

public class StairTriggerI : MonoBehaviour
{
    public Transform targetPosition; // vị trí tầng 2

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // teleport
            other.transform.position = targetPosition.position;

            // reset velocity (tránh bug)
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }
}

