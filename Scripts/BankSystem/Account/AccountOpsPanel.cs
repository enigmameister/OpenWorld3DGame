using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AccountOpsPanel : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private AccountOperationsUI owner;

    [Header("Menu Buttons (order matters: Up/Down)")]
    [SerializeField] private List<Button> menuButtons = new();

    [Header("Card Operations Visual Lock")]
    [SerializeField] private Button cardOperationsButton;
    [SerializeField] private GameObject cardOperationsDisabledBg;

    [Header("Highlight Fade")]
    [SerializeField] private float normalAlpha = 0.10f;
    [SerializeField] private float selectedAlpha = 0.55f;
    [SerializeField] private float fadeSpeed = 10f;

    [Header("SubPanels")]
    [SerializeField] private CashAmountPanelUI depositPanel;
    [SerializeField] private CashAmountPanelUI withdrawPanel;
    [SerializeField] private AccountTransferPanelUI transferPanel;
    [SerializeField] private LoanMenuNavigation loanMenuNavigation;
    [SerializeField] private NewCreditCardPanelUI newCreditCardPanel;
    [SerializeField] private TransactionsHistoryPanelUI transactionsHistoryPanel;
    [SerializeField] private CloseAccountUI closeAccountPanel;

    [Header("External Screen")]
    [SerializeField] private CreditCardOperationsUI creditCardOperationsUI;

    private int _accountId;
    private bool _isOpen;
    private int _selectedIndex;

    private readonly List<Image> _buttonImages = new();
    private readonly List<float> _targetAlphas = new();

    private enum Mode
    {
        Menu,
        Deposit,
        Withdraw,
        Transfer,
        LoanModule,
        NewCreditCard,
        TransactionsHistory,
        CloseAccount
    }

    private Mode _mode = Mode.Menu;

    public void BindOwner(AccountOperationsUI ui) => owner = ui;

    private int _escapeConsumedFrame = -1;
    private bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

    private void Awake()
    {
        CacheButtonImages();
        ForceCloseAllSubPanels();
        _isOpen = false;
    }

    private void OnEnable()
    {
        CacheButtonImages();
        ApplySelection(immediate: true);
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (loanMenuNavigation != null && loanMenuNavigation.IsInSubLoanPanel())
        {
            TickFade();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !IsEscapeConsumedThisFrame)
        {
            ConsumeEscapeThisFrame();
            BankDialogueUI.SuppressEscapeFrames = 1;

            if (AnySubpanelActive() || _mode != Mode.Menu)
            {
                OpenMenu();
                ConsumeEscapeThisFrame();
                return;
            }

            ExitToDialogueStart();
            return;
        }

        if (owner != null && owner.IsLoanPagesFocused())
        {
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                owner.ExitLoanPagesFocus();
                FocusMenuIndex(0);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                owner.ExitLoanPagesFocus();
                FocusMenuIndex(menuButtons.Count - 1);
                return;
            }

            TickFade();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(+1);

        bool confirm =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.E);

        if (confirm) ClickSelected();

        TickFade();
    }

    public void ConsumeEscapeThisFrame()
    {
        _escapeConsumedFrame = Time.frameCount;
    }

    private void FocusMenuIndex(int index)
    {
        if (menuButtons == null || menuButtons.Count == 0) return;

        _selectedIndex = Mathf.Clamp(index, 0, menuButtons.Count - 1);
        ApplySelection(immediate: true);
    }

    public void OpenForAccount(int accountId)
    {
        _accountId = accountId;
        _isOpen = true;

        ForceCloseAllSubPanels();
        RefreshCardOperationsButton();
        OpenMenu();
    }

    public void OpenMenu()
    {
        _mode = Mode.Menu;

        // 🔒 bardzo ważne: przy powrocie z subpanelu
        // zjedz ESC na tę i następną klatkę
        ConsumeEscapeThisFrame();
        BankDialogueUI.SuppressEscapeFrames = 2;

        // 🔒 jeśli dialog NPC zdążył się pokazać, zamknij go ponownie
        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        ForceCloseAllSubPanels();
        SetMenuInteractable(true);
        RefreshCardOperationsButton();

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, menuButtons.Count - 1));
        ApplySelection(immediate: true);
    }

    public void OnOwnerClosed()
    {
        _isOpen = false;
        _mode = Mode.Menu;
        ForceCloseAllSubPanels();
    }

    public void OnDepositButton() => OpenDeposit();
    public void OnWithdrawButton() => OpenWithdraw();
    public void OnTransferButton() => OpenTransfer();
    public void OnLoanButton() => OpenLoanModule();
    public void OnNewCreditCardButton() => OpenNewCreditCard();
    public void OnCardOperationsButton() => OpenCardOperations();
    public void OnTransactionsHistory() => OpenTransactionsHistory();
    public void OnCloseAccountButton() => OpenCloseAccount();

    public void OnExitButton() => ExitToDialogueStart();

    private void OpenDeposit()
    {
        if (owner != null) owner.ExitLoanPagesFocus();
        if (!depositPanel || owner == null) return;

        ForceCloseAllSubPanels();
        _mode = Mode.Deposit;
        SetMenuInteractable(false);

        depositPanel.gameObject.SetActive(true);
        depositPanel.Open(owner, _accountId);
    }

    private void OpenWithdraw()
    {
        if (owner != null) owner.ExitLoanPagesFocus();
        if (!withdrawPanel || owner == null) return;

        ForceCloseAllSubPanels();
        _mode = Mode.Withdraw;
        SetMenuInteractable(false);

        withdrawPanel.gameObject.SetActive(true);
        withdrawPanel.Open(owner, _accountId);
    }

    private void OpenTransfer()
    {
        if (owner != null) owner.ExitLoanPagesFocus();
        if (!transferPanel || owner == null) return;

        ForceCloseAllSubPanels();
        _mode = Mode.Transfer;
        SetMenuInteractable(false);

        transferPanel.gameObject.SetActive(true);
        transferPanel.Open(owner, _accountId);
    }

    private void OpenLoanModule()
    {
        if (!loanMenuNavigation || owner == null) return;

        ForceCloseAllSubPanels();
        _mode = Mode.LoanModule;
        SetMenuInteractable(false);

        loanMenuNavigation.Open(owner, _accountId);
    }

    private void OpenTransactionsHistory()
    {
        if (transactionsHistoryPanel == null || owner == null)
            return;

        _mode = Mode.TransactionsHistory;

        ForceCloseAllSubPanels();
        transactionsHistoryPanel.Open(owner, owner.AccountId);
    }
    private void OpenNewCreditCard()
    {
        if (owner != null) owner.ExitLoanPagesFocus();
        if (!newCreditCardPanel || owner == null) return;

        ForceCloseAllSubPanels();
        _mode = Mode.NewCreditCard;
        SetMenuInteractable(false);

        newCreditCardPanel.gameObject.SetActive(true);
        newCreditCardPanel.Open(owner, _accountId);
    }

    private void OpenCardOperations()
    {
        if (creditCardOperationsUI == null)
            creditCardOperationsUI = FindFirstObjectByType<CreditCardOperationsUI>();

        if (creditCardOperationsUI == null || owner == null)
            return;

        ForceCloseAllSubPanels();

        owner.Close(unlockPlayer: false);

        _mode = Mode.Menu;
        _isOpen = false;

        creditCardOperationsUI.OpenFromAccountOperations(owner, _accountId);
    }
    private void OpenCloseAccount()
    {
        if (closeAccountPanel == null || owner == null)
            return;

        ForceCloseAllSubPanels();
        _mode = Mode.CloseAccount;
        SetMenuInteractable(false);

        closeAccountPanel.Open(this, owner, _accountId);
    }

    private void ExitToDialogueStart()
    {
        if (owner == null) return;
        owner.ReturnToDialogueStart();
    }

    private void ForceCloseAllSubPanels()
    {
        if (depositPanel) depositPanel.Close(goBackToMenu: false);
        if (withdrawPanel) withdrawPanel.Close(goBackToMenu: false);
        if (transferPanel) transferPanel.Close(goBackToMenu: false);
        if (loanMenuNavigation) loanMenuNavigation.Close();
        if (newCreditCardPanel) newCreditCardPanel.Close(goBackToMenu: false);
        if (transactionsHistoryPanel) transactionsHistoryPanel.Hide();
        if (closeAccountPanel) closeAccountPanel.Close(returnToMenu: false);
    }

    private void SetMenuInteractable(bool v)
    {
        for (int i = 0; i < menuButtons.Count; i++)
            if (menuButtons[i]) menuButtons[i].interactable = v;
    }

    private bool AccountHasAnyCards()
    {
        var bank = BankSystem.Instance;
        if (bank == null) return false;

        var cards = bank.GetCardsForAccount(_accountId, includeDeleted: false);
        return cards != null && cards.Count > 0;
    }

    private void RefreshCardOperationsButton()
    {
        if (!cardOperationsButton) return;

        bool hasCards = AccountHasAnyCards();
        cardOperationsButton.interactable = hasCards;

        if (cardOperationsDisabledBg)
            cardOperationsDisabledBg.SetActive(!hasCards);
    }

    private void MoveSelection(int delta)
    {
        if (menuButtons == null || menuButtons.Count == 0) return;

        if (delta < 0 && _selectedIndex == 0 && owner != null && owner.HasVisibleLoanPages())
        {
            owner.FocusLoanPagesFromMenu(fromEnd: false);
            return;
        }

        if (delta > 0 && _selectedIndex == menuButtons.Count - 1 && owner != null && owner.HasVisibleLoanPages())
        {
            owner.FocusLoanPagesFromMenu(fromEnd: true);
            return;
        }

        int count = menuButtons.Count;
        for (int tries = 0; tries < count; tries++)
        {
            _selectedIndex = (_selectedIndex + delta + count) % count;
            var b = menuButtons[_selectedIndex];
            if (b != null && b.gameObject.activeInHierarchy && b.interactable) break;
        }

        ApplySelection(immediate: false);
    }

    private void ClickSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= menuButtons.Count) return;
        var b = menuButtons[_selectedIndex];
        if (b == null || !b.interactable) return;
        b.onClick?.Invoke();
    }

    private void ApplySelection(bool immediate)
    {
        if (_mode != Mode.Menu) return;

        EnsureLists();

        for (int i = 0; i < menuButtons.Count; i++)
            _targetAlphas[i] = (i == _selectedIndex) ? selectedAlpha : normalAlpha;

        if (EventSystem.current != null)
        {
            var b = (_selectedIndex >= 0 && _selectedIndex < menuButtons.Count) ? menuButtons[_selectedIndex] : null;
            if (b != null)
                EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        if (immediate)
        {
            for (int i = 0; i < _buttonImages.Count; i++)
                SetAlpha(_buttonImages[i], _targetAlphas[i]);
        }
    }

    private void TickFade()
    {
        for (int i = 0; i < _buttonImages.Count; i++)
        {
            var img = _buttonImages[i];
            if (img == null) continue;

            float a = img.color.a;
            float target = _targetAlphas[i];
            float next = Mathf.MoveTowards(a, target, fadeSpeed * Time.unscaledDeltaTime);
            SetAlpha(img, next);
        }
    }

    private void CacheButtonImages()
    {
        _buttonImages.Clear();
        _targetAlphas.Clear();

        for (int i = 0; i < menuButtons.Count; i++)
        {
            Image img = null;
            if (menuButtons[i] != null)
                img = menuButtons[i].GetComponent<Image>();

            _buttonImages.Add(img);
            _targetAlphas.Add(normalAlpha);

            if (img != null) SetAlpha(img, normalAlpha);
        }
    }

    private void EnsureLists()
    {
        while (_targetAlphas.Count < menuButtons.Count) _targetAlphas.Add(normalAlpha);
        while (_buttonImages.Count < menuButtons.Count) _buttonImages.Add(null);
    }

    private static void SetAlpha(Image img, float a)
    {
        if (!img) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    private bool AnySubpanelActive()
    {
        return (depositPanel && depositPanel.gameObject.activeInHierarchy)
            || (withdrawPanel && withdrawPanel.gameObject.activeInHierarchy)
            || (transferPanel && transferPanel.gameObject.activeInHierarchy)
            || (loanMenuNavigation && loanMenuNavigation.gameObject.activeInHierarchy)
            || (newCreditCardPanel && newCreditCardPanel.gameObject.activeInHierarchy)
            || (closeAccountPanel && closeAccountPanel.gameObject.activeInHierarchy);
    }
}