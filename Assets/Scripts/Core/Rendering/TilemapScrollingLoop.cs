using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class TilemapScrollingLoop : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private Transform groupA;
    [SerializeField] private Transform groupB;

    [Header("Scrolling")]
    [SerializeField] private Vector2 scrollDirection = Vector2.left;
    [SerializeField] private float scrollSpeed = 1f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Pixel Perfect")]
    [SerializeField] private bool pixelPerfect = true;
    [SerializeField] private float pixelsPerUnit = 32f;

    private float loopLength;
    private Vector3 groupAStart;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnValidate()
    {
        if (scrollDirection.sqrMagnitude > 0f)
            scrollDirection = scrollDirection.normalized;
    }

    private void LateUpdate()
    {
        if (!initialized || groupA == null || groupB == null)
            return;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float offset = Mathf.Repeat(scrollSpeed * time, loopLength);
        Vector3 direction = new Vector3(scrollDirection.x, scrollDirection.y, 0f);

        Vector3 groupAPosition = groupAStart - direction * offset;
        SetGroupPosition(groupA, groupAPosition);
        SetGroupPosition(groupB, groupAPosition + direction * loopLength);
    }

    private void Initialize()
    {
        if (groupA == null || groupB == null)
            return;

        loopLength = ResolveLoopLength(groupA);
        if (loopLength <= 0f)
            return;

        groupAStart = groupA.position;
        Vector3 direction = new Vector3(scrollDirection.x, scrollDirection.y, 0f);

        SetGroupPosition(groupA, groupAStart);
        SetGroupPosition(groupB, groupAStart + direction * loopLength);

        initialized = true;
    }

    private float ResolveLoopLength(Transform group)
    {
        Tilemap tilemap = group.GetComponentInChildren<Tilemap>();
        if (tilemap == null)
            return 0f;

        tilemap.CompressBounds();
        Bounds bounds = tilemap.localBounds;

        bool horizontal = Mathf.Abs(scrollDirection.x) >= Mathf.Abs(scrollDirection.y);
        return horizontal ? bounds.size.x : bounds.size.y;
    }

    private void SetGroupPosition(Transform group, Vector3 position)
    {
        group.position = pixelPerfect ? SnapGroupToPixel(position) : position;
    }

    private Vector3 SnapGroupToPixel(Vector3 position)
    {
        if (!pixelPerfect || pixelsPerUnit <= 0f)
            return position;

        float unit = 1f / pixelsPerUnit;
        position.x = Mathf.Round(position.x / unit) * unit;
        position.y = Mathf.Round(position.y / unit) * unit;
        return position;
    }
}