using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RepayLoanPanel : MonoBehaviour
{
    private enum Focus
    {
        Value,
        Cancel,
        Confirm,
        Back
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Input")]
    [SerializeField] private TMP_InputField amountInput;   // Money_Value
    [SerializeField] private GameObject valueBlocked;      // Money_Value/Blocked

    [Header("Texts")]
    [SerializeField] private TMP_Text processingText;      // Processing_Text
    [SerializeField] private TMP_Text successText;         // Success_Text

    [Header("Buttons")]
    [SerializeField] private Button moreButton;            // More+
    [SerializeField] private Button lessButton;            // Less-
    [SerializeField] private Button confirmButton;         // Button_Confirm
    [SerializeField] private Button cancelButton;          // Button_Cancel
    [SerializeField] private Button backButton;            // Button_Back

    [Header("Behavior")]
    [SerializeField] private int step = 1;
    [SerializeField] private float operationDelaySeconds = 5f;

    [Header("Hold Acceleration")]
    [SerializeField] private float holdStartDelay = 0.25f;
    [SerializeField] private float holdInitialInterval = 0.08f;
    [SerializeField] private float holdMinInterval = 0.015f;
    [SerializeField] private float holdAccelPerSecond = 0.20f;

    private LoanMenuNavigation _owner;
    private AccountOperationsUI _host;
    private ActiveLoan _loan;

    private int _amount;
    private bool _isOpen;
    private bool _isProcessing;

    private bool _requestSelectConfirm;
    private bool _selectMeansEdit;
    private bool _suppressAmountFormatting;

    private Coroutine _processingCo;
    private Coroutine _holdCo;

    private Focus _focus = Focus.Value;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        Show(false);

        if (processingText) processingText.gameObject.SetActive(false);
        if (successText) successText.gameObject.SetActive(false);

        if (moreButton) moreButton.onClick.AddListener(() => AddAmount(+step));
        if (lessButton) lessButton.onClick.AddListener(() => AddAmount(-step));
        if (confirmButton) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton) cancelButton.onClick.AddListener(CancelAmount);
        if (backButton) backButton.onClick.AddListener(OnBackClicked);

        if (amountInput)
        {
            amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            amountInput.characterLimit = 10;
            amountInput.lineType = TMP_InputField.LineType.SingleLine;
            amountInput.richText = false;

            amountInput.onValueChanged.AddListener(_ =>
            {
                if (_suppressAmountFormatting) return;
                SyncAmountFromInput();
            });

            amountInput.onSelect.AddListener(_ =>
            {
                if (!_isOpen) return;
                RefreshAmountTextKeepDollar();
            });

            amountInput.onDeselect.AddListener(_ =>
            {
                if (!_isOpen) return;
                RefreshAmountTextKeepDollar();
            });

            amountInput.onEndEdit.AddListener(_ =>
            {
                if (!_isOpen) return;
                RefreshAmountTextKeepDollar();
            });

            amountInput.onSubmit.AddListener(_ =>
            {
                if (!_isOpen) return;
                _requestSelectConfirm = true;
            });
        }

        AttachHoldTriggers(moreButton, +1);
        AttachHoldTriggers(lessButton, -1);
    }

    private void Update()
    {
        if (!_isOpen || _isProcessing) return;

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

        if (_requestSelectConfirm)
        {
            _requestSelectConfirm = false;
            EnterButtons(Focus.Confirm);
            return;
        }

        // VALUE edit mode
        if (_focus == Focus.Value && _selectMeansEdit && amountInput != null && amountInput.isFocused)
        {
            if (enter || left || right)
            {
                _selectMeansEdit = false;
                amountInput.DeactivateInputField();
                RefreshAmountTextKeepDollar();
                EnterButtons(Focus.Confirm);
                return;
            }

            return;
        }

        // Buttons zone
        if (_focus != Focus.Value)
        {
            if (amountInput) amountInput.DeactivateInputField();

            if (up)
            {
                MoveButtonFocus(-1);
                return;
            }

            if (down)
            {
                MoveButtonFocus(+1);
                return;
            }

            if (left || right)
            {
                EnterValueArrowMode();
                return;
            }

            if (enter)
            {
                ActivateFocused();
                return;
            }

            return;
        }

        // VALUE arrow mode
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

        if (left || right)
        {
            EnterButtons(Focus.Confirm);
            return;
        }

        if (enter)
        {
            EnterValueEditMode();
            return;
        }
    }

    public void Open(LoanMenuNavigation owner, AccountOperationsUI host, ActiveLoan loan)
    {
        _owner = owner;
        _host = host;
        _loan = loan;

        _amount = 0;
        _isOpen = true;
        _isProcessing = false;

        if (processingText) processingText.gameObject.SetActive(false);
        if (successText) successText.gameObject.SetActive(false);

        Show(true);
        EnterValueEditMode();

        if (amountInput)
        {
            amountInput.Select();
            amountInput.ActivateInputField();
        }

        RefreshAll();
    }

    public void Hide()
    {
        StopProcessing();
        StopHold();

        _isOpen = false;
        Show(false);
    }

    public void Close(bool goBackToMenu)
    {
        Hide();

        if (goBackToMenu)
        {
            if (_owner != null)
                _owner.BackToMenu();
            else
                _host?.ShowMainMenu();
        }
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

    private void OnBackClicked()
    {
        _host?.ConsumeEscapeThisFrame();
        _owner?.ConsumeEscapeThisFrame();
        Hide();
        _owner?.CloseRepayLoan();
    }

    private void CancelAmount()
    {
        if (_isProcessing) return;

        SetAmount(0);

        if (processingText) processingText.gameObject.SetActive(false);
        if (successText) successText.gameObject.SetActive(false);

        EnterValueEditMode();
        RefreshAll();
    }

    private void Confirm()
    {
        if (_isProcessing) return;
        if (_loan == null) return;
        if (_loan.finished || _loan.defaulted) return;
        if (_amount <= 0) return;

        int balance = GetCurrentBalance();
        if (balance <= 0) return;

        StopProcessing();
        _processingCo = StartCoroutine(CoProcessRepay());
    }

    private IEnumerator CoProcessRepay()
    {
        _isProcessing = true;

        if (successText) successText.gameObject.SetActive(false);
        if (processingText) processingText.gameObject.SetActive(true);

        float t = 0f;
        while (t < operationDelaySeconds)
        {
            t += Time.unscaledDeltaTime;

            if (processingText)
            {
                int dots = (int)(t * 3f) % 4;
                string d = dots == 0 ? "" : new string('.', dots);
                processingText.text = $"PROCESSING{d}";
            }

            yield return null;
        }

        bool ok = false;
        int repaid = 0;
        string reason = "";

        var loanSystem = LoanSystem.Instance;
        if (loanSystem != null && _loan != null)
            ok = loanSystem.TryRepayLoan(_loan, _amount, out repaid, out reason);

        if (processingText) processingText.gameObject.SetActive(false);

        if (successText)
        {
            successText.gameObject.SetActive(true);
            successText.text = ok ? "SUCCESS" : "FAILED";
        }

        _isProcessing = false;

        _host?.Refresh();
        _owner?.RefreshAfterLoanChange();

        if (!ok)
        {
            RefreshAll();
            yield break;
        }

        bool fullyRepaid = _loan == null || _loan.finished || _loan.remainingToRepay <= 0;

        if (fullyRepaid)
        {
            yield return new WaitForSecondsRealtime(0.4f);

            Hide();

            _owner?.RefreshAfterLoanChange();
            _owner?.BackToMenu();
            yield break;
        }

        SetAmount(0);

        if (amountInput)
        {
            amountInput.SetTextWithoutNotify("0$");
            amountInput.Select();
            amountInput.ActivateInputField();
        }

        EnterValueEditMode();
        RefreshAll();

        yield return new WaitForSecondsRealtime(0.8f);
        if (successText) successText.gameObject.SetActive(false);
    }

    private void StopProcessing()
    {
        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }

        _isProcessing = false;

        if (processingText)
        {
            processingText.text = "";
            processingText.gameObject.SetActive(false);
        }
    }

    private void SyncAmountFromInput()
    {
        if (!amountInput) return;

        string raw = amountInput.text;
        int parsed = ParseDigits(raw);
        int balance = GetCurrentBalance();

        _amount = Mathf.Clamp(parsed, 0, balance);
        RefreshAmountTextKeepDollar();
        RefreshAll();
    }

    private void AddAmount(int delta)
    {
        if (_isProcessing) return;

        int balance = GetCurrentBalance();
        _amount = Mathf.Clamp(_amount + delta, 0, balance);

        RefreshAmountTextKeepDollar();
        RefreshAll();
    }

    private void SetAmount(int value)
    {
        int balance = GetCurrentBalance();
        _amount = Mathf.Clamp(value, 0, balance);
        RefreshAmountTextKeepDollar();
    }

    private void RefreshAmountTextKeepDollar()
    {
        if (!amountInput) return;

        _suppressAmountFormatting = true;

        string formatted = $"{_amount}$";
        amountInput.SetTextWithoutNotify(formatted);

        int caretPos = Mathf.Clamp(formatted.Length - 1, 0, formatted.Length);
        amountInput.caretPosition = caretPos;
        amountInput.selectionAnchorPosition = caretPos;
        amountInput.selectionFocusPosition = caretPos;

        _suppressAmountFormatting = false;
    }

    private void EnterValueArrowMode()
    {
        _focus = Focus.Value;
        _selectMeansEdit = false;

        if (amountInput)
        {
            amountInput.SetTextWithoutNotify($"{_amount}$");

            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(amountInput.gameObject);

            amountInput.DeactivateInputField();
        }

        RefreshAll();
    }

    private void EnterValueEditMode()
    {
        _focus = Focus.Value;
        _selectMeansEdit = true;

        if (amountInput)
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(amountInput.gameObject);

            amountInput.SetTextWithoutNotify(_amount.ToString());
            amountInput.Select();
            amountInput.ActivateInputField();
        }

        RefreshAll();
    }

    private void EnterButtons(Focus start)
    {
        _focus = start;
        _selectMeansEdit = false;

        if (amountInput)
            amountInput.DeactivateInputField();

        SelectButtonByFocus();
        RefreshAll();
    }

    private void MoveButtonFocus(int dir)
    {
        Focus[] order = { Focus.Cancel, Focus.Confirm, Focus.Back };
        int idx = 0;

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == _focus)
            {
                idx = i;
                break;
            }
        }

        idx = (idx + dir) % order.Length;
        if (idx < 0) idx += order.Length;

        _focus = order[idx];
        SelectButtonByFocus();
        RefreshAll();
    }

    private void SelectButtonByFocus()
    {
        if (!EventSystem.current) return;

        Button b = _focus switch
        {
            Focus.Cancel => cancelButton,
            Focus.Confirm => confirmButton,
            Focus.Back => backButton,
            _ => null
        };

        if (b)
            EventSystem.current.SetSelectedGameObject(b.gameObject);
    }

    private void ActivateFocused()
    {
        switch (_focus)
        {
            case Focus.Cancel:
                CancelAmount();
                break;

            case Focus.Confirm:
                Confirm();
                break;

            case Focus.Back:
                OnBackClicked();
                break;

            default:
                EnterValueEditMode();
                break;
        }
    }

    private void RefreshAll()
    {
        bool valueActive = _focus == Focus.Value && !_isProcessing;

        if (valueBlocked)
            valueBlocked.SetActive(!valueActive);

        int balance = GetCurrentBalance();
        bool canConfirm = !_isProcessing && _loan != null && !_loan.finished && !_loan.defaulted && _amount > 0 && balance > 0;

        if (confirmButton) confirmButton.interactable = canConfirm;
        if (cancelButton) cancelButton.interactable = !_isProcessing;
        if (backButton) backButton.interactable = !_isProcessing;
        if (moreButton) moreButton.interactable = !_isProcessing && balance > 0;
        if (lessButton) lessButton.interactable = !_isProcessing && _amount > 0;
    }

    private int GetCurrentBalance()
    {
        if (_loan == null) return 0;

        var bank = BankSystem.Instance;
        if (bank == null) return 0;

        return Mathf.Max(0, bank.GetBalance(_loan.accountId));
    }

    private static int ParseDigits(string raw)
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

    private void AttachHoldTriggers(Button btn, int dir)
    {
        if (!btn) return;

        var et = btn.GetComponent<EventTrigger>();
        if (!et) et = btn.gameObject.AddComponent<EventTrigger>();
        if (et.triggers == null) et.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        void Add(EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            et.triggers.Add(entry);
        }

        Add(EventTriggerType.PointerDown, () => StartHold(dir));
        Add(EventTriggerType.PointerUp, StopHold);
        Add(EventTriggerType.PointerExit, StopHold);
    }

    private void StartHold(int dir)
    {
        if (_isProcessing) return;

        StopHold();
        _holdCo = StartCoroutine(CoHold(dir));
    }

    private void StopHold()
    {
        if (_holdCo != null)
        {
            StopCoroutine(_holdCo);
            _holdCo = null;
        }
    }

    private IEnumerator CoHold(int dir)
    {
        yield return new WaitForSecondsRealtime(holdStartDelay);

        float interval = holdInitialInterval;
        float held = 0f;

        while (true)
        {
            AddAmount(dir * step);

            yield return new WaitForSecondsRealtime(interval);

            held += interval;
            interval = Mathf.Max(holdMinInterval, holdInitialInterval - held * holdAccelPerSecond);
        }
    }
}