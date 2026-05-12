using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class YSort : MonoBehaviour
{
    private SpriteRenderer sr;
    private Collider2D col;

    [Header("Sorting Settings")]
    [Tooltip("Độ mịn khi sort (100 = chuẩn pixel)")]
    [SerializeField] private float multiplier = 100f;

    [Tooltip("Ưu tiên dùng collider để lấy chân object")]
    [SerializeField] private bool useCollider = true;

    [Tooltip("Chỉ update khi object di chuyển (tối ưu performance)")]
    [SerializeField] private bool updateOnlyWhenMoving = true;

    [Header("Pixel Perfect")]
    [SerializeField] private bool pixelPerfect = true;
    [SerializeField] private float pixelsPerUnit = 32f;

    private float lastY;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private float GetSortY()
    {
        float y = useCollider && col != null ? col.bounds.min.y : transform.position.y;
        return pixelPerfect ? SnapToPixelGrid(y) : y;
    }

    private float SnapToPixelGrid(float y)
    {
        if (pixelsPerUnit <= 0f)
            return y;

        float unit = 1f / pixelsPerUnit;
        return Mathf.Round(y / unit) * unit;
    }

    private void LateUpdate()
    {
        float currentY = GetSortY();

        if (updateOnlyWhenMoving && Mathf.Approximately(currentY, lastY))
            return;

        sr.sortingOrder = Mathf.RoundToInt(-currentY * multiplier);
        lastY = currentY;
    }
}