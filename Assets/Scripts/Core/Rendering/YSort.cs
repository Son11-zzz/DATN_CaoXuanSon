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

    private float lastY;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private float GetSortY()
    {
        // 1. Ưu tiên collider (chuẩn nhất cho object lớn)
        if (useCollider && col != null)
            return col.bounds.min.y;

        // 2. Fallback: pivot (phù hợp player, sprite pivot bottom)
        return transform.position.y;
    }

    private void LateUpdate()
    {
        float currentY = GetSortY();

        // Tối ưu: chỉ update khi có thay đổi
        if (updateOnlyWhenMoving && Mathf.Approximately(currentY, lastY))
            return;

        sr.sortingOrder = Mathf.RoundToInt(-currentY * multiplier);
        lastY = currentY;
    }
}