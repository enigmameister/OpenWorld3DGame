using System.Collections;
using TMPro;
using UnityEngine;

public class ATMScreenAccountSettings : MonoBehaviour, IATMBackHandler
{
    private enum State
    {
        PickMode,
        EnterCurrentPin,
        EnterNewPin,
        PickStatus,
        PickConfirm,
        ConfirmBox,
        Processing
    }

    [Header("Controller")]
    public ATMUIController controller;

    [Header("Menu buttons")]
    public ATMMenuButtonView changePinBtn;
    public ATMMenuButtonView changeStatusBtn;
    public ATMMenuButtonView cancelBtn;

    [Header("Wallet texts")]
    public TextMeshProUGUI accountWalletText;
    public TextMeshProUGUI playerWalletText;

    [Header("PIN widgets")]
    public ATMScreenEnterPin pinCurrent;
    public GameObject pinCurrentInactive; // obiekt "Inactive" pod PIN_Enter_Current
    public ATMScreenEnterPin pinNew;
    public GameObject pinNewInactive;     // obiekt "Inactive" pod PIN_Enter_New

    [Header("STATUS widgets")]
    public TMP_Dropdown statusDropdown;
    public GameObject statusDropdownInactive; // jak PIN Inactive (opcjonalne)

    private int _selectedStatusIndex; // 0 = Active, 1 = Blocked
    private int _pendingStatusIndex;
    private bool _isChangingStatusFlow;

    [Header("Attempts UI (only for wrong current PIN)")]
    public TextMeshProUGUI attemptsText;
    public int maxAttempts = 3;

    [Header("Confirm")]
    public ATMMenuButtonView confirmBtn;

    [Header("ConfirmBox")]
    public GameObject confirmBoxRoot;
    public TextMeshProUGUI confirmQuestionText;
    public ATMMenuButtonView yesBtn;
    public ATMMenuButtonView noBtn;

    [Header("Processing")]
    public TextMeshProUGUI processingText;
    public float processingSeconds = 2.5f;

    // runtime
    private State _state;
    private int _menuIndex;       // 0=pin,1=status,2=cancel
    private int _confirmBoxIndex; // 0=yes,1=no

    private string _typedCurrent = "";
    private string _typedNew = "";

    private int _attemptsLeft;

    private InventoryItemInstance _card;

    public void Open(InventoryItemInstance card, ATMUIController ui)
    {
        _card = card;
        controller = ui;

        ResetAll();
        RefreshWallets();   // <-- DODAJ
    }

    void OnEnable()
    {
        ResetAttempts();
        ResetAll();
        RefreshWallets();   // <-- DODAJ
    }

    private void ResetAll()
    {
        _state = State.PickMode;
        _menuIndex = 0;
        _confirmBoxIndex = 0;

        _typedCurrent = "";
        _typedNew = "";

        ResetAttempts(); // attempts ukryte na starcie

        if (pinCurrent) { pinCurrent.ClearTyped(); pinCurrent.SetAcceptInput(false); }
        if (pinNew) { pinNew.ClearTyped(); pinNew.SetAcceptInput(false); }

        SetInactive(pinCurrentInactive, true);
        SetInactive(pinNewInactive, true);

        if (confirmBtn) confirmBtn.SetSelected(false);

        if (confirmBoxRoot) confirmBoxRoot.SetActive(false);
        if (processingText) processingText.gameObject.SetActive(false);

        // “hold active” off na starcie
        changePinBtn?.SetHoldActive(false);
        changeStatusBtn?.SetHoldActive(false);

        SetupStatusDropdown();
        SetInactive(statusDropdownInactive, true);   // ✅ zamiast dropdownInactive
        RefreshSelection();
    }

    private void SetupStatusDropdown()
    {
        if (statusDropdown == null) return;

        statusDropdown.ClearOptions();
        statusDropdown.AddOptions(new System.Collections.Generic.List<string>
    {
        "ACTIVE",
        "BLOCKED",
        "PENDING (N/A)",
        "REVOKED (N/A)"
    });

        _selectedStatusIndex = GetStatusIndexFromCard();
        statusDropdown.value = _selectedStatusIndex;
        statusDropdown.RefreshShownValue();

        RefreshStatusDropdownVisual();
    }

