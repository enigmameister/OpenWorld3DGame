using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Splines.Examples;
using UnityEngine;

public class ATMUIController : MonoBehaviour
{
    [Header("Root (CanvasGroup)")]
    [SerializeField] private CanvasGroup root;

    [Header("Screens")]
    [SerializeField] private GameObject screenCardCheck;
    [SerializeField] private GameObject screenNoCard;
    [SerializeField] private GameObject screenSelectCard;
    [SerializeField] private GameObject screenEnterPin;
    [SerializeField] private GameObject screenBlocked;
    [SerializeField] private GameObject screenAccountInfo;

    [Header("Extra screens")]
    [SerializeField] private GameObject screenOperations;
    [SerializeField] private GameObject screenAccountSettings; // <-- NOWE

    [Header("Flow")]
    [SerializeField] private float cardCheckSeconds = 2f;

    public bool IsOpen { get; private set; }

    private Coroutine _flow;
    private Coroutine _flowRoutine;
    private bool _busy;

    private InventoryItemInstance _selectedCard;
    private int _attemptsLeft;

    // prosta “baza kont” na czas dev (accountId -> saldo)
    // private readonly Dictionary<int, int> _accountBalances = new();

    public static bool AnyOpen { get; private set; }

    void Awake()
    {
        if (!root) root = GetComponent<CanvasGroup>();
        HideImmediate();
        ResetFlow();
    }

