using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset;

    public Vector2 minBounds;
    public Vector2 maxBounds;

    public bool isSnapping = false;

    private Vector3 velocity = Vector3.zero;
    private Transform lastResolvedTarget;
    private float baseZ;

    private void Awake()
    {
        baseZ = transform.position.z;
    }

    void LateUpdate()
    {
        TryResolveTarget();
        if (target == null) return;

        Vector3 targetPos = target.position + offset;

        bool hasValidBounds = maxBounds.x > minBounds.x && maxBounds.y > minBounds.y;

        float x = hasValidBounds ? Mathf.Clamp(targetPos.x, minBounds.x, maxBounds.x) : targetPos.x;
        float y = hasValidBounds ? Mathf.Clamp(targetPos.y, minBounds.y, maxBounds.y) : targetPos.y;

        float z = Mathf.Approximately(offset.z, 0f) ? baseZ : offset.z;
        Vector3 finalPos = new Vector3(x, y, z);

        if (isSnapping)
        {
            transform.position = finalPos;
            isSnapping = false;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                finalPos,
                ref velocity,
                1f / smoothSpeed
            );
        }
    }

    private void TryResolveTarget()
    {
        if (target != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        target = player.transform;

        if (lastResolvedTarget != target)
        {
            lastResolvedTarget = target;
            isSnapping = true;
        }
    }

    public void SetBounds(Bounds bounds)
    {
        minBounds = bounds.min;
        maxBounds = bounds.max;

        isSnapping = true;
    }
}