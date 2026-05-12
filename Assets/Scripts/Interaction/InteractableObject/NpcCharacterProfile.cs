using UnityEngine;

public enum NpcRoutineArchetype
{
    Classmate,
    Teacher,
    StationaryVendor
}

/// <summary>Định nghĩa nhân vật: id, dialogue, hình, archetype, routine data-driven.</summary>
[CreateAssetMenu(fileName = "NpcCharacterProfile", menuName = "Scriptable Objects/NPC/NpcCharacterProfile")]
public class NpcCharacterProfile : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Id duy nhất (TalkToNpc, PlayerPrefs). VD: npc_lan")]
    public string characterId;

    [Tooltip("Tên hiển thị khi tương tác")]
    public string displayName;

    [Header("Visual (optional)")]
    public Sprite worldSprite;
    public Color spriteTint = Color.white;

    [Header("Quest & Dialogue")]
    public QuestData quest;
    public DialogueData firstTimeDialogue;
    public DialogueData inProgressDialogue;
    public DialogueData turnInDialogue;

    [Tooltip("Hội thoại thường sau khi quest đã xong (hoặc khi không cần nhánh pre-accept).")]
    public DialogueData defaultDialogue;

    [Tooltip("Sau first-time, nếu quest gắn profile chưa được nhận: ưu tiên hội thoại này thay vì defaultDialogue (vd: đề nghị nhận quest). Để trống = dùng defaultDialogue.")]
    public DialogueData preAcceptDialogue;

    [Header("Routine")]
    [Tooltip("Lớp: học sinh / giáo viên / quầy tĩnh (giờ mở trong NpcSimpleRoutineData).")]
    public NpcRoutineArchetype routineArchetype = NpcRoutineArchetype.Classmate;

    [Tooltip("Lịch theo giờ + id điểm trong NpcScenePointRegistry (mỗi scene).")]
    public NpcSimpleRoutineData routineData;
}
