using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Graph", fileName = "DialogueGraph")]
public class DialogueGraph : ScriptableObject
{
    public List<DialogueNode> nodes = new();
    public string startNodeId = "start";

    private Dictionary<string, DialogueNode> _nodeMap;

    public DialogueNode GetNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        _nodeMap ??= BuildNodeMap();

        _nodeMap.TryGetValue(id.Trim(), out DialogueNode node);
        return node;
    }

    private Dictionary<string, DialogueNode> BuildNodeMap()
    {
        Dictionary<string, DialogueNode> map =
            new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);

        if (nodes == null)
            return map;

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNode node = nodes[i];

            if (node == null)
                continue;

            if (string.IsNullOrWhiteSpace(node.id))
                continue;

            map[node.id.Trim()] = node;
        }

        return map;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _nodeMap = null;
    }
#endif
}

[Serializable]
public class DialogueNode
{
    public string id = "start";

    [TextArea(2, 6)]
    public string npcText;

    public List<DialogueOption> options = new();

    public bool endAfterNpcLine;
}

[Serializable]
public class DialogueOption
{
    [TextArea(1, 3)]
    public string playerText;

    public string nextNodeId;

    public string debugEvent;
}