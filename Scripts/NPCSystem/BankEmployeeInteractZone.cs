using UnityEngine;

public class BankEmployeeInteractZone : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BankEmployee employee;
    [SerializeField] private BankDialogueUI dialogueUI;

    [Header("Settings")]
    [SerializeField] private bool autoCloseWhenOffDuty = true;

    [Header("Damage / Death Lock")]
    [SerializeField] private bool blockWhenDamagedOrProvoked = true;
    [SerializeField] private bool closeDialogueWhenEmployeeUnavailable = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool playerInside;
    private int playerInsideCount;

    private bool sessionStarted;
    private bool dialogClosedByEsc;

    private NPCController npcController;
    private NPCCore npcCore;
    private DialogueGraphUI storyUi;

    private void Awake()
    {
        if (employee == null)
            employee = GetComponentInParent<BankEmployee>();

        if (employee != null)
        {
            npcController = employee.GetComponent<NPCController>();
            npcCore = employee.GetComponent<NPCCore>();
        }

        if (npcController == null)
            npcController = GetComponentInParent<NPCController>();

        if (npcCore == null)
            npcCore = GetComponentInParent<NPCCore>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);

        storyUi = FindFirstObjectByType<DialogueGraphUI>(FindObjectsInactive.Include);

        if (dialogueUI != null)
            dialogueUI.DialogueClosed += OnDialogueClosed;
    }

    private void OnDestroy()
    {
        if (dialogueUI != null)
            dialogueUI.DialogueClosed -= OnDialogueClosed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideCount++;

        if (playerInsideCount == 1)
        {
            playerInside = true;

            if (debugLogs)
            {
                string employeeName = employee != null ? employee.EmployeeName : "NULL";
                Debug.Log($"[BANK NPC] Player entered zone of {employeeName}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);

        if (playerInsideCount > 0)
            return;

        playerInside = false;

        if (debugLogs && employee != null)
            Debug.Log($"[BANK NPC] Player left zone of {employee.EmployeeName}");

        if (dialogueUI != null && dialogueUI.IsOpen)
            dialogueUI.Close();

        if (sessionStarted && dialogClosedByEsc)
        {
            sessionStarted = false;
            dialogClosedByEsc = false;

            if (dialogueUI != null)
                dialogueUI.ResetSession();

            if (debugLogs && employee != null)
                Debug.Log($"[BANK SESJA] ZAKONCZONA z {employee.EmployeeName}");
        }
    }

    private void Update()
    {
        if (!playerInside)
            return;

        if (dialogueUI == null || employee == null)
            return;

        if (!CanUseBankEmployee())
        {
            HandleEmployeeUnavailable();
            return;
        }

        if (dialogueUI.IsOpen)
        {
            if (autoCloseWhenOffDuty && !employee.IsWorkingNow())
            {
                dialogClosedByEsc = true;
                dialogueUI.Close();

                if (debugLogs)
                    Debug.Log($"[BANK] Zamknieto dialog - pracownik poza godzinami. Sesja: {sessionStarted}");
            }

            return;
        }

        bool interactPressed =
            (PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressedThisFrame) ||
            Input.GetKeyDown(KeyCode.E);

        if (!interactPressed)
            return;

        TryOpenDialogue();
    }

    private void TryOpenDialogue()
    {
        if (PlayerInputHandler.GameplayInputBlocked)
        {
            if (debugLogs)
                Debug.LogWarning("[BANK NPC] Nie otwarto dialogu, bo GameplayInputBlocked = true.");

            return;
        }

        if (!employee.IsWorkingNow())
        {
            if (debugLogs)
                Debug.Log($"[BANK] {employee.EmployeeName} poza godzinami ({employee.openHour}:00-{employee.closeHour}:00)");

            return;
        }

        if (employee.dialogueGraph == null)
        {
            Debug.LogWarning($"[BANK NPC] {employee.EmployeeName} nie ma przypisanego Dialogue Graph.");
            return;
        }

        if (!sessionStarted)
        {
            sessionStarted = true;
            dialogClosedByEsc = false;
            dialogueUI.ResetSession();
        }

        if (storyUi != null && storyUi.IsOpen)
            storyUi.Close();

        if (debugLogs)
            Debug.Log($"[BANK NPC] Opening dialogue: {employee.EmployeeName}, graph={employee.dialogueGraph.name}");

        dialogueUI.OpenDialogue(employee.dialogueGraph, employee.EmployeeName);
    }

    private void OnDialogueClosed()
    {
        if (!sessionStarted)
            return;

        dialogClosedByEsc = true;

        if (debugLogs)
            Debug.Log($"[BANK SESJA] Dialog zamkniety, sesja nadal aktywna: {sessionStarted}");
    }

    private bool CanUseBankEmployee()
    {
        if (employee == null)
            return false;

        if (npcCore != null && npcCore.IsDead)
            return false;

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
        if (closeDialogueWhenEmployeeUnavailable && dialogueUI != null && dialogueUI.IsOpen)
            dialogueUI.Close();

        sessionStarted = false;
        dialogClosedByEsc = false;

        if (dialogueUI != null)
            dialogueUI.ResetSession();
    }
}