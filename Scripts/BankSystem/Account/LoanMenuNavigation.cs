using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoanMenuNavigation : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LoanMenuScreen menuPanel;
    [SerializeField] private SelectedLoanPanel operationsMenu;

    [SerializeField] private TakeLoanPanel takeLoanPanel;
    [SerializeField] private RepayLoanPanel repayLoanPanel;
    [SerializeField] private RestructureLoanPanelUI restructureLoanPanel;
    [SerializeField] private DeferLoanPanelUI deferLoanPanel;
    [SerializeField] private ChangeDueDatePanelUI changeDueDatePanel;

    private ActiveLoan _selectedLoan; // Currently Loan Button in Loan Menu
    private enum LoanMode
    {
        Menu,
        Operations,
        TakeLoan,
        RepayLoan,
        RestructureLoan,
        DeferLoan,
        ChangeDueDate
    }

    private AccountOperationsUI owner;
    private readonly List<ActiveLoan> _currentLoans = new();

    private int _accountId;
    private bool _open;
    private int _selectedIndex;
    private LoanMode _mode = LoanMode.Menu;

    public AccountOperationsUI Owner => owner;
    public int AccountId => _accountId;

    private int _escapeConsumedFrame = -1;
    private bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

    public void ConsumeEscapeThisFrame()
    {
        _escapeConsumedFrame = Time.frameCount;
    }

    public void Open(AccountOperationsUI ui, int accountId)
    {
        owner = ui;
        _accountId = accountId;
        _open = true;
        _mode = LoanMode.Menu;
        _selectedIndex = 0;

        gameObject.SetActive(true);

        ForceCloseAllLoanPanels();

        if (menuPanel != null)
            menuPanel.Show(true);

        RefreshMenu();
    }
    public void Close()
    {
        _open = false;
        _mode = LoanMode.Menu;

        ForceCloseAllLoanPanels();

        if (menuPanel != null)
            menuPanel.Show(false);

        BankDialogueUI.SuppressEscapeFrames = 2;
        gameObject.SetActive(false);
    }

    public void RefreshMenu()
    {
        RebuildMenu();

        _selectedIndex = Mathf.Clamp(
            _selectedIndex,
            0,
            Mathf.Max(0, menuPanel != null ? menuPanel.ButtonCount - 1 : 0)
        );

        if (menuPanel != null)
            menuPanel.RefreshNow();

        ApplySelection();
    }

    public void BackToMenu()
    {
        if (!_open) return;

        ConsumeEscapeThisFrame();
        owner?.ConsumeEscapeThisFrame();
        BankDialogueUI.SuppressEscapeFrames = 1;

        _mode = LoanMode.Menu;

        ForceCloseAllLoanPanels();

        if (menuPanel != null)
            menuPanel.Show(true);

        _selectedIndex = 0;
        RefreshMenu();
    }

    public void CloseToAccountOps()
    {
        Close();

        if (owner != null)
        {
            owner.ConsumeEscapeThisFrame();
            owner.ShowMainMenu();
        }
    }

    public void OpenTakeLoan()
    {
        if (!_open || takeLoanPanel == null || owner == null)
            return;

        _mode = LoanMode.TakeLoan;

        if (menuPanel != null)
            menuPanel.Show(false);

        ForceCloseAllLoanPanels();

        takeLoanPanel.gameObject.SetActive(true);
        takeLoanPanel.Open(owner, _accountId, owner.CurrentCitizenId);
    }

    public void OnTakeLoanFinished()
    {
        BackToMenu();
    }

    private void Update()
    {
        if (!_open)
            return;

        if (_mode != LoanMode.Menu)
            return;

        if (menuPanel == null)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveSelection(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveSelection(+1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            ActivateSelected();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !IsEscapeConsumedThisFrame)
        {
            ConsumeEscapeThisFrame();
            CloseToAccountOps();
        }
    }

    private void RebuildMenu()
    {
        _currentLoans.Clear();

        string citizenId = owner != null ? owner.CurrentCitizenId : null;

        if (LoanSystem.Instance != null && !string.IsNullOrWhiteSpace(citizenId))
            _currentLoans.AddRange(LoanSystem.Instance.GetLoansForCitizen(citizenId, includeFinished: false));

        List<string> labels = BuildLabels(_currentLoans);

        if (menuPanel != null)
            menuPanel.Rebuild(labels, OnLoanMenuButtonClicked);

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, labels.Count - 1));
    }

    private List<string> BuildLabels(List<ActiveLoan> loans)
    {
        var labels = new List<string>();

        if (loans != null)
        {
            for (int i = 0; i < loans.Count; i++)
            {
                var loan = loans[i];
                if (loan == null) continue;

                labels.Add($"LOAN #{i + 1} - LEFT {loan.remainingToRepay}$");
            }
        }

        labels.Add("TAKE LOAN");
        labels.Add("BACK");

        return labels;
    }

    private void MoveSelection(int dir)
    {
        if (menuPanel == null || menuPanel.ButtonCount == 0)
            return;

        int count = menuPanel.ButtonCount;
        _selectedIndex = (_selectedIndex + dir + count) % count;
        ApplySelection();
    }

    private void ApplySelection()
    {
        if (menuPanel == null || menuPanel.ButtonCount == 0)
            return;

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, menuPanel.ButtonCount - 1);

        menuPanel.SetSelectedIndex(_selectedIndex);

        var btn = menuPanel.GetButton(_selectedIndex);
        if (btn != null && btn.Button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(btn.Button.gameObject);
    }

    private void ActivateSelected()
    {
        if (menuPanel != null && EventSystem.current != null)
        {
            var current = EventSystem.current.currentSelectedGameObject;

            if (current != null)
            {
                for (int i = 0; i < menuPanel.ButtonCount; i++)
                {
                    var btn = menuPanel.GetButton(i);
                    if (btn != null && btn.Button != null && btn.Button.gameObject == current)
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }
        }

        OnLoanMenuButtonClicked(_selectedIndex);
    }

    private void OnLoanMenuButtonClicked(int index)
    {
        int loansCount = _currentLoans.Count;

        if (index < loansCount)
        {
            _selectedLoan = _currentLoans[index];

            OpenOperationsMenu();
            return;
        }

        if (index == loansCount)
        {
            OpenTakeLoan();
            return;
        }

        if (index == loansCount + 1)
        {
            CloseToAccountOps();
        }
    }

    private void OpenOperationsMenu()
    {
        _mode = LoanMode.Operations;

        if (menuPanel != null)
            menuPanel.Show(false);

        if (operationsMenu != null)
            operationsMenu.Open(this, _selectedLoan);
    }

    private void ForceCloseAllLoanPanels()
    {
        if (takeLoanPanel) takeLoanPanel.Hide();
        if (repayLoanPanel) repayLoanPanel.Hide();
        if (restructureLoanPanel) restructureLoanPanel.Hide();
        if (deferLoanPanel) deferLoanPanel.Hide();
        if (operationsMenu) operationsMenu.Close();
        if (changeDueDatePanel) changeDueDatePanel.Hide();
    }

    public bool IsInSubLoanPanel()
    {
        return _open && _mode != LoanMode.Menu;
    }

    public void BackToLoanMenu()
    {
        ForceCloseAllLoanPanels();

        _mode = LoanMode.Menu;

        if (menuPanel != null)
            menuPanel.Show(true);

        RefreshMenu();
    }

    public void RefreshAfterLoanChange()
    {
        owner?.Refresh();

        string citizenId = owner != null ? owner.CurrentCitizenId : null;

        _currentLoans.Clear();
        if (LoanSystem.Instance != null && !string.IsNullOrWhiteSpace(citizenId))
            _currentLoans.AddRange(LoanSystem.Instance.GetLoansForCitizen(citizenId, includeFinished: false));

        // jeœli aktualnie zaznaczona po¿yczka zosta³a sp³acona / zniknê³a
        bool selectedStillExists = _selectedLoan != null
            && !_selectedLoan.finished
            && !_selectedLoan.defaulted
            && _currentLoans.Contains(_selectedLoan);

        if (!selectedStillExists)
        {
            _selectedLoan = null;
            BackToMenu();
            return;
        }

        // jeœli jesteœmy w panelu operacji wybranej po¿yczki, odœwie¿ go
        if (operationsMenu != null)
            operationsMenu.RefreshView(_selectedLoan);
    }

    public void OpenRepayLoan()
    {
        if (!_open || repayLoanPanel == null || owner == null || _selectedLoan == null)
            return;

        _mode = LoanMode.RepayLoan;

        if (menuPanel != null)
            menuPanel.Show(false);

        if (operationsMenu != null)
            operationsMenu.Hide();

        repayLoanPanel.gameObject.SetActive(true);
        repayLoanPanel.Open(this, owner, _selectedLoan);
    }

    public void OpenRestructureLoan()
    {
        if (!_open || restructureLoanPanel == null || owner == null || _selectedLoan == null)
            return;

        _mode = LoanMode.RestructureLoan;

        if (menuPanel != null)
            menuPanel.Show(false);

        if (operationsMenu != null)
            operationsMenu.Hide();

        restructureLoanPanel.gameObject.SetActive(true);
        restructureLoanPanel.Open(this, _selectedLoan);
    }

    public void OpenDeferLoan()
    {
        if (!_open || deferLoanPanel == null || owner == null || _selectedLoan == null)
            return;

        _mode = LoanMode.DeferLoan;

        if (menuPanel != null)
            menuPanel.Show(false);

        if (operationsMenu != null)
            operationsMenu.Hide();

        deferLoanPanel.gameObject.SetActive(true);
        deferLoanPanel.Open(this, owner, _selectedLoan);
    }

    public void OpenChangeDueDate()
    {
        if (!_open || changeDueDatePanel == null || owner == null || _selectedLoan == null)
            return;

        _mode = LoanMode.ChangeDueDate;

        if (menuPanel != null)
            menuPanel.Show(false);

        if (operationsMenu != null)
            operationsMenu.Hide();

        changeDueDatePanel.gameObject.SetActive(true);
        changeDueDatePanel.Open(this, owner, _selectedLoan);
    }

    public void CloseDeferLoan()
    {
        if (deferLoanPanel != null)
            deferLoanPanel.Hide();

        if (operationsMenu != null && _selectedLoan != null && !_selectedLoan.finished && !_selectedLoan.defaulted)
        {
            _mode = LoanMode.Operations;
            operationsMenu.Open(this, _selectedLoan);
            return;
        }

        BackToMenu();
    }

    public void CloseRepayLoan()
    {
        if (repayLoanPanel != null)
            repayLoanPanel.Hide();

        if (operationsMenu != null && _selectedLoan != null && !_selectedLoan.finished && !_selectedLoan.defaulted)
        {
            _mode = LoanMode.Operations;
            operationsMenu.Open(this, _selectedLoan);
            return;
        }

        BackToMenu();
    }

    public void CloseRestructureLoan()
    {
        if (restructureLoanPanel != null)
            restructureLoanPanel.Hide();

        if (operationsMenu != null && _selectedLoan != null && !_selectedLoan.finished && !_selectedLoan.defaulted)
        {
            _mode = LoanMode.Operations;
            operationsMenu.Open(this, _selectedLoan);
            return;
        }

        BackToMenu();
    }

    public void CloseChangeDueDate()
    {
        if (changeDueDatePanel != null)
            changeDueDatePanel.Hide();

        if (operationsMenu != null && _selectedLoan != null && !_selectedLoan.finished && !_selectedLoan.defaulted)
        {
            _mode = LoanMode.Operations;
            operationsMenu.Open(this, _selectedLoan);
            return;
        }

        BackToMenu();
    }
}