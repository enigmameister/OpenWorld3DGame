using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Graph Registry", fileName = "DialogueGraphRegistry")]
public class DialogueGraphRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;              // np. "Start", "OpenAccount", "NewCard"
        public DialogueGraph graph;     // asset
    }

    public List<Entry> entries = new();

    private Dictionary<string, DialogueGraph> _map;

    public DialogueGraph Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        // lazy build
        _map ??= BuildMap();
        _map.TryGetValue(key.Trim(), out var g);
        return g;
    }

    private Dictionary<string, DialogueGraph> BuildMap()
    {
        var dict = new Dictionary<string, DialogueGraph>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.key) || e.graph == null) continue;
            dict[e.key.Trim()] = e.graph;
        }
        return dict;
    }
}
