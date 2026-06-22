using System;
using UnityEngine;

public static class DialogueMissionEventRouter
{
    public static event Action<string> OnDialogueEvent;

    public static void Trigger(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        eventKey = eventKey.Trim();

        Debug.Log($"[DialogueMissionEventRouter] Trigger: {eventKey}");

        OnDialogueEvent?.Invoke(eventKey);
    }
}