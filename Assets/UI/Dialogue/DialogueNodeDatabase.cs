using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueNodeDatabase", menuName = "Scriptable Objects/DialogueNodeDatabase")]
public class DialogueNodeDatabase : ScriptableObject
{
    [Tooltip("List of DialogueData nodes. Node id defaults to asset name.")]
    public List<DialogueData> nodes = new List<DialogueData>();

    public DialogueData GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            if (n.name == nodeId) return n;
        }

        return null;
    }
}
