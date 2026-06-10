using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NewCreditCardPanelUI : MonoBehaviour
{
    private enum Focus
    {
        PinEdit,
        PinSave,
        VariantPick,
        VariantSave,
        Actions
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Fee / Info")]
    [SerializeField] private TMP_Text feeValueText;
    [SerializeField] private TMP_Text cardIdText;
    [SerializeField] private TMP_Text cardStatusText;
    [SerializeField] private TMP_Text notEnoughCashText;

    [Header("Buttons (Actions)")]
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button backBtn;

    [Header("PIN Entry")]
    [SerializeField] private Transform pinContainer;
    [SerializeField] private PinSlotView pinSlotPrefab;
    [SerializeField] private int pinLength = 4;
    [SerializeField] private Button pinSaveBtn;
    [SerializeField] private Button pinCancelBtn;

    [Header("Variant Picker")]
    [SerializeField] private Transform variantContainer;
    [SerializeField] private VariantSlotView variantSlotPrefab;
    [SerializeField] private Button variantSaveBtn;
    [SerializeField] private Button variantCancelBtn;

    [Header("Card Item (Inventory)")]
    [SerializeField] private BankCardItemData bankCardItemData;

    [Header("Fees")]
    [SerializeField] private int cardFeeCash = 5;

    [Header("Processing")]
    [SerializeField] private GameObject processingRoot;   // np. GO z TMP "PROCESSING..."
    [SerializeField] private TMP_Text processingText;
    [SerializeField] private float confirmDelaySeconds = 5f;

    // runtime
    private AccountOperationsUI _owner;
    private int _accountId;

    private Focus _focus;

    private readonly List<PinSlotView> _pinSlots = new();
    private int[] _pinDigits;
    private int _activePinIndex;

    private readonly List<VariantSlotView> _variantSlots = new();
    private int _variantHoverIndex;
    private int _variantSelectedIndex = -1;

    private bool _pinSaved;
    private bool _variantSaved;

    private Coroutine _processingCo;
    private bool _suppressSubmitOneFrame;

    // --- button nav indices ---
    private int _pinButtonsIndex = 0;      // 0=Save, 1=Cancel
    private int _variantButtonsIndex = 0;  // 0=Save, 1=Cancel
    private int _actionsIndex = 1;         // 0=Cancel, 1=Confirm, 2=Back (start na Confirm)

    private void Awake()
    {
        Show(false);

        if (confirmBtn) confirmBtn.onClick.AddListener(OnConfirm);
        if (cancelBtn) cancelBtn.onClick.AddListener(OnCancel);
        if (backBtn) backBtn.onClick.AddListener(OnBack);

        if (pinSaveBtn) pinSaveBtn.onClick.AddListener(SavePin);
        if (pinCancelBtn) pinCancelBtn.onClick.AddListener(ClearPin);

        if (variantSaveBtn) variantSaveBtn.onClick.AddListener(SaveVariant);
        if (variantCancelBtn) variantCancelBtn.onClick.AddListener(ClearVariant);

        if (processingRoot) processingRoot.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen()) return;

        // ESC = Back (wyjście do menu operations)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBack();
            return;
        }

        // podczas processing blokujemy input
        if (_processingCo != null) return;

        // Focus nav
        // UP/DOWN navigation
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (HandleUpDownInGroups(-1)) return;
            MoveFocus(-1);
            return;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (HandleUpDownInGroups(+1)) return;
            MoveFocus(+1);
            return;
        }

        // LEFT/RIGHT inside button groups
        if (HandleLeftRightInGroups()) return;

        // Enter / Space = akcja na focusie
        bool enter =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space);

        if (enter)
        {
            if (_suppressSubmitOneFrame) return;   // <-- KLUCZ
            ActivateFocused();
            return;
        }

        // szybkie lewo/prawo pomiędzy edycją a SAVE (jak w Create Account)
        if (_focus == Focus.PinSave)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _focus = Focus.PinEdit;
                _activePinIndex = Mathf.Max(0, (_pinDigits?.Length ?? 1) - 1);
                ApplyFocus();
                RefreshAll();
                return;
            }

            // opcjonalnie: Right z PinSave przerzuca od razu do VariantPick
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _focus = Focus.VariantPick;
                ApplyFocus();
                RefreshAll();
                return;
            }
        }

        if (_focus == Focus.VariantSave)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _focus = Focus.VariantPick;
                ApplyFocus();
                RefreshAll();
                return;
            }

            // opcjonalnie: Right z VariantSave przerzuca do Actions
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _focus = Focus.Actions;
                ApplyFocus();
                RefreshAll();
                return;
            }
        }

        // PIN digits
        if (_focus == Focus.PinEdit)
        {
            HandlePinInput();
            return;
        }

        // Variant left/right
        if (_focus == Focus.VariantPick)
        {
            HandleVariantInput();
            return;
        }
    }

    private void LateUpdate()
    {
        if (!IsOpen()) return;
        if (!EventSystem.current) return;

        // Gdy jesteśmy w trybie "inputowym" – NIGDY nie trzymaj zaznaczonego buttona,
        // bo Unity przerzuca highlight po nawigacji Selectable.
        if (_focus == Focus.PinEdit || _focus == Focus.VariantPick)
        {
            if (EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
    // =========================
    // Public API
    // =========================
    public void Open(AccountOperationsUI owner, int accountId)
    {
        _owner = owner;
        _accountId = accountId;

        if (feeValueText) feeValueText.text = $"{cardFeeCash}$";
        if (cardIdText) cardIdText.text = "---";

        var bank = BankSystem.Instance;
        float h = bank != null ? bank.CardActivationDelayHours : 24f;
        if (cardStatusText) cardStatusText.text = $"PENDING ({h:0.#}H)";

        if (notEnoughCashText) notEnoughCashText.gameObject.SetActive(false);
        if (processingRoot) processingRoot.SetActive(false);

        BuildPinSlots(pinLength);

        int vCount = 6;
        if (bank != null) vCount = Mathf.Max(1, bank.VariantCount);
        BuildVariantSlots(vCount);

        // kolor wariantów
        if (bank != null)
        {
            for (int i = 0; i < _variantSlots.Count; i++)
                _variantSlots[i].SetPreviewColor(bank.GetVariantColor(i));
        }

        ResetDraft();

        // Start: PIN edit (ale gracz może od razu zjechać do varianta)
        _focus = Focus.PinEdit;

        Show(true);
        ApplyFocus();
        RefreshAll();
    }

    public void Close(bool goBackToMenu)
    {
        StopProcessing();
        Show(false);

        if (goBackToMenu && _owner != null)
            _owner.ReturnToMenuFromSubPanel();

        _owner = null;
        _accountId = 0;
    }

    // =========================
    // Focus logic
    // =========================
    private void MoveFocus(int dir)
    {
        Focus[] order =
        {
            Focus.PinEdit,
            Focus.PinSave,
            Focus.VariantPick,
            Focus.VariantSave,
            Focus.Actions
        };

        int idx = System.Array.IndexOf(order, _focus);
        if (idx < 0) idx = 0;

        idx = Mathf.Clamp(idx + dir, 0, order.Length - 1);
        _focus = order[idx];

        ApplyFocus();
        RefreshAll();
    }

    private void ActivateFocused()
    {
        switch (_focus)
        {
            case Focus.PinSave:
                if (_pinButtonsIndex == 0)
                {
                    if (pinSaveBtn && pinSaveBtn.interactable) pinSaveBtn.onClick.Invoke();
                }
                else
                {
                    if (pinCancelBtn) pinCancelBtn.onClick.Invoke();
                }
                break;

            case Focus.VariantPick:
                // wybór wariantu ENTER -> żółty border i przejście do VariantSave (na SAVE)
                _variantSelectedIndex = _variantHoverIndex;
                _variantButtonsIndex = 0;
                _focus = Focus.VariantSave;
                ApplyFocus();
                RefreshAll();
                break;

            case Focus.VariantSave:
                if (_variantButtonsIndex == 0)
                {
                    if (variantSaveBtn && variantSaveBtn.interactable) variantSaveBtn.onClick.Invoke();
                }
                else
                {
                    if (variantCancelBtn) variantCancelBtn.onClick.Invoke();
                }
                break;

            case Focus.Actions:
                if (_actionsIndex == 0) { if (cancelBtn) cancelBtn.onClick.Invoke(); }
                else if (_actionsIndex == 1) { if (confirmBtn && confirmBtn.interactable) confirmBtn.onClick.Invoke(); }
                else { if (backBtn) backBtn.onClick.Invoke(); }
                break;
        }
    }

    private void ApplyFocus()
    {
        if (!EventSystem.current) return;

        // tryby inputowe: nie wybieramy buttonów
        if (_focus == Focus.PinEdit || _focus == Focus.VariantPick)
        {
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        GameObject go = null;

        if (_focus == Focus.PinSave)
        {
            go = (_pinButtonsIndex == 0)
                ? (pinSaveBtn ? pinSaveBtn.gameObject : null)
                : (pinCancelBtn ? pinCancelBtn.gameObject : null);
        }
        else if (_focus == Focus.VariantSave)
        {
            go = (_variantButtonsIndex == 0)
                ? (variantSaveBtn ? variantSaveBtn.gameObject : null)
                : (variantCancelBtn ? variantCancelBtn.gameObject : null);
        }
        else if (_focus == Focus.Actions)
        {
            if (_actionsIndex == 0) go = cancelBtn ? cancelBtn.gameObject : null;
            else if (_actionsIndex == 1) go = confirmBtn ? confirmBtn.gameObject : null;
            else go = backBtn ? backBtn.gameObject : null;
        }

        EventSystem.current.SetSelectedGameObject(go);
    }

    // =========================
    // Actions
    // =========================
    private void OnBack() => Close(goBackToMenu: true);

    private void OnCancel() => Close(goBackToMenu: true);

    private void OnConfirm()
    {
        if (!CanConfirm()) return;
        if (_processingCo != null) return;

        _processingCo = StartCoroutine(CoConfirm());
    }

    private IEnumerator CoConfirm()
    {
        // UI processing
        if (notEnoughCashText) notEnoughCashText.gameObject.SetActive(false);

        if (processingRoot) processingRoot.SetActive(true);
        int dots = 0;
        float elapsed = 0f;

        while (elapsed < confirmDelaySeconds)
        {
            dots = (dots + 1) % 4; // 0..3
            if (processingText) processingText.text = "PROCESSING" + new string('.', dots);

            elapsed += 0.35f;
            yield return new WaitForSecondsRealtime(0.35f);
        }

        // 0) BankSystem
        var bank = BankSystem.Instance;
        if (bank == null)
        {
            if (processingRoot) processingRoot.SetActive(false);
            _processingCo = null;
            yield break;
        }

        // 0.5) limit kart (zanim pobierzemy fee!)
        int cardsCount = bank.CountCardsForAccount(_accountId, includeRevoked: false);
        if (cardsCount >= BankSystem.Instance.MaxCardsPerAccount)
        {
            if (notEnoughCashText)
            {
                notEnoughCashText.gameObject.SetActive(true);
                notEnoughCashText.text = "CARD LIMIT REACHED (5)";
            }

            if (processingRoot) processingRoot.SetActive(false);
            _processingCo = null;
            yield break;
        }


        // Pobranie opłaty z CASH
        var ps = FindFirstObjectByType<PlayerStats>();
        if (ps != null)
        {
            // u Ciebie jest SpendMoney i pole money
            if (ps.money < cardFeeCash || !ps.SpendMoney(cardFeeCash))
            {
                if (notEnoughCashText)
                {
                    notEnoughCashText.gameObject.SetActive(true);
                    notEnoughCashText.text = "Brak gotówki";
                }

                StopProcessing();
                yield break;
            }
        }

        // Issue card (Pending + activateAt w BankSystem)
        if (bank == null)
        {
            StopProcessing();
            yield break;
        }

        int pinInt = 0;
        int.TryParse(GetPinString(), out pinInt);
        int variant = Mathf.Max(0, _variantSelectedIndex);

        var rec = bank.IssueCard(
            accountId: _accountId,
            pin: pinInt,
            colorVariant: variant,
            initialStatus: BankCardStatus.Pending
        );

        // Inventory item (tak jak create account)
        if (bankCardItemData != null)
        {
            var inst = new InventoryItemInstance(bankCardItemData) { count = 1 };
            inst.hasBankCardMeta = true;
            inst.bankCard = new BankCardMeta
            {
                cardId = rec.cardId,
                accountId = rec.accountId,
                pin = rec.pin,
                status = rec.status,
                colorVariant = rec.colorVariant,
                activateAt = rec.activateAt
            };

            var inv = InventoryUI.Instance;
            if (inv != null)
                inv.TryAddItem(inst);
        }

        // (opcjonalnie) pokaż na chwilę ID w UI – ale Ty chcesz raczej wrócić do menu
        if (cardIdText) cardIdText.text = rec.cardId;

        StopProcessing();
        Close(goBackToMenu: true);
    }

    private void StopProcessing()
    {
        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }
        if (processingRoot) processingRoot.SetActive(false);
    }

    // =========================
    // PIN
    // =========================
    private void BuildPinSlots(int count)
    {
        if (!pinContainer || !pinSlotPrefab) return;

        for (int i = pinContainer.childCount - 1; i >= 0; i--)
            Destroy(pinContainer.GetChild(i).gameObject);

        _pinSlots.Clear();
        _pinDigits = new int[count];

        for (int i = 0; i < count; i++)
        {
            _pinDigits[i] = -1;
            var slot = Instantiate(pinSlotPrefab, pinContainer);
            slot.SetDigit(-1);
            slot.SetArrow(false);
            _pinSlots.Add(slot);
        }

        _activePinIndex = 0;
    }

    private void HandlePinInput()
    {
        if (_pinDigits == null || _pinDigits.Length == 0) return;

        // LEFT: normalnie cofaj slot
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _activePinIndex = Mathf.Max(0, _activePinIndex - 1);
            RefreshAll();
            return;
        }

        // RIGHT: na ostatnim slocie + PIN kompletny -> przejdź do PinSave
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            bool isLast = _activePinIndex >= _pinDigits.Length - 1;

            if (isLast)
            {
                if (IsPinComplete())
                {
                    _focus = Focus.PinSave;     // ⬅️ klucz
                    ApplyFocus();
                    RefreshAll();
                }
                // jeśli niekompletny -> nie przesuwaj już indeksu
                return;
            }

            _activePinIndex = Mathf.Min(_pinDigits.Length - 1, _activePinIndex + 1);
            RefreshAll();
            return;
        }

        // BACKSPACE
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_pinDigits[_activePinIndex] != -1) _pinDigits[_activePinIndex] = -1;
            else if (_activePinIndex > 0) { _activePinIndex--; _pinDigits[_activePinIndex] = -1; }

            _pinSaved = false;
            RefreshAll();
            return;
        }

        // DIGIT
        int d = GetDigitDown();
        if (d >= 0)
        {
            _pinDigits[_activePinIndex] = d;
            _pinSaved = false;

            bool wasLast = (_activePinIndex >= _pinDigits.Length - 1);

            if (!wasLast)
            {
                _activePinIndex++;
                RefreshAll();
                return;
            }

            // jeśli wpisaliśmy ostatnią cyfrę i teraz PIN jest kompletny -> auto na PinSave
            if (IsPinComplete())
            {
                _focus = Focus.PinSave;     // ⬅️ klucz (auto)
                ApplyFocus();
            }

            RefreshAll();
        }
    }

    private void SavePin()
    {
        if (!IsPinComplete()) return;

        _pinSaved = true;

        // start wariantów: hover na 0 i czyścimy wybór
        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;
        _variantSaved = false;

        // ważne: przełącz fokus dopiero next-frame
        DeferFocus(Focus.VariantPick);
    }

    private void ClearPin()
    {
        _pinSaved = false;
        for (int i = 0; i < _pinDigits.Length; i++) _pinDigits[i] = -1;
        _activePinIndex = 0;
        RefreshAll();
    }

    private bool IsPinComplete()
    {
        if (_pinDigits == null) return false;
        for (int i = 0; i < _pinDigits.Length; i++)
            if (_pinDigits[i] < 0) return false;
        return true;
    }

    private string GetPinString()
    {
        if (!IsPinComplete()) return null;
        string s = "";
        for (int i = 0; i < _pinDigits.Length; i++) s += _pinDigits[i].ToString();
        return s;
    }

    private int GetDigitDown()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) return 9;
        return -1;
    }

    // =========================
    // Variant
    // =========================
    private void BuildVariantSlots(int count)
    {
        if (!variantContainer || !variantSlotPrefab) return;

        for (int i = variantContainer.childCount - 1; i >= 0; i--)
            Destroy(variantContainer.GetChild(i).gameObject);

        _variantSlots.Clear();
        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(variantSlotPrefab, variantContainer);
            slot.SetHover(false);
            slot.SetSelected(false);
            if (slot.activeBorder) slot.activeBorder.SetActive(false);
            if (slot.arrow) slot.arrow.SetActive(false);
            _variantSlots.Add(slot);
        }

        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;
    }

    private void HandleVariantInput()
    {
        if (_variantSlots.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _variantHoverIndex = (_variantHoverIndex - 1 + _variantSlots.Count) % _variantSlots.Count;
            _variantSaved = false; // zmiana unieważnia save
            RefreshAll();
            return;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            _variantHoverIndex = (_variantHoverIndex + 1) % _variantSlots.Count;
            _variantSaved = false;
            RefreshAll();
            return;
        }

        // Enter obsłuży ActivateFocused() – tu nic
    }
    private void SaveVariant()
    {
        if (_variantSelectedIndex < 0) return;

        _variantSaved = true;

        _actionsIndex = 1; // CONFIRM
        DeferFocus(Focus.Actions);
    }

    private void ClearVariant()
    {
        _variantSaved = false;
        _variantSelectedIndex = -1;
        _variantHoverIndex = 0;
        RefreshAll();
    }

    // =========================
    // UI refresh
    // =========================
    private void ResetDraft()
    {
        _pinSaved = false;
        _variantSaved = false;

        _activePinIndex = 0;
        if (_pinDigits != null)
            for (int i = 0; i < _pinDigits.Length; i++) _pinDigits[i] = -1;

        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;
    }

    private void RefreshAll()
    {
        if (notEnoughCashText) notEnoughCashText.gameObject.SetActive(false);

        // PIN visual
        for (int i = 0; i < _pinSlots.Count; i++)
        {
            _pinSlots[i].SetDigit(_pinDigits[i]);

            bool arrow = (_focus == Focus.PinEdit && i == _activePinIndex);
            _pinSlots[i].SetArrow(arrow);
        }

        // Variant visual
        for (int i = 0; i < _variantSlots.Count; i++)
        {
            bool inPicker = (_focus == Focus.VariantPick);

            bool hover = inPicker && (i == _variantHoverIndex);
            bool chosen = (i == _variantSelectedIndex);

            // strzałka tylko w pickerze
            _variantSlots[i].SetHover(hover);

            // border:
            // - w pickerze: border = hover (biały)
            // - poza pickerem: border = chosen
            //   a żółty gdy: jesteśmy w VariantSave (po ENTER) lub gdy _variantSaved
            bool highlight = chosen && (_focus == Focus.VariantSave || _variantSaved);

            if (inPicker)
                _variantSlots[i].SetSelected(hover, highlighted: false);
            else
                _variantSlots[i].SetSelected(chosen, highlighted: highlight);
        }

        // buttons state
        if (pinSaveBtn) pinSaveBtn.interactable = !_pinSaved && IsPinComplete();
        if (pinCancelBtn) pinCancelBtn.interactable = true;

        if (variantSaveBtn) variantSaveBtn.interactable = !_variantSaved && _variantSelectedIndex >= 0;
        if (variantCancelBtn) variantCancelBtn.interactable = true;

        if (confirmBtn) confirmBtn.interactable = CanConfirm();
    }

    private bool CanConfirm()
    {
        if (!_pinSaved) return false;
        if (!_variantSaved) return false;
        if (_variantSelectedIndex < 0) return false;
        return true;
    }

    private bool IsOpen()
    {
        if (!root) return gameObject.activeInHierarchy;
        return root.gameObject.activeInHierarchy && root.alpha > 0.01f;
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

    private Coroutine _deferFocusCo;

    private void DeferFocus(Focus target)
    {
        if (_deferFocusCo != null) StopCoroutine(_deferFocusCo);
        _deferFocusCo = StartCoroutine(CoDeferFocus(target));
    }

    private IEnumerator CoDeferFocus(Focus target)
    {
        // KLUCZ: odczekaj 1 klatkę, żeby Enter z poprzedniego przycisku nie "przeszedł" dalej
        yield return null;

        _focus = target;

        // dla focusów "inputowych" czyścimy selection żeby UI nie trzymał buttona
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        ApplyFocus();
        RefreshAll();

        _deferFocusCo = null;
    }

    private bool HandleUpDownInGroups(int dir)
    {
        // dir: -1 up, +1 down

        // --- ACTIONS: Cancel -> Confirm -> Back ---
        if (_focus == Focus.Actions)
        {
            if (dir < 0)
            {
                if (_actionsIndex > 0)
                {
                    _actionsIndex--;
                    ApplyFocus();
                    RefreshAll();
                    return true;
                }

                // UP z CANCEL -> wróć do VariantSave (żeby było: VariantSave -> Cancel -> Confirm -> Back)
                _focus = Focus.VariantSave;
                _variantButtonsIndex = 0; // default SAVE
                ApplyFocus();
                RefreshAll();
                return true;
            }
            else
            {
                if (_actionsIndex < 2)
                {
                    _actionsIndex++;
                    ApplyFocus();
                    RefreshAll();
                    return true;
                }

                // DOWN z BACK -> wróć na górę (PIN edit)
                _focus = Focus.PinEdit;
                ApplyFocus();
                RefreshAll();
                return true;
            }
        }

        // --- VARIANT SAVE/CANCEL group ---
        if (_focus == Focus.VariantSave)
        {
            if (dir < 0)
            {
                // UP z VariantSave -> VariantPick
                _focus = Focus.VariantPick;
                ApplyFocus();
                RefreshAll();
                return true;
            }
            else
            {
                // DOWN z VariantSave -> Actions i startuj na CANCEL (żeby było logicznie w dół)
                _focus = Focus.Actions;
                _actionsIndex = 0; // Cancel
                ApplyFocus();
                RefreshAll();
                return true;
            }
        }

        // --- PIN SAVE/CANCEL group ---
        if (_focus == Focus.PinSave)
        {
            if (dir < 0)
            {
                // UP z PinSave -> PinEdit (ostatni slot)
                _focus = Focus.PinEdit;
                _activePinIndex = Mathf.Max(0, (_pinDigits?.Length ?? 1) - 1);
                ApplyFocus();
                RefreshAll();
                return true;
            }
            else
            {
                // DOWN z PinSave -> VariantPick (start od 0)
                _focus = Focus.VariantPick;
                _variantHoverIndex = 0;
                ApplyFocus();
                RefreshAll();
                return true;
            }
        }

        return false;
    }

    private bool HandleLeftRightInGroups()
    {
        // --- PIN SAVE/CANCEL ---
        if (_focus == Focus.PinSave)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _pinButtonsIndex = 1 - _pinButtonsIndex;
                ApplyFocus();
                RefreshAll();
                return true;
            }
        }

        // --- VARIANT SAVE/CANCEL ---
        if (_focus == Focus.VariantSave)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _variantButtonsIndex = 1 - _variantButtonsIndex;
                ApplyFocus();
                RefreshAll();
                return true;
            }
        }

        return false;
    }
}
