using System.Collections;
using UnityEngine;

public class NPCDialogueGraphInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactRadius = 2.5f;
    [SerializeField] private bool requirePlayerLookingAtNpc = true;
    [SerializeField] private float lookMaxDistance = 3.0f;
    [SerializeField] private LayerMask lookMask = ~0;

    [Header("Dialogue")]
    [SerializeField] private DialogueGraph defaultGraph;

    [Tooltip("Optional")]
    [SerializeField] private DialogueGraphRegistry graphRegistry;

    [SerializeField] private string notStartedGraphKey = "TestHouse_Offer";
    [SerializeField] private string activeGraphKey = "TestHouse_Active";
    [SerializeField] private string readyToClaimGraphKey = "TestHouse_Claim";
    [SerializeField] private string completedGraphKey = "TestHouse_Completed";

    [Header("Optional")]
    [SerializeField] private string speakerName = "Story";
    [SerializeField] private GameObject promptRoot;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Look At Player")]
    [SerializeField] private bool facePlayerWhenNearby = true;
    [SerializeField] private bool returnToOriginalRotationOnExit = true;

    [SerializeField] private float faceSmoothTime = 0.18f;
    [SerializeField] private float returnSmoothTime = 0.25f;
    [SerializeField] private float maxTurnSpeed = 540f;
    [SerializeField] private float faceHoldDistance = 4f;

    [Header("Mission List")]
    [SerializeField] private NPCMissionGiver missionGiver;
    [SerializeField] private NPCMissionListUI missionListUI;

    private Quaternion originalRotation;
    private bool shouldFacePlayer;
    private bool shouldReturnRotation;

    private float turnVelocity;

    private Transform player;
    private Camera playerCam;
    private bool playerInside;
    private DialogueGraphUI dialogueUi;
    private BankDialogueUI bankUi;

    private void Awake()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (missionGiver == null)
            missionGiver = GetComponent<NPCMissionGiver>();

        if (missionListUI == null)
            missionListUI = FindFirstObjectByType<NPCMissionListUI>(FindObjectsInactive.Include);
    }
    private void Start()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
            player = playerGo.transform;

        dialogueUi = FindFirstObjectByType<DialogueGraphUI>(FindObjectsInactive.Include);
        bankUi = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);

        originalRotation = transform.rotation;
        playerCam = Camera.main;
    }

    private void Update()
    {
        // Obracanie dzia³a niezale¿nie od wciskania E.
        HandleFacing();

        if (!playerInside)
            return;

        if (DevConsole.IsOpen)
            return;

        if (InventoryUI.IsInventoryOpen)
            return;

        bool pressed = PlayerInputHandler.Instance != null
            ? PlayerInputHandler.Instance.InteractPressed
            : Input.GetKeyDown(KeyCode.E);

        if (!pressed)
            return;

        if (!CanStartDialogue())
            return;

        StartDialogue();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        shouldFacePlayer = true;
        shouldReturnRotation = false;
        turnVelocity = 0f;

        if (promptRoot != null)
            promptRoot.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = false;
        shouldFacePlayer = false;
        shouldReturnRotation = true;
        turnVelocity = 0f;

        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (dialogueUi != null && dialogueUi.IsOpen)
            dialogueUi.Close();
    }

    private bool CanStartDialogue()
    {
        if (player == null)
            return false;

        float interactRadiusSqr = interactRadius * interactRadius;
        float distSqr = (player.position - transform.position).sqrMagnitude;

        if (distSqr > interactRadiusSqr)
            return false;

        if (!requirePlayerLookingAtNpc)
            return true;

        if (playerCam == null)
            playerCam = Camera.main;

        if (playerCam == null)
            return true;

        if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward, out RaycastHit hit, lookMaxDistance, lookMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private void StartDialogue()
    {
        if (missionGiver != null && missionGiver.HasAnyMission())
        {
            if (missionListUI == null)
                missionListUI = FindFirstObjectByType<NPCMissionListUI>(FindObjectsInactive.Include);

            if (missionListUI != null)
            {
                if (!missionListUI.HasVisibleMissions(missionGiver))
                {
                    if (debugLogs)
                        Debug.Log($"[NPCDialogueGraphInteractor] {name}: no visible missions, interaction ignored.");

                    return;
                }

                BankDialogueUI bankUi = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);

                if (bankUi != null && bankUi.IsOpen)
                    bankUi.Close();

                missionListUI.Open(missionGiver, this);
                return;
            }

            // Wa¿ne:
            // jeœli NPC ma missionGiver, ale nie ma MissionListUI,
            // nie odpalaj starego default dialogu przypadkiem.
            return;
        }
        DialogueGraph graph = ResolveGraph();

        if (graph == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[NPCDialogueGraphInteractor] {name}: DialogueGraph missing.");

            return;
        }

        OpenDialogueGraphDirect(graph, speakerName);
    }

    public void OpenDialogueGraphDirect(DialogueGraph graph, string speaker)
    {
        if (graph == null)
            return;

        DialogueGraphUI ui = FindFirstObjectByType<DialogueGraphUI>(FindObjectsInactive.Include);

        if (ui == null)
        {
            if (debugLogs)
                Debug.LogWarning("[NPCDialogueGraphInteractor] DialogueGraphUI not found in scene.");

            return;
        }

        BankDialogueUI bankUi = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (bankUi != null && bankUi.IsOpen)
            bankUi.Close();

        ui.Open(graph, string.IsNullOrWhiteSpace(speaker) ? speakerName : speaker, this);
    }

    private DialogueGraph ResolveGraph()
    {
        if (graphRegistry == null)
            return defaultGraph;

        // Je¿eli misja jeszcze nie istnieje, u¿yj defaultGraph.
        if (KillArmedNPCMission.Instance == null)
            return defaultGraph;

        switch (KillArmedNPCMission.Instance.State)
        {
            case KillArmedNPCMission.MissionState.NotStarted:
                return graphRegistry.Get(notStartedGraphKey) ?? defaultGraph;

            case KillArmedNPCMission.MissionState.Active:
                return graphRegistry.Get(activeGraphKey) ?? defaultGraph;

            case KillArmedNPCMission.MissionState.ReadyToClaim:
                return graphRegistry.Get(readyToClaimGraphKey) ?? defaultGraph;

            case KillArmedNPCMission.MissionState.RewardClaimed:
                return graphRegistry.Get(completedGraphKey) ?? defaultGraph;

            default:
                return defaultGraph;
        }
    }
    private void HandleFacing()
    {
        if (shouldFacePlayer && facePlayerWhenNearby)
        {
            if (player == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                    player = playerGo.transform;
            }

            if (player == null)
                return;

            Vector3 dir = player.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f)
                return;

            float dist = dir.magnitude;

            if (dist > faceHoldDistance)
                return;

            float targetYaw = Quaternion.LookRotation(dir.normalized).eulerAngles.y;
            float currentYaw = transform.eulerAngles.y;

            float smoothYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref turnVelocity,
                faceSmoothTime,
                maxTurnSpeed,
                Time.deltaTime
            );

            transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);

            return;
        }

        if (shouldReturnRotation && returnToOriginalRotationOnExit)
        {
            float targetYaw = originalRotation.eulerAngles.y;
            float currentYaw = transform.eulerAngles.y;

            float smoothYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref turnVelocity,
                returnSmoothTime,
                maxTurnSpeed,
                Time.deltaTime
            );

            transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);

            if (Mathf.Abs(Mathf.DeltaAngle(smoothYaw, targetYaw)) <= 0.5f)
            {
                transform.rotation = originalRotation;
                shouldReturnRotation = false;
                turnVelocity = 0f;
            }
        }
    }
}