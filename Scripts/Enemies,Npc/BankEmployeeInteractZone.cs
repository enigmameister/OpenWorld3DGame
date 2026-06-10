using UnityEngine;

public class BankEmployeeInteractZone : MonoBehaviour
{
    [Header("Refs")]
    public BankEmployee employee;
    public BankDialogueUI dialogueUI;

    private bool _playerInside;

    private bool _sessionStarted;        // sesja rozpoczęta po E
    private bool _dialogClosedByEsc;     // rozmowa zamknięta w trakcie sesji

    // opcjonalnie: jeśli chcesz auto-zamykać po godzinach
    [SerializeField] private bool autoCloseWhenOffDuty = true;

    private int _playerInsideCount = 0;


    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInsideCount++;
        if (_playerInsideCount == 1)
        {
            _playerInside = true;
            Debug.Log($"[BANK NPC] Player entered zone of {employee.EmployeeName}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInsideCount = Mathf.Max(0, _playerInsideCount - 1);
        if (_playerInsideCount == 0)
        {
            _playerInside = false;
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

                Debug.Log($"[BANK SESJA] ZAKONCZONA z {employee.EmployeeName}");
            }
        }
    }

    private void Awake()
    {
        if (dialogueUI != null)
            dialogueUI.DialogueClosed += OnDialogueClosed;
    }

    private void OnDestroy()
    {
        if (dialogueUI != null)
            dialogueUI.DialogueClosed -= OnDialogueClosed;
    }

    private void OnDialogueClosed()
    {
        if (!_sessionStarted) return;

        _dialogClosedByEsc = true;

        // ✅ odblokuj gameplay po zamknięciu UI dialogu
        PlayerInputHandler.SetGameplayBlocked(false);

        Debug.Log($"[BANK SESJA] Dialog zamkniety (event), sesja nadal aktywna: {_sessionStarted}");
    }

    void Update()
    {
        if (PlayerInputHandler.GameplayInputBlocked) return;

        if (!_playerInside) return;
        if (dialogueUI == null || employee == null) return;

        // 0) jeśli rozmowa otwarta, a zrobiło się "po godzinach" -> zamknij UI (sesję zostaw jak chcesz)
        if (dialogueUI.IsOpen && autoCloseWhenOffDuty && !employee.IsWorkingNow())
        {
            _dialogClosedByEsc = true; // traktuj jak "przerwane"
            dialogueUI.Close();
            Debug.Log($"[BANK] Zamknieto dialog - pracownik poza godzinami. Sesja: {_sessionStarted}");
            return;
        }

        // 1) ESC gdy dialog jest otwarty -> zamknij UI rozmowy (sesja nadal aktywna)
        if (dialogueUI.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _dialogClosedByEsc = true;
                dialogueUI.Close();
                Debug.Log($"[BANK SESJA] Dialog zamkniety (ESC), sesja nadal aktywna: {_sessionStarted}");
            }
            return;
        }

        // 2) E = intencja rozmowy
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!employee.IsWorkingNow())
            {
                Debug.Log($"[BANK] {employee.EmployeeName} poza godzinami ({employee.openHour}:00-{employee.closeHour}:00)");
                return;
            }

            if (!_sessionStarted)
            {
                _sessionStarted = true;
                _dialogClosedByEsc = false;
                dialogueUI.ResetSession();
            }

            dialogueUI.OpenDialogue(employee.dialogueGraph, employee.EmployeeName);
        }
    }

}
