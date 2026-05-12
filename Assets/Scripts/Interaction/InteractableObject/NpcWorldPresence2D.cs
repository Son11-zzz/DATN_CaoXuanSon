using UnityEngine;

/// <summary>Ẩn / hiện collider + sprite (và tùy chọn mở rộng) cho NPC world. Tách khỏi loco để mở rộng VFX/Canvas.</summary>
[DisallowMultipleComponent]
public class NpcWorldPresence2D : MonoBehaviour
{
    public void SetWorldHidden(bool hidden)
    {
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
        {
            if (col != null) col.enabled = !hidden;
        }

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null) sr.enabled = !hidden;
        }
    }
}
