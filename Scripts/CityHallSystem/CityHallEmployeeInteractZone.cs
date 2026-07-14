using UnityEngine;
using UnityEngine.AI;

public class CityHallEmployeeInteractZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CityHallEmployee employee;
    [SerializeField] private CityHallDialogueUI dialogueUI;

    [Header("Interaction")]
    [SerializeField] private bool closeDialogueOnExit = true;
    [SerializeField] private bool autoCloseWhenOffDuty = true;

    [Header("NPC Facing")]
    [SerializeField] private bool facePlayerDuringInteraction = true;
    [SerializeField] private bool restoreOriginalRotationAfterInteraction = true;

    [Header("NPC State")]
    [SerializeField] private bool blockWhenDamagedOrProvoked = true;
    [SerializeField] private bool closeDialogueWhenEmployeeUnavailable = true;

    [Header("Citizen ID Photo")]
    [SerializeField] private CitizenIdPhotoSequence citizenIdPhotoSequence;

    [Header("File Pickup")]
    [SerializeField] private FilePickupSequence filePickupSequence;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool playerInside;
    private int playerInsideCount;

    private bool sessionStarted;

    private Transform player;
    private Quaternion originalEmployeeRotation;

    private NPCCore npcCore;
    private NPCController npcController;
    private NavMeshAgent agent;

    private bool agentWasStopped;
    private bool agentSuspended;

    private void Awake()
    {
        ResolveReferences();

        if (employee != null)
            originalEmployeeRotation = employee.transform.rotation;

        if (dialogueUI != null)
            dialogueUI.DialogueClosed += HandleDialogueClosed;
    }

    private void OnDestroy()
    {
        if (dialogueUI != null)
            dialogueUI.DialogueClosed -= HandleDialogueClosed;

        ResumeEmployeeAgent();
    }

    private void Update()
    {
        if (!playerInside)
            return;

        if (employee == null || dialogueUI == null)
            return;

        if (!CanUseEmployee())
        {
            HandleEmployeeUnavailable();
            return;
        }

        bool filePickupMovementActive =
            filePickupSequence != null &&
            filePickupSequence.IsRunning;

        if (sessionStarted &&
            dialogueUI.IsOpen &&
            facePlayerDuringInteraction &&
            !filePickupMovementActive)
        {
            FaceEmployeeTowardsPlayer();
        }

        if (dialogueUI.IsOpen)
        {
            if (autoCloseWhenOffDuty &&
                !employee.IsWorkingNow())
            {
                dialogueUI.Close();
            }

            return;
        }

        bool interactPressed =
            PlayerInputHandler.Instance != null &&
            PlayerInputHandler.Instance.InteractPressedThisFrame;

        if (!interactPressed)
            return;

        TryOpenDialogue();
    }

    // =========================================================
    // TRIGGER
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideCount++;

        if (playerInsideCount > 1)
            return;

        playerInside = true;

        player = ResolvePlayerTransform(other);

        if (debugLogs)
        {
            string employeeName = employee != null
                ? employee.EmployeeName
                : "NULL";

            Debug.Log(
                $"[CITY HALL NPC] Player entered interaction zone: " +
                $"{employeeName}"
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideCount = Mathf.Max(
            0,
            playerInsideCount - 1
        );

        if (playerInsideCount > 0)
            return;

        playerInside = false;
        player = null;

        if (debugLogs && employee != null)
        {
            Debug.Log(
                $"[CITY HALL NPC] Player left interaction zone: " +
                $"{employee.EmployeeName}"
            );
        }

        bool filePickupSequenceRunning =
            filePickupSequence != null &&
            filePickupSequence.IsRunning;

        if (filePickupSequenceRunning)
        {
            // NPC odszed³ od nieruchomego punktu interakcji
            // albo gracz chwilowo opuœci³ ma³y trigger.
            // Sekwencja nadal trwa.
            return;
        }

        if (closeDialogueOnExit &&
            dialogueUI != null &&
            dialogueUI.IsOpen &&
            sessionStarted)
        {
            dialogueUI.Close();
        }

        EndInteractionSession();
    }

    // =========================================================
    // OPEN DIALOGUE
    // =========================================================

    private void TryOpenDialogue()
    {
        if (PlayerInputHandler.GameplayInputBlocked)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    "[CITY HALL NPC] Dialogue blocked because " +
                    "GameplayInputBlocked is true."
                );
            }

            return;
        }

        if (!employee.IsWorkingNow())
        {
            if (debugLogs)
            {
                Debug.Log(
                    $"[CITY HALL NPC] {employee.EmployeeName} is off duty. " +
                    $"Hours: {employee.OpenHour}:00–{employee.CloseHour}:00"
                );
            }

            return;
        }

        if (employee.DialogueGraph == null)
        {
            Debug.LogWarning(
                $"[CITY HALL NPC] {employee.EmployeeName} " +
                $"has no DialogueGraph assigned."
            );

            return;
        }

        if (!CanUseEmployee())
            return;

        sessionStarted = true;

        SuspendEmployeeAgent();

        if (facePlayerDuringInteraction)
            FaceEmployeeTowardsPlayerImmediate();

        if (debugLogs)
        {
            Debug.Log(
                $"[CITY HALL NPC] Opening dialogue: " +
                $"employee={employee.EmployeeName}, " +
                $"role={employee.EmployeeRole}, " +
                $"graph={employee.DialogueGraph.name}"
            );
        }

        switch (employee.EmployeeRole)
        {
            case CityHallEmployee.Role.CitizenIdClerk:
                {
                    if (citizenIdPhotoSequence == null)
                    {
                        citizenIdPhotoSequence =
                            FindFirstObjectByType<CitizenIdPhotoSequence>(
                                FindObjectsInactive.Include
                            );
                    }

                    citizenIdPhotoSequence?.SetServicingClerk(employee);
                    break;
                }

            case CityHallEmployee.Role.FilePickupClerk:
                {
                    if (filePickupSequence == null)
                    {
                        filePickupSequence =
                            FindFirstObjectByType<FilePickupSequence>(
                                FindObjectsInactive.Include
                            );
                    }

                    filePickupSequence?.SetServicingClerk(employee);
                    break;
                }
        }

        dialogueUI.OpenDialogue(
            employee.DialogueGraph,
            employee.EmployeeName,
            employee.EmployeeRole
        );
    }

    // =========================================================
    // AVAILABILITY
    // =========================================================

    private bool CanUseEmployee()
    {
        if (employee == null)
            return false;

        if (npcCore != null)
        {
            if (npcCore.IsDead)
                return false;
        }

        if (npcController == null)
            return true;

        if (npcController.IsDead)
            return false;

        if (!blockWhenDamagedOrProvoked)
            return true;

        if (npcController.IsProvoked)
            return false;

        if (npcController.IsInteractionLocked)
            return false;

        if (npcController.IsScaredVisible)
            return false;

        return true;
    }

    private void HandleEmployeeUnavailable()
    {
        if (closeDialogueWhenEmployeeUnavailable &&
            dialogueUI != null &&
            dialogueUI.IsOpen &&
            sessionStarted)
        {
            dialogueUI.Close();
        }

        EndInteractionSession();
    }

    // =========================================================
    // SESSION
    // =========================================================

    private void HandleDialogueClosed()
    {
        if (!sessionStarted)
            return;

        EndInteractionSession();
    }

    private void EndInteractionSession()
    {
        sessionStarted = false;

        ResumeEmployeeAgent();

        if (restoreOriginalRotationAfterInteraction &&
            employee != null)
        {
            employee.transform.rotation =
                originalEmployeeRotation;
        }
    }

    // =========================================================
    // FACING
    // =========================================================

    private void FaceEmployeeTowardsPlayer()
    {
        if (employee == null || player == null)
            return;

        Vector3 direction =
            player.position - employee.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        employee.transform.rotation =
            Quaternion.RotateTowards(
                employee.transform.rotation,
                targetRotation,
                employee.FacePlayerSpeedDeg * Time.deltaTime
            );
    }

    private void FaceEmployeeTowardsPlayerImmediate()
    {
        if (employee == null || player == null)
            return;

        Vector3 direction =
            player.position - employee.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        employee.transform.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    // =========================================================
    // NAVMESH AGENT
    // =========================================================

    private void SuspendEmployeeAgent()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        if (agentSuspended)
            return;

        agentWasStopped = agent.isStopped;
        agent.isStopped = true;

        if (agent.hasPath)
            agent.ResetPath();

        agentSuspended = true;
    }

    private void ResumeEmployeeAgent()
    {
        if (!agentSuspended)
            return;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = agentWasStopped;
        }

        agentSuspended = false;
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void ResolveReferences()
    {
        if (employee == null)
            employee = GetComponentInParent<CityHallEmployee>();

        if (dialogueUI == null)
        {
            dialogueUI =
                FindFirstObjectByType<CityHallDialogueUI>(
                    FindObjectsInactive.Include
                );
        }

        if (employee != null)
        {
            npcCore = employee.GetComponent<NPCCore>();
            npcController = employee.GetComponent<NPCController>();
            agent = employee.GetComponent<NavMeshAgent>();
        }

        if (npcCore == null)
            npcCore = GetComponentInParent<NPCCore>();

        if (npcController == null)
            npcController = GetComponentInParent<NPCController>();

        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();
    }

    private Transform ResolvePlayerTransform(Collider other)
    {
        if (other == null)
            return null;

        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller != null)
            return controller.transform;

        PlayerStats playerStats =
            other.GetComponentInParent<PlayerStats>();

        if (playerStats != null)
            return playerStats.transform;

        return other.transform.root;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (employee == null)
            employee = GetComponentInParent<CityHallEmployee>();
    }
#endif
}