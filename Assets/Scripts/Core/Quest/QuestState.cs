using System;
using System.Collections.Generic;

[Serializable]
public class QuestProgress
{
    public string questId;
    public bool accepted;
    public bool completed;

    // progress for collect objectives: itemName -> amount
    public Dictionary<string, int> itemCounts = new Dictionary<string, int>();
}
