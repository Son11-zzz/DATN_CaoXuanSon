using UnityEngine;

public class PersistentPlayer : MonoBehaviour
{
    public static PersistentPlayer Instance;

    private Rigidbody2D cachedRb;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        cachedRb = GetComponent<Rigidbody2D>();

        DontDestroyOnLoad(gameObject);
    }

    public void TeleportTo(Vector3 position, bool resetVelocity = true)
    {
        transform.position = position;

        if (resetVelocity && cachedRb != null)
        {
            cachedRb.linearVelocity = Vector2.zero;
            cachedRb.angularVelocity = 0f;
        }
    }
}