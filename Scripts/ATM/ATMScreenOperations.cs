using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
public class ATMScreenOperations : MonoBehaviour, IATMBackHandler

{
    public enum Mode { None, Deposit, Withdraw }

    [Header("Controller")]
    public ATMUIController controller;

    private InventoryItemInstance _card;
    private ATMUIController _ui;

    [Header("UI - values")]
    public TextMeshProUGUI accWalletText; // Account Money at Bank System
    public TextMeshProUGUI playerWalletText; // Player Money In InventoryUI
    public TextMeshProUGUI feeInfoText; // Taxes of operations
    // To jest Twoje: SelectedValueBorder/Value (TMP) – najlepiej, żeby wyświetlało tylko liczbę
    public TextMeshProUGUI selectedText;

    // Processing (TMP) – możesz wyłączyć w Inspectorze, skrypt i tak steruje .SetActive
    public TextMeshProUGUI processingText;

    [Header("Selectables")]
    public ATMSelectableView depositBtn;
    public ATMSelectableView withdrawBtn;
    public ATMSelectableView confirmBtn;
    public ATMSelectableView cancelBtn;

    [Header("Amount buttons in order (grid)")]
    public ATMSelectableView[] amountBtns; // 6 elementów
    public int[] amountValues;            // 6 wartości (musi tyle samo)

    [Header("Timing")]
    public float processingSeconds = 2.5f;

    // state
    private Mode _mode = Mode.None;
    private int _selectedAmount = 0;

    private int _cursor = 0;
    private bool _busy;

    private readonly List<ATMSelectableView> _nav = new();

    void Awake()
    {
        BuildNav();
        ResetAll();
    }

    void OnEnable()
    {
        BuildNav();
        ResetAll();

        RefreshWallets();
        RefreshSelectedText();
        RefreshVisuals();

        MoveCursorToFirstEnabled();
    }

    public void Open(InventoryItemInstance card, ATMUIController ui)
    {
        _card = card;
        _ui = ui;

        // jeśli używasz "controller" w skrypcie:
        controller = ui;

        // reset + odśwież
        BuildNav();
        ResetAll();
        RefreshWallets();
        RefreshVisuals();
    }
    void BuildNav()
    {
        _nav.Clear();

        if (depositBtn) _nav.Add(depositBtn);
        if (withdrawBtn) _nav.Add(withdrawBtn);

        if (amountBtns != null)
        {
            for (int i = 0; i < amountBtns.Length; i++)
                if (amountBtns[i]) _nav.Add(amountBtns[i]);
        }

        if (confirmBtn) _nav.Add(confirmBtn);
        if (cancelBtn) _nav.Add(cancelBtn);

        _cursor = Mathf.Clamp(_cursor, 0, Mathf.Max(0, _nav.Count - 1));
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_busy) return;

        // nawigacja
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            Move(+1);

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            Move(-1);

