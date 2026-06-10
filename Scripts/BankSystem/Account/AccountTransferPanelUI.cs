using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AccountTransferPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField accountNumberInput;   // InputAccount_Number
    [SerializeField] private TMP_InputField valueInput;           // InputMoney_Value

    [Header("Blocked overlays (Image objects)")]
    [SerializeField] private GameObject accountBlocked;           // Account_Number/Blocked
    [SerializeField] private GameObject valueBlocked;             // Money_Value/Blocked
    [SerializeField] private GameObject confirmBlocked;           // Confirm Btn/Blocked
    [Header("Enter buttons")]
    [SerializeField] private Button accountEnterButton;           // Account_Number/Enter
    [SerializeField] private Button valueEnterButton;             // Money_Value/Enter

    [Header("Status texts (single TMP each)")]
    [SerializeField] private TMP_Text accountStatusText;          // CHECKING/CORRECT/WRONG
    [SerializeField] private TMP_Text valueStatusText;            // CHECKING/CORRECT/WRONG
    [SerializeField] private TMP_Text readyText;                  // PROCESSING.. -> READY

    [Header("Status Colors")]
    [SerializeField] private Color checkingColor = new(1f, 0.85f, 0.1f);
    [SerializeField] private Color correctColor = new(0.2f, 1f, 0.2f);
    [SerializeField] private Color wrongColor = new(1f, 0.2f, 0.2f);

    [Header("Value +/-")]
    [SerializeField] private Button moreButton;                   // More+
    [SerializeField] private Button lessButton;                   // Less-
    [SerializeField] private int step = 1;

    [Header("Bottom buttons")]
    [SerializeField] private Button confirmButton;                // Confirm
    [SerializeField] private Button cancelButton;                 // Cancel
    [SerializeField] private Button backButton;                   // Back

    [Header("Selected indicators (optional)")]
    [SerializeField] private GameObject selAccount;
    [SerializeField] private GameObject selValue;
    [SerializeField] private GameObject selConfirm;
    [SerializeField] private GameObject selCancel;
    [SerializeField] private GameObject selBack;


    [Header("Timings")]
    [SerializeField] private float checkDelaySeconds = 5f;
    [SerializeField] private float confirmDelaySeconds = 5f;

    private AccountOperationsUI _host;
    private int _fromAccountId;

    private bool _open;
    private bool _busy;

    private int _toAccountId;
    private int _amount;

    private bool _accountOk;
    private bool _valueOk;

    private Coroutine _co;

    private bool _editingValue;
    private bool _editingAccount;
    private bool _skipNextValueEnterPress;
    private bool _skipNextAccountEnterPress;

    private FocusStep _focus = FocusStep.AccountInput;

    public bool IsOpen => _open;

    private enum FocusStep
    {
        AccountInput,
        AccountEnter,
        ValueInput,
        ValueEnter,
        Confirm,
        Cancel,
        Back
    }

    private void Awake()
    {
        Show(false);
        ResetAllUI();

        if (moreButton) moreButton.onClick.AddListener(() => AddAmount(+step));
        if (lessButton) lessButton.onClick.AddListener(() => AddAmount(-step));

        if (accountEnterButton) accountEnterButton.onClick.AddListener(() => TryStartAccountCheck());
        if (valueEnterButton) valueEnterButton.onClick.AddListener(() => TryStartValueCheck());

        if (confirmButton) confirmButton.onClick.AddListener(() => TryConfirmTransfer());
        if (cancelButton) cancelButton.onClick.AddListener(CancelAll);
        if (backButton) backButton.onClick.AddListener(OnBackClicked);

        if (accountNumberInput)
        {
            accountNumberInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            accountNumberInput.characterLimit = 10; // albo ile chcesz
            accountNumberInput.lineType = TMP_InputField.LineType.SingleLine;
            accountNumberInput.richText = false;

            accountNumberInput.onValueChanged.AddListener(_ => SyncAccountFromInput());

            accountNumberInput.onSubmit.AddListener(_ =>
            {
                if (!_open) return;

                _editingAccount = false;
                accountNumberInput.DeactivateInputField();
                SetFocus(FocusStep.AccountEnter);
                _skipNextAccountEnterPress = true;
            });
        }

        if (valueInput)
        {
            valueInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            valueInput.characterLimit = 10;
            valueInput.lineType = TMP_InputField.LineType.SingleLine;
            valueInput.richText = false;

            valueInput.onValueChanged.AddListener(_ => SyncAmountFromInput());

            valueInput.onSubmit.AddListener(_ =>
            {
                if (!_open) return;

                _editingValue = false;
                valueInput.DeactivateInputField();
                RefreshValueTextWithDollar();
                SetFocus(FocusStep.ValueEnter);
                _skipNextValueEnterPress = true;
            });

            valueInput.onSelect.AddListener(_ =>
            {
                if (!_open) return;

                if (_editingValue)
                    valueInput.SetTextWithoutNotify(_amount.ToString());
            });

            valueInput.onDeselect.AddListener(_ =>
            {
                if (!_open) return;
                RefreshValueTextWithDollar();
            });

            valueInput.onEndEdit.AddListener(_ =>
            {
                if (!_open) return;
                RefreshValueTextWithDollar();
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

        // =========================
        // ACCOUNT INPUT - edit mode
        // =========================
        if (_editingAccount && accountNumberInput != null && accountNumberInput.isFocused)
        {
            if (left)
            {
                _editingAccount = false;
                accountNumberInput.DeactivateInputField();
                SetFocus(FocusStep.AccountInput);
                return;
            }

            if (enter || right)
            {
                _editingAccount = false;
                accountNumberInput.DeactivateInputField();
                SetFocus(FocusStep.AccountEnter);
                _skipNextAccountEnterPress = true;
                return;
            }

            return;
        }

        // =========================
        // VALUE INPUT - edit mode
        // =========================
        if (_editingValue && valueInput != null && valueInput.isFocused)
        {
            if (up)
            {
                AddAmount(+step);
                return;
            }

            if (down)
            {
                AddAmount(-step);
                return;
            }

            if (left)
            {
                _editingValue = false;
                valueInput.DeactivateInputField();
                SetFocus(FocusStep.ValueInput);
                return;
            }

            if (enter || right)
            {
                _editingValue = false;
                valueInput.DeactivateInputField();
                RefreshValueTextWithDollar();
                SetFocus(FocusStep.ValueEnter);
                _skipNextValueEnterPress = true;
                return;
            }

            return;
        }

        if (_focus == FocusStep.Back)
        {
            if (left)
            {
                SetFocus(FocusStep.Cancel);
                return;
            }

            if (up)
            {
                SetFocus(FocusStep.Cancel);
                return;
            }

            if (down)
            {
                SetFocus(FocusStep.AccountInput);
                return;
            }

            if (enter)
            {
                OnBackClicked();
                return;
            }
        }
        // =========================
        // Focused states
        // =========================
        switch (_focus)
        {
            case FocusStep.AccountInput:
                {
                    if (enter || right)
                    {
                        StartEditAccount();
                        return;
                    }

                    if (down)
                    {
                        SetFocus(FocusStep.ValueInput);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    return;
                }

            case FocusStep.AccountEnter:
                {
                    if (_skipNextAccountEnterPress)
                    {
                        _skipNextAccountEnterPress = false;
                        return;
                    }

                    if (left)
                    {
                        StartEditAccount();
                        return;
                    }

                    if (down)
                    {
                        SetFocus(FocusStep.ValueInput);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.AccountInput);
                        return;
                    }

                    if (enter)
                    {
                        TryStartAccountCheck();
                        return;
                    }

                    return;
                }

            case FocusStep.ValueInput:
                {
                    if (right)
                    {
                        StartEditValue();
                        return;
                    }

                    if (enter)
                    {
                        SetFocus(FocusStep.ValueEnter);
                        return;
                    }

                    if (left || up)
                    {
                        SetFocus(FocusStep.AccountInput);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(confirmButton != null && confirmButton.interactable
                            ? FocusStep.Confirm
                            : FocusStep.Cancel);
                        return;
                    }

                    return;
                }

            case FocusStep.ValueEnter:
                {
                    if (_skipNextValueEnterPress)
                    {
                        _skipNextValueEnterPress = false;
                        return;
                    }

                    if (left)
                    {
                        StartEditValue();
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.ValueInput);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(confirmButton != null && confirmButton.interactable
                            ? FocusStep.Confirm
                            : FocusStep.Cancel);
                        return;
                    }

                    if (enter)
                    {
                        TryStartValueCheck();
                        return;
                    }

                    return;
                }

            case FocusStep.Cancel:
                {
                    if (right)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    break;
                }

            case FocusStep.Back:
                {
                    if (left)
                    {
                        SetFocus(FocusStep.Cancel);
                        return;
                    }

                    break;
                }
        }

        // =========================
        // fallback
        // =========================
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

    // =========================
    // Public API
    // =========================

    private void RefreshValueTextWithDollar()
    {
        if (!valueInput) return;

        valueInput.SetTextWithoutNotify($"{_amount}$");

        // caret przed znakiem $
        int caretPos = Mathf.Max(0, valueInput.text.Length - 1);
        valueInput.caretPosition = caretPos;
        valueInput.selectionAnchorPosition = caretPos;
        valueInput.selectionFocusPosition = caretPos;
    }
    private void OnBackClicked()
    {
        _host?.ConsumeEscapeThisFrame();
        Close(goBackToMenu: true);
    }

    public void Open(AccountOperationsUI host, int fromAccountId)
    {
        _host = host;
        _fromAccountId = fromAccountId;

        _open = true;
        _busy = false;

        ResetAllUI();
        Show(true);

        SetFocus(FocusStep.AccountInput);
    }

    public void Hide()
    {
        _open = false;
        _busy = false;
        StopCo();
        Show(false);
    }

    public void Close(bool goBackToMenu)
    {
        Hide();
        if (goBackToMenu) _host?.ShowMainMenu();
    }

    // =========================
    // Focus / Selection
    // =========================
    private void MoveFocus(int dir)
    {
        bool forward = dir > 0;
        FocusStep next = _focus;

        switch (_focus)
        {
            case FocusStep.AccountInput:
            case FocusStep.AccountEnter:
                next = forward ? FocusStep.ValueInput : FocusStep.Back;
                break;

            case FocusStep.ValueInput:
                next = forward ? (CanFocusConfirm() ? FocusStep.Confirm : FocusStep.Cancel) : FocusStep.AccountInput;
                break;

            case FocusStep.ValueEnter:
                next = forward ? (CanFocusConfirm() ? FocusStep.Confirm : FocusStep.Cancel) : FocusStep.ValueInput;
                break;

            case FocusStep.Confirm:
                next = forward ? FocusStep.Cancel : FocusStep.ValueInput;
                break;

            case FocusStep.Cancel:
                next = forward ? FocusStep.Back : (CanFocusConfirm() ? FocusStep.Confirm : FocusStep.ValueInput);
                break;

            case FocusStep.Back:
                next = forward ? FocusStep.AccountInput : FocusStep.Cancel;
                break;
        }

        SetFocus(next);
    }

    private void SetFocus(FocusStep step)
    {
        _focus = step;

        _editingAccount = false;
        _editingValue = false;

        if (accountNumberInput) accountNumberInput.DeactivateInputField();
        if (valueInput) valueInput.DeactivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();

        if (!EventSystem.current) return;

        GameObject selectedGO = null;

        switch (step)
        {
            case FocusStep.AccountEnter:
                selectedGO = accountEnterButton ? accountEnterButton.gameObject : null;
                break;

            case FocusStep.ValueEnter:
                selectedGO = valueEnterButton ? valueEnterButton.gameObject : null;
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

            // AccountInput i ValueInput:
            // tylko kółko/sekcja selected, bez zielonego focusa na InputField
            case FocusStep.AccountInput:
            case FocusStep.ValueInput:
            default:
                selectedGO = null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(selectedGO);
    }

    private bool CanFocusConfirm()
    {
        return confirmButton != null && confirmButton.interactable;
    }

    private void ActivateFocused()
    {
        switch (_focus)
        {
            case FocusStep.AccountInput:
                StartEditAccount();
                break;

            case FocusStep.AccountEnter:
                TryStartAccountCheck();
                break;

            case FocusStep.ValueInput:
                StartEditValue();
                break;

            case FocusStep.ValueEnter:
                TryStartValueCheck();
                break;

            case FocusStep.Confirm:
                TryConfirmTransfer();
                break;

            case FocusStep.Cancel:
                CancelAll();
                break;

            case FocusStep.Back:
                OnBackClicked();
                break;
        }
    }

    private void UpdateSelectedVisuals()
    {
        if (selAccount) selAccount.SetActive(_focus == FocusStep.AccountInput || _focus == FocusStep.AccountEnter);
        if (selValue) selValue.SetActive(_focus == FocusStep.ValueInput || _focus == FocusStep.ValueEnter);
        if (selConfirm) selConfirm.SetActive(_focus == FocusStep.Confirm);
        if (selCancel) selCancel.SetActive(_focus == FocusStep.Cancel);
        if (selBack) selBack.SetActive(_focus == FocusStep.Back);
    }

    private void UpdateBlockedOverlays()
    {
        bool accountInputActive = (_focus == FocusStep.AccountInput || _editingAccount) && !_busy;
        bool valueInputActive = (_focus == FocusStep.ValueInput || _editingValue) && !_busy;

        if (accountBlocked) accountBlocked.SetActive(!accountInputActive);
        if (valueBlocked) valueBlocked.SetActive(!valueInputActive);

        if (accountEnterButton)
            accountEnterButton.interactable = !_busy && _toAccountId > 0;

        if (valueEnterButton)
            valueEnterButton.interactable = !_busy && _amount > 0;

        bool confirmActive = !_busy && _accountOk && _valueOk;

        if (confirmButton)
            confirmButton.interactable = confirmActive;

        if (confirmBlocked)
            confirmBlocked.SetActive(!confirmActive);

        if (selConfirm)
            selConfirm.SetActive(_focus == FocusStep.Confirm && confirmButton.interactable);
    }

    // =========================
    // Input parsing
    // =========================
    private void SyncAccountFromInput()
    {
        _toAccountId = ParseInt(accountNumberInput ? accountNumberInput.text : "");
        _accountOk = false;
        SetStatus(accountStatusText, "", Color.white);
        UpdateBlockedOverlays();
    }

    private void SyncAmountFromInput()
    {
        _amount = ParseInt(valueInput ? valueInput.text : "");
        _valueOk = false;
        SetStatus(valueStatusText, "", Color.white);
        UpdateBlockedOverlays();

        if (!_editingValue)
            RefreshValueTextWithDollar();
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

    private void AddAmount(int delta)
    {
        _amount = Mathf.Max(0, _amount + delta);
        RefreshValueTextWithDollar();
        _valueOk = false;
        SetStatus(valueStatusText, "", Color.white);
        UpdateBlockedOverlays();
    }

    // =========================
    // Checking
    // =========================
    private void TryStartAccountCheck()
    {
        if (_busy) return;
        if (_toAccountId <= 0) return;

        // nie można na swoje konto
        if (_toAccountId == _fromAccountId)
        {
            _accountOk = false;
            SetStatus(accountStatusText, "WRONG", wrongColor);
            UpdateBlockedOverlays();
            return;
        }

        StopCo();
        _co = StartCoroutine(CoCheckAccount());
    }

    private IEnumerator CoCheckAccount()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoCheckingText(accountStatusText);

        var bank = BankSystem.Instance;
        bool exists = (bank != null) && bank.TryGetAccount(_toAccountId, out _);

        _accountOk = exists;
        SetStatus(accountStatusText, exists ? "CORRECT" : "WRONG", exists ? correctColor : wrongColor);

        _busy = false;
        UpdateBlockedOverlays();

        // po checku konta -> przejdź na ValueInput
        if (exists) SetFocus(FocusStep.ValueInput);
    }

    private void TryStartValueCheck()
    {
        if (_busy) return;
        if (_amount <= 0) return;

        StopCo();
        _co = StartCoroutine(CoCheckValue());
    }

    private IEnumerator CoCheckValue()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoCheckingText(valueStatusText);

        var bank = BankSystem.Instance;
        bool ok = false;

        if (bank != null)
        {
            int bal = bank.GetBalance(_fromAccountId);
            ok = bal >= _amount;
        }

        _valueOk = ok;
        SetStatus(valueStatusText, ok ? "CORRECT" : "WRONG", ok ? correctColor : wrongColor);

        _busy = false;
        UpdateBlockedOverlays();

        // po checku kwoty -> jeśli oba OK, focus na CONFIRM
        if (_accountOk && _valueOk)
            SetFocus(FocusStep.Confirm);
    }

    private IEnumerator CoCheckingText(TMP_Text t)
    {
        if (!t) yield break;

        float elapsed = 0f;
        while (elapsed < checkDelaySeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            int dots = ((int)(elapsed * 3f)) % 4;
            string d = dots == 0 ? "" : new string('.', dots);
            SetStatus(t, $"CHECKING{d}", checkingColor);
            yield return null;
        }
    }

    // =========================
    // Confirm transfer
    // =========================
    private void TryConfirmTransfer()
    {
        if (_busy) return;
        if (!_accountOk || !_valueOk) return;

        StopCo();
        _co = StartCoroutine(CoConfirmTransfer());
    }

    private IEnumerator CoConfirmTransfer()
    {
        _busy = true;
        UpdateBlockedOverlays();

        yield return CoProcessingReady();

        var bank = BankSystem.Instance;
        bool ok = false;

        if (bank != null)
        {
            // Prosty transfer bez fee:
            ok = bank.TransferNoFee(_fromAccountId, _toAccountId, _amount);
        }

        if (!ok)
        {
            // jeśli fail na końcu, pokaż jako WRONG (np. ktoś zdjął saldo w międzyczasie)
            SetStatus(readyText, "FAILED", wrongColor);
        }
        else
        {
            SetStatus(readyText, "READY", correctColor);
            _host?.Refresh();
        }

        _busy = false;
        UpdateBlockedOverlays();

        // zostaw READY, a gracz może BACK/ESC
        SetFocus(FocusStep.Back);
    }

    private IEnumerator CoProcessingReady()
    {
        if (!readyText) yield break;

        float elapsed = 0f;
        while (elapsed < confirmDelaySeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            int dots = ((int)(elapsed * 3f)) % 4;
            string d = dots == 0 ? "" : new string('.', dots);
            SetStatus(readyText, $"PROCESSING{d}", checkingColor);
            yield return null;
        }
    }

    // =========================
    // Cancel / reset
    // =========================
    private void CancelAll()
    {
        if (_busy) return;

        _toAccountId = 0;
        _amount = 0;
        _accountOk = false;
        _valueOk = false;

        if (accountNumberInput) accountNumberInput.SetTextWithoutNotify("");
        if (valueInput) valueInput.SetTextWithoutNotify("0$");

        SetStatus(accountStatusText, "", Color.white);
        SetStatus(valueStatusText, "", Color.white);
        SetStatus(readyText, "", Color.white);

        SetFocus(FocusStep.AccountInput);
    }

    private void ResetAllUI()
    {
        _toAccountId = 0;
        _amount = 0;
        _accountOk = false;
        _valueOk = false;

        if (accountNumberInput) accountNumberInput.SetTextWithoutNotify("");
        if (valueInput) valueInput.SetTextWithoutNotify("0$");

        SetStatus(accountStatusText, "", Color.white);
        SetStatus(valueStatusText, "", Color.white);
        SetStatus(readyText, "", Color.white);

        _focus = FocusStep.AccountInput;
        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private static void SetStatus(TMP_Text t, string text, Color c)
    {
        if (!t) return;
        t.text = text;
        t.color = c;
        t.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void StopCo()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        _busy = false;
    }

    private void Show(bool v)
    {
        if (!root) { gameObject.SetActive(v); return; }

        // ✅ najważniejsze: obiekt panelu ma być fizycznie włączony/wyłączony
        root.gameObject.SetActive(v);

        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
    }
    private void StartEditAccount()
    {
        if (!accountNumberInput) return;

        _focus = FocusStep.AccountInput;
        _editingAccount = true;
        _editingValue = false;

        if (valueInput) valueInput.DeactivateInputField();
        accountNumberInput.DeactivateInputField();

        accountNumberInput.interactable = true;

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(accountNumberInput.gameObject);

        accountNumberInput.Select();
        accountNumberInput.ActivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }

    private void StartEditValue()
    {
        if (!valueInput) return;

        _focus = FocusStep.ValueInput;
        _editingValue = true;
        _editingAccount = false;

        valueInput.interactable = true;
        valueInput.SetTextWithoutNotify(_amount.ToString());
        valueInput.Select();
        valueInput.ActivateInputField();

        UpdateSelectedVisuals();
        UpdateBlockedOverlays();
    }
}
