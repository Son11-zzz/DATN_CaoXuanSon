using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class QuestManager
{
    /// <inheritdoc cref="Persist_ApplyQuestPayload"/>
    public IReadOnlyList<QuestData> Persist_AllConfiguredQuestAssets => allQuests;

    public QuestData Persist_FindQuestByStableId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        string needle = id.Trim();

        if (firstDayQuest != null && string.Equals(firstDayQuest.GetId(), needle, StringComparison.Ordinal))
        {
            return firstDayQuest;
        }

        if (firstDayQuest != null && string.Equals(firstDayQuest.name, needle, StringComparison.Ordinal))
        {
            return firstDayQuest;
        }

        for (int i = 0; i < activeQuests.Count; i++)
        {
            var q = activeQuests[i];
            if (q != null && (string.Equals(q.GetId(), needle, StringComparison.Ordinal) || string.Equals(q.name, needle, StringComparison.Ordinal)))
            {
                return q;
            }
        }

        for (int i = 0; i < allQuests.Count; i++)
        {
            var q = allQuests[i];
            if (q != null && (string.Equals(q.GetId(), needle, StringComparison.Ordinal) || string.Equals(q.name, needle, StringComparison.Ordinal)))
            {
                return q;
            }
        }

        return null;
    }

    public QuestPayload CaptureQuestPayload()
    {
        var payload = new QuestPayload();

        var progList = new List<QuestProgressPayload>(progress.Count);
        foreach (var kv in progress)
        {
            if (kv.Value == null || string.IsNullOrWhiteSpace(kv.Value.questId)) continue;
            progList.Add(new QuestProgressPayload
            {
                questId = kv.Value.questId,
                accepted = kv.Value.accepted,
                completed = kv.Value.completed
            });
        }

        payload.progress = progList.OrderBy(p => p.questId, StringComparer.Ordinal).ToArray();

        payload.activeQuestIdsOrdered = activeQuests
            .Where(q => q != null)
            .Select(q => q.GetId())
            .ToArray();

        payload.readyToTurnInQuestIds = readyToTurnIn
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        payload.firstTimeNpcTalkedIds = firstTimeNpcTalked
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        payload.firstDayQuestPrefsAssigned = ReadFirstDayPlayerPrefsAssigned();
        return payload;
    }

    public void Persist_ApplyQuestPayload(QuestPayload p)
    {
        if (p == null)
        {
            return;
        }

        progress.Clear();
        activeQuests.Clear();
        readyToTurnIn.Clear();
        firstTimeNpcTalked.Clear();

        if (p.progress != null)
        {
            for (int i = 0; i < p.progress.Length; i++)
            {
                var block = p.progress[i];
                if (block == null || string.IsNullOrWhiteSpace(block.questId)) continue;

                progress[block.questId] = new QuestProgress
                {
                    questId = block.questId,
                    accepted = block.accepted,
                    completed = block.completed,
                    itemCounts = new Dictionary<string, int>()
                };
            }
        }

        if (p.activeQuestIdsOrdered != null)
        {
            for (int i = 0; i < p.activeQuestIdsOrdered.Length; i++)
            {
                var q = Persist_FindQuestByStableId(p.activeQuestIdsOrdered[i]);
                if (q != null && !activeQuests.Contains(q))
                {
                    activeQuests.Add(q);
                }
            }
        }

        if (p.readyToTurnInQuestIds != null)
        {
            for (int i = 0; i < p.readyToTurnInQuestIds.Length; i++)
            {
                readyToTurnIn.Add(p.readyToTurnInQuestIds[i]);
            }
        }

        if (p.firstTimeNpcTalkedIds != null)
        {
            for (int i = 0; i < p.firstTimeNpcTalkedIds.Length; i++)
            {
                string id = p.firstTimeNpcTalkedIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    firstTimeNpcTalked.Add(id.Trim());
                }
            }
        }

        if (firstDayQuest != null)
        {
            string key = Persist_FirstDayPlayerPrefsKey();
            if (p.firstDayQuestPrefsAssigned)
            {
                PlayerPrefs.SetInt(key, 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
            firstDayQuestChecked = false;
            TryAutoAssignFirstDayQuest();
            firstDayQuestChecked = firstDayQuestChecked || p.firstDayQuestPrefsAssigned || IsAccepted(firstDayQuest);
        }

        Recalculate();
    }

    /// <summary>Quest + prefs reset for Main Menu « New Game » while staying in-session.</summary>
    public void ResetRuntimeQuestStateForSave()
    {
        progress.Clear();
        activeQuests.Clear();
        readyToTurnIn.Clear();
        firstTimeNpcTalked.Clear();
        firstDayQuestChecked = false;

        if (firstDayQuest != null)
        {
            PlayerPrefs.DeleteKey(Persist_FirstDayPlayerPrefsKey());
            PlayerPrefs.Save();
        }

        OnQuestUpdated?.Invoke();
    }

    bool ReadFirstDayPlayerPrefsAssigned()
    {
        if (firstDayQuest == null) return false;
        string key = Persist_FirstDayPlayerPrefsKey();
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    string Persist_FirstDayPlayerPrefsKey()
    {
        return $"QuestManager.FirstDayAssigned.{firstDayQuest.GetId()}";
    }
}
