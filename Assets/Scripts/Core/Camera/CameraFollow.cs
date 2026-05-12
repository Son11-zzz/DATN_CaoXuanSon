using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset;

    public Vector2 minBounds;
    public Vector2 maxBounds;

    public bool isSnapping = false;

    [Header("Target Follow")]
    [SerializeField] private bool enableTargetFollow = true;

    [Header("Auto Pan")]
    [SerializeField] private bool autoPanWhenNoTarget = false;
    [SerializeField] private Vector2 autoPanSpeed = new Vector2(0.5f, 0.25f);

    [Header("Bounds")]
    [SerializeField] private bool useTilemapBounds = false;
    [SerializeField] private Transform boundsRoot;

    [Header("Pixel Perfect")]
    [SerializeField] private bool pixelPerfect = true;
    [SerializeField] private float pixelsPerUnit = 32f;

    private Vector3 velocity = Vector3.zero;
    private Transform lastResolvedTarget;
    private float baseZ;
    private Vector2 autoPanDirection = Vector2.one;
    private Vector3 internalPosition;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        baseZ = transform.position.z;
        internalPosition = transform.position;
        TrySetBoundsFromTilemaps();
    }

    private void LateUpdate()
    {
        if (enableTargetFollow)
        {
            TryResolveTarget();
        }
        else
        {
            target = null;
        }

        if (target == null && !autoPanWhenNoTarget)
            return;

        Vector3 finalPos = GetDesiredPosition();

        Vector3 desiredPos;
        if (isSnapping || target == null)
        {
            desiredPos = finalPos;  
            isSnapping = false;
        }
        else
        {
            desiredPos = Vector3.SmoothDamp(
                internalPosition,
                finalPos,
                ref velocity,
                1f / smoothSpeed
            );
        }

        desiredPos.z = finalPos.z;
        internalPosition = desiredPos;

        if (pixelPerfect)
            desiredPos = SnapToPixelGrid(desiredPos);

        transform.position = desiredPos;
    }

    private Vector3 GetDesiredPosition()
    {
        bool hasValidBounds = maxBounds.x > minBounds.x && maxBounds.y > minBounds.y;
        float z = Mathf.Approximately(offset.z, 0f) ? baseZ : offset.z;

        float minX = minBounds.x;
        float maxX = maxBounds.x;
        float minY = minBounds.y;
        float maxY = maxBounds.y;

        if (hasValidBounds && cam != null)
        {
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;

            minX += camWidth;
            maxX -= camWidth;
            minY += camHeight;
            maxY -= camHeight;

            if (maxX < minX) { float mid = (minX + maxX) * 0.5f; minX = mid; maxX = mid; }
            if (maxY < minY) { float mid = (minY + maxY) * 0.5f; minY = mid; maxY = mid; }
        }

        if (target != null)
        {
            Vector3 targetPos = target.position + offset;
            float x = hasValidBounds ? Mathf.Clamp(targetPos.x, minX, maxX) : targetPos.x;
            float y = hasValidBounds ? Mathf.Clamp(targetPos.y, minY, maxY) : targetPos.y;
            return new Vector3(x, y, z);
        }

        Vector3 current = internalPosition;
        Vector3 next = current + new Vector3(autoPanSpeed.x * autoPanDirection.x, autoPanSpeed.y * autoPanDirection.y, 0f) * Time.deltaTime;

        if (hasValidBounds)
        {
            if (next.x <= minX)
            {
                next.x = minX;
                autoPanDirection.x = 1f;
            }
            else if (next.x >= maxX)
            {
                next.x = maxX;
                autoPanDirection.x = -1f;
            }

            if (next.y <= minY)
            {
                next.y = minY;
                autoPanDirection.y = 1f;
            }
            else if (next.y >= maxY)
            {
                next.y = maxY;
                autoPanDirection.y = -1f;
            }
        }

        next.z = z;
        return next;
    }

    private Vector3 SnapToPixelGrid(Vector3 position)
    {
        if (pixelsPerUnit <= 0f)
            return position;

        float unit = 1f / pixelsPerUnit;
        position.x = Mathf.Round(position.x / unit) * unit;
        position.y = Mathf.Round(position.y / unit) * unit;
        return position;
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

    private void TrySetBoundsFromTilemaps()
    {
        if (!useTilemapBounds)
            return;

        Transform root = boundsRoot != null ? boundsRoot : transform;
        if (root == null)
            return;

        if (!TryGetCombinedTilemapBounds(root, out Bounds bounds) && !TryGetColliderBounds(root, out bounds))
            return;

        SetBounds(bounds);
    }

    private static bool TryGetColliderBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Collider2D collider2D = root.GetComponentInChildren<Collider2D>(true);
        if (collider2D == null)
            return false;

        bounds = collider2D.bounds;
        return true;
    }

    private static bool TryGetCombinedTilemapBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        bool hasBounds = false;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.localBounds.size == Vector3.zero)
                continue;

            Bounds worldBounds = TransformBounds(tilemap.transform.localToWorldMatrix, tilemap.localBounds);

            if (!hasBounds)
            {
                bounds = worldBounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(worldBounds);
        }

        return hasBounds;
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 extents = bounds.extents;

        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

        Vector3 newExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

        return new Bounds(center, newExtents * 2f);
    }
}