    void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1) Daj szansę aktywnemu screenowi obsłużyć ESC
            var handler = GetComponentInChildren<IATMBackHandler>(includeInactive: false);
            if (handler != null && handler.HandleBack())
                return;

            // 2) Fallbacki (Twoja stara logika)
            if (screenAccountInfo != null && screenAccountInfo.activeInHierarchy)
                BackToSelectCardFromAccountInfo();
            else
                Close();
        }
    }

    public void Open()
    {

        if (IsOpen) return;
        IsOpen = true;
        AnyOpen = true;

        ShowRoot(true);
        ResetFlow();

        MouseLook.IsLookLocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartFlow();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        AnyOpen = false;

        StopFlow();
        ResetFlow();
        ShowRoot(false);

        MouseLook.IsLookLocked = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void StartFlow()
    {
        StopFlow();
        _flow = StartCoroutine(Flow_Start());
    }

    private void StopFlow()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
            _flowRoutine = null;
        }
        _busy = false;
    }

    private IEnumerator Flow_Start()
    {
        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        var inv = InventoryUI.Instance;
        if (inv == null || BankSystem.Instance == null)
        {
            ShowScreen(screenNoCard);
            yield break;
        }

        var all = inv.GetAllInstancesDistinct();
        var bankCards = all
            .Where(i => i != null && i.data is BankCardItemData && i.hasBankCardMeta)
            .ToList();

        if (bankCards.Count == 0)
        {
            ShowScreen(screenNoCard);
            yield break;
        }

        // Zawsze pokazuj wybór kart – on zrobi sync i sam zdecyduje co selectable
        ShowScreen(screenSelectCard);

        var sc = screenSelectCard.GetComponent<ATMScreenSelectCard>();
        if (sc != null) sc.OpenAndRebuild();

    }

    public void ResetFlow()
    {
        SetAllScreens(false);
        if (screenCardCheck) screenCardCheck.SetActive(true);
        _selectedCard = null;
        _attemptsLeft = 3;
    }

    private void ShowScreen(GameObject screen)
    {
        SetAllScreens(false);
        if (screen) screen.SetActive(true);
    }

    private void SetAllScreens(bool on)
    {
        if (screenCardCheck) screenCardCheck.SetActive(on);
        if (screenNoCard) screenNoCard.SetActive(on);
        if (screenSelectCard) screenSelectCard.SetActive(on);
        if (screenEnterPin) screenEnterPin.SetActive(on);
        if (screenBlocked) screenBlocked.SetActive(on);
        if (screenAccountInfo) screenAccountInfo.SetActive(on);
        if (screenOperations) screenOperations.SetActive(on);
        if (screenAccountSettings) screenAccountSettings.SetActive(on);
    }

    private void ShowRoot(bool show)
    {
        if (!root) { gameObject.SetActive(show); return; }
        root.alpha = show ? 1f : 0f;
        root.blocksRaycasts = show;
        root.interactable = show;
    }

    // --- SELECT CARD -> PIN ---
    public void SelectCard(InventoryItemInstance card)
    {
        if (_busy) return;
        _selectedCard = card;
        _attemptsLeft = 3;

        if (_flowRoutine != null) StopCoroutine(_flowRoutine);
        _flowRoutine = StartCoroutine(GoToEnterPinFlow());
    }

    private IEnumerator GoToEnterPinFlow()
    {
        _busy = true;

        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        ShowScreen(screenEnterPin);

        var pin = screenEnterPin.GetComponent<ATMScreenEnterPin>();
        if (pin != null)
        {
            pin.Open(_selectedCard, this, _attemptsLeft, acceptInput: true);
            pin.onPinSubmit = OnPinSubmit;
            pin.onCancel = () => Close();
        }

        _busy = false;
    }

    private void OnPinSubmit(string typedPin)
    {
        if (_busy) return;
        if (_selectedCard == null || !_selectedCard.hasBankCardMeta) return;
        if (typedPin.Length != 4) return;

        if (!int.TryParse(typedPin, out int pinInt))
            return;

        // karta zablokowana lokalnie -> nie wpuszczaj
        if (_selectedCard.bankCard.status == BankCardStatus.Blocked)
        {
            ClearActiveCard();
            ShowScreenCardCheckThenSelectCard();
            return;
        }

        // STRICT: bank musi istnieć i karta musi być w rejestrze
        if (BankSystem.Instance == null)
            return; // albo: Close() jeśli wolisz

        bool ok = BankSystem.Instance.ValidatePin(_selectedCard.bankCard.cardId, pinInt);

        if (ok)
        {
            if (_flowRoutine != null) StopCoroutine(_flowRoutine);
            _flowRoutine = StartCoroutine(GoToAccountInfoFlow());
            return;
        }

        // Zły PIN
        _attemptsLeft--;

        if (_attemptsLeft <= 0)
        {
            string cardId = _selectedCard.bankCard.cardId;

            bool blocked = BankSystem.Instance.BlockCardAsPinFail3x(cardId);
            Debug.Log($"[ATM] BlockCardAsPinFail3x({cardId}) => {blocked}");

            // sync item
            var meta = _selectedCard.bankCard;
            meta.status = BankCardStatus.Blocked;
            _selectedCard.bankCard = meta;

            if (!blocked)
                BankSystem.Instance.ChangeCardStatus(cardId, BankCardStatus.Blocked);

            if (_flowRoutine != null) StopCoroutine(_flowRoutine);
            _flowRoutine = StartCoroutine(GoToBlockedFlow());
        }
        else
        {
            var pin = screenEnterPin.GetComponent<ATMScreenEnterPin>();
            if (pin != null)
            {
                pin.SetAttempts(_attemptsLeft);
                pin.ClearTyped();
            }
        }
    }


    private IEnumerator GoToAccountInfoFlow()
    {
        _busy = true;

        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        ShowScreen(screenAccountInfo);

        var acc = screenAccountInfo.GetComponent<ATMScreenAccountInfo>();
        if (acc != null) acc.Open(_selectedCard, this);

        _busy = false;
    }

    private IEnumerator GoToBlockedFlow()
    {
        _busy = true;

        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        ShowScreen(screenBlocked);
        _busy = false;
    }

    // --- BACK: AccountInfo -> SelectCard (z CardCheck) ---
    public void BackToSelectCardFromAccountInfo()
    {
        if (_busy) return;

        if (_flowRoutine != null) StopCoroutine(_flowRoutine);
        _flowRoutine = StartCoroutine(BackToSelectCardFlow());
    }

    private IEnumerator BackToSelectCardFlow()
    {
        _busy = true;

        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        ShowScreen(screenSelectCard);
        _busy = false;
    }

    // --- OPERATIONS ---
    public void OpenOperations()
    {
        if (_selectedCard != null && _selectedCard.hasBankCardMeta && _selectedCard.bankCard.status == BankCardStatus.Blocked)
        {
            ClearActiveCard();
            BackToSelectCardFromAccountInfo(); // albo ShowScreenCardCheckThenSelectCard()
            return;
        }

        if (screenOperations == null) return;
        ShowScreen(screenOperations);

        var ops = screenOperations.GetComponent<ATMScreenOperations>();
        if (ops != null) ops.Open(_selectedCard, this);
    }

    public void ShowAccountInfoFromOperations()
    {
        ShowScreen(screenAccountInfo);

        var acc = screenAccountInfo.GetComponent<ATMScreenAccountInfo>();
        if (acc != null) acc.Open(_selectedCard, this);
    }

    // --- ACCOUNT SETTINGS ---
    public void OpenAccountSettings()
    {
        if (_selectedCard != null && _selectedCard.hasBankCardMeta && _selectedCard.bankCard.status == BankCardStatus.Blocked)
        {
            ClearActiveCard();
            BackToSelectCardFromAccountInfo(); // albo ShowScreenCardCheckThenSelectCard()
            return;
        }

        if (screenAccountSettings == null) return;
        ShowScreen(screenAccountSettings);

        var s = screenAccountSettings.GetComponent<ATMScreenAccountSettings>();
        if (s != null) s.Open(_selectedCard, this);
    }

    public void ShowAccountInfoFromSettings()
    {
        ShowScreen(screenAccountInfo);

        var acc = screenAccountInfo.GetComponent<ATMScreenAccountInfo>();
        if (acc != null) acc.Open(_selectedCard, this);
    }

    // --- WALLET ADAPTERS ---
    public int GetAccountWallet()
    {
        int accId = CurrentAccountIdOrInvalid();
        if (accId < 0 || BankSystem.Instance == null) return 0;
        return BankSystem.Instance.GetBalance(accId);
    }

    public void SetAccountWallet(int v)
    {
        // nie rób SetAccountWallet “na twardo”, tylko operacje przez BankSystem
        // (to usuń albo zostaw tylko do debug).
    }

    public int GetPlayerWallet()
    {
        var ps = FindFirstObjectByType<PlayerStats>();
        return ps != null ? Mathf.Max(0, ps.money) : 0;
    }

    public void SetPlayerWallet(int v)
    {
        var ps = FindFirstObjectByType<PlayerStats>();
        if (ps == null) return;

        ps.money = Mathf.Max(0, v);

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RefreshCashUI();
    }

    public InventoryItemInstance GetSelectedCard() => _selectedCard;

    private int CurrentAccountIdOrInvalid()
    {
        if (_selectedCard == null || !_selectedCard.hasBankCardMeta) return -1;
        return _selectedCard.bankCard.accountId; // int
    }

    public void ShowScreenAccountInfo()
    {
        ShowScreen(screenAccountInfo);
    }

    public void BlockCardAndShowBlocked(InventoryItemInstance card)
    {
        if (card != null && card.hasBankCardMeta)
        {
            var meta = card.bankCard;
            meta.status = BankCardStatus.Blocked;
            card.bankCard = meta;

            // Źródło prawdy: bank -> ustaw także powód
            if (BankSystem.Instance != null)
                BankSystem.Instance.BlockCardAsOwnerBlocked(meta.cardId);
        }

        _selectedCard = card;

        if (_flowRoutine != null) StopCoroutine(_flowRoutine);
        _flowRoutine = StartCoroutine(GoToBlockedFlow());
    }

    public void ClearActiveCard()
    {
        _selectedCard = null;
    }

    public void ShowScreenCardCheckThenSelectCard()
    {
        if (_busy) return;

        if (_flowRoutine != null) StopCoroutine(_flowRoutine);
        _flowRoutine = StartCoroutine(CoCardCheckThenSelectCard());
    }
    private IEnumerator CoCardCheckThenSelectCard()
    {
        _busy = true;

        ShowScreen(screenCardCheck);
        yield return new WaitForSecondsRealtime(cardCheckSeconds);

        // pokaż listę kart (onlyUsable:true już odfiltruje BLOCKED)
        ShowScreen(screenSelectCard);

        _busy = false;
    }

    public void ShowNoCardScreen()
    {
        ShowScreen(screenNoCard);
    }

    private void HideImmediate() => ShowRoot(false);
}
