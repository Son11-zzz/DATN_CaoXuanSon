using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapGroupScrollingLoop : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private Transform groupA;
    [SerializeField] private Transform groupB;

    [Header("Size Reference")]
    [SerializeField] private Tilemap referenceTilemap;

    [Tooltip("Bật để set kích thước loop thủ công")]
    [SerializeField] private bool useCustomLoopSize = false;

    [SerializeField] private Vector2 customLoopSize = new Vector2(20f, 10f);

    [Header("Scrolling Settings")]
    [SerializeField] private Vector2 speed = new Vector2(-0.2f, 0f);

    private Vector2 loopSize;

    private void Awake()
    {
        if (groupA == null || groupB == null)
        {
            Debug.LogError("TilemapGroupScrollingLoop: Missing group references.");
            enabled = false;
            return;
        }

        if (useCustomLoopSize)
        {
            loopSize = customLoopSize;
            return;
        }

        if (referenceTilemap == null)
        {
            Debug.LogError("TilemapGroupScrollingLoop: Missing reference tilemap.");
            enabled = false;
            return;
        }

        Vector3 size = referenceTilemap.localBounds.size;
        loopSize = new Vector2(size.x, size.y);
    }

    private void Update()
    {
        Vector3 delta = new Vector3(speed.x, speed.y, 0f) * Time.deltaTime;

        groupA.localPosition += delta;
        groupB.localPosition += delta;

        Wrap(groupA, groupB);
        Wrap(groupB, groupA);
    }

    private void Wrap(Transform moving, Transform other)
    {
        if (speed.x < 0f && moving.localPosition.x <= other.localPosition.x - loopSize.x)
        {
            moving.localPosition = new Vector3(other.localPosition.x + loopSize.x, moving.localPosition.y, moving.localPosition.z);
        }
        else if (speed.x > 0f && moving.localPosition.x >= other.localPosition.x + loopSize.x)
        {
            moving.localPosition = new Vector3(other.localPosition.x - loopSize.x, moving.localPosition.y, moving.localPosition.z);
        }

        if (speed.y < 0f && moving.localPosition.y <= other.localPosition.y - loopSize.y)
        {
            moving.localPosition = new Vector3(moving.localPosition.x, other.localPosition.y + loopSize.y, moving.localPosition.z);
        }
        else if (speed.y > 0f && moving.localPosition.y >= other.localPosition.y + loopSize.y)
        {
            moving.localPosition = new Vector3(moving.localPosition.x, other.localPosition.y - loopSize.y, moving.localPosition.z);
        }
    }
}