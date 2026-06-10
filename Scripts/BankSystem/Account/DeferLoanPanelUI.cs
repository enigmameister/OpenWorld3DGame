using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeferLoanPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Installment input")]
    [SerializeField] private TMP_InputField installmentValueInput;
    [SerializeField] private GameObject installmentBlocked;

    [Header("Buttons")]
    [SerializeField] private Button moreButton;
    [SerializeField] private Button lessButton;
    [SerializeField] private Button checkButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button backButton;

    [Header("Blocked overlays")]
    [SerializeField] private GameObject checkBlocked;
    [SerializeField] private GameObject confirmBlocked;
    [SerializeField] private GameObject cancelBlocked;

    [Header("Texts")]
    [SerializeField] private TMP_Text deferInfoText;
    [SerializeField] private TMP_Text statusText;            // STATUS value
    [SerializeField] private TMP_Text bankProcessingText;    // BankProcessing
    [SerializeField] private TMP_Text resumeProcessingText;  // ResumeProcessing

    [Header("Resume Root (VALUES ONLY)")]
    [SerializeField] private GameObject resumeRoot;
    [SerializeField] private TMP_Text amountValueText;
    [SerializeField] private TMP_Text installmentValueText;
    [SerializeField] private TMP_Text taxValueText;
    [SerializeField] private TMP_Text loanPlayerValueText;
    [SerializeField] private TMP_Text loanTotalValueText;

    [Header("Selected indicators")]
    [SerializeField] private GameObject selInstallment;
    [SerializeField] private GameObject selCheck;
    [SerializeField] private GameObject selConfirm;
    [SerializeField] private GameObject selCancel;
    [SerializeField] private GameObject selBack;

    [Header("Colors")]
    [SerializeField] private Color checkingColor = new(1f, 0.85f, 0.1f);
    [SerializeField] private Color correctColor = new(0.2f, 1f, 0.2f);
    [SerializeField] private Color wrongColor = new(1f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;

    [Header("Config")]
    [SerializeField] private int step = 1;
    [SerializeField] private float checkDelaySeconds = 5f;
    [SerializeField] private float confirmDelaySeconds = 5f;

    private LoanMenuNavigation _controller;
    private AccountOperationsUI _host;
    private LoanSystem _loanSystem;
    private ActiveLoan _loan;
    private LoanSystem.LoanDeferOffer _offer;

    private bool _open;
    private bool _busy;
    private bool _editingInstallment;
    private bool _checkPassed;
    private bool _deferUnavailableLocked;

    private int _deferMonths;
    private Coroutine _co;

    private FocusStep _focus = FocusStep.Back;

    public bool IsOpen => _open;

    private enum FocusStep
    {
        InstallmentInput,
        Check,
        Confirm,
        Cancel,
        Back
    }

    private void Awake()
    {
        Show(false);
        ResetAllUI();

        if (moreButton) moreButton.onClick.AddListener(() => AddMonths(+step));
        if (lessButton) lessButton.onClick.AddListener(() => AddMonths(-step));
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

            installmentValueInput.onValueChanged.AddListener(_ => SyncMonthsFromInput());
            installmentValueInput.onSelect.AddListener(_ =>
            {
                if (!_open || !_editingInstallment) return;
                installmentValueInput.SetTextWithoutNotify(_deferMonths.ToString());
            });
            installmentValueInput.onDeselect.AddListener(_ =>
            {
                if (!_open) return;
                _editingInstallment = false;
                RefreshMonthsText();
                UpdateBlockedOverlays();
            });
            installmentValueInput.onEndEdit.AddListener(_ =>
            {
                if (!_open) return;
                _editingInstallment = false;
                RefreshMonthsText();
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

        // jeśli defer niedostępny -> tylko BACK
        if (_deferUnavailableLocked)
        {
            if (_focus != FocusStep.Back)
                SetFocus(FocusStep.Back);

            if (enter)
                OnBackClicked();

            return;
        }

        // INSTALLMENT: lewo/prawo zmienia miesiące
        if (_focus == FocusStep.InstallmentInput)
        {
            if (left)
            {
                AddMonths(-step);
                return;
            }

            if (right)
            {
                AddMonths(+step);
                return;
            }

            if (enter)
            {
                _editingInstallment = false;
                if (installmentValueInput) installmentValueInput.DeactivateInputField();
                RefreshMonthsText();
                SetFocus(FocusStep.Check);
                return;
            }
        }

        // jeśli edytujemy input i klikamy lewo/prawo
        if (_editingInstallment && installmentValueInput != null && installmentValueInput.isFocused)
        {
            if (left)
            {
                AddMonths(-step);
                return;
            }

            if (right)
            {
                AddMonths(+step);
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
        {
            ActivateFocused();
        }
    }

    public void Open(LoanMenuNavigation controller, AccountOperationsUI host, ActiveLoan loan)
    {
        _controller = controller;
        _host = host;
        _loan = loan;
        _loanSystem = LoanSystem.Instance;

        _open = true;
        _busy = false;

        ResetAllUI();
        Show(true);

        RefreshAvailabilityState();

        if (_deferUnavailableLocked)
            SetFocus(FocusStep.Back);
        else
            SetFocus(FocusStep.InstallmentInput);
    }

    public void Hide()
    {
        _open = false;
        _busy = false;
        StopCo();
        Show(false);
    }

    private void OnBackClicked()
    {
        _controller?.ConsumeEscapeThisFrame();
        _host?.ConsumeEscapeThisFrame();

        // 🔥 KLUCZOWE
        BankDialogueUI.SuppressEscapeFrames = 2;

        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        _controller?.CloseDeferLoan();
    }

    private void RefreshAvailabilityState()
    {
        string info = _loanSystem != null ? _loanSystem.GetDeferInfoText(_loan) : "LOAN SYSTEM MISSING";
        _deferUnavailableLocked = info != "SELECT PERIOD AND PRESS CHECK";
        SetStatus(deferInfoText, info, _deferUnavailableLocked ? wrongColor : checkingColor);
        UpdateBlockedOverlays();
    }

    private void RefreshMonthsText()
    {
        if (!installmentValueInput) return;

        int min = _loanSystem != null ? _loanSystem.deferMinMonths : 1;
        int max = _loanSystem != null ? _loanSystem.deferMaxMonths : 36;
        _deferMonths = Mathf.Clamp(_deferMonths, min, max);

        installmentValueInput.SetTextWithoutNotify(_deferMonths.ToString());
        int caretPos = installmentValueInput.text.Length;
        installmentValueInput.caretPosition = caretPos;
        installmentValueInput.selectionAnchorPosition = caretPos;
        installmentValueInput.selectionFocusPosition = caretPos;
    }

    private void SyncMonthsFromInput()
    {
        if (_deferUnavailableLocked) return;

        _deferMonths = ParseInt(installmentValueInput ? installmentValueInput.text : "0");
        ResetCheckState(false);
        UpdateBlockedOverlays();
    }

    private void AddMonths(int delta)
    {
        if (_deferUnavailableLocked) return;
        if (_focus != FocusStep.InstallmentInput) return;

        int min = _loanSystem != null ? _loanSystem.deferMinMonths : 1;
        int max = _loanSystem != null ? _loanSystem.deferMaxMonths : 36;

        _deferMonths = Mathf.Clamp(_deferMonths + delta, min, max);
        RefreshMonthsText();

        ResetCheckState(false);
        UpdateBlockedOverlays();
    }

    private void StartEditInstallment()
    {
        if (_deferUnavailableLocked) return;
        if (!installmentValueInput) return;

        _editingInstallment = true;
        installmentValueInput.interactable = true;
        installmentValueInput.SetTextWithoutNotify(_deferMonths.ToString());

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(installmentValueInput.gameObject);

        installmentValueInput.Select();
        installmentValueInput.ActivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private void TryStartCheck()
    {
        if (_busy || _deferUnavailableLocked) return;

        if (_loanSystem == null || _loan == null)
        {
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            return;
        }

        if (!_loanSystem.CanDeferLoan(_loan, out var reason))
        {
            SetStatus(deferInfoText, reason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            ResetCheckState(false);
            RefreshAvailabilityState();
            return;
        }

        _deferMonths = Mathf.Clamp(_deferMonths, _loanSystem.deferMinMonths, _loanSystem.deferMaxMonths);
        RefreshMonthsText();

        StopCo();
        _co = StartCoroutine(CoCheckOffer());
    }

    private IEnumerator CoCheckOffer()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoProcessingText(bankProcessingText, "PROCESSING", checkDelaySeconds);

        if (_loanSystem == null || _loan == null)
        {
            _checkPassed = false;
            if (resumeRoot) resumeRoot.SetActive(false);
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        if (!_loanSystem.CanDeferLoan(_loan, out var reason))
        {
            _checkPassed = false;
            if (resumeRoot) resumeRoot.SetActive(false);
            SetStatus(deferInfoText, reason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            RefreshAvailabilityState();
            _busy = false;
            UpdateBlockedOverlays();
            yield break;
        }

        _offer = _loanSystem.BuildDeferOffer(_loan, _deferMonths);
        _checkPassed = _offer.deferMonths > 0;

        if (_checkPassed)
        {
            if (resumeRoot) resumeRoot.SetActive(true);
            FillResumeRoot();
            SetStatus(deferInfoText, "NEW TERMS READY", correctColor);
            ShowDecision("READY", correctColor);
            StartCoroutine(CoForceFocusNextFrame(FocusStep.Confirm));
        }
        else
        {
            if (resumeRoot) resumeRoot.SetActive(false);
            ShowDecision("FAILED", wrongColor);
        }

        _busy = false;
        UpdateBlockedOverlays();
    }

    private void FillResumeRoot()
    {
        if (!_checkPassed || _loan == null)
        {
            ClearResumeRoot();
            return;
        }

        int oldAmount = Mathf.Max(0, _loan.principal);
        int oldInstallments = Mathf.Max(0, _loan.installmentsLeft);
        int oldTax = Mathf.Max(0, _loan.bankTax);
        int oldPlayerLoan = Mathf.Max(0, _loan.remainingToRepay);
        int oldTotal = Mathf.Max(0, _loan.totalToRepay);

        int newAmount = oldAmount;
        int newInstallments = Mathf.Max(0, _offer.deferMonths);
        int newTax = Mathf.Max(0, _offer.addedBankTax);
        int newPlayerLoan = Mathf.Max(0, _offer.remainingToRepay);
        int newTotal = Mathf.Max(0, _offer.totalToRepay);

        SetValueText(amountValueText, $"{oldAmount}$");

        SetValueText(installmentValueText,
            BuildDiffText($"{oldInstallments} MONTHS", $"{newInstallments} MONTHS"));

        float pct = oldPlayerLoan > 0
            ? (newTax / (float)oldPlayerLoan) * 100f
            : 0f;

        SetValueText(taxValueText,
            BuildDiffText($"{oldTax}$", $"{newTax}$ ({pct:0.#}%)"));

        SetValueText(loanPlayerValueText,
            BuildDiffText($"{oldPlayerLoan}$", $"{newPlayerLoan}$"));

        SetValueText(loanTotalValueText,
            BuildDiffText($"{oldTotal}$", $"{newTotal}$"));
    }

    private void TryConfirm()
    {
        if (_busy) return;
        if (!_checkPassed) return;

        if (_loanSystem == null || _loan == null) return;

        StopCo();
        _co = StartCoroutine(CoConfirmDefer());
    }

    private IEnumerator CoConfirmDefer()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoProcessingText(resumeProcessingText, "PROCESSING", confirmDelaySeconds);

        bool ok = _loanSystem != null &&
                  _loan != null &&
                  _loanSystem.ApplyDeferOffer(_loan, _offer, out _);

        if (!ok)
        {
            string failReason = (_loanSystem != null && _loan != null && _loanSystem.TryGetDeferBlockReason(_loan, out var r))
                ? r
                : "FAILED";

            SetStatus(deferInfoText, failReason, wrongColor);
            ShowDecision("FAILED", wrongColor);
            _busy = false;
            RefreshAvailabilityState();
            UpdateBlockedOverlays();
            yield break;
        }

        _host?.Refresh();
        _controller?.RefreshAfterLoanChange();

        ResetCheckState(false);
        SetStatus(deferInfoText, "LOAN IS DEFERRED. UNBLOCK TO CONTINUE", wrongColor);
        ShowDecision("READY", correctColor);
        _deferUnavailableLocked = true;
        SetFocus(FocusStep.Back);

        _busy = false;
        UpdateBlockedOverlays();
    }

    private void CancelCheck()
    {
        if (_busy) return;
        if (!_checkPassed) return;

        ResetCheckState(false);
        SetStatus(deferInfoText, "SELECT PERIOD AND PRESS CHECK", checkingColor);
        SetFocus(FocusStep.InstallmentInput);
    }

    private void ResetAllUI()
    {
        _deferMonths = _loanSystem != null ? Mathf.Max(1, _loanSystem.deferMinMonths) : 1;
        _checkPassed = false;
        _busy = false;
        _editingInstallment = false;
        _deferUnavailableLocked = false;
        _offer = default;

        RefreshMonthsText();

        if (resumeRoot) resumeRoot.SetActive(false);

        HideDecision();
        HideProcessingTexts();
        ClearResumeRoot();

        _focus = FocusStep.Back;
        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private void ResetCheckState(bool keepDecision)
    {
        _checkPassed = false;
        _offer = default;

        if (resumeRoot) resumeRoot.SetActive(false);

        ClearResumeRoot();
        HideProcessingTexts();

        if (!keepDecision)
            HideDecision();
    }

    private void ClearResumeRoot()
    {
        SetValueText(amountValueText, "0$");
        SetValueText(installmentValueText, "0 MONTHS");
        SetValueText(taxValueText, "0$");
        SetValueText(loanPlayerValueText, "0$");
        SetValueText(loanTotalValueText, "0$");
    }

    private void SetFocus(FocusStep step)
    {
        if (_deferUnavailableLocked && step != FocusStep.Back)
            step = FocusStep.Back;

        bool hasOffer = _checkPassed && resumeRoot != null && resumeRoot.activeSelf;

        if (!hasOffer)
        {
            if (step == FocusStep.Confirm || step == FocusStep.Cancel)
                step = FocusStep.Back;
        }

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
            case FocusStep.InstallmentInput:
            default:
                selectedGO = null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(selectedGO);
    }

    private void UpdateSelectedVisuals()
    {
        if (selInstallment) selInstallment.SetActive(_focus == FocusStep.InstallmentInput && !_deferUnavailableLocked);
        if (selCheck) selCheck.SetActive(_focus == FocusStep.Check && checkButton != null && checkButton.interactable);
        if (selConfirm) selConfirm.SetActive(_focus == FocusStep.Confirm && confirmButton != null && confirmButton.interactable);
        if (selCancel) selCancel.SetActive(_focus == FocusStep.Cancel && cancelButton != null && cancelButton.interactable);
        if (selBack) selBack.SetActive(_focus == FocusStep.Back);
    }

    private void UpdateBlockedOverlays()
    {
        int min = _loanSystem != null ? _loanSystem.deferMinMonths : 1;
        int max = _loanSystem != null ? _loanSystem.deferMaxMonths : 36;
        bool monthsOk = _deferMonths >= min && _deferMonths <= max;

        bool loanOk = !_deferUnavailableLocked && _loanSystem != null && _loan != null && _loanSystem.CanDeferLoan(_loan, out _);

        bool inputActive = (_focus == FocusStep.InstallmentInput || _editingInstallment) && !_busy && loanOk;
        if (installmentBlocked) installmentBlocked.SetActive(!inputActive);

        bool canCheck = !_busy && loanOk && monthsOk;
        bool canConfirm = !_busy && _checkPassed;
        bool canCancel = !_busy && _checkPassed;

        if (checkButton) checkButton.interactable = canCheck;
        if (checkBlocked) checkBlocked.SetActive(false); // CHECK ma być widoczny normalnie

        if (confirmButton) confirmButton.interactable = canConfirm;
        if (confirmBlocked) confirmBlocked.SetActive(!canConfirm);

        if (cancelButton) cancelButton.interactable = canCancel;
        if (cancelBlocked) cancelBlocked.SetActive(!canCancel);

        if (moreButton) moreButton.interactable = !_busy && loanOk && _focus == FocusStep.InstallmentInput;
        if (lessButton) lessButton.interactable = !_busy && loanOk && _focus == FocusStep.InstallmentInput;

        if (installmentValueInput)
            installmentValueInput.interactable = !_busy && loanOk && _focus == FocusStep.InstallmentInput;
    }

    private IEnumerator CoProcessingText(TMP_Text t, string prefix, float duration)
    {
        if (!t) yield break;

        t.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            int dots = ((int)(elapsed * 3f)) % 4;
            string d = dots == 0 ? "" : new string('.', dots);
            t.text = $"{prefix}{d}";
            t.color = checkingColor;
            yield return null;
        }

        t.gameObject.SetActive(false);
    }

    private void ShowDecision(string text, Color color)
    {
        if (!statusText) return;
        statusText.gameObject.SetActive(true);
        statusText.text = text;
        statusText.color = color;
    }

    private void HideDecision()
    {
        if (!statusText) return;
        statusText.text = "";
        statusText.gameObject.SetActive(false);
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

    private static int ParseInt(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;

        int val = 0;
        for (int i = 0; i < raw.Length; i++)
            if (char.IsDigit(raw[i]))
                val = val * 10 + (raw[i] - '0');

        return Mathf.Max(0, val);
    }

    private void StopCo()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        _busy = false;
        HideProcessingTexts();
    }

    private void Show(bool v)
    {
        if (!root)
        {
            gameObject.SetActive(v);
            return;
        }

        root.gameObject.SetActive(v);
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
    }

    private static void SetStatus(TMP_Text t, string text, Color c)
    {
        if (!t) return;
        t.text = text;
        t.color = c;
        t.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private static void SetValueText(TMP_Text t, string text)
    {
        if (!t) return;
        t.text = text;
    }

    private void MoveFocus(int dir)
    {
        bool forward = dir > 0;

        if (_deferUnavailableLocked)
        {
            SetFocus(FocusStep.Back);
            return;
        }

        bool hasOffer = _checkPassed && resumeRoot != null && resumeRoot.activeSelf;

        switch (_focus)
        {
            case FocusStep.InstallmentInput:
                SetFocus(forward ? FocusStep.Check : FocusStep.Back);
                break;

            case FocusStep.Check:
                if (hasOffer)
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
                if (hasOffer)
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

    private IEnumerator CoForceFocusNextFrame(FocusStep step)
    {
        yield return null;
        SetFocus(step);
    }

    private string BuildDiffText(string oldValue, string newValue)
    {
        return $"<color=#FF4040><s>{oldValue}</s></color>  {newValue}";
    }


}