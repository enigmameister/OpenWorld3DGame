using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueGraphUI : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private DialogueWindowUI window;

    [Header("End Nodes")]
    [SerializeField] private bool autoCloseEndNodes = true;
    [SerializeField] private float endNodeCloseDelay = 1.4f;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private DialogueGraph currentGraph;
    private DialogueNode currentNode;
    private string currentSpeakerName = "NPC";

    private readonly List<DialogueOption> visibleOptions = new();
    private readonly List<string> visibleOptionTexts = new();
    private readonly List<string> pendingDialogueEvents = new();

    private Coroutine closeCoroutine;

    private bool isOpen;
    private bool waitingForChoice;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (window == null)
            window = FindFirstObjectByType<DialogueWindowUI>(FindObjectsInactive.Include);

        CloseImmediate();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }
    }

    public void Open(DialogueGraph graph, string speakerName, MonoBehaviour owner = null)
    {
        if (graph == null)
        {
            Debug.LogWarning("[DialogueGraphUI] Tried to open null graph.");
            return;
        }

        if (window == null)
            window = FindFirstObjectByType<DialogueWindowUI>(FindObjectsInactive.Include);

        if (window == null)
        {
            Debug.LogWarning("[DialogueGraphUI] DialogueWindowUI missing.");
            return;
        }

        StopRunningCoroutines();

        isOpen = true;
        waitingForChoice = false;

        currentGraph = graph;
        currentSpeakerName = string.IsNullOrWhiteSpace(speakerName) ? "NPC" : speakerName;

        pendingDialogueEvents.Clear();
        visibleOptions.Clear();
        visibleOptionTexts.Clear();

        window.OpenWindow(clearHistory: true, lockPlayer: true);

        DialogueNode start = currentGraph.GetNode(currentGraph.startNodeId);

        if (start == null && currentGraph.nodes != null && currentGraph.nodes.Count > 0)
            start = currentGraph.nodes[0];

        GoToNode(start);
    }

    private void GoToNode(string nodeId)
    {
        if (currentGraph == null)
            return;

        DialogueNode node = currentGraph.GetNode(nodeId);

        if (node == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[DialogueGraphUI] Node not found: {nodeId}");

            Close();
            return;
        }

        GoToNode(node);
    }

    private void GoToNode(DialogueNode node)
    {
        currentNode = node;

        if (currentNode == null)
        {
            Close();
            return;
        }

        StopRunningCoroutines();

        waitingForChoice = false;
        visibleOptions.Clear();
        visibleOptionTexts.Clear();

        if (window != null)
            window.HideOptions();

        window.TypeLine(currentSpeakerName, currentNode.npcText, false, () =>
        {
            if (!isOpen)
                return;

            if (currentNode.endAfterNpcLine)
            {
                if (autoCloseEndNodes)
                    closeCoroutine = StartCoroutine(CloseAfterDelay(endNodeCloseDelay));

                return;
            }

            BuildVisibleOptions();

            if (visibleOptions.Count <= 0)
            {
                if (autoCloseEndNodes)
                    closeCoroutine = StartCoroutine(CloseAfterDelay(endNodeCloseDelay));

                return;
            }

            waitingForChoice = true;

            window.ShowOptions(visibleOptionTexts, SelectOption);
        });
    }

    private void BuildVisibleOptions()
    {
        visibleOptions.Clear();
        visibleOptionTexts.Clear();

        if (currentNode == null || currentNode.options == null)
            return;

        for (int i = 0; i < currentNode.options.Count; i++)
        {
            DialogueOption option = currentNode.options[i];

            if (option == null)
                continue;

            if (string.IsNullOrWhiteSpace(option.playerText))
                continue;

            visibleOptions.Add(option);
            visibleOptionTexts.Add(option.playerText);
        }
    }

    private void SelectOption(int visibleIndex)
    {
        if (!isOpen)
            return;

        if (!waitingForChoice)
            return;

        if (visibleIndex < 0 || visibleIndex >= visibleOptions.Count)
            return;

        DialogueOption option = visibleOptions[visibleIndex];

        waitingForChoice = false;

        if (window != null)
            window.HideOptions();

        window.TypeLine("PLAYER", option.playerText, true, () =>
        {
            if (!isOpen)
                return;

            if (!string.IsNullOrWhiteSpace(option.debugEvent))
                QueueDialogueEvent(option.debugEvent);

            if (string.IsNullOrWhiteSpace(option.nextNodeId))
            {
                Close();
                return;
            }

            GoToNode(option.nextNodeId);
        });
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Close();
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        waitingForChoice = false;

        StopRunningCoroutines();

        currentGraph = null;
        currentNode = null;

        visibleOptions.Clear();
        visibleOptionTexts.Clear();

        if (window != null)
            window.CloseWindow(unlockPlayer: true);

        TriggerPendingDialogueEvents();
    }

    public void CloseImmediate()
    {
        isOpen = false;
        waitingForChoice = false;

        StopRunningCoroutines();

        currentGraph = null;
        currentNode = null;

        visibleOptions.Clear();
        visibleOptionTexts.Clear();
        pendingDialogueEvents.Clear();

        if (window != null)
            window.CloseWindowImmediate();
    }

    private void StopRunningCoroutines()
    {
        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
    }

    private void QueueDialogueEvent(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return;

        eventKey = eventKey.Trim();

        if (!pendingDialogueEvents.Contains(eventKey))
            pendingDialogueEvents.Add(eventKey);

        if (debugLogs)
            Debug.Log($"[DialogueGraphUI] Queued dialogue event: {eventKey}");
    }

    private void TriggerPendingDialogueEvents()
    {
        if (pendingDialogueEvents.Count == 0)
            return;

        for (int i = 0; i < pendingDialogueEvents.Count; i++)
        {
            string eventKey = pendingDialogueEvents[i];

            if (string.IsNullOrWhiteSpace(eventKey))
                continue;

            if (debugLogs)
                Debug.Log($"[DialogueGraphUI] Trigger pending event after dialogue close: {eventKey}");

            DialogueMissionEventRouter.Trigger(eventKey);
        }

        pendingDialogueEvents.Clear();
    }
}