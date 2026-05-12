using System;
using UnityEngine;

/// <summary>
/// Trạng thái "lần đầu làm quen" dùng chung cho <see cref="QuestGiverNPC"/> và <see cref="NPC"/>.
/// Đồng bộ PlayerPrefs (persist) ↔ QuestManager (TalkToNpc / runtime).
/// </summary>
public static class NpcFirstMeetDialogueState
{
    private const string KeyPrefix = "QuestGiverNPC.FirstTime.";

    private static readonly string[] LegacyFirstTimeSceneSuffixes =
    {
        "00_Bootstrap",
        "20_Town",
        "21_SchoolArea",
        "22_PlayerHouse",
    };

    private static readonly (string modernId, string legacyId)[] LegacyNpcIdPairs =
    {
        ("npc_ThayGiaoBa", "teacher_gpa_tips"),
        ("npc_LeToanThang", "ltt"),
    };

    public static string KeyForNpc(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return string.Empty;
        return KeyPrefix + npcId.Trim();
    }

    /// <summary>Id còn lại trong cặp legacy (modern ↔ tên cũ), hoặc null.</summary>
    public static string LegacyTalkAliasFor(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return null;
        string id = npcId.Trim();
        for (int p = 0; p < LegacyNpcIdPairs.Length; p++)
        {
            var (modern, legacy) = LegacyNpcIdPairs[p];
            if (string.Equals(id, modern, StringComparison.Ordinal)) return legacy;
            if (string.Equals(id, legacy, StringComparison.Ordinal)) return modern;
        }

        return null;
    }

    /// <summary>Nhập prefs dạng cũ (có tên scene / id prefab trước profile).</summary>
    public static void EnsureMigratedPlayerPrefs(string npcId)
    {
        string key = KeyForNpc(npcId);
        if (string.IsNullOrWhiteSpace(key)) return;
        if (PlayerPrefs.GetInt(key, 0) != 0) return;

        string id = npcId.Trim();
        for (int i = 0; i < LegacyFirstTimeSceneSuffixes.Length; i++)
        {
            string scene = LegacyFirstTimeSceneSuffixes[i];
            if (TryCopyLegacy(scene, id, key)) return;

            for (int p = 0; p < LegacyNpcIdPairs.Length; p++)
            {
                if (!string.Equals(id, LegacyNpcIdPairs[p].modernId, StringComparison.Ordinal)) continue;
                if (TryCopyLegacy(scene, LegacyNpcIdPairs[p].legacyId, key)) return;
            }
        }
    }

    private static bool TryCopyLegacy(string sceneSuffix, string idPart, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(idPart)) return false;
        string legacy = $"{KeyPrefix}{sceneSuffix}.{idPart}";
        if (PlayerPrefs.GetInt(legacy, 0) != 1) return false;
        PlayerPrefs.SetInt(targetKey, 1);
        return true;
    }

    public static bool IsFirstTalkRegisteredWithQuestManager(string npcId)
    {
        if (QuestManager.Instance == null || string.IsNullOrWhiteSpace(npcId)) return false;
        return QuestManager.Instance.HasTalkedToNpc(npcId.Trim());
    }

    public static void SyncPlayerPrefsAndQuestManager(string npcId)
    {
        string key = KeyForNpc(npcId);
        if (string.IsNullOrWhiteSpace(key)) return;

        bool prefsDone = PlayerPrefs.GetInt(key, 0) == 1;
        bool mgrDone = IsFirstTalkRegisteredWithQuestManager(npcId);

        if (mgrDone && !prefsDone)
            PlayerPrefs.SetInt(key, 1);
        else if (prefsDone && !mgrDone && QuestManager.Instance != null)
            QuestManager.Instance.RegisterFirstTimeDialogueCompleted(npcId.Trim());
    }

    /// <summary>Có nên mở firstTimeDialogue không (lần đầu chưa xong theo prefs hoặc quest manager).</summary>
    public static bool ShouldOfferFirstTimeIntro(string npcId, DialogueData firstTimeDialogue)
    {
        if (firstTimeDialogue == null) return false;
        string key = KeyForNpc(npcId);
        if (string.IsNullOrWhiteSpace(key)) return true;
        if (PlayerPrefs.GetInt(key, 0) == 1) return false;
        return !IsFirstTalkRegisteredWithQuestManager(npcId);
    }

    public static void MarkFirstTimeConsumed(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return;
        string key = KeyForNpc(npcId);
        if (!string.IsNullOrWhiteSpace(key))
            PlayerPrefs.SetInt(key, 1);
        QuestManager.Instance?.RegisterFirstTimeDialogueCompleted(npcId.Trim());
    }

    public static void DeleteFirstTimeKey(string npcId)
    {
        string key = KeyForNpc(npcId);
        if (!string.IsNullOrWhiteSpace(key))
            PlayerPrefs.DeleteKey(key);
    }
}