    private int GetStatusIndexFromCard()
    {
        if (_card == null || !_card.hasBankCardMeta)
            return 0;

        return _card.bankCard.status switch
        {
            BankCardStatus.Blocked => 1,
            _ => 0 // Active / Pending / Revoked → fallback Active
        };
    }

    private void RefreshStatusDropdownVisual()
    {
        if (statusDropdown?.captionText == null) return;
        statusDropdown.captionText.color = Color.white;
    }
    private void SetInactive(GameObject go, bool on)
    {
        if (go) go.SetActive(on);
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_state == State.Processing) return;

        // LEWO / PRAWO / GÓRA / DÓŁ
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            Move(-1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            Move(+1);

        // ENTER
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Submit();
    }

    private void RefreshWallets()
    {
        if (controller == null)
            return;

        if (accountWalletText != null)
            accountWalletText.text = $"ACCOUNT WALLET: {controller.GetAccountWallet()}";

        if (playerWalletText != null)
            playerWalletText.text = $"PLAYER WALLET: {controller.GetPlayerWallet()}";
    }

    private void Move(int dir)
    {
        switch (_state)
        {
            case State.PickMode:
                _menuIndex = (_menuIndex + dir + 3) % 3;
                RefreshSelection();
                break;

            case State.EnterCurrentPin:
                // strzałka w lewo = cofka do menu
                if (dir < 0)
                {
                    GoToPickModeFromInputs();
                }
                break;

            case State.EnterNewPin:
                // strzałka w lewo = cofka do current (czyści new)
                if (dir < 0)
                {
                    ClearNewInput();
                    GoToEnterCurrent(fromNewBack: true);
                }
                break;

            case State.PickConfirm:
                if (dir < 0)
                {
                    if (_isChangingStatusFlow)
                        GoToPickStatus(true);
                    else
                        GoToEnterNew(true);
                }
                break;

            case State.ConfirmBox:
                _confirmBoxIndex = (_confirmBoxIndex + dir + 2) % 2;
                RefreshSelection();
                break;

            case State.PickStatus:
                // toggle Active <-> Blocked
                _selectedStatusIndex = (_selectedStatusIndex == 0) ? 1 : 0;

                if (statusDropdown)
                {
                    statusDropdown.value = _selectedStatusIndex;
                    statusDropdown.RefreshShownValue();
                }
                break;
        }
    }

