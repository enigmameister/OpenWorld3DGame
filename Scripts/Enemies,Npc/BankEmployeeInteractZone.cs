using UnityEngine;

public class BankEmployeeInteractZone : MonoBehaviour
{
    [Header("Refs")]
    public BankEmployee employee;
    public BankDialogueUI dialogueUI;

    private bool _playerInside;

    private bool _sessionStarted;
    private bool _dialogClosedByEsc;

    [SerializeField] private bool autoCloseWhenOffDuty = true;

    [Header("Damage / Death Lock")]
    [SerializeField] private bool blockWhenDamagedOrProvoked = true;
    [SerializeField] private bool closeDialogueWhenEmployeeUnavailable = true;

    private int _playerInsideCount = 0;

    private NPCController npcController;
    private NPCCore npcCore;

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
        if (!other.CompareTag("Player")) return;

        _playerInsideCount++;

        if (_playerInsideCount == 1)
        {
            _playerInside = true;

            if (employee != null)
                Debug.Log($"[BANK NPC] Player entered zone of {employee.EmployeeName}");
            else
                Debug.Log("[BANK NPC] Player entered zone, but employee is null.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInsideCount = Mathf.Max(0, _playerInsideCount - 1);

        if (_playerInsideCount == 0)
        {
            _playerInside = false;

            if (employee != null)
                Debug.Log($"[BANK NPC] Player left zone of {employee.EmployeeName}");

            if (dialogueUI != null && dialogueUI.IsOpen)
            {
                dialogueUI.Close();
                PlayerInputHandler.SetGameplayBlocked(false);
            }

            if (_sessionStarted && _dialogClosedByEsc)
            {
                _sessionStarted = false;
                _dialogClosedByEsc = false;

                if (dialogueUI != null)
                    dialogueUI.ResetSession();

                if (employee != null)
                    Debug.Log($"[BANK SESJA] ZAKONCZONA z {employee.EmployeeName}");
            }
        }
    }

    private void OnDialogueClosed()
    {
        if (!_sessionStarted) return;

        _dialogClosedByEsc = true;

        PlayerInputHandler.SetGameplayBlocked(false);

        Debug.Log($"[BANK SESJA] Dialog zamkniety (event), sesja nadal aktywna: {_sessionStarted}");
    }

    private void Update()
    {
        if (!_playerInside)
            return;

        if (dialogueUI == null)
        {
            Debug.LogWarning("[BANK NPC] dialogueUI is NULL.");
            return;
        }

        if (employee == null)
        {
            Debug.LogWarning("[BANK NPC] employee is NULL.");
            return;
        }

        if (!CanUseBankEmployee())
        {
            HandleEmployeeUnavailable();
            return;
        }

        if (dialogueUI.IsOpen && autoCloseWhenOffDuty && !employee.IsWorkingNow())
        {
            _dialogClosedByEsc = true;
            dialogueUI.Close();
            PlayerInputHandler.SetGameplayBlocked(false);

            Debug.Log($"[BANK] Zamknieto dialog - pracownik poza godzinami. Sesja: {_sessionStarted}");
            return;
        }

        if (dialogueUI.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _dialogClosedByEsc = true;
                dialogueUI.Close();
                PlayerInputHandler.SetGameplayBlocked(false);

                Debug.Log($"[BANK SESJA] Dialog zamkniety ESC. Sesja nadal aktywna: {_sessionStarted}");
            }

            return;
        }

        bool interactPressed =
            (PlayerInputHandler.Instance != null && PlayerInputHandler.Instance.InteractPressedThisFrame) ||
            Input.GetKeyDown(KeyCode.E);

        if (!interactPressed)
            return;

        if (PlayerInputHandler.GameplayInputBlocked)
        {
            Debug.LogWarning("[BANK NPC] Nie otwarto dialogu, bo GameplayInputBlocked = true.");
            return;
        }

        if (!employee.IsWorkingNow())
        {
            Debug.Log($"[BANK] {employee.EmployeeName} poza godzinami ({employee.openHour}:00-{employee.closeHour}:00)");
            return;
        }

        if (employee.dialogueGraph == null)
        {
            Debug.LogWarning($"[BANK NPC] {employee.EmployeeName} nie ma przypisanego Dialogue Graph.");
            return;
        }

        if (!_sessionStarted)
        {
            _sessionStarted = true;
            _dialogClosedByEsc = false;
            dialogueUI.ResetSession();
        }

        DialogueGraphUI storyUi = FindFirstObjectByType<DialogueGraphUI>(FindObjectsInactive.Include);

        if (storyUi != null)
            storyUi.Close();

        Debug.Log($"[BANK NPC] Opening dialogue: {employee.EmployeeName}, graph={employee.dialogueGraph.name}");

        dialogueUI.OpenDialogue(employee.dialogueGraph, employee.EmployeeName);
    }

    private bool CanUseBankEmployee()
    {
        if (employee == null)
        {
            Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: employee null.");
            return false;
        }

        if (npcCore != null && npcCore.IsDead)
        {
            Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: npcCore dead.");
            return false;
        }

        if (npcController != null)
        {
            if (npcController.IsDead)
            {
                Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: npcController dead.");
                return false;
            }

            if (blockWhenDamagedOrProvoked)
            {
                if (npcController.IsProvoked)
                {
                    Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: NPC provoked.");
                    return false;
                }

                if (npcController.IsInteractionLocked)
                {
                    Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: interaction locked.");
                    return false;
                }

                if (npcController.IsScaredVisible)
                {
                    Debug.LogWarning("[BANK NPC] CanUseBankEmployee false: scared visible.");
                    return false;
                }
            }
        }

        return true;
    }

    private void HandleEmployeeUnavailable()
    {
        if (closeDialogueWhenEmployeeUnavailable && dialogueUI != null && dialogueUI.IsOpen)
        {
            dialogueUI.Close();
            PlayerInputHandler.SetGameplayBlocked(false);
        }

        _sessionStarted = false;
        _dialogClosedByEsc = false;

        if (dialogueUI != null)
            dialogueUI.ResetSession();
    }
}