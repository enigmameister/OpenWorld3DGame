using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChangeDueDatePanelUI : MonoBehaviour
{
    private enum FocusStep
    {
        InstallmentInput,
        Check,
        Confirm,
        Cancel,
        Back
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Owner")]
    [SerializeField] private LoanMenuNavigation controller;

    [Header("Installment / Day Input")]
    [SerializeField] private TMP_InputField installmentValueInput; // Installment_Value
    [SerializeField] private GameObject installmentBlocked;        // Blocked
    [SerializeField] private Button moreButton;                    // More+
    [SerializeField] private Button lessButton;                    // Less-

    [Header("Info")]
    [SerializeField] private TMP_Text dueDateInfoText;             // DueDate_Info
    [SerializeField] private TMP_Text dueDateValueText;            // DueDate_Value

    [Header("Buttons")]
    [SerializeField] private Button checkButton;                   // Check
    [SerializeField] private Button confirmButton;                 // Confirm
    [SerializeField] private Button cancelButton;                  // Cancel
    [SerializeField] private Button backButton;                    // Back

    [Header("Blocked overlays")]
    [SerializeField] private GameObject checkBlocked;
    [SerializeField] private GameObject confirmBlocked;
    [SerializeField] private GameObject cancelBlocked;

    [Header("Selected")]
    [SerializeField] private GameObject selectedInstallment;
    [SerializeField] private GameObject selectedCheck;
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Decision / Processing")]
    [SerializeField] private TMP_Text bankVerdictText;             // BankVerdict/Result
    [SerializeField] private TMP_Text bankProcessingText;          // BankProcessing
    [SerializeField] private TMP_Text resumeProcessingText;        // ResumeProcessing

    [Header("Timing")]
    [SerializeField] private float checkDelaySeconds = 5f;
    [SerializeField] private float confirmDelaySeconds = 5f;
    [SerializeField] private int step = 1;

    [Header("Colors")]
    [SerializeField] private Color checkingColor = new(1f, 0.85f, 0.1f);
    [SerializeField] private Color correctColor = new(0.2f, 1f, 0.2f);
    [SerializeField] private Color wrongColor = new(1f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;

    private AccountOperationsUI _host;
    private LoanSystem _loanSystem;
    private ActiveLoan _loan;

    private bool _open;
    private bool _busy;
    private bool _editingInstallment;
    private bool _checkPassed;
    private bool _lockedByCooldown;

    private int _selectedDay;
    private int _checkedDay;

    private Coroutine _flowCo;
    private FocusStep _focus = FocusStep.Back;

    public bool IsOpen => _open;

    private void Awake()
    {
        Show(false);
        ResetAllUI();

        if (moreButton) moreButton.onClick.AddListener(() => AddDay(+step));
        if (lessButton) lessButton.onClick.AddListener(() => AddDay(-step));
        if (checkButton) checkButton.onClick.AddListener(TryStartCheck);
        if (confirmButton) confirmButton.onClick.AddListener(TryConfirm);
        if (cancelButton) cancelButton.onClick.AddListener(CancelCheck);
        if (backButton) backButton.onClick.AddListener(OnBackClicked);

        if (installmentValueInput)
        {
            installmentValueInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            installmentValueInput.characterLimit = 2;
            installmentValueInput.lineType = TMP_InputField.LineType.SingleLine;
            installmentValueInput.richText = false;

            installmentValueInput.onValueChanged.AddListener(_ => SyncDayFromInput());
            installmentValueInput.onSelect.AddListener(_ =>
            {
                if (!_open || !_editingInstallment) return;
                installmentValueInput.SetTextWithoutNotify(_selectedDay.ToString());
            });
            installmentValueInput.onDeselect.AddListener(_ =>
            {
                if (!_open) return;
                _editingInstallment = false;
                RefreshDayText();
                UpdateBlockedOverlays();
            });
            installmentValueInput.onEndEdit.AddListener(_ =>
            {
                if (!_open) return;
                _editingInstallment = false;
                RefreshDayText();
                UpdateBlockedOverlays();
            });
        }
    }

    private void Update()
    {
        if (!_open || _busy) return;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackClicked();
            return;
        }

        if (_lockedByCooldown)
        {
            if (_focus != FocusStep.Back)
                SetFocus(FocusStep.Back);

            if (enter)
                OnBackClicked();

            return;
        }

        if (_focus == FocusStep.InstallmentInput)
        {
            if (left)
            {
                AddDay(-step);
                return;
            }

            if (right)
            {
                AddDay(+step);
                return;
            }

            if (enter)
            {
                _editingInstallment = false;
                if (installmentValueInput) installmentValueInput.DeactivateInputField();
                RefreshDayText();
                SetFocus(FocusStep.Check);
                return;
            }
        }

        if (_editingInstallment && installmentValueInput != null && installmentValueInput.isFocused)
        {
            if (left)
            {
                AddDay(-step);
                return;
            }

            if (right)
            {
                AddDay(+step);
                return;
            }

            if (enter)
            {
                _editingInstallment = false;
                installmentValueInput.DeactivateInputField();
                RefreshDayText();
                SetFocus(FocusStep.Check);
                return;
            }
        }

        if (up)
        {
            MoveFocus(-1);
            return;
        }

        if (down)
        {
            MoveFocus(+1);
            return;
        }

        if (enter)
            ActivateFocused();
    }

    public void Open(LoanMenuNavigation owner, AccountOperationsUI host, ActiveLoan loan)
    {
        controller = owner;
        _host = host;
        _loan = loan;
        _loanSystem = LoanSystem.Instance;

        _open = true;
        _busy = false;

        ResetAllUI();
        Show(true);

        RefreshAvailabilityState();

        if (_lockedByCooldown)
            SetFocus(FocusStep.Back);
        else
            SetFocus(FocusStep.InstallmentInput);
    }

    public void Hide()
    {
        _open = false;
        _busy = false;
        StopFlow();
        Show(false);
    }

    private void OnBackClicked()
    {
        controller?.ConsumeEscapeThisFrame();
        _host?.ConsumeEscapeThisFrame();
        BankDialogueUI.SuppressEscapeFrames = 2;

        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        controller?.CloseChangeDueDate();
    }

    private void RefreshAvailabilityState()
    {
        string citizenId = _host != null ? _host.CurrentCitizenId : null;
        string info = _loanSystem != null ? _loanSystem.GetDueDateInfoText(citizenId) : "LOAN SYSTEM MISSING";

        _lockedByCooldown = info != "SELECT NEW DAY AND PRESS CHECK";

        SetStatus(dueDateInfoText, info, _lockedByCooldown ? wrongColor : checkingColor);

        if (dueDateValueText)
            dueDateValueText.text = _loanSystem != null ? _loanSystem.GetDueDateValueText(citizenId) : "";

        UpdateBlockedOverlays();
    }

    private void RefreshDayText()
    {
        if (!installmentValueInput) return;

        int min = _loanSystem != null ? _loanSystem.dueDateMinDay : 1;
        int max = _loanSystem != null ? _loanSystem.dueDateMaxDay : 31;

        _selectedDay = Mathf.Clamp(_selectedDay, min, max);

        installmentValueInput.SetTextWithoutNotify(_selectedDay.ToString());
        int caretPos = installmentValueInput.text.Length;
        installmentValueInput.caretPosition = caretPos;
        installmentValueInput.selectionAnchorPosition = caretPos;
        installmentValueInput.selectionFocusPosition = caretPos;
    }

    private void SyncDayFromInput()
    {
        if (_lockedByCooldown) return;

        _selectedDay = ParseInt(installmentValueInput ? installmentValueInput.text : "0");
        ResetCheckState(false);
        UpdateBlockedOverlays();
    }

    private void AddDay(int delta)
    {
        if (_lockedByCooldown) return;
        if (_focus != FocusStep.InstallmentInput) return;

        int min = _loanSystem != null ? _loanSystem.dueDateMinDay : 1;
        int max = _loanSystem != null ? _loanSystem.dueDateMaxDay : 31;

        _selectedDay = Mathf.Clamp(_selectedDay + delta, min, max);
        RefreshDayText();

        ResetCheckState(false);
        UpdateBlockedOverlays();
    }

    private void StartEditInstallment()
    {
        if (_lockedByCooldown) return;
        if (!installmentValueInput) return;

        _editingInstallment = true;
        installmentValueInput.interactable = true;
        installmentValueInput.SetTextWithoutNotify(_selectedDay.ToString());

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(installmentValueInput.gameObject);

        installmentValueInput.Select();
        installmentValueInput.ActivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private void TryStartCheck()
    {
        if (_busy || _lockedByCooldown) return;
        if (_loanSystem == null || _host == null)
        {
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            return;
        }

        string citizenId = _host.CurrentCitizenId;
        if (!_loanSystem.CanChangeDueDate(citizenId, out var reason))
        {
            SetStatus(dueDateInfoText, reason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            ResetCheckState(false);
            RefreshAvailabilityState();
            return;
        }

        int min = _loanSystem.dueDateMinDay;
        int max = _loanSystem.dueDateMaxDay;
        _selectedDay = Mathf.Clamp(_selectedDay, min, max);
        RefreshDayText();

        StopFlow();
        _flowCo = StartCoroutine(CoCheckDueDate());
    }

    private IEnumerator CoCheckDueDate()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoProcessingText(bankProcessingText, "PROCESSING", checkDelaySeconds);

        if (_loanSystem == null || _host == null)
        {
            _checkPassed = false;
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        string citizenId = _host.CurrentCitizenId;
        if (!_loanSystem.CanChangeDueDate(citizenId, out var reason))
        {
            _checkPassed = false;
            SetStatus(dueDateInfoText, reason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        int currentDay = _loanSystem.GetPreferredDueDay(citizenId);
        if (currentDay == _selectedDay)
        {
            _checkPassed = false;
            SetStatus(dueDateInfoText, "SAME DAY ALREADY SET", wrongColor);
            ShowDecision("FAILED", wrongColor);
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        _checkedDay = _selectedDay;
        _checkPassed = true;

        SetStatus(dueDateInfoText, "NEW DATE READY", correctColor);
        ShowDecision("READY", correctColor);
        StartCoroutine(CoForceFocusNextFrame(FocusStep.Confirm));

        _busy = false;
        UpdateBlockedOverlays();
    }

    private void TryConfirm()
    {
        if (_busy) return;
        if (!_checkPassed) return;
        if (_loanSystem == null || _host == null) return;

        StopFlow();
        _flowCo = StartCoroutine(CoConfirmDueDate());
    }

    private IEnumerator CoConfirmDueDate()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoProcessingText(resumeProcessingText, "PROCESSING", confirmDelaySeconds);

        string citizenId = _host.CurrentCitizenId;
        string reason = "";
        bool ok = false;

        if (_loanSystem != null && !string.IsNullOrWhiteSpace(citizenId))
            ok = _loanSystem.ApplyChangeDueDate(citizenId, _checkedDay, out reason);

        if (!ok)
        {
            SetStatus(dueDateInfoText, string.IsNullOrWhiteSpace(reason) ? "FAILED" : reason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        ResetCheckState(false);
        SetStatus(dueDateInfoText, "ONLY ONCE PER MONTH", wrongColor);

        if (dueDateValueText)
            dueDateValueText.text = _loanSystem.GetDueDateValueText(citizenId);

        ShowDecision("READY", correctColor);
        _lockedByCooldown = true;
        SetFocus(FocusStep.Back);

        _busy = false;
        UpdateBlockedOverlays();
    }

    private void CancelCheck()
    {
        if (_busy) return;
        if (!_checkPassed) return;

        ResetCheckState(false);
        SetStatus(dueDateInfoText, "SELECT NEW DAY AND PRESS CHECK", checkingColor);
        SetFocus(FocusStep.InstallmentInput);
    }

    private void ResetAllUI()
    {
        _selectedDay = _loanSystem != null ? Mathf.Max(1, _loanSystem.GetPreferredDueDay(_host != null ? _host.CurrentCitizenId : null)) : 1;
        _checkedDay = _selectedDay;
        _checkPassed = false;
        _busy = false;
        _editingInstallment = false;
        _lockedByCooldown = false;

        RefreshDayText();
        HideDecision();
        HideProcessingTexts();

        if (dueDateInfoText) dueDateInfoText.text = "";
        if (dueDateValueText) dueDateValueText.text = "";

        _focus = FocusStep.Back;
        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private void ResetCheckState(bool keepDecision)
    {
        _checkPassed = false;
        HideProcessingTexts();

        if (!keepDecision)
            HideDecision();
    }

    private void SetFocus(FocusStep step)
    {
        if (_lockedByCooldown && step != FocusStep.Back)
            step = FocusStep.Back;

        bool hasDecision = _checkPassed;

        if (!hasDecision && (step == FocusStep.Confirm || step == FocusStep.Cancel))
            step = FocusStep.Back;

        _focus = step;

        if (!_editingInstallment && installmentValueInput)
            installmentValueInput.DeactivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();

        if (!EventSystem.current) return;

        GameObject selectedGO = null;

        switch (_focus)
        {
            case FocusStep.Check:
                selectedGO = checkButton ? checkButton.gameObject : null;
                break;
            case FocusStep.Confirm:
                selectedGO = confirmButton ? confirmButton.gameObject : null;
                break;
            case FocusStep.Cancel:
                selectedGO = cancelButton ? cancelButton.gameObject : null;
                break;
            case FocusStep.Back:
                selectedGO = backButton ? backButton.gameObject : null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(selectedGO);
    }

    private void UpdateSelectedVisuals()
    {
        if (selectedInstallment) selectedInstallment.SetActive(_focus == FocusStep.InstallmentInput && !_lockedByCooldown);
        if (selectedCheck) selectedCheck.SetActive(_focus == FocusStep.Check && checkButton != null && checkButton.interactable);
        if (selectedConfirm) selectedConfirm.SetActive(_focus == FocusStep.Confirm && confirmButton != null && confirmButton.interactable);
        if (selectedCancel) selectedCancel.SetActive(_focus == FocusStep.Cancel && cancelButton != null && cancelButton.interactable);
        if (selectedBack) selectedBack.SetActive(_focus == FocusStep.Back);
    }

    private void UpdateBlockedOverlays()
    {
        int min = _loanSystem != null ? _loanSystem.dueDateMinDay : 1;
        int max = _loanSystem != null ? _loanSystem.dueDateMaxDay : 31;
        bool dayOk = _selectedDay >= min && _selectedDay <= max;

        bool canEdit = !_lockedByCooldown && !_busy;
        bool canCheck = canEdit && dayOk;
        bool canConfirm = !_busy && _checkPassed;
        bool canCancel = !_busy && _checkPassed;

        bool inputActive = (_focus == FocusStep.InstallmentInput || _editingInstallment) && canEdit;
        if (installmentBlocked) installmentBlocked.SetActive(!inputActive);

        if (checkButton) checkButton.interactable = canCheck;
        if (checkBlocked) checkBlocked.SetActive(false);

        if (confirmButton) confirmButton.interactable = canConfirm;
        if (confirmBlocked) confirmBlocked.SetActive(!canConfirm);

        if (cancelButton) cancelButton.interactable = canCancel;
        if (cancelBlocked) cancelBlocked.SetActive(!canCancel);

        if (moreButton) moreButton.interactable = canEdit && _focus == FocusStep.InstallmentInput;
        if (lessButton) lessButton.interactable = canEdit && _focus == FocusStep.InstallmentInput;

        if (installmentValueInput)
            installmentValueInput.interactable = canEdit && _focus == FocusStep.InstallmentInput;
    }

    private void MoveFocus(int dir)
    {
        bool forward = dir > 0;

        if (_lockedByCooldown)
        {
            SetFocus(FocusStep.Back);
            return;
        }

        bool hasDecision = _checkPassed;

        switch (_focus)
        {
            case FocusStep.InstallmentInput:
                SetFocus(forward ? FocusStep.Check : FocusStep.Back);
                break;

            case FocusStep.Check:
                if (hasDecision)
                    SetFocus(forward ? FocusStep.Confirm : FocusStep.InstallmentInput);
                else
                    SetFocus(forward ? FocusStep.Back : FocusStep.InstallmentInput);
                break;

            case FocusStep.Confirm:
                SetFocus(forward ? FocusStep.Cancel : FocusStep.Check);
                break;

            case FocusStep.Cancel:
                SetFocus(forward ? FocusStep.Back : FocusStep.Confirm);
                break;

            case FocusStep.Back:
                if (hasDecision)
                    SetFocus(forward ? FocusStep.InstallmentInput : FocusStep.Cancel);
                else
                    SetFocus(forward ? FocusStep.InstallmentInput : FocusStep.Check);
                break;
        }
    }

    private void ActivateFocused()
    {
        switch (_focus)
        {
            case FocusStep.InstallmentInput:
                StartEditInstallment();
                break;

            case FocusStep.Check:
                TryStartCheck();
                break;

            case FocusStep.Confirm:
                TryConfirm();
                break;

            case FocusStep.Cancel:
                CancelCheck();
                break;

            case FocusStep.Back:
                OnBackClicked();
                break;
        }
    }

    private IEnumerator CoProcessingText(TMP_Text target, string prefix, float duration)
    {
        if (!target) yield break;

        target.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            int dots = ((int)(elapsed * 3f)) % 4;
            string d = dots == 0 ? "" : new string('.', dots);
            target.text = $"{prefix}{d}";
            target.color = checkingColor;
            yield return null;
        }

        target.gameObject.SetActive(false);
    }

    private IEnumerator CoForceFocusNextFrame(FocusStep step)
    {
        yield return null;
        SetFocus(step);
    }

    private void ShowDecision(string text, Color color)
    {
        if (!bankVerdictText) return;
        bankVerdictText.gameObject.SetActive(true);
        bankVerdictText.text = text;
        bankVerdictText.color = color;
    }

    private void HideDecision()
    {
        if (!bankVerdictText) return;
        bankVerdictText.text = "";
        bankVerdictText.gameObject.SetActive(false);
    }

    private void HideProcessingTexts()
    {
        if (bankProcessingText)
        {
            bankProcessingText.text = "";
            bankProcessingText.gameObject.SetActive(false);
        }

        if (resumeProcessingText)
        {
            resumeProcessingText.text = "";
            resumeProcessingText.gameObject.SetActive(false);
        }
    }

    private void StopFlow()
    {
        if (_flowCo != null)
        {
            StopCoroutine(_flowCo);
            _flowCo = null;
        }

        _busy = false;
        HideProcessingTexts();
    }

    private static int ParseInt(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;

        int val = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            if (char.IsDigit(raw[i]))
                val = val * 10 + (raw[i] - '0');
        }

        return Mathf.Max(0, val);
    }

    private void Show(bool visible)
    {
        if (!root)
        {
            gameObject.SetActive(visible);
            return;
        }

        root.gameObject.SetActive(visible);
        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    private static void SetStatus(TMP_Text target, string text, Color color)
    {
        if (!target) return;
        target.text = text;
        target.color = color;
        target.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}