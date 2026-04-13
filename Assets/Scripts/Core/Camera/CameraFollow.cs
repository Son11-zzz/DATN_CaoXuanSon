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

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position + offset;

        float x = Mathf.Clamp(targetPos.x, minBounds.x, maxBounds.x);
        float y = Mathf.Clamp(targetPos.y, minBounds.y, maxBounds.y);

        Vector3 finalPos = new Vector3(x, y, offset.z);

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


    public void SetBounds(Bounds bounds)
    {
        minBounds = bounds.min;
        maxBounds = bounds.max;

        isSnapping = true;
    }
}