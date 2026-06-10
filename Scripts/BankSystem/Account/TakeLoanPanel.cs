using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TakeLoanPanel : MonoBehaviour
{
    public enum Focus
    {
        Amount,
        Installment,
        BankTax,
        Check,
        Confirm,
        Cancel,
        Back
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Sections - Selected (aktywny focus)")]
    [SerializeField] private GameObject amountSelected;
    [SerializeField] private GameObject installmentSelected;
    [SerializeField] private GameObject bankTaxSelected;

    [Header("Sections - Blocked (nieaktywne / niedostępne)")]
    [SerializeField] private GameObject amountBlocked;
    [SerializeField] private GameObject installmentBlocked;
    [SerializeField] private GameObject bankTaxBlocked;
    [SerializeField] private GameObject statusBlocked;
    [SerializeField] private GameObject confirmBlockedOverlay;
    [SerializeField] private GameObject cancelBlockedOverlay;

    [Header("Texts")]
    [SerializeField] private TMP_Text amountValueText;
    [SerializeField] private TMP_Text installmentValueText;
    [SerializeField] private TMP_Text bankTaxValueText;
    [SerializeField] private TMP_Text statusValueText;

    [Header("Processing")]
    [SerializeField] private TMP_Text bankProcessing;
    [SerializeField] private float checkDelaySeconds = 5f;

    [SerializeField] private TMP_Text resumeProcessing;
    [SerializeField] private float confirmDelaySeconds = 5f;

    [Header("Resume")]
    [SerializeField] private GameObject resumeRoot;
    [SerializeField] private TMP_Text resumeAmount;
    [SerializeField] private TMP_Text resumeInstallment;
    [SerializeField] private TMP_Text resumeBankTax;
    [SerializeField] private TMP_Text resumePlayerLoan;
    [SerializeField] private TMP_Text resumeTotalLoan;

    [Header("Buttons - Selected objects")]
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Buttons")]
    [SerializeField] private Button checkButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button backButton;

    [Header("Arrow Buttons")]
    [SerializeField] private Button amountLess;
    [SerializeField] private Button amountMore;
    [SerializeField] private Button instLess;
    [SerializeField] private Button instMore;

    [Header("Config")]
    [SerializeField] private int[] amountOptions = { 2500, 5000, 10000, 20000, 50000 };
    [SerializeField, Range(1, 12)] private int minMonths = 1;
    [SerializeField, Range(1, 12)] private int maxMonths = 12;

    [Header("Debug")]
    [SerializeField] private bool debugApproveAll = true;
    [SerializeField, Range(0f, 1f)] private float debugTaxRate = 0.15f;

    [SerializeField] private LoanMenuNavigation loanMenuNavigation;

    private AccountOperationsUI _owner;
    private int _accountId;
    private string _citizenId;

    private Focus _focus;
    private int _amountIndex;
    private int _months;

    private bool _open;
    private bool _hasAmount;
    private bool _hasMonths;
    private bool _bankTaxUnlocked;
    private bool _isProcessing;
    private bool _hasCounterOffer;

    private LoanVerdict _verdict = LoanVerdict.None;
    private LoanQuote _lastQuote;

    private Coroutine _checkCo;
    private Coroutine _processingAnimCo;
    private Coroutine _confirmCo;

    private void Awake()
    {
        Show(false);
    }

    public void Open(AccountOperationsUI owner, int accountId, string citizenId)
    {
        _owner = owner;
        _accountId = accountId;
        _citizenId = citizenId;

        _open = true;
        _amountIndex = 0;
        _months = minMonths;

        _hasAmount = amountOptions != null && amountOptions.Length > 0;
        _hasMonths = true;
        _bankTaxUnlocked = _hasAmount && _hasMonths;

        _isProcessing = false;
        _hasCounterOffer = false;
        _verdict = LoanVerdict.None;
        _lastQuote = default;

        if (bankProcessing) bankProcessing.gameObject.SetActive(false);
        if (resumeProcessing) resumeProcessing.gameObject.SetActive(false);
        if (resumeRoot) resumeRoot.SetActive(false);

        if (bankTaxValueText) bankTaxValueText.text = "-";
        if (statusValueText) statusValueText.text = "-";

        if (resumeAmount) resumeAmount.text = "0$";
        if (resumeInstallment) resumeInstallment.text = "0 MONTHS";
        if (resumeBankTax) resumeBankTax.text = "0$";
        if (resumePlayerLoan) resumePlayerLoan.text = "0$";
        if (resumeTotalLoan) resumeTotalLoan.text = "0$";

        _focus = Focus.Amount;

        RefreshAll();
        Show(true);
    }

    public void Hide()
    {
        _open = false;
        StopProcessing();

        if (bankTaxValueText) bankTaxValueText.text = "-";
        if (statusValueText) statusValueText.text = "-";
        if (resumeRoot) resumeRoot.SetActive(false);

        Show(false);
    }

    public void Close(bool goBackToMenu)
    {
        Hide();

        if (goBackToMenu)
            _owner?.ShowMainMenu();
    }

    private void Update()
    {
        if (!_open || root == null || root.alpha < 0.5f)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BankDialogueUI.SuppressEscapeFrames = 2;
            OnBack();
            return;
        }

        if (_isProcessing)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveFocus(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveFocus(+1);

        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.01f) right = true;
        if (wheel < -0.01f) left = true;

        if (left)
        {
            if (_focus == Focus.Check)
            {
                _focus = Focus.BankTax;
                RefreshFocus();
                RefreshAll();
                return;
            }

            OnAdjust(-1);
        }

        if (right)
        {
            if (_focus == Focus.BankTax)
            {
                _focus = Focus.Check;
                RefreshFocus();
                RefreshAll();
                return;
            }

            OnAdjust(+1);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            ActivateFocused();
    }

    private void MoveFocus(int dir)
    {
        if (resumeRoot != null && resumeRoot.activeSelf)
        {
            Focus[] order = { Focus.Confirm, Focus.Cancel, Focus.Back };
            int idx = System.Array.IndexOf(order, _focus);
            if (idx < 0) idx = 0;
            idx = Mathf.Clamp(idx + dir, 0, order.Length - 1);
            _focus = order[idx];
        }
        else
        {
            Focus[] order = { Focus.Amount, Focus.Installment, Focus.Check, Focus.Back };
            int idx = System.Array.IndexOf(order, _focus);

            // jeśli fokus był na BankTax, traktuj go jak Check
            if (idx < 0 && _focus == Focus.BankTax)
                idx = System.Array.IndexOf(order, Focus.Check);

            if (idx < 0) idx = 0;

            idx = Mathf.Clamp(idx + dir, 0, order.Length - 1);

            var next = order[idx];
            if (next == Focus.Check && !_bankTaxUnlocked)
                return;

            _focus = next;
        }

        RefreshFocus();
        RefreshAll();
    }

    private void OnAdjust(int delta)
    {
        if (_isProcessing) return;

        switch (_focus)
        {
            case Focus.Amount:
                if (amountOptions == null || amountOptions.Length == 0) return;
                _amountIndex = Mathf.Clamp(_amountIndex + delta, 0, amountOptions.Length - 1);
                _hasAmount = true;
                InvalidateVerdict();
                break;

            case Focus.Installment:
                _months = Mathf.Clamp(_months + delta, minMonths, maxMonths);
                _hasMonths = true;
                InvalidateVerdict();
                break;
        }

        _bankTaxUnlocked = _hasAmount && _hasMonths;
        RefreshAll();
    }

    private void ActivateFocused()
    {
        if (_isProcessing) return;

        if (resumeRoot != null && resumeRoot.activeSelf)
        {
            switch (_focus)
            {
                case Focus.Confirm: OnConfirm(); return;
                case Focus.Cancel: OnCancel(); return;
                case Focus.Back: OnBack(); return;
            }
        }

        switch (_focus)
        {
            case Focus.Check:
                StartCheck();
                break;

            case Focus.Back:
                OnBack();
                break;
        }
    }

    private void StartCheck()
    {
        if (_isProcessing) return;
        if (!_bankTaxUnlocked) return;
        if (amountOptions == null || amountOptions.Length == 0) return;

        if (string.IsNullOrWhiteSpace(_citizenId))
        {
            _verdict = LoanVerdict.Denied;
            _lastQuote = default;
            if (statusValueText) statusValueText.text = "NO CITIZEN ID";
            RefreshAll();
            return;
        }

        StopProcessing();
        _hasCounterOffer = false;
        _checkCo = StartCoroutine(CoCheck());
    }

    private IEnumerator CoCheck()
    {
        _isProcessing = true;
        _verdict = LoanVerdict.Processing;

        if (resumeRoot) resumeRoot.SetActive(false);
        StartBankProcessingAnim();

        RefreshAll();

        yield return new WaitForSecondsRealtime(checkDelaySeconds);

        int amount = amountOptions[_amountIndex];
        int months = _months;

        if (debugApproveAll)
        {
            int bankTax = Mathf.RoundToInt(amount * debugTaxRate);
            int total = amount + bankTax;
            int monthly = Mathf.Max(1, Mathf.CeilToInt((float)total / months));

            _lastQuote = new LoanQuote
            {
                verdict = LoanVerdict.Approved,
                taxRate = debugTaxRate,
                bankTax = bankTax,
                totalToRepay = total,
                monthlyPayment = monthly,
                reason = "DEBUG APPROVED"
            };

            _verdict = LoanVerdict.Approved;
        }
        else
        {
            var loanSys = LoanSystem.Instance;
            if (loanSys == null)
            {
                _verdict = LoanVerdict.Denied;
                _lastQuote = new LoanQuote
                {
                    verdict = LoanVerdict.Denied,
                    reason = "LoanSystem missing"
                };
            }
            else
            {
                _lastQuote = loanSys.RequestQuote(_citizenId, amount, months);
                _verdict = _lastQuote.verdict;

                if (_verdict == LoanVerdict.Denied)
                {
                    var sug = loanSys.SuggestTerms(
                        _citizenId,
                        amountOptions,
                        minMonths,
                        maxMonths,
                        amount,
                        months
                    );

                    if (sug.hasSuggestion)
                    {
                        int newIndex = System.Array.IndexOf(amountOptions, sug.suggestedAmount);
                        if (newIndex >= 0) _amountIndex = newIndex;

                        _months = sug.suggestedMonths;
                        _lastQuote = default;
                        _hasCounterOffer = true;
                        _focus = Focus.Amount;
                    }
                }
            }
        }

        _isProcessing = false;
        StopBankProcessingAnim();

        ApplyVerdictToUI();
        RefreshAll();
    }

    private void ApplyVerdictToUI()
    {
        if (_verdict == LoanVerdict.Approved)
        {
            if (statusValueText) statusValueText.text = "APPROVED";
            if (bankTaxValueText) bankTaxValueText.text = $"{_lastQuote.bankTax:n0}$ ({_lastQuote.taxRate * 100f:0.0}%)";

            if (resumeRoot) resumeRoot.SetActive(true);
            FillResume();
            _focus = Focus.Confirm;
        }
        else if (_verdict == LoanVerdict.Denied)
        {
            if (!_hasCounterOffer && statusValueText)
                statusValueText.text = "DENIED";

            if (bankTaxValueText) bankTaxValueText.text = "-";
            if (resumeRoot) resumeRoot.SetActive(false);
            _focus = Focus.Check;
        }
        else
        {
            if (statusValueText) statusValueText.text = "-";
            if (bankTaxValueText) bankTaxValueText.text = "-";
            if (resumeRoot) resumeRoot.SetActive(false);
        }

        RefreshFocus();
    }

    private void FillResume()
    {
        int amount = amountOptions[_amountIndex];
        int months = _months;

        if (resumeAmount) resumeAmount.text = $"{amount:n0}$";
        if (resumeInstallment) resumeInstallment.text = $"{months} MONTHS";
        if (resumeBankTax) resumeBankTax.text = $"{_lastQuote.bankTax:n0}$";
        if (resumePlayerLoan) resumePlayerLoan.text = $"{amount:n0}$";
        if (resumeTotalLoan) resumeTotalLoan.text = $"{_lastQuote.totalToRepay:n0}$";
    }

    private void OnConfirm()
    {
        if (_verdict != LoanVerdict.Approved) return;
        if (_isProcessing) return;
        if (amountOptions == null || amountOptions.Length == 0) return;

        StopProcessing();
        _checkCo = StartCoroutine(CoConfirmLoan());
    }

    private IEnumerator CoConfirmLoan()
    {
        _isProcessing = true;

        StartResumeProcessingAnim();
        RefreshAll();

        yield return new WaitForSecondsRealtime(confirmDelaySeconds);

        int amount = amountOptions[_amountIndex];
        int months = _months;

        bool success = false;

        if (LoanSystem.Instance != null)
            success = LoanSystem.Instance.ConfirmLoan(_citizenId, amount, months, _lastQuote, out _);

        _isProcessing = false;
        StopResumeProcessingAnim();

        if (success)
        {
            _owner?.Refresh();
            Hide();

            if (loanMenuNavigation != null)
                loanMenuNavigation.OnTakeLoanFinished();
            else
                _owner?.ShowMainMenu();
        }
        else
        {
            if (statusValueText) statusValueText.text = "DENIED";
            if (resumeRoot) resumeRoot.SetActive(false);
            _focus = Focus.Check;
            RefreshAll();
        }
    }

    private void OnCancel()
    {
        InvalidateVerdict();
        if (resumeRoot) resumeRoot.SetActive(false);
        _focus = Focus.Amount;
        RefreshAll();
    }

    private void OnBack()
    {
        _owner?.ConsumeEscapeThisFrame();
        BankDialogueUI.SuppressEscapeFrames = 2;

        if (loanMenuNavigation != null)
            loanMenuNavigation.ConsumeEscapeThisFrame();

        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        Hide();

        if (loanMenuNavigation != null)
            loanMenuNavigation.BackToMenu();
        else
            _owner?.ShowMainMenu();
    }

    private void InvalidateVerdict()
    {
        StopProcessing();
        _hasCounterOffer = false;
        _verdict = LoanVerdict.None;
        _lastQuote = default;

        if (statusValueText) statusValueText.text = "-";
        if (bankTaxValueText) bankTaxValueText.text = "-";
        if (resumeRoot) resumeRoot.SetActive(false);
    }

    private void StopProcessing()
    {
        if (_checkCo != null)
        {
            StopCoroutine(_checkCo);
            _checkCo = null;
        }

        if (_confirmCo != null)
        {
            StopCoroutine(_confirmCo);
            _confirmCo = null;
        }

        if (_processingAnimCo != null)
        {
            StopCoroutine(_processingAnimCo);
            _processingAnimCo = null;
        }

        _isProcessing = false;

        StopBankProcessingAnim();
        StopResumeProcessingAnim();
    }

    private void RefreshAll()
    {
        int amount = (amountOptions != null && amountOptions.Length > 0)
            ? amountOptions[Mathf.Clamp(_amountIndex, 0, amountOptions.Length - 1)]
            : 0;

        bool inResume = resumeRoot != null && resumeRoot.activeSelf;
        bool lockAll = _isProcessing;

        bool canCancel = inResume && !_isProcessing;

        if (cancelButton) cancelButton.interactable = canCancel;

        if (cancelBlockedOverlay)
            cancelBlockedOverlay.SetActive(!canCancel);

        if (amountValueText) amountValueText.text = amount.ToString("n0");
        if (installmentValueText) installmentValueText.text = _months.ToString();

        _bankTaxUnlocked = _hasAmount && _hasMonths;

        if (lockAll)
        {
            if (amountBlocked) amountBlocked.SetActive(true);
            if (installmentBlocked) installmentBlocked.SetActive(true);
            if (bankTaxBlocked) bankTaxBlocked.SetActive(true);
            if (statusBlocked) statusBlocked.SetActive(false);
            if (confirmBlockedOverlay) confirmBlockedOverlay.SetActive(true);
            if (cancelBlockedOverlay) cancelBlockedOverlay.SetActive(!canCancel);
        }
        else
        {
            if (amountBlocked) amountBlocked.SetActive(_focus != Focus.Amount);

            if (installmentBlocked)
                installmentBlocked.SetActive(_focus != Focus.Installment);

            if (bankTaxBlocked)
                bankTaxBlocked.SetActive(!_bankTaxUnlocked || (_focus != Focus.BankTax && _focus != Focus.Check));

            if (statusBlocked)
                statusBlocked.SetActive(true);

            if (confirmBlockedOverlay)
                confirmBlockedOverlay.SetActive(!(inResume && _verdict == LoanVerdict.Approved));
        }

        bool canCheck = _bankTaxUnlocked && !_isProcessing && !inResume;
        bool canConfirm = inResume && _verdict == LoanVerdict.Approved && !_isProcessing;
        bool canBack = !_isProcessing;

        if (checkButton) checkButton.interactable = canCheck;
        if (confirmButton) confirmButton.interactable = canConfirm;
        if (cancelButton) cancelButton.interactable = canCancel;
        if (backButton) backButton.interactable = canBack;

        RefreshFocus();
    }

    private void RefreshFocus()
    {
        if (amountSelected) amountSelected.SetActive(_focus == Focus.Amount);
        if (installmentSelected) installmentSelected.SetActive(_focus == Focus.Installment);
        if (bankTaxSelected) bankTaxSelected.SetActive(_focus == Focus.BankTax || _focus == Focus.Check);

        bool inResume = resumeRoot != null && resumeRoot.activeSelf;

        if (selectedConfirm) selectedConfirm.SetActive(inResume && _focus == Focus.Confirm);
        if (selectedCancel) selectedCancel.SetActive(inResume && _focus == Focus.Cancel);
        if (selectedBack) selectedBack.SetActive(_focus == Focus.Back);

        if (EventSystem.current == null)
            return;

        GameObject go = null;

        if (inResume)
        {
            switch (_focus)
            {
                case Focus.Confirm:
                    go = confirmButton ? confirmButton.gameObject : null;
                    break;

                case Focus.Cancel:
                    go = cancelButton ? cancelButton.gameObject : null;
                    break;

                case Focus.Back:
                    go = backButton ? backButton.gameObject : null;
                    break;
            }
        }
        else
        {
            switch (_focus)
            {
                case Focus.Check:
                    go = checkButton ? checkButton.gameObject : null;
                    break;

                case Focus.Back:
                    go = backButton ? backButton.gameObject : null;
                    break;
            }
        }

        EventSystem.current.SetSelectedGameObject(go);
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

    private IEnumerator CoDots(TMP_Text target, string prefix)
    {
        if (target == null) yield break;

        int step = 0;
        while (true)
        {
            step = (step + 1) % 4;
            string dots = new string('.', step);
            target.text = $"{prefix}{dots}";
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    private void StartBankProcessingAnim()
    {
        if (bankProcessing == null) return;

        bankProcessing.gameObject.SetActive(true);

        if (_processingAnimCo != null)
            StopCoroutine(_processingAnimCo);

        _processingAnimCo = StartCoroutine(CoDots(bankProcessing, "PROCESSING"));
    }

    private void StopBankProcessingAnim()
    {
        if (_processingAnimCo != null)
        {
            StopCoroutine(_processingAnimCo);
            _processingAnimCo = null;
        }

        if (bankProcessing != null)
        {
            bankProcessing.text = "";
            bankProcessing.gameObject.SetActive(false);
        }
    }

    private void StartResumeProcessingAnim()
    {
        if (resumeProcessing == null) return;

        resumeProcessing.gameObject.SetActive(true);

        if (_confirmCo != null)
            StopCoroutine(_confirmCo);

        _confirmCo = StartCoroutine(CoDots(resumeProcessing, "PROCESSING"));
    }

    private void StopResumeProcessingAnim()
    {
        if (_confirmCo != null)
        {
            StopCoroutine(_confirmCo);
            _confirmCo = null;
        }

        if (resumeProcessing != null)
        {
            resumeProcessing.text = "";
            resumeProcessing.gameObject.SetActive(false);
        }
    }

    // ===== Inspector OnClick handlers =====

    public void OnClickCheck()
    {
        _focus = Focus.Check;
        RefreshFocus();
        StartCheck();
    }

    public void OnClickConfirm()
    {
        _focus = Focus.Confirm;
        RefreshFocus();
        OnConfirm();
    }

    public void OnClickCancel()
    {
        _focus = Focus.Cancel;
        RefreshFocus();
        OnCancel();
    }

    public void OnClickBack()
    {
        _focus = Focus.Back;
        RefreshFocus();
        OnBack();
    }

    public void OnClickAmountLess()
    {
        _focus = Focus.Amount;
        RefreshFocus();
        OnAdjust(-1);
    }

    public void OnClickAmountMore()
    {
        _focus = Focus.Amount;
        RefreshFocus();
        OnAdjust(+1);
    }

    public void OnClickInstallmentLess()
    {
        _focus = Focus.Installment;
        RefreshFocus();
        OnAdjust(-1);
    }

    public void OnClickInstallmentMore()
    {
        _focus = Focus.Installment;
        RefreshFocus();
        OnAdjust(+1);
    }
}