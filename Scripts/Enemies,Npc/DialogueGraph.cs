using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Graph", fileName = "DialogueGraph")]
public class DialogueGraph : ScriptableObject
{
    public List<DialogueNode> nodes = new();
    public string startNodeId = "start";

    public DialogueNode GetNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return nodes.Find(n => n != null && n.id == id);
    }
}

[Serializable]
public class DialogueNode
{
    public string id = "start";

    [TextArea(2, 6)]
    public string npcText;

    public List<DialogueOption> options = new(); // 0..4

    // je¿eli true, po npcText od razu koniec rozmowy (bez opcji)
    public bool endAfterNpcLine;
}

[Serializable]
public class DialogueOption
{
    [TextArea(1, 3)]
    public string playerText;

    // id nastêpnego node'a; puste = zakoñcz dialog po wyborze
    public string nextNodeId;

    // opcjonalnie: event/akcja
    public string debugEvent;
}
