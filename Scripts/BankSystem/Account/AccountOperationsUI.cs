using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccountOperationsUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Fields (TMP)")]
    [SerializeField] private TMP_Text accountNumberValue;
    [SerializeField] private TMP_Text citizenIdValue;
    [SerializeField] private TMP_Text createdValue;
    [SerializeField] private TMP_Text balanceValue;
    [SerializeField] private TMP_Text cashValue;

    [Header("Controller (like BankCardOpsPanel)")]
    [SerializeField] private AccountOpsPanel opsPanel;

    [Header("Loan Info")]
    [SerializeField] private GameObject panelLoanInfo;

    [Header("Loan Pages")]
    [SerializeField] private Transform pageContainer;
    [SerializeField] private Transform loanContainer;
    [SerializeField] private LoanButtonView loanPageButtonPrefab;
    [SerializeField] private LoanEntryView loanInfoPagePrefab;

    private readonly List<LoanButtonView> _loanPageButtons = new();
    private readonly List<LoanEntryView> _loanPages = new();
    private int _selectedLoanPageIndex = 0;
    private bool _focusLoanPages = false;

    [SerializeField] private GameObject loanSummaryRoot;          // LoanInfo_LoanEntryView albo jego parent
    [SerializeField] private GameObject installmentsListRoot;     // InstallmentsListRoot
    [SerializeField] private Transform installmentsContainer;     // CurrentInstallmentsContainer
    [SerializeField] private InstallmentInfoLoanEntryView installmentRowPrefab;
    [SerializeField] private Button installmentsBackButton;

    private ActiveLoan _currentInstallmentsLoan;
    private readonly List<InstallmentInfoLoanEntryView> _installmentRows = new();
    private bool _showingInstallmentsList = false;
    private readonly List<ActiveLoan> _currentLoansCache = new();

    private int _accountId;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public int AccountId => _accountId;

    private void Awake()
    {
        Show(false);

        if (opsPanel != null)
            opsPanel.BindOwner(this);

        if (installmentsBackButton != null)
        {
            installmentsBackButton.onClick.RemoveAllListeners();
            installmentsBackButton.onClick.AddListener(CloseInstallmentsList);
        }
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (_showingInstallmentsList)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInstallmentsList();
                return;
            }

            return;
        }

        if (!_focusLoanPages) return;
        if (!HasVisibleLoanPages()) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _selectedLoanPageIndex = Mathf.Max(0, _selectedLoanPageIndex - 1);
            RefreshLoanPageFocusOnly();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            _selectedLoanPageIndex = Mathf.Min(_loanPageButtons.Count - 1, _selectedLoanPageIndex + 1);
            RefreshLoanPageFocusOnly();
        }

        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OpenInstallmentsListForCurrentPage();
            return;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            SelectLoanPage(_selectedLoanPageIndex, applyImmediately: true);
            return;
        }
    }

    public void OpenForAccount(int accountId)
    {
        // ✅ zamknij dialog UI, ale nie unlockuj gracza (AccountOps przejmie locki)
        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        _accountId = accountId;
        _isOpen = true;

        Refresh();

        Show(true);
        PlayerInputHandler.SetGameplayBlocked(true);

        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm) pm.enabled = false;

        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = false;

        if (opsPanel != null)
            opsPanel.OpenForAccount(accountId);
    }

    public void Close(bool unlockPlayer)
    {
        _isOpen = false;
        Show(false);

        if (opsPanel != null)
            opsPanel.OnOwnerClosed(); // posprzątaj subpanele/menu

        if (!unlockPlayer) return;

        MouseLook.IsLookLocked = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (unlockPlayer)
            PlayerInputHandler.SetGameplayBlocked(false);

        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm) pm.enabled = true;

        var wm = FindFirstObjectByType<WeaponManager>();
        if (wm) wm.enabled = true;
    }

    public void Refresh()
    {
        if (accountNumberValue)
            accountNumberValue.text = _accountId > 0 ? _accountId.ToString("000") : "---";

        if (citizenIdValue)
            citizenIdValue.text = string.IsNullOrWhiteSpace(CurrentCitizenId) ? "000" : CurrentCitizenId;

        if (createdValue)
            createdValue.text = "00-00-00 | 23:59";

        var ps = FindFirstObjectByType<PlayerStats>();
        if (cashValue)
            cashValue.text = ps ? $"{Mathf.Max(0, ps.money)}$" : "0$";

        var bank = BankSystem.Instance;
        if (bank != null && _accountId > 0 && balanceValue)
            balanceValue.text = $"{bank.GetBalance(_accountId)}$";

        _showingInstallmentsList = false;

        if (installmentsListRoot != null)
            installmentsListRoot.SetActive(false);

        if (loanSummaryRoot != null)
            loanSummaryRoot.SetActive(true);

        ClearInstallmentsList();

        var loanSys = LoanSystem.Instance;
        if (panelLoanInfo != null)
        {
            if (loanSys == null || string.IsNullOrWhiteSpace(CurrentCitizenId))
            {
                panelLoanInfo.SetActive(false);
                ClearLoanPages();
            }
            else
            {
                var loans = loanSys.GetLoansForCitizen(CurrentCitizenId, includeFinished: false);

                bool hasLoans = loans != null && loans.Count > 0;
                panelLoanInfo.SetActive(hasLoans);

                _currentLoansCache.Clear();
                if (loans != null)
                    _currentLoansCache.AddRange(loans);

                if (hasLoans)

                    RebuildLoanPages(loans);
                else
                    ClearLoanPages();
            }
        }
    }
    public string CurrentCitizenId
    {
        get
        {
            var ps = FindFirstObjectByType<PlayerStats>();
            return ps ? ps.citizenId : null;
        }
    }

    public void ReturnToDialogueStart()
    {
        // zamknij bez unlock (dialog przejmie)
        Close(unlockPlayer: false);

        var dlg = FindFirstObjectByType<BankDialogueUI>();
        if (dlg != null)
            dlg.ReturnToStartSameSession();
        else
            Close(unlockPlayer: true);
    }

    public void ReopenFromCardOperations()
    {
        _isOpen = true;
        Show(true);
        Refresh();

        if (opsPanel != null)
        {
            // klucz: to ustawia opsPanel._isOpen = true i odpala menu poprawnie
            opsPanel.OpenForAccount(_accountId);

            // opcjonalnie (polecam): żeby ESC użyty do wyjścia z CardOps
            // nie zamknął od razu AccountOps w tej samej klatce
            opsPanel.ConsumeEscapeThisFrame();
        }
    }

    private void Show(bool v)
    {
        if (!root)
        {
            gameObject.SetActive(v);
            return;
        }

        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
        root.gameObject.SetActive(v);
    }

    // ==== Compatibility wrappers for old subpanels ====
    // (stare panele wołają te metody na host UI)

    public void ShowMainMenu()
    {
        if (opsPanel != null)
            opsPanel.OpenMenu();
    }

    public void ReturnToMenuFromSubPanel()
    {
        if (opsPanel != null)
            opsPanel.OpenMenu();
    }

    public void ConsumeEscapeThisFrame()
    {
        if (opsPanel != null)
            opsPanel.ConsumeEscapeThisFrame();
    }

    private void ClearLoanPages()
    {
        for (int i = 0; i < _loanPageButtons.Count; i++)
        {
            if (_loanPageButtons[i] != null)
                Destroy(_loanPageButtons[i].gameObject);
        }
        _loanPageButtons.Clear();

        for (int i = 0; i < _loanPages.Count; i++)
        {
            if (_loanPages[i] != null)
                Destroy(_loanPages[i].gameObject);
        }
        _loanPages.Clear();

        ClearInstallmentsList();
    }

    private void RebuildLoanPages(List<ActiveLoan> loans)
    {
        ClearLoanPages();

        if (loans == null || loans.Count == 0)
            return;

        _selectedLoanPageIndex = Mathf.Clamp(_selectedLoanPageIndex, 0, loans.Count - 1);

        for (int i = 0; i < loans.Count; i++)
        {
            var btn = Instantiate(loanPageButtonPrefab, pageContainer);
            btn.Setup(i, false, OnLoanPageClicked);
            _loanPageButtons.Add(btn);

            var page = Instantiate(loanInfoPagePrefab, loanContainer);
            var loan = loans[i];
            page.Bind(loan, () => OpenInstallmentsList(loan));

            page.gameObject.SetActive(i == _selectedLoanPageIndex);
            _loanPages.Add(page);
        }

        SelectLoanPage(_selectedLoanPageIndex, applyImmediately: true);
    }

    private void OnLoanPageClicked(int index)
    {
        _selectedLoanPageIndex = index;
        SelectLoanPage(index, applyImmediately: true);
    }

    private void SelectLoanPage(int index, bool applyImmediately)
    {
        if (index < 0 || index >= _loanPages.Count) return;

        _selectedLoanPageIndex = index;

        for (int i = 0; i < _loanPages.Count; i++)
            _loanPages[i].gameObject.SetActive(applyImmediately && i == _selectedLoanPageIndex);

        for (int i = 0; i < _loanPageButtons.Count; i++)
            _loanPageButtons[i].SetSelected(_focusLoanPages && i == _selectedLoanPageIndex);
    }

    public bool HasVisibleLoanPages()
    {
        return panelLoanInfo != null &&
               panelLoanInfo.activeSelf &&
               _loanPageButtons != null &&
               _loanPageButtons.Count > 0;
    }

    public void FocusLoanPagesFromMenu(bool fromEnd)
    {
        if (!HasVisibleLoanPages())
            return;

        _focusLoanPages = true;

        if (_loanPageButtons.Count <= 0)
            return;

        _selectedLoanPageIndex = Mathf.Clamp(_selectedLoanPageIndex, 0, _loanPageButtons.Count - 1);

        // wejście od góry -> [1], wejście od dołu -> ostatnia strona
        if (!fromEnd)
            _selectedLoanPageIndex = 0;
        else
            _selectedLoanPageIndex = _loanPageButtons.Count - 1;

        RefreshLoanPageFocusOnly();
    }

    public void ExitLoanPagesFocus()
    {
        _focusLoanPages = false;
        RefreshLoanPageFocusOnly();
    }

    private void RefreshLoanPageFocusOnly()
    {
        for (int i = 0; i < _loanPageButtons.Count; i++)
            _loanPageButtons[i].SetSelected(_focusLoanPages && i == _selectedLoanPageIndex);
    }

    public bool IsLoanPagesFocused()
    {
        return _focusLoanPages;
    }

    private void ClearInstallmentsList()
    {
        for (int i = 0; i < _installmentRows.Count; i++)
        {
            if (_installmentRows[i] != null)
                Destroy(_installmentRows[i].gameObject);
        }

        _installmentRows.Clear();
        _currentInstallmentsLoan = null;
    }

    private void OpenInstallmentsList(ActiveLoan loan)
    {
        if (loan == null) return;
        if (LoanSystem.Instance == null) return;

        _currentInstallmentsLoan = loan;
        _showingInstallmentsList = true;

        if (loanSummaryRoot != null)
            loanSummaryRoot.SetActive(false);

        if (installmentsListRoot != null)
            installmentsListRoot.SetActive(true);

        ClearInstallmentsList();

        var items = LoanSystem.Instance.BuildUpcomingInstallments(loan);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = Instantiate(installmentRowPrefab, installmentsContainer);

            var due = item.dueAtUtcTicks > 0
                ? new DateTime(item.dueAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;

            row.Bind(item.amount, due, item.status);
            _installmentRows.Add(row);
        }

        if (installmentsBackButton != null)
            installmentsBackButton.Select();
    }

    private void CloseInstallmentsList()
    {
        _showingInstallmentsList = false;

        ConsumeEscapeThisFrame();

        if (installmentsListRoot != null)
            installmentsListRoot.SetActive(false);

        if (loanSummaryRoot != null)
            loanSummaryRoot.SetActive(true);

        ClearInstallmentsList();

        RefreshLoanPageFocusOnly();
    }

    private void OpenInstallmentsListForCurrentPage()
    {
        if (_selectedLoanPageIndex < 0 || _selectedLoanPageIndex >= _loanPages.Count)
            return;

        if (_selectedLoanPageIndex >= _currentLoansCache.Count)
            return;

        OpenInstallmentsList(_currentLoansCache[_selectedLoanPageIndex]);
    }
}