    private void Submit()
    {
        switch (_state)
        {
            case State.PickMode:
                if (_menuIndex == 0) BeginChangePin();
                else if (_menuIndex == 1) BeginChangeStatus();
                else BackToAccountInfo();
                break;

            case State.PickStatus:
                _pendingStatusIndex = _selectedStatusIndex;
                GoToConfirm();
                break;

            case State.EnterCurrentPin:
                {
                    if (pinCurrent == null) break;

                    if (pinCurrent.GetTyped().Length != pinCurrent.pinLength)
                        break;

                    _typedCurrent = pinCurrent.GetTyped();

                    // sprawdź current pin
                    if (_card == null || !_card.hasBankCardMeta)
                    {
                        // brak karty -> wróć do menu
                        GoToPickModeFromInputs();
                        break;
                    }

                    int typed = int.Parse(_typedCurrent);

                    // Źródło prawdy: BankSystem (fallback: meta na itemie)
                    bool valid = false;
                    if (BankSystem.Instance != null)
                    {
                        valid = BankSystem.Instance.ValidatePin(_card.bankCard.cardId, typed);
                    }
                    else
                    {
                        valid = (typed == _card.bankCard.pin);
                    }

                    if (valid)
                    {
                        // OK -> idziemy do NEW PIN
                        GoToEnterNew(fromConfirmBack: false);
                    }
                    else
                    {
                        // ZŁY -> odejmij próbę i wymuś wpis od nowa
                        DecreaseAttemptsAndRestartCurrent();

                        // jeśli koniec prób -> blokuj kartę + screen blocked
                        if (_attemptsLeft <= 0)
                        {
                            controller?.BlockCardAndShowBlocked(_card);
                        }
                    }

                    break;
                }


            case State.EnterNewPin:
                SubmitNewPin();
                break;

            case State.PickConfirm:
                OpenConfirmBox();
                break;

            case State.ConfirmBox:
                if (_confirmBoxIndex == 0)
                {
                    if (_isChangingStatusFlow) StartCoroutine(DoChangeStatus());
                    else StartCoroutine(DoChangePin());
                }
                else CloseConfirmBox();
                break;

        }
    }
    private IEnumerator DoChangeStatus()
    {
        _state = State.Processing;

        if (confirmBoxRoot) confirmBoxRoot.SetActive(false);
        if (processingText)
        {
            processingText.text = "PROCESSING...";
            processingText.gameObject.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(processingSeconds);

        bool blockedNow = false;

        if (_card != null && _card.hasBankCardMeta && BankSystem.Instance != null)
        {
            var newStatus = (_pendingStatusIndex == 0)
                ? BankCardStatus.Active
                : BankCardStatus.Blocked;

            // 1) źródło prawdy: bank
            if (newStatus == BankCardStatus.Blocked)
            {
                BankSystem.Instance.BlockCardAsOwnerBlocked(_card.bankCard.cardId);
            }
            else
            {
                BankSystem.Instance.ChangeCardStatus(_card.bankCard.cardId, BankCardStatus.Active);
            }

            // 2) sync kopii na itemie
            var meta = _card.bankCard;
            meta.status = newStatus;
            _card.bankCard = meta;

            blockedNow = (newStatus == BankCardStatus.Blocked);
        }

        if (processingText) processingText.gameObject.SetActive(false);

        if (blockedNow)
        {
            ResetAll();
            controller?.ClearActiveCard();
            controller?.ShowScreenCardCheckThenSelectCard();
            yield break;
        }

        ResetAll();
    }



    // ========== FLOW CHANGE PIN ==========

    private void BeginChangePin()
    {
        // 1x ENTER ma od razu wejść w current
        _isChangingStatusFlow = false;
        GoToEnterCurrent(fromNewBack: false);
    }

    private void BeginChangeStatus()
    {
        _isChangingStatusFlow = true;
        GoToPickStatus(false);
    }

    private void GoToPickStatus(bool fromConfirmBack)
    {
        _state = State.PickStatus;

        changeStatusBtn?.SetHoldActive(true);
        changePinBtn?.SetHoldActive(false);

        if (pinCurrent) pinCurrent.SetAcceptInput(false);
        if (pinNew) pinNew.SetAcceptInput(false);

        SetInactive(pinCurrentInactive, true);
        SetInactive(pinNewInactive, true);
        SetInactive(statusDropdownInactive, false);

        if (!fromConfirmBack)
            _selectedStatusIndex = GetStatusIndexFromCard();

        statusDropdown.value = _selectedStatusIndex;
        statusDropdown.RefreshShownValue();

        RefreshSelection();
    }

    private void GoToEnterCurrent(bool fromNewBack)
    {
        _state = State.EnterCurrentPin;

        // CHANGE PIN ma być "hold active" bez migania
        changePinBtn?.SetHoldActive(true);
        changeStatusBtn?.SetHoldActive(false);

        // attempts pokazuj tylko po błędzie -> na wejściu do current ukryj
        // (ale jeśli wracasz z NEW do CURRENT, attempts nadal ma być ukryte)
        SetAttemptsVisible(false);

        // current aktywny
        if (!fromNewBack)
        {
            // wchodząc pierwszy raz - czyść current żeby nie pamiętał
            ClearCurrentInput();
        }

        if (pinCurrent) pinCurrent.SetAcceptInput(true);
        SetInactive(pinCurrentInactive, false);

        // new nieaktywny
        if (pinNew) pinNew.SetAcceptInput(false);
        SetInactive(pinNewInactive, true);

        RefreshSelection();
    }

    private void SubmitCurrentPin()
    {
        if (_card == null || !_card.hasBankCardMeta) return;
        if (pinCurrent == null) return;

        if (pinCurrent.GetTyped().Length != pinCurrent.pinLength)
            return;

        _typedCurrent = pinCurrent.GetTyped();
        int typedInt = int.Parse(_typedCurrent);

        if (typedInt != _card.bankCard.pin)
        {
            DecreaseAttemptsAndRestartCurrent();

            if (_attemptsLeft <= 0)
            {
                // opcjonalnie: po 0 prób wróć do menu (albo zablokuj kartę)
                GoToPickModeFromInputs();
            }
            return;
        }

        // OK -> chowamy attempts i przechodzimy do NEW
        SetAttemptsVisible(false);
        GoToEnterNew(fromConfirmBack: false);
    }

    private void GoToEnterNew(bool fromConfirmBack)
    {
        _state = State.EnterNewPin;

        // attempts przy NEW zawsze ukryte
        SetAttemptsVisible(false);

        // CHANGE PIN wciąż hold-active, bez migania
        changePinBtn?.SetHoldActive(true);

        // current ma pozostać widoczny (nie szary) aby dało się wrócić
        if (pinCurrent) pinCurrent.SetAcceptInput(false);
        SetInactive(pinCurrentInactive, false);

        // NEW: wchodząc normalnie - czyść, wracając z CONFIRM możesz zostawić albo czyścić
        if (!fromConfirmBack)
            ClearNewInput();

        if (pinNew) pinNew.SetAcceptInput(true);
        SetInactive(pinNewInactive, false);

        RefreshSelection();
    }

    private void SubmitNewPin()
    {
        if (_card == null || !_card.hasBankCardMeta) return;
        if (pinNew == null) return;

        if (pinNew.GetTyped().Length != pinNew.pinLength)
            return;

        _typedNew = pinNew.GetTyped();

        // NEW PIN nie może być taki sam jak current/aktualny pin karty
        if (!string.IsNullOrEmpty(_typedCurrent) && _typedNew == _typedCurrent)
        {
            // odrzuć, wpis od nowa (bez attempts)
            ClearNewInput();
            SetAttemptsVisible(false);
            return;
        }

        // dodatkowo: jeśli ktoś ominął current, to i tak nie pozwól ustawić tego samego co karta
        if (int.Parse(_typedNew) == _card.bankCard.pin)
        {
            ClearNewInput();
            SetAttemptsVisible(false);
            return;
        }

        GoToConfirm();
    }
    private void GoToConfirm()
    {
        _state = State.PickConfirm;

        if (pinCurrent) pinCurrent.SetAcceptInput(false);
        if (pinNew) pinNew.SetAcceptInput(false);

        // ✅ dropdown nieaktywny w confirm
        SetInactive(statusDropdownInactive, true);

        SetAttemptsVisible(false);
        RefreshSelection();
    }

    private void OpenConfirmBox()
    {
        _state = State.ConfirmBox;
        _confirmBoxIndex = 0;

        if (confirmQuestionText)
        {
            confirmQuestionText.text = _isChangingStatusFlow
                ? "CHANGE CARD STATUS ?"
                : "ARE YOU SURE ?";
        }

        if (confirmBoxRoot) confirmBoxRoot.SetActive(true);
        RefreshSelection();
    }


    private void CloseConfirmBox()
    {
        if (confirmBoxRoot) confirmBoxRoot.SetActive(false);
        _state = State.PickConfirm;
        RefreshSelection();
    }

    private IEnumerator DoChangePin()
    {
        _state = State.Processing;

        if (confirmBoxRoot) confirmBoxRoot.SetActive(false);
        if (processingText) processingText.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(processingSeconds);

        // REALNA ZMIANA PIN (BankSystem = źródło prawdy)
        if (_card != null && _card.hasBankCardMeta)
        {
            int current = int.Parse(_typedCurrent);
            int newPin = int.Parse(_typedNew);

            bool changed = false;

            if (newPin != current)
            {
                if (BankSystem.Instance != null)
                {
                    // zmień w banku
                    changed = BankSystem.Instance.ChangePin(_card.bankCard.cardId, current, newPin);
                }
                else
                {
                    // fallback: tylko meta (gdyby BankSystem nie był dostępny)
                    changed = (current == _card.bankCard.pin);
                }
            }

            if (changed)
            {
                // sync kopii na itemie
                var meta = _card.bankCard;
                meta.pin = newPin;
                _card.bankCard = meta;
            }
            else
            {
                // nie udało się (np. current pin nie zgadza się w banku) -> wróć do wpisywania current
                ClearNewInput();
                GoToEnterCurrent(fromNewBack: false);

                if (processingText) processingText.gameObject.SetActive(false);
                yield break;
            }
        }


        if (processingText) processingText.gameObject.SetActive(false);

        ResetAll();
    }

    // ========== BACK / CANCEL FLOW ==========

    private void GoToPickModeFromInputs()
    {
        _state = State.PickMode;

        // reset wpisów, żeby nie pamiętało po ESC/LEFT
        ClearCurrentInput();
        ClearNewInput();

        ResetAttempts(); // wróć do "brak attempts"

        if (pinCurrent) pinCurrent.SetAcceptInput(false);
        if (pinNew) pinNew.SetAcceptInput(false);

        SetInactive(pinCurrentInactive, true);
        SetInactive(pinNewInactive, true);

        // przyciski trybu wracają do normalnego migania kursora (hold off)
        changePinBtn?.SetHoldActive(false);
        changeStatusBtn?.SetHoldActive(false);

        RefreshSelection();
    }

    private void BackToAccountInfo()
    {
        controller?.ShowScreenAccountInfo(); // masz to po swojej stronie
    }

    // ESC obsługiwany przez interfejs
    public bool HandleBack()
    {
        switch (_state)
        {
            case State.ConfirmBox:
                CloseConfirmBox();
                return true;

            case State.PickConfirm:
                if (_isChangingStatusFlow)
                    GoToPickStatus(fromConfirmBack: true);
                else
                    GoToEnterNew(fromConfirmBack: true);
                return true;

            case State.EnterNewPin:
                // NEW -> CURRENT
                ClearNewInput();
                GoToEnterCurrent(fromNewBack: true);
                return true;

            case State.EnterCurrentPin:
                // CURRENT -> MENU
                GoToPickModeFromInputs();
                return true;

            case State.PickMode:
                // MENU -> AccountInfo
                BackToAccountInfo();
                return true;

            case State.PickStatus:
                SetInactive(statusDropdownInactive, true);
                changeStatusBtn?.SetHoldActive(false);
                GoToPickModeFromInputs();
                return true;
        }

        return false;
    }

    // ========== UI SELECTION ==========

    private void RefreshSelection()
    {
        // wyczyść miganie
        changePinBtn?.SetSelected(false);
        changeStatusBtn?.SetSelected(false);
        cancelBtn?.SetSelected(false);
        confirmBtn?.SetSelected(false);
        yesBtn?.SetSelected(false);
        noBtn?.SetSelected(false);

        // ustaw zależnie od stanu
        if (_state == State.PickMode)
        {
            // hold off w menu
            changePinBtn?.SetHoldActive(false);
            changeStatusBtn?.SetHoldActive(false);

            if (_menuIndex == 0) changePinBtn?.SetSelected(true);
            else if (_menuIndex == 1) changeStatusBtn?.SetSelected(true);
            else cancelBtn?.SetSelected(true);
        }

        else if (_state == State.EnterCurrentPin || _state == State.EnterNewPin || _state == State.PickConfirm || _state == State.ConfirmBox || _state == State.PickStatus)
        {
            if (_isChangingStatusFlow)
            {
                changeStatusBtn?.SetHoldActive(true);
                changePinBtn?.SetHoldActive(false);
            }
            else
            {
                changePinBtn?.SetHoldActive(true);
                changeStatusBtn?.SetHoldActive(false);
            }
        }


        if (_state == State.PickConfirm)
        {
            confirmBtn?.SetSelected(true);
        }
        else if (_state == State.ConfirmBox)
        {
            if (_confirmBoxIndex == 0) yesBtn?.SetSelected(true);
            else noBtn?.SetSelected(true);
        }
    }

    // ========== INPUT CLEAR ==========

    private void ClearCurrentInput()
    {
        _typedCurrent = "";
        if (pinCurrent) pinCurrent.ClearTyped();
    }

    private void ClearNewInput()
    {
        _typedNew = "";
        if (pinNew) pinNew.ClearTyped();
    }

    // ========== ATTEMPTS ==========

    private void SetAttemptsVisible(bool v)
    {
        if (attemptsText) attemptsText.gameObject.SetActive(v);
    }

    private void ResetAttempts()
    {
        _attemptsLeft = maxAttempts;
        if (attemptsText) attemptsText.text = $"ATTEMPTS LEFT: {_attemptsLeft}";
        SetAttemptsVisible(false);
    }

    private void DecreaseAttemptsAndRestartCurrent()
    {
        _attemptsLeft = Mathf.Max(0, _attemptsLeft - 1);
        if (attemptsText) attemptsText.text = $"ATTEMPTS LEFT: {_attemptsLeft}";
        SetAttemptsVisible(true);

        // wpis od nowa
        ClearCurrentInput();
        if (pinCurrent) pinCurrent.SetAcceptInput(true);
    }
}