        // Enter
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ActivateCurrent();
    }

    void Move(int dir)
    {
        if (_nav.Count == 0) return;

        int start = _cursor;
        for (int i = 0; i < _nav.Count; i++)
        {
            _cursor = (_cursor + dir + _nav.Count) % _nav.Count;

            var v = _nav[_cursor];
            if (v != null && !v.disabled) break;
        }

        if (start != _cursor) RefreshVisuals();
    }

    void MoveCursorToFirstEnabled()
    {
        if (_nav.Count == 0) return;

        for (int i = 0; i < _nav.Count; i++)
        {
            if (_nav[i] != null && !_nav[i].disabled)
            {
                _cursor = i;
                RefreshVisuals();
                return;
            }
        }
    }

    void ActivateCurrent()
    {
        if (_nav.Count == 0) return;

        var cur = _nav[_cursor];
        if (!cur || cur.disabled) return;

        if (cur == depositBtn) SetMode(Mode.Deposit);
        else if (cur == withdrawBtn) SetMode(Mode.Withdraw);
        else if (cur == confirmBtn) Confirm();
        else if (cur == cancelBtn) Cancel();
        else
        {
            // amount
            int idx = IndexOfAmount(cur);
            if (idx >= 0 && idx < amountValues.Length)
                SelectAmount(amountValues[idx]);
        }

        RefreshVisuals();
    }

    int IndexOfAmount(ATMSelectableView v)
    {
        if (amountBtns == null) return -1;
        for (int i = 0; i < amountBtns.Length; i++)
            if (amountBtns[i] == v) return i;
        return -1;
    }

    void SetMode(Mode m)
    {
        _mode = m;
        _selectedAmount = 0;
        RefreshSelectedText();
        RefreshFeeInfo();
    }

    void SelectAmount(int value)
    {
        if (_mode == Mode.None) return;

        _selectedAmount = Mathf.Max(0, value);
        RefreshSelectedText();
        RefreshFeeInfo(_selectedAmount);
    }


    void Cancel()
    {
        // 1) Jeśli jest kwota -> cofnij tylko kwotę
        if (_selectedAmount > 0)
        {
            _selectedAmount = 0;
            UpdateSelectedText();
            RefreshVisuals();
            return;
        }

        // 2) Jeśli nie ma kwoty, ale jest tryb -> cofnij tryb
        if (_mode != Mode.None)
        {
            _mode = Mode.None;
            RefreshSelectedText();
            return;
        }

        // 3) Jeśli nic nie wybrane -> możesz wrócić do AccountInfo (opcjonalnie)
        // controller.ShowAccountInfoFromOperations();
    }

    void Confirm()
    {
        if (_mode == Mode.None) return;
        if (_selectedAmount <= 0) return;

        StartCoroutine(DoOperation());
    }

    IEnumerator DoOperation()
    {
        _busy = true;

        if (processingText)
        {
            processingText.gameObject.SetActive(true);
            processingText.text = "PROCESSING...";
        }

        yield return new WaitForSecondsRealtime(processingSeconds);

        bool ok = ApplyOperation(_mode, _selectedAmount);

        if (processingText)
            processingText.gameObject.SetActive(false);

        if (ok)
        {
            RefreshWallets();

            // Po udanej operacji: wszystko odznacz
            ResetAll();
            RefreshSelectedText();
        }

        RefreshVisuals();

        // po zmianie disabled stanów, upewnij się, że kursor jest na enabled
        if (_nav.Count > 0 && _nav[_cursor] && _nav[_cursor].disabled)
            MoveCursorToFirstEnabled();

        _busy = false;
    }

    bool ApplyOperation(Mode mode, int amount)
    {
        if (controller == null) return false;
        if (_card == null || !_card.hasBankCardMeta) return false;
        if (BankSystem.Instance == null) return false;

        int accountId = _card.bankCard.accountId;

        // (opcjonalnie) jeżeli konto zablokowane / karta nieważna, bank może odrzucić
        // a ATM i tak powinien mieć tylko valid cards, ale to jest "safety net".

        int playerCash = controller.GetPlayerWallet();

        if (mode == Mode.Deposit)
        {
            if (playerCash < amount) return false;

            // player płaci "amount", na konto wchodzi "amount-fee"
            if (!BankSystem.Instance.DepositFromPlayer(accountId, amount, out int fee, out int net))
                return false;

            controller.SetPlayerWallet(playerCash - amount);

            // (opcjonalnie) tu możesz zapisać fee/net do jakiegoś TMP "last op"
            // Debug.Log($"Deposit {amount}, fee {fee}, credited {net}");

            return true;
        }
        else if (mode == Mode.Withdraw)
        {
            // jeśli chcesz fee również przy wypłacie:
            if (!BankSystem.Instance.WithdrawToPlayer(accountId, amount, out int fee, out int totalDebited))
                return false;

            controller.SetPlayerWallet(playerCash + amount);

            // Debug.Log($"Withdraw {amount}, fee {fee}, debited {totalDebited}");
            return true;
        }

        return false;
    }
    void ResetAll()
    {
        _mode = Mode.None;
        _selectedAmount = 0;

        if (processingText)
            processingText.gameObject.SetActive(false);

        RefreshFeeInfo();
        _cursor = 0;
    }


    void RefreshWallets()
    {
        if (!controller) return;

        if (accWalletText) accWalletText.text = $"ACC WALLET: {controller.GetAccountWallet()}";
        if (playerWalletText) playerWalletText.text = $"PLAYER WALLET: {controller.GetPlayerWallet()}";
    }

    void RefreshSelectedText()
    {
        if (!selectedText) return;

        // U Ciebie “SELECTED:” jest osobnym napisem, więc tu dajemy tylko wartość
        selectedText.text = (_selectedAmount > 0) ? _selectedAmount.ToString() : "—";
    }

    void RefreshVisuals()
    {
        // 1) Dostępność elementów zależnie od stanu
        bool amountsEnabled = (_mode != Mode.None);

        if (amountBtns != null)
        {
            for (int i = 0; i < amountBtns.Length; i++)
                if (amountBtns[i]) amountBtns[i].SetDisabled(!amountsEnabled);
        }

        bool canConfirm = (_mode != Mode.None && _selectedAmount > 0);
        if (confirmBtn) confirmBtn.SetDisabled(!canConfirm);

        if (cancelBtn) cancelBtn.SetDisabled(false);
        if (depositBtn) depositBtn.SetDisabled(false);
        if (withdrawBtn) withdrawBtn.SetDisabled(false);

        // 2) Najpierw wyłącz miganie wszystkim
        for (int i = 0; i < _nav.Count; i++)
            if (_nav[i]) _nav[i].SetSelected(false);

        // 3) Miganie aktualnego kursora
        if (_nav.Count > 0 && _nav[_cursor] && !_nav[_cursor].disabled)
            _nav[_cursor].SetSelected(true);

        // 4) “Stałe” podświetlenie wybranych (jeśli chcesz – zależy jak działa ATMSelectableView)
        // Jeśli ATMSelectableView ma tylko miganie, to zostaw.
        // Jeśli ma też “SetActiveSelected(bool)” to tu można dopalić.
        if (depositBtn) depositBtn.SetActiveSelected(_mode == Mode.Deposit);
        if (withdrawBtn) withdrawBtn.SetActiveSelected(_mode == Mode.Withdraw);

        // “Stałe” podświetlenie wybranej kwoty
        if (amountBtns != null && amountValues != null)
        {
            for (int i = 0; i < amountBtns.Length && i < amountValues.Length; i++)
            {
                if (!amountBtns[i]) continue;
                bool chosen = (_selectedAmount > 0 && amountValues[i] == _selectedAmount);
                amountBtns[i].SetActiveSelected(chosen);
            }
        }
    }

    public bool HandleBack()
    {
        if (_busy) return true;

        // jeśli chcesz: ESC najpierw działa jak Cancel (cofnij kwotę/tryb)
        if (_selectedAmount > 0)
        {
            _selectedAmount = 0;
            UpdateSelectedText();
            RefreshVisuals();
            return true;
        }

        if (_mode != Mode.None)
        {
            _mode = Mode.None;
            RefreshSelectedText();
            RefreshVisuals();
            return true;
        }

        // dopiero jak nic nie jest wybrane → wróć do AccountInfo
        controller.ShowAccountInfoFromOperations();
        return true;
    }

    void UpdateSelectedText()
    {
        if (!selectedText) return;

        selectedText.text = (_selectedAmount > 0)
            ? _selectedAmount.ToString()
            : "—";
    }

    void RefreshFeeInfo(int amount = 0)
    {
        if (!feeInfoText || BankSystem.Instance == null)
            return;

        float rate = BankSystem.Instance.TransactionFeeRate;
        int percent = Mathf.RoundToInt(rate * 100f);

        if (amount <= 0)
        {
            feeInfoText.text = $"TRANSACTION FEE: {percent}%";
            return;
        }

        int fee = Mathf.Max(1, Mathf.RoundToInt(amount * rate));
        int net = amount - fee;

        if (_mode == Mode.Deposit)
        {
            feeInfoText.text =
                $"BANK FEE {(rate * 100f):0.#}% | FEE {fee} | NET {net}";
        }
        else if (_mode == Mode.Withdraw)
        {
            feeInfoText.text =
                $"BANK FEE {(rate * 100f):0.#}% | FEE {fee} | NET {net}";
        }
    }

}
