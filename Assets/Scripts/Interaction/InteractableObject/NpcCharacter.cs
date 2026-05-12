using UnityEngine;

/// <summary>
/// Gan <see cref="NpcCharacterProfile"/> cho NPC dat tay trong scene. Chay truoc <see cref="QuestGiverNPC"/> (DefaultExecutionOrder).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class NpcCharacter : MonoBehaviour
{
    [SerializeField] private NpcCharacterProfile profile;

    public NpcCharacterProfile Profile => profile;

    private void Awake()
    {
        if (profile == null)
        {
            Debug.LogWarning($"NpcCharacter '{name}': profile null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.characterId))
        {
            Debug.LogWarning($"NpcCharacter '{name}': profile '{profile.name}' thieu characterId.");
        }

        var questGiver = GetComponent<QuestGiverNPC>();
        if (questGiver != null)
        {
            questGiver.ApplyCharacterProfile(profile);
        }
        else
        {
            var simpleNpc = GetComponent<NPC>();
            if (simpleNpc != null)
            {
                simpleNpc.ApplyWorldProfileDialogues(
                    profile.characterId,
                    profile.firstTimeDialogue,
                    profile.defaultDialogue);
            }
        }

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (profile.worldSprite != null) sr.sprite = profile.worldSprite;
            sr.color = profile.spriteTint;
        }

        var mover = GetComponent<NpcSimpleMover2D>();
        if (mover != null)
        {
            if (profile.routineData != null) mover.RoutineData = profile.routineData;
            mover.MarkForceRefreshNext();
            mover.RefreshSchedule();
        }

        if (!string.IsNullOrWhiteSpace(profile.displayName))
        {
            gameObject.name = profile.displayName;
        }
    }
}
