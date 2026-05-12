using System;
using UnityEngine;

/// <summary>Phân loại khi cùng scene có nhiều anchor cho cùng NPC (bình minh / sau giờ / lối vào-ra trường).</summary>
public enum NpcSceneAnchorKind
{
    Default = 0,
    TownMorningHome = 1,
    TownFromSchool = 2,
    SchoolEntrance = 3,
    SchoolExit = 4
}

/// <summary>
/// Anchor trong scene cho NPC runtime. Gán theo characterId.
/// </summary>
public class NpcSceneAnchor2D : MonoBehaviour
{
    [Tooltip("Định danh NPC: trùng NpcRuntimeBootstrap entry npcId (vd. ltt) hoặc Profile.characterId (vd. npc_LeToanThang) nếu có gắn cả hai.")]
    [SerializeField] private string npcId;

    [Tooltip("Nếu trống sẽ dùng cho mọi scene. Nếu có giá trị thì phải trùng active scene name.")]
    [SerializeField] private string sceneName;

    [SerializeField] private bool isDefault;

    [SerializeField] private NpcSceneAnchorKind anchorKind = NpcSceneAnchorKind.Default;

    public NpcSceneAnchorKind AnchorKind => anchorKind;

    public string NpcId => npcId != null ? npcId.Trim() : string.Empty;
    public bool IsDefault => isDefault;

    public bool Matches(string expectedNpcId, string activeSceneName)
    {
        if (string.IsNullOrWhiteSpace(expectedNpcId)) return false;
        if (!string.Equals(NpcId, expectedNpcId, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(sceneName)) return true;
        return string.Equals(sceneName.Trim(), activeSceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Khớp với id trong bootstrap (npcId ngắn) hoặc <see cref="NpcCharacterProfile.characterId"/> trên prefab nếu khác nhau.
    /// </summary>
    public bool MatchesBootstrapOrProfile(string bootstrapEntryId, string profileCharacterId, string activeSceneName)
    {
        if (!string.IsNullOrWhiteSpace(bootstrapEntryId) && Matches(bootstrapEntryId, activeSceneName)) return true;
        if (!string.IsNullOrWhiteSpace(profileCharacterId) && Matches(profileCharacterId, activeSceneName)) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isDefault ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
