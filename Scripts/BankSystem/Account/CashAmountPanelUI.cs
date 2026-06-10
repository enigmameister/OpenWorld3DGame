using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CashAmountPanelUI : MonoBehaviour
{
    public enum Mode
    {
        DepositToAccount,   // gracz -> konto
        WithdrawToPlayer    // konto -> gracz
    }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.DepositToAccount;

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("UI")]
    [SerializeField] private TMP_InputField amountInput;   // Money_Value
    [SerializeField] private TMP_Text processingText;      // Processing_Text
    [SerializeField] private TMP_Text successText;         // Success_Text
    [SerializeField] private GameObject blockedValue;      // Value Image Blocked
    [SerializeField] private Button moreButton;            // More+
    [SerializeField] private Button lessButton;            // Less-
    [SerializeField] private Button confirmButton;         // Button_Confirm
    [SerializeField] private Button cancelButton;          // Button_Cancel
    [SerializeField] private Button backButton;            // Button_Back

    private bool _requestSelectConfirm;


    [Header("Behavior")]
    [SerializeField] private int step = 1;
    [SerializeField] private float operationDelaySeconds = 5f;

    [Header("Hold Acceleration")]
    [SerializeField] private float holdStartDelay = 0.25f;      // po ilu sekundach start powtarzania
    [SerializeField] private float holdInitialInterval = 0.08f; // startowa prędkość
    [SerializeField] private float holdMinInterval = 0.015f;    // max prędkość
    [SerializeField] private float holdAccelPerSecond = 0.20f;  // jak szybko przyspiesza

    private AccountOperationsUI _host;
    private int _accountId;

    private int _amount;
    private bool _isOpen;
    private bool _isProcessing;

    private Coroutine _processingCo;
    private Coroutine _holdCo;

    public bool IsOpen => _isOpen;

    private enum Zone { Amount, Buttons }
    private enum BtnSel { Cancel, Confirm, Back }

    private Zone _zone = Zone.Amount;
    private BtnSel _btnSel = BtnSel.Confirm;

    // gdy selectujemy amountInput “tylko jako selection” (tryb strzałek), to NIE wchodzimy w edycję
    private bool _selectMeansEdit;

    private bool _suppressAmountFormatting;
    private void Awake()
    {
        Show(false);

        if (processingText) processingText.gameObject.SetActive(false);
        if (successText) successText.gameObject.SetActive(false);

        if (moreButton) moreButton.onClick.AddListener(() => AddAmount(+step));
        if (lessButton) lessButton.onClick.AddListener(() => AddAmount(-step));
        if (cancelButton) cancelButton.onClick.AddListener(CancelAmount);
        if (backButton) backButton.onClick.AddListener(OnBackClicked); // zamiast lambda w Update
        if (confirmButton) confirmButton.onClick.AddListener(Confirm);

        // Enter w input -> focus na CONFIRM
        if (amountInput)
        {
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
        
                RefreshAmountTextKeepDollar();
            });

            amountInput.onEndEdit.AddListener(_ =>
            {
                if (!_isOpen) return;

              
                RefreshAmountTextKeepDollar();
            });

            amountInput.onSubmit.AddListener(_ => { _requestSelectConfirm = true; });
        }

        // Hold na More/Less (EventTrigger dokładamy kodem)
        AttachHoldTriggers(moreButton, +1);
        AttachHoldTriggers(lessButton, -1);
    }

    private void RefreshAmountTextKeepDollar()
    {
        if (!amountInput) return;

        _suppressAmountFormatting = true;

        string formatted = $"{_amount}$";
        amountInput.SetTextWithoutNotify(formatted);

        // kursor zawsze przed $
        int caretPos = Mathf.Clamp(formatted.Length - 1, 0, formatted.Length);
        amountInput.caretPosition = caretPos;
        amountInput.selectionAnchorPosition = caretPos;
        amountInput.selectionFocusPosition = caretPos;

        _suppressAmountFormatting = false;
    }
    private void EnterButtons(BtnSel start)
    {
        SetBlocked(true);
        _zone = Zone.Buttons;
        _btnSel = start;

        _selectMeansEdit = false;
 

        if (amountInput) amountInput.DeactivateInputField();

        SelectButtonByState();
    }

    private void EnterAmountArrowMode()
    {
        SetBlocked(false);
        _zone = Zone.Amount;
        _selectMeansEdit = false;
       

        if (amountInput)
        {
            amountInput.SetTextWithoutNotify($"{_amount}$");
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(amountInput.gameObject);
            amountInput.DeactivateInputField(); // klucz: strzałki nie idą do TMP
        }
    }

    private void EnterAmountEditMode()
    {
        SetBlocked(false);
        _zone = Zone.Amount;

        _selectMeansEdit = true; // teraz select = edycja
      

        if (amountInput)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(amountInput.gameObject);
            amountInput.SetTextWithoutNotify(_amount.ToString());
            amountInput.Select();
            amountInput.ActivateInputField();
        }
    }

    private void SelectButtonByState()
    {
        if (!EventSystem.current) return;

        Button b = _btnSel switch
        {
            BtnSel.Cancel => cancelButton,
            BtnSel.Confirm => confirmButton,
            _ => backButton
        };

        if (b) EventSystem.current.SetSelectedGameObject(b.gameObject);
    }

    private void CycleButtons(int dir)
    {
        // kolejność pętli: CANCEL -> CONFIRM -> BACK
        int i = _btnSel switch
        {
            BtnSel.Cancel => 0,
            BtnSel.Confirm => 1,
            _ => 2
        };

        i = (i + dir) % 3;
        if (i < 0) i += 3;

        _btnSel = (i == 0) ? BtnSel.Cancel : (i == 1 ? BtnSel.Confirm : BtnSel.Back);
        SelectButtonByState();
    }
    private void OnBackClicked()
    {
        _host?.ConsumeEscapeThisFrame();
        Close(goBackToMenu: true);
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
            _host?.ConsumeEscapeThisFrame();
            Close(goBackToMenu: true);
            return;
        }

        if (_requestSelectConfirm)
        {
            _requestSelectConfirm = false;
            EnterButtons(BtnSel.Confirm);
            return; // ważne: nie pozwól, żeby ta sama klatka odpaliła Confirm()
        }

        // =========================================================
        // ZONE: BUTTONS (CANCEL / CONFIRM / BACK)
        // =========================================================
        if (_zone == Zone.Buttons)
        {
            // na przyciskach: VALUE nie może łapać inputu
            if (amountInput) amountInput.DeactivateInputField();

            // ↑/↓ = pętla CANCEL -> CONFIRM -> BACK
            if (up) { CycleButtons(-1); return; }
            if (down) { CycleButtons(+1); return; }

            // ←/→ = wróć do VALUE (tryb strzałek)
            if (left || right)
            {
                EnterAmountArrowMode();
                return;
            }

            // ENTER = aktywuj zaznaczony przycisk
            // ENTER = aktywuj REALNIE zaznaczony przycisk
            if (enter)
            {
                ActivateSelectedButton();
                return;
            }

            return;
        }

        // =========================================================
        // ZONE: AMOUNT
        // =========================================================

        // EDYCJA TEKSTU tylko wtedy, gdy _selectMeansEdit==true
        if (_selectMeansEdit && amountInput != null && amountInput.isFocused)
        {
            // Enter / Right = wyjście z edycji i przejście na CONFIRM
            if (enter || right || left)
            {
                _selectMeansEdit = false;
              

                amountInput.DeactivateInputField();
                amountInput.SetTextWithoutNotify($"{_amount}$");

                EnterButtons(BtnSel.Confirm);
                return;
            }

            // podczas edycji nie zmieniamy kwoty strzałkami
            return;
        }

        // TRYB STRZAŁEK na VALUE (arrow mode) — nawet jeśli jest zaznaczone w EventSystem
        if (up) { AddAmount(+step); return; }
        if (down) { AddAmount(-step); return; }

        // ←/→ przejście na CONFIRM (buttons)
        if (left || right)
        {
            EnterButtons(BtnSel.Confirm);
            return;
        }

        // ENTER na VALUE = wejście w edycję
        if (enter)
        {
            EnterAmountEditMode();
            return;
        }
    }

    private void ActivateSelectedButton()
    {
        var es = EventSystem.current;
        var go = es ? es.currentSelectedGameObject : null;

        if (go != null)
        {
            var btn = go.GetComponentInParent<Button>();
            if (btn != null)
            {
                if (btn == confirmButton) { Confirm(); return; }
                if (btn == cancelButton) { CancelAmount(); return; }
                if (btn == backButton) { OnBackClicked(); return; }
            }
        }

        // fallback gdyby EventSystem nic nie miał
        if (_btnSel == BtnSel.Confirm) Confirm();
        else if (_btnSel == BtnSel.Cancel) CancelAmount();
        else OnBackClicked();
    }
    public void Open(AccountOperationsUI host, int accountId)
    {
        _host = host;
        _accountId = accountId;

        _isOpen = true;
        _isProcessing = false;

        Show(true);
        ResetToAmountEdit(clearAmount: true, hideStatusTexts: true);
    }

    public void Close(bool goBackToMenu, bool consumeEscape = false)
    {
        if (consumeEscape) _host?.ConsumeEscapeThisFrame();

        StopProcessing();
        StopHold();

        _isOpen = false;
        Show(false);

        if (goBackToMenu && _host != null)
            _host.ShowMainMenu();
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

    private void CancelAmount()
    {
        ResetToAmountEdit(clearAmount: true, hideStatusTexts: true);
    }

    private void SyncAmountFromInput()
    {
        if (!amountInput) return;

        string raw = amountInput.text;
        int parsed = 0;

        if (!string.IsNullOrEmpty(raw))
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (char.IsDigit(raw[i]))
                    sb.Append(raw[i]);
            }

            int.TryParse(sb.ToString(), out parsed);
        }

        _amount = Mathf.Max(0, parsed);
        RefreshAmountTextKeepDollar();
    }

    private void AddAmount(int delta)
    {
        if (_isProcessing) return;
        SetAmount(_amount + delta);
    }

    private void SetAmount(int value, bool updateInput = true)
    {
        _amount = Mathf.Max(0, value);

        if (updateInput)
            RefreshAmountTextKeepDollar();
    }

    private void Confirm()
    {
        if (_isProcessing) return;
        if (_amount <= 0) return;

        var bank = BankSystem.Instance;
        var ps = FindFirstObjectByType<PlayerStats>();
        if (bank == null || ps == null) return;

        // Walidacje “na wejściu” (żeby nie robić 5s czekania na fail)
        if (mode == Mode.DepositToAccount)
        {
            if (ps.money < _amount) return;
        }
        else // Withdraw
        {
            // tu nie odejmujemy fee z amount – fee dolicza bank do debetu konta
            // jeśli saldo za małe, bank zwróci false w finalize
        }

        StopProcessing();
        _processingCo = StartCoroutine(CoProcess());
    }

    private IEnumerator CoProcess()
    {
        _isProcessing = true;
        SetBlocked(true);

        if (successText) successText.gameObject.SetActive(false);
        if (processingText) processingText.gameObject.SetActive(true);

        float t = 0f;
        int dots = 0;

        while (t < operationDelaySeconds)
        {
            t += Time.unscaledDeltaTime;

            dots = (int)(t * 3f) % 4;
            if (processingText)
            {
                string d = dots == 0 ? "" : new string('.', dots);
                processingText.text = $"PROCESSING{d}";
            }

            yield return null;
        }

        var bank = BankSystem.Instance;
        var ps = FindFirstObjectByType<PlayerStats>();
        if (bank == null || ps == null)
        {
            _isProcessing = false;
            SetBlocked(false);
            EnterAmountEditMode();
            yield break;
        }

        bool ok = false;

        if (mode == Mode.DepositToAccount)
        {
            if (ps.money >= _amount)
            {
                ok = bank.DepositFromPlayer(_accountId, _amount, out int fee, out int net);
                if (ok)
                {
                    ps.SpendMoney(_amount);
                }
            }
        }
        else // WithdrawToPlayer
        {
            ok = bank.WithdrawToPlayer(_accountId, _amount, out int fee, out int totalDebited);
            if (ok)
            {
                ps.AddMoney(_amount);
            }
        }

        if (processingText) processingText.gameObject.SetActive(false);

        if (successText)
        {
            successText.gameObject.SetActive(true);
            successText.text = ok ? "SUCCESS" : "FAILED";
        }

        _isProcessing = false;
        SetBlocked(false);

        _host?.Refresh();

        if (ok)
            ResetToAmountEdit(clearAmount: true, hideStatusTexts: false);
        else
            ResetToAmountEdit(clearAmount: false, hideStatusTexts: false);

        yield return new WaitForSecondsRealtime(0.8f);

        if (successText)
            successText.gameObject.SetActive(false);
    }

    private void StopProcessing()
    {
        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }
        _isProcessing = false;
        if (processingText) processingText.gameObject.SetActive(false);
    }

    // =========================
    // HOLD (More/Less)
    // =========================
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
        // szybki klik już robi onClick, ale hold ma po chwili zacząć “spamować”
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

    private void SetBlocked(bool blocked)
    {
        if (blockedValue)
            blockedValue.SetActive(blocked);
    }

    private void ResetToAmountEdit(bool clearAmount, bool hideStatusTexts)
    {
        if (clearAmount)
            SetAmount(0, updateInput: false);

        if (hideStatusTexts)
        {
            if (processingText) processingText.gameObject.SetActive(false);
            if (successText) successText.gameObject.SetActive(false);
        }

        _isProcessing = false;
        SetBlocked(false);
        EnterAmountEditMode();
    }
}
