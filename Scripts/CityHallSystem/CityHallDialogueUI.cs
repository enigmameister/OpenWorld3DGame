using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CityHallDialogueUI : MonoBehaviour
{
    [Header("Citizen ID Application")]
    [SerializeField] private CitizenIdApplicationUI citizenIdApplicationUI;

    [Header("File Pickup")]
    [SerializeField] private FilePickupSequence filePickupSequence;

    [Header("Shared Dialogue Window")]
    [SerializeField] private DialogueWindowUI window;

    [Header("Optional Graph Registry")]
    [SerializeField] private DialogueGraphRegistry registry;

    [Header("Dialogue Timing")]
    [SerializeField, Min(0f)] private float npcPostDelay = 0.25f;
    [SerializeField, Min(0f)] private float playerPostDelay = 0.55f;

    [Header("Visit Check")]
    [SerializeField, Min(0f)] private float visitCheckDuration = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public bool IsOpen { get; private set; }

    public event System.Action DialogueClosed;

    private DialogueGraph currentGraph;
    private DialogueNode currentNode;

    private string currentNpcName = "CITY HALL EMPLOYEE";

    private bool waitingForChoice;
    private bool currentLineIsPlayer;

    private Coroutine postDelayCoroutine;
    private Coroutine visitCheckCoroutine;
    private Coroutine filePickupCheckCoroutine;

    private void Awake()
    {
        ResolveWindow();

        if (filePickupSequence == null)
        {
            filePickupSequence =
                FindFirstObjectByType<FilePickupSequence>(
                    FindObjectsInactive.Include
                );
        }

        if (window != null)
            window.CloseWindowImmediate();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (EscapePressedThisFrame())
        {
            Close();
            return;
        }
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void OpenDialogue(
    DialogueGraph graph,
    string npcDisplayName)
    {
        OpenDialogue(
            graph,
            npcDisplayName,
            CityHallEmployee.Role.Receptionist
        );
    }

    public void OpenDialogue(
        DialogueGraph graph,
        string npcDisplayName,
        CityHallEmployee.Role employeeRole)
    {
        if (graph == null)
        {
            Debug.LogWarning("[CITY HALL DIALOGUE] DialogueGraph is missing.");
            return;
        }

        if (!ResolveWindow())
        {
            Debug.LogWarning("[CITY HALL DIALOGUE] DialogueWindowUI is missing.");
            return;
        }

        StopActiveCoroutines();

        currentGraph = graph;
        currentNpcName = string.IsNullOrWhiteSpace(npcDisplayName)
            ? "CITY HALL EMPLOYEE"
            : npcDisplayName;

        IsOpen = true;
        waitingForChoice = false;

        window.OpenWindow(
            clearHistory: true,
            lockPlayer: true
        );

        DialogueNode startNode = currentGraph.GetNode(currentGraph.startNodeId);

        if (startNode == null &&
            currentGraph.nodes != null &&
            currentGraph.nodes.Count > 0)
        {
            startNode = currentGraph.nodes[0];
        }

        if (employeeRole == CityHallEmployee.Role.CitizenIdClerk)
        {
            string existingApplicationNodeId =
                GetCitizenIdExistingApplicationNode();

            if (!string.IsNullOrWhiteSpace(existingApplicationNodeId))
            {
                DialogueNode existingApplicationNode =
                    currentGraph.GetNode(existingApplicationNodeId);

                if (existingApplicationNode != null)
                {
                    startNode = existingApplicationNode;

                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[CITY HALL DIALOGUE] Existing Citizen ID state: " +
                            $"{existingApplicationNodeId}"
                        );
                    }
                }
            }
        }

        if (startNode == null)
        {
            Debug.LogWarning(
                $"[CITY HALL DIALOGUE] Graph '{currentGraph.name}' has no start node."
            );

            Close();
            return;
        }

        if (debugLogs)
        {
            Debug.Log(
                $"[CITY HALL DIALOGUE] Opened graph={currentGraph.name}, " +
                $"npc={currentNpcName}, start={startNode.id}"
            );
        }

        GoToNode(startNode);
    }

    public void OpenDialogueFromRegistry(
      string graphKey,
      string npcDisplayName,
      CityHallEmployee.Role employeeRole =
          CityHallEmployee.Role.Receptionist)
    {
        if (registry == null)
        {
            Debug.LogWarning(
                "[CITY HALL DIALOGUE] Registry is missing."
            );

            return;
        }

        DialogueGraph graph = registry.Get(graphKey);

        if (graph == null)
        {
            Debug.LogWarning(
                $"[CITY HALL DIALOGUE] Graph key not found: {graphKey}"
            );

            return;
        }

        OpenDialogue(
            graph,
            npcDisplayName,
            employeeRole
        );
    }

    public void Close()
    {
        bool wasOpen = IsOpen;

        StopActiveCoroutines();

        waitingForChoice = false;
        currentNode = null;
        currentGraph = null;

        IsOpen = false;

        if (window != null)
            window.CloseWindow(unlockPlayer: true);

        if (wasOpen)
            DialogueClosed?.Invoke();

        if (debugLogs)
            Debug.Log("[CITY HALL DIALOGUE] Dialogue closed.");
    }

    // =========================================================
    // NODE FLOW
    // =========================================================

    private void GoToNode(DialogueNode node)
    {
        if (!IsOpen || node == null)
            return;

        currentNode = node;
        waitingForChoice = false;

        HideOptions();

        if (debugLogs)
        {
            Debug.Log(
                $"[CITY HALL DIALOGUE] Node: {currentNode.id}"
            );
        }

        TypeLine(
            currentNpcName,
            currentNode.npcText,
            isPlayerLine: false,
            onDone: () =>
            {
                if (!IsOpen || currentNode == null)
                    return;

                if (currentNode.endAfterNpcLine)
                {
                    waitingForChoice = false;
                    TryRunAutomaticEventOption(currentNode);
                    return;
                }

                ShowOptions(currentNode.options);
                waitingForChoice = true;
            }
        );
    }

    private void ShowOptions(List<DialogueOption> options)
    {
        if (window == null)
            return;

        if (options == null || options.Count == 0)
        {
            waitingForChoice = false;
            return;
        }

        List<string> texts = new List<string>();

        for (int i = 0; i < options.Count; i++)
        {
            DialogueOption option = options[i];

            texts.Add(option != null
                ? option.playerText
                : string.Empty);
        }

        window.ShowOptions(texts, OnOptionSelected);
    }

    private void HideOptions()
    {
        if (window != null)
            window.HideOptions();
    }

    private void OnOptionSelected(int index)
    {
        if (!IsOpen ||
            !waitingForChoice ||
            currentNode == null ||
            currentNode.options == null)
        {
            return;
        }

        if (index < 0 || index >= currentNode.options.Count)
            return;

        DialogueOption option = currentNode.options[index];

        if (option == null)
            return;

        waitingForChoice = false;
        HideOptions();

        bool silentOption = string.IsNullOrWhiteSpace(option.playerText);

        if (silentOption)
        {
            ProcessOption(option);
            return;
        }

        TypeLine(
            "PLAYER",
            option.playerText,
            isPlayerLine: true,
            onDone: () => ProcessOption(option)
        );
    }

    private void ProcessOption(DialogueOption option)
    {
        if (!IsOpen || option == null)
            return;

        string nextToken = option.nextNodeId;

        if (string.Equals(
                nextToken,
                "@event",
                System.StringComparison.OrdinalIgnoreCase))
        {
            nextToken = ExecuteEvent(option.debugEvent);
        }

        if (string.Equals(
                nextToken,
                "@hold",
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextToken) ||
            string.Equals(
                nextToken,
                "@end",
                System.StringComparison.OrdinalIgnoreCase))
        {
            Close();
            return;
        }

        if (!TryResolveNode(
                nextToken,
                out DialogueGraph nextGraph,
                out DialogueNode nextNode))
        {
            Debug.LogWarning(
                $"[CITY HALL DIALOGUE] Cannot resolve next token: {nextToken}"
            );

            Close();
            return;
        }

        currentGraph = nextGraph;
        GoToNode(nextNode);
    }

    private void TryRunAutomaticEventOption(DialogueNode node)
    {
        if (node == null ||
            node.options == null ||
            node.options.Count != 1)
        {
            return;
        }

        DialogueOption option = node.options[0];

        if (option == null)
            return;

        if (!string.Equals(
                option.nextNodeId,
                "@event",
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ProcessOption(option);
    }

    // =========================================================
    // CITY HALL EVENTS
    // =========================================================

    private string ExecuteEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return "@end";

        eventName = eventName.Trim();

        if (debugLogs)
            Debug.Log($"[CITY HALL EVENT] {eventName}");

        switch (eventName)
        {
            case "REGISTER_CITIZEN_ID_VISIT":
                return RegisterCitizenIdVisit();

            case "REGISTER_FILE_PICKUP_VISIT":
                return RegisterFilePickupVisit();

            case "CHECK_CITIZEN_ID_VISIT":
                BeginCitizenIdVisitCheck();
                return "@hold";

            case "BEGIN_CITIZEN_ID_APPLICATION":
                return BeginCitizenIdApplication();

            case "REPORT_LOST_CITIZEN_ID":
                return ReportLostCitizenId();

            case "CANCEL_ACTIVE_VISIT":
                return CancelActiveVisit();

            case "OPEN_CITIZEN_ID_APPLICATION":
                OpenCitizenIdApplication();
                return "@hold";

            case "BEGIN_FILE_PICKUP_SEARCH":
                BeginFilePickupSearch();
                return "@hold";

            case "CHECK_FILE_PICKUP_VISIT":
                BeginFilePickupVisitCheck();
                return "@hold";

            case "FINALIZE_CITIZEN_ID_APPLICATION":
                return FinalizeCitizenIdApplication();

            default:
                Debug.LogWarning(
                    $"[CITY HALL EVENT] Unknown event: {eventName}"
                );

                return "@end";
        }
    }

    private string BeginCitizenIdApplication()
    {
        CityHallVisitRegistry visits = CityHallVisitRegistry.Instance;

        if (visits == null ||
            !visits.HasUsableVisit(CityHallVisitType.CitizenId))
        {
            return "no_registration";
        }

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        // Wniosek ju¿ istnieje lub dokument czeka na odbiór.
        if (service != null && service.HasApplication)
            return "application_already_exists";

        // Na tym etapie NIE sprawdzamy PlayerStats.citizenId.
        // To pole pochodzi jeszcze ze starej integracji bankowej.
        return "application_intro";
    }

    private string ReportLostCitizenId()
    {
        CityHallVisitRegistry visits = CityHallVisitRegistry.Instance;

        if (visits == null ||
            !visits.HasUsableVisit(CityHallVisitType.CitizenId))
        {
            return "no_registration";
        }

        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats == null || !playerStats.HasCitizenID)
            return "no_citizen_id_to_report";

        return "lost_id_intro";
    }

    private string RegisterCitizenIdVisit()
    {
        CityHallVisitRegistry visits = CityHallVisitRegistry.Instance;

        if (visits == null)
        {
            Debug.LogWarning(
                "[CITY HALL] CityHallVisitRegistry.Instance is missing."
            );

            return "@end";
        }

        bool registered = visits.TryRegisterVisit(
            CityHallVisitType.CitizenId,
            out string failureReason
        );

        if (registered)
            return "visit_registered";

        switch (failureReason)
        {
            case "VISIT_ALREADY_REGISTERED":
                return "visit_already_registered";

            case "OTHER_VISIT_ALREADY_ACTIVE":
                return "other_visit_active";

            default:
                Debug.LogWarning(
                    $"[CITY HALL] Registration failed: {failureReason}"
                );

                return "@end";
        }
    }

    private string RegisterFilePickupVisit()
    {
        CityHallVisitRegistry visits =
            CityHallVisitRegistry.Instance;

        if (visits == null)
        {
            Debug.LogWarning(
                "[CITY HALL] CityHallVisitRegistry.Instance is missing."
            );

            return "@end";
        }

        bool registered = visits.TryRegisterVisit(
            CityHallVisitType.FilePickup,
            out string failureReason
        );

        if (registered)
            return "visit_registered";

        switch (failureReason)
        {
            case "VISIT_ALREADY_REGISTERED":
                return "visit_already_registered";

            case "OTHER_VISIT_ALREADY_ACTIVE":
                return "other_visit_active";

            default:
                Debug.LogWarning(
                    $"[CITY HALL] File Pickup registration failed: " +
                    $"{failureReason}"
                );

                return "@end";
        }
    }

    private void BeginCitizenIdVisitCheck()
    {
        if (visitCheckCoroutine != null)
            StopCoroutine(visitCheckCoroutine);

        visitCheckCoroutine = StartCoroutine(
            CitizenIdVisitCheckRoutine()
        );
    }

    private IEnumerator CitizenIdVisitCheckRoutine()
    {
        DialogueGraph graphAtStart = currentGraph;

        if (!TryGetNodeFromCurrentGraph(
                "checking",
                out DialogueNode checkingNode))
        {
            Debug.LogWarning(
                "[CITY HALL] Node 'checking' not found."
            );

            visitCheckCoroutine = null;
            yield break;
        }

        GoToNode(checkingNode);

        float timer = 0f;

        while (timer < visitCheckDuration)
        {
            if (!IsOpen || currentGraph != graphAtStart)
            {
                visitCheckCoroutine = null;
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsOpen)
        {
            visitCheckCoroutine = null;
            yield break;
        }

        CityHallVisitRegistry visits = CityHallVisitRegistry.Instance;

        bool hasVisit =
            visits != null &&
            visits.HasUsableVisit(
                CityHallVisitType.CitizenId
            );

        string resultNodeId = ResolveCitizenIdServiceNode(hasVisit);

        if (hasVisit)
        {
            visits.TryBeginVisit(
                CityHallVisitType.CitizenId
            );
        }

        if (!TryGetNodeFromCurrentGraph(
                resultNodeId,
                out DialogueNode resultNode))
        {
            Debug.LogWarning(
                $"[CITY HALL] Result node not found: {resultNodeId}"
            );

            Close();
            visitCheckCoroutine = null;
            yield break;
        }

        visitCheckCoroutine = null;
        GoToNode(resultNode);
    }

    private string ResolveCitizenIdServiceNode(bool hasVisit)
    {
        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        // Istniej¹cy wniosek ma pierwszeñstwo przed rejestracj¹ wizyty.
        if (service != null && service.HasApplication)
        {
            service.RefreshStatus();

            return service.Status switch
            {
                CitizenIdApplicationStatus.WaitingForPhoto
                    => "photo_required",

                CitizenIdApplicationStatus.PhotoCompleted
                    => "photo_completed",

                CitizenIdApplicationStatus.Processing
                    => "application_submitted",

                CitizenIdApplicationStatus.ReadyForPickup
                    => "application_ready",

                CitizenIdApplicationStatus.Issued
                    => "already_has_citizen_id",

                _ => hasVisit
                    ? "registered"
                    : "no_registration"
            };
        }

        return hasVisit
            ? "registered"
            : "no_registration";
    }

    private string GetCitizenIdExistingApplicationNode()
    {
        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null || !service.HasApplication)
            return null;

        service.RefreshStatus();

        return service.Status switch
        {
            CitizenIdApplicationStatus.WaitingForPhoto
                => "photo_required",

            CitizenIdApplicationStatus.PhotoCompleted
                => "photo_completed",

            CitizenIdApplicationStatus.Processing
                => "application_submitted",

            CitizenIdApplicationStatus.ReadyForPickup
                => "application_ready",

            CitizenIdApplicationStatus.Issued
                => "already_has_citizen_id",

            _ => null
        };
    }

    private string CancelActiveVisit()
    {
        CityHallVisitRegistry visits = CityHallVisitRegistry.Instance;

        if (visits == null)
            return "@end";

        visits.CancelActiveVisit();
        visits.ClearVisit();

        return "register_menu";
    }

    // =========================================================
    // GRAPH RESOLUTION
    // =========================================================

    private bool TryResolveNode(
        string token,
        out DialogueGraph graph,
        out DialogueNode node)
    {
        graph = null;
        node = null;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        token = token.Trim();

        if (token.StartsWith(
                "graph:",
                System.StringComparison.OrdinalIgnoreCase))
        {
            if (registry == null)
                return false;

            string rest = token.Substring("graph:".Length).Trim();

            string graphKey = rest;
            string nodeId = null;

            int slashIndex = rest.IndexOf('/');

            if (slashIndex >= 0)
            {
                graphKey = rest.Substring(0, slashIndex).Trim();
                nodeId = rest.Substring(slashIndex + 1).Trim();
            }

            DialogueGraph targetGraph = registry.Get(graphKey);

            if (targetGraph == null)
                return false;

            if (string.IsNullOrWhiteSpace(nodeId))
                nodeId = targetGraph.startNodeId;

            DialogueNode targetNode = targetGraph.GetNode(nodeId);

            if (targetNode == null)
                return false;

            graph = targetGraph;
            node = targetNode;
            return true;
        }

        if (currentGraph == null)
            return false;

        DialogueNode localNode = currentGraph.GetNode(token);

        if (localNode == null)
            return false;

        graph = currentGraph;
        node = localNode;
        return true;
    }

    private bool TryGetNodeFromCurrentGraph(
        string nodeId,
        out DialogueNode node)
    {
        node = null;

        if (currentGraph == null ||
            string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        node = currentGraph.GetNode(nodeId);
        return node != null;
    }

    // =========================================================
    // TYPEWRITER
    // =========================================================

    private void TypeLine(
        string speaker,
        string text,
        bool isPlayerLine,
        System.Action onDone)
    {
        if (window == null)
            return;

        StopPostDelay();

        currentLineIsPlayer = isPlayerLine;

        window.TypeLine(
            speaker,
            text ?? string.Empty,
            isPlayerLine,
            () =>
            {
                float delay = currentLineIsPlayer
                    ? playerPostDelay
                    : npcPostDelay;

                postDelayCoroutine = StartCoroutine(
                    PostDelayRoutine(delay, onDone)
                );
            }
        );
    }

    private IEnumerator PostDelayRoutine(
        float delay,
        System.Action callback)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        postDelayCoroutine = null;

        if (IsOpen)
            callback?.Invoke();
    }

    private void StopPostDelay()
    {
        if (postDelayCoroutine == null)
            return;

        StopCoroutine(postDelayCoroutine);
        postDelayCoroutine = null;
    }

    private void StopActiveCoroutines()
    {
        StopPostDelay();

        if (visitCheckCoroutine != null)
        {
            StopCoroutine(visitCheckCoroutine);
            visitCheckCoroutine = null;
        }

        if (filePickupCheckCoroutine != null)
        {
            StopCoroutine(filePickupCheckCoroutine);
            filePickupCheckCoroutine = null;
        }
    }

    // =========================================================
    // REFERENCES / INPUT
    // =========================================================

    private bool ResolveWindow()
    {
        if (window != null)
            return true;

        window = FindFirstObjectByType<DialogueWindowUI>(
            FindObjectsInactive.Include
        );

        return window != null;
    }

    private bool EscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    private void OpenCitizenIdApplication()
    {
        if (citizenIdApplicationUI == null)
        {
            citizenIdApplicationUI =
                FindFirstObjectByType<CitizenIdApplicationUI>(
                    FindObjectsInactive.Include
                );
        }

        if (citizenIdApplicationUI == null)
        {
            Debug.LogWarning(
                "[CITY HALL] CitizenIdApplicationUI not found."
            );

            return;
        }

        waitingForChoice = false;
        HideOptions();

        if (window != null)
            window.SetVisualVisibleOnly(false);

        citizenIdApplicationUI.Open(this);
    }

    public void ResumeAfterCitizenIdApplication()
    {
        if (!IsOpen)
            return;

        StopActiveCoroutines();

        waitingForChoice = false;
        currentNode = null;

        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        string targetNodeId;

        if (service == null || !service.HasApplication)
        {
            targetNodeId = "registered";
        }
        else
        {
            targetNodeId = service.Status switch
            {
                CitizenIdApplicationStatus.WaitingForPhoto
                    => "photo_required",

                CitizenIdApplicationStatus.PhotoCompleted
                    => "photo_completed",

                CitizenIdApplicationStatus.Processing
                    => "application_submitted",

                CitizenIdApplicationStatus.ReadyForPickup
                    => "application_ready",

                CitizenIdApplicationStatus.Issued
                    => "already_has_citizen_id",

                _ => "registered"
            };
        }

        DialogueNode targetNode =
            currentGraph != null
                ? currentGraph.GetNode(targetNodeId)
                : null;

        if (targetNode == null)
        {
            Close();
            return;
        }

        if (window != null)
        {
            // Ponownie inicjalizuje widok oraz typewriter,
            // ale pozostawia tê sam¹ sesjê dialogow¹.
            window.OpenWindow(
                clearHistory: true,
                lockPlayer: true
            );
        }

        GoToNode(targetNode);
    }

    private string FinalizeCitizenIdApplication()
    {
        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null)
            return "@end";

        if (!service.TryFinalizeApplication())
        {
            return service.Status switch
            {
                CitizenIdApplicationStatus.WaitingForPhoto
                    => "photo_required",

                CitizenIdApplicationStatus.Processing
                    => "application_submitted",

                CitizenIdApplicationStatus.ReadyForPickup
                    => "application_ready",

                _ => "registered"
            };
        }

        CityHallVisitRegistry.Instance?.TryCompleteVisit(
            CityHallVisitType.CitizenId
        );

        return "application_submitted";
    }

    public IEnumerator ShowTemporaryNpcLine(
    string speaker,
    string text,
    float holdTime = 0.5f)
    {
        if (!ResolveWindow())
            yield break;

        window.OpenWindow(
            clearHistory: false,
            lockPlayer: false
        );

        window.HideOptions();

        bool finished = false;

        window.TypeLine(
            string.IsNullOrWhiteSpace(speaker)
                ? "ID TELLER"
                : speaker,
            text ?? string.Empty,
            false,
            () => finished = true
        );

        while (!finished)
            yield return null;

        if (holdTime > 0f)
            yield return new WaitForSecondsRealtime(holdTime);
    }

    public void CloseTemporaryDialogue()
    {
        if (window != null)
            window.CloseWindow(unlockPlayer: false);
    }

    private void BeginFilePickupVisitCheck()
    {
        if (filePickupCheckCoroutine != null)
            StopCoroutine(filePickupCheckCoroutine);

        filePickupCheckCoroutine =
            StartCoroutine(FilePickupVisitCheckRoutine());
    }

    private IEnumerator FilePickupVisitCheckRoutine()
    {
        DialogueGraph graphAtStart = currentGraph;

        if (!TryGetNodeFromCurrentGraph(
                "checking",
                out DialogueNode checkingNode))
        {
            Debug.LogWarning(
                "[CITY HALL] File Pickup node 'checking' not found."
            );

            filePickupCheckCoroutine = null;
            yield break;
        }

        GoToNode(checkingNode);

        float timer = 0f;

        while (timer < visitCheckDuration)
        {
            if (!IsOpen || currentGraph != graphAtStart)
            {
                filePickupCheckCoroutine = null;
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsOpen)
        {
            filePickupCheckCoroutine = null;
            yield break;
        }

        CityHallVisitRegistry visits =
            CityHallVisitRegistry.Instance;

        bool hasVisit =
            visits != null &&
            visits.HasUsableVisit(
                CityHallVisitType.FilePickup
            );

        string resultNodeId;

        if (!hasVisit)
        {
            resultNodeId = "no_registration";
        }
        else
        {
            visits.TryBeginVisit(
                CityHallVisitType.FilePickup
            );

            // Po potwierdzeniu wizyty NPC informuje,
            // ¿e idzie po dokumenty, a nastêpnie uruchamia
            // BEGIN_FILE_PICKUP_SEARCH.
            resultNodeId = "registered";
        }

        if (!TryGetNodeFromCurrentGraph(
                resultNodeId,
                out DialogueNode resultNode))
        {
            Debug.LogWarning(
                $"[CITY HALL] File Pickup result node not found: " +
                $"{resultNodeId}"
            );

            Close();
            filePickupCheckCoroutine = null;
            yield break;
        }

        filePickupCheckCoroutine = null;
        GoToNode(resultNode);
    }

    private string ResolveFilePickupDocumentNode()
    {
        CitizenIdApplicationService service =
            CitizenIdApplicationService.Instance;

        if (service == null || !service.HasApplication)
            return "nothing_ready";

        service.RefreshStatus();

        return service.Status switch
        {
            CitizenIdApplicationStatus.WaitingForPhoto
                => "application_not_ready",

            CitizenIdApplicationStatus.PhotoCompleted
                => "application_not_ready",

            CitizenIdApplicationStatus.Processing
                => "application_not_ready",

            CitizenIdApplicationStatus.ReadyForPickup
                => "document_ready",

            CitizenIdApplicationStatus.Issued
                => "already_collected",

            _ => "nothing_ready"
        };
    }

    private void BeginFilePickupSearch()
    {
        if (filePickupSequence == null)
        {
            filePickupSequence =
                FindFirstObjectByType<FilePickupSequence>(
                    FindObjectsInactive.Include
                );
        }

        if (filePickupSequence == null)
        {
            Debug.LogWarning(
                "[CITY HALL] FilePickupSequence is missing."
            );

            Close();
            return;
        }

        waitingForChoice = false;
        HideOptions();

        bool started =
            filePickupSequence.BeginSearch();

        if (!started)
        {
            Debug.LogWarning(
                "[CITY HALL] File Pickup search could not start."
            );

            Close();
        }
    }

    public void ResumeFilePickupAfterSearch()
    {
        if (!IsOpen || currentGraph == null)
            return;

        string resultNodeId =
            ResolveFilePickupDocumentNode();

        if (!TryGetNodeFromCurrentGraph(
                resultNodeId,
                out DialogueNode resultNode))
        {
            Debug.LogWarning(
                $"[CITY HALL] File Pickup result node missing: " +
                $"{resultNodeId}"
            );

            Close();
            return;
        }

        GoToNode(resultNode);
    }
}