using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BankAccountCreateUI : MonoBehaviour
{
    private enum CardStep
    {
        None,
        PinEditing,
        PinSaved,
        VariantEditing,
        VariantSaved
    }

    private enum FocusZone
    {
        ConfirmButtons, // ZATWIERDZ / COFNIJ
        CardSlider,     // slider "czy wydać kartę"
        PinEntry,       // wpisywanie PIN
        VariantPicker,  // wybór wariantu
    }
    private FocusZone _focus = FocusZone.ConfirmButtons;

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Panel - Account")]
    [SerializeField] private TMP_Text valueCitizenId;
    [SerializeField] private TMP_Text valueAccountId;
    [SerializeField] private TMP_Text valueFee;
    [SerializeField] private TMP_Text notEnoughCashText; // np. czerwony "Brak gotówki" przy sliderze (opcjonalnie)

    [Header("Account Buttons")]
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button backBtn;

    [Header("Card Toggle (Slider 0/1)")]
    [SerializeField] private Slider cardToggleSlider;

    [Header("Card Toggle - Visual")]
    [SerializeField] private GameObject sliderSelectedDot;

    [Header("Panel - Card (cały dolny panel)")]
    [SerializeField] private GameObject panelCard;

    [Header("Fees")]
    [SerializeField] private int accountFeeCash = 10;
    [SerializeField] private int cardFeeCash = 5;

    [Header("Card - PIN (dynamic)")]
    [SerializeField] private Transform pinContainer;                 // PIN_Container (z HorizontalLayoutGroup)
    [SerializeField] private PinSlotView pinSlotPrefab;              // prefab slota PIN
    [SerializeField] private int pinLength = 4;
    [SerializeField] private Button pinSaveBtn;
    [SerializeField] private Button pinCancelBtn;

    [Header("Card - Variant (dynamic)")]
    [SerializeField] private Transform variantContainer;             // VariantCard_Picker_Container (z HorizontalLayoutGroup)
    [SerializeField] private VariantSlotView variantSlotPrefab;      // prefab slota Variant
    [SerializeField] private Button variantSaveBtn;
    [SerializeField] private Button variantCancelBtn;

    [Header("Top Buttons - Blocked")]
    [SerializeField] private GameObject confirmBlocked;
    [SerializeField] private GameObject cancelBlocked;

    [Header("Top Buttons - Selected")]
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Card Section Selected Dots")]
    [SerializeField] private GameObject pinSelectedDot;
    [SerializeField] private GameObject variantSelectedDot;

    [Header("Card PIN Buttons - Blocked")]
    [SerializeField] private GameObject pinSaveBlocked;
    [SerializeField] private GameObject pinCancelBlocked;

    [Header("Card Variant Buttons - Blocked")]
    [SerializeField] private GameObject variantSaveBlocked;
    [SerializeField] private GameObject variantCancelBlocked;

    [Header("Colors")]
    [SerializeField] private Color slotIdleColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color slotActiveColor = new Color(1f, 0.9f, 0.2f, 1f);    // złoty
    [SerializeField] private Color slotLockedColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
    [SerializeField] private Color slotConfirmedColor = new Color(0.2f, 1f, 0.2f, 1f);

    [Header("Inventory (Card Item)")]
    [SerializeField] private BankCardItemData bankCardItemData;

    [SerializeField] private TMP_Text processingText; // podłącz "Processing"
    [SerializeField] private float verifyPhaseSeconds = 2f;
    [SerializeField] private float createPhaseSeconds = 8f;
    private Coroutine _processingCo;
    private Coroutine _focusCo;
    private int _suppressSubmitFrames = 0;

    // ===== runtime =====
    private Action<int> _onConfirm;
    private string _citizenId;
    private int _proposedAccountId;

    private bool _issueCard;
    private CardStep _cardStep = CardStep.None;

    private List<PinSlotView> _pinSlots = new();
    private int[] _pinDigits;
    private int _activePinIndex;

    private List<VariantSlotView> _variantSlots = new();
    private int _variantHoverIndex;
    private int _variantSelectedIndex = -1;

    private bool _pinSaved;
    private bool _variantSaved;

    private bool _pinAwaitingConfirm;      // po 1. Enter gdy PIN kompletny


    private void Awake()
    {
        Show(false);

        if (confirmBtn) confirmBtn.onClick.AddListener(ConfirmFinal);
        if (cancelBtn) cancelBtn.onClick.AddListener(CancelFlow);
        if (backBtn) backBtn.onClick.AddListener(OnBackClicked);

        if (pinSaveBtn) pinSaveBtn.onClick.AddListener(SavePin);
        if (pinCancelBtn) pinCancelBtn.onClick.AddListener(CancelPin);

        if (variantSaveBtn) variantSaveBtn.onClick.AddListener(SaveVariant);
        if (variantCancelBtn) variantCancelBtn.onClick.AddListener(CancelVariant);

        if (cardToggleSlider)
        {
            // slider jako "toggle" 0/1 (i żeby strzałki go nie przesuwały)
            cardToggleSlider.minValue = 0f;
            cardToggleSlider.maxValue = 1f;
            cardToggleSlider.wholeNumbers = true;   // ✅ to powoduje natychmiastowe 0/1
            cardToggleSlider.value = 0f;

            cardToggleSlider.onValueChanged.AddListener(OnCardToggleSliderChanged);

            var nav = new Navigation { mode = Navigation.Mode.None };
            cardToggleSlider.navigation = nav;
        }

        if (cardToggleSlider && cardToggleSlider.GetComponent<BlockSliderDrag>() == null)
            cardToggleSlider.gameObject.AddComponent<BlockSliderDrag>();

        // klik na slider -> toggle 0/1
        var click = cardToggleSlider.GetComponent<PointerClickToggleSlider>();
        if (click == null) click = cardToggleSlider.gameObject.AddComponent<PointerClickToggleSlider>();
        click.Init(this);

        if (notEnoughCashText) notEnoughCashText.gameObject.SetActive(false);

        // button visual + nav
        EnsureButtonTint(pinSaveBtn);
        EnsureButtonTint(pinCancelBtn);
        EnsureButtonTint(variantSaveBtn);
        EnsureButtonTint(variantCancelBtn);
        EnsureButtonTint(confirmBtn);
        EnsureButtonTint(cancelBtn);

        // strzałkami lewo/prawo możesz przełączać SAVE <-> CANCEL
        SetupHorizontalNav(pinSaveBtn, pinCancelBtn);
        SetupHorizontalNav(variantSaveBtn, variantCancelBtn);
        SetupHorizontalNav3(confirmBtn, cancelBtn, backBtn);

    }

    private void Update()
    {
        if (!IsOpen()) return;

        if (_suppressSubmitFrames > 0)
        {
            _suppressSubmitFrames--;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackClicked();
            return;
        }

        if (HandleEnterOnButtons())
            return;

        if (HandleTopButtonsHorizontalNav())
            return;

        bool sliderSelected = cardToggleSlider &&
                              EventSystem.current &&
                              EventSystem.current.currentSelectedGameObject == cardToggleSlider.gameObject;

        if (sliderSelected)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryToggleCardIssue(!_issueCard);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                TryToggleCardIssue(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                TryToggleCardIssue(true);
                return;
            }
        }

        if (!_issueCard) return;

        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        // ===== PIN SAVE / CANCEL =====
        if (selected == pinSaveBtn?.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) && pinCancelBtn && pinCancelBtn.interactable)
            {
                EventSystem.current.SetSelectedGameObject(pinCancelBtn.gameObject);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _pinAwaitingConfirm = false;
                _pinSaved = false;
                _focus = FocusZone.PinEntry;
                _cardStep = CardStep.PinEditing;
                EventSystem.current?.SetSelectedGameObject(null);
                RefreshAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _focus = FocusZone.ConfirmButtons;
                ApplyFocus();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _focus = FocusZone.VariantPicker;
                ApplyFocus();
                return;
            }

            return;
        }

        if (selected == pinCancelBtn?.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) && pinSaveBtn && pinSaveBtn.interactable)
            {
                EventSystem.current.SetSelectedGameObject(pinSaveBtn.gameObject);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _focus = FocusZone.PinEntry;
                _cardStep = CardStep.PinEditing;
                EventSystem.current?.SetSelectedGameObject(null);
                RefreshAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _focus = FocusZone.ConfirmButtons;
                ApplyFocus();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _focus = FocusZone.VariantPicker;
                ApplyFocus();
                return;
            }

            return;
        }

        // ===== VARIANT SAVE / CANCEL =====
        if (selected == variantSaveBtn?.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) && variantCancelBtn && variantCancelBtn.interactable)
            {
                EventSystem.current.SetSelectedGameObject(variantCancelBtn.gameObject);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _variantSaved = false;
                _focus = FocusZone.VariantPicker;
                _cardStep = CardStep.VariantEditing;
                EventSystem.current?.SetSelectedGameObject(null);
                RefreshAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _focus = FocusZone.PinEntry;
                ApplyFocus();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _focus = FocusZone.ConfirmButtons;
                ApplyFocus();
                return;
            }

            return;
        }

        if (selected == variantCancelBtn?.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) && variantSaveBtn && variantSaveBtn.interactable)
            {
                EventSystem.current.SetSelectedGameObject(variantSaveBtn.gameObject);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _focus = FocusZone.VariantPicker;
                _cardStep = CardStep.VariantEditing;
                EventSystem.current?.SetSelectedGameObject(null);
                RefreshAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _focus = FocusZone.PinEntry;
                ApplyFocus();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _focus = FocusZone.ConfirmButtons;
                ApplyFocus();
                return;
            }

            return;
        }

        // ===== Slider direct =====
        if (_focus == FocusZone.CardSlider && cardToggleSlider)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                TryToggleCardIssue(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                TryToggleCardIssue(true);
                return;
            }
        }

        // ===== PIN editing =====
        if (_focus == FocusZone.PinEntry && _cardStep == CardStep.PinEditing)
        {
            HandlePinEditingInput();
            return;
        }

        // ===== Variant editing =====
        if (_focus == FocusZone.VariantPicker && _cardStep == CardStep.VariantEditing)
        {
            HandleVariantEditingInput();
            return;
        }
    }

    // === Public API ===

    public void Open(string citizenId, Action<int> onConfirm)
    {
        _finalizing = false;
        _suppressSubmitFrames = 0;

        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }

        if (processingText)
        {
            processingText.text = "";
            processingText.gameObject.SetActive(false);
        }

        _focus = FocusZone.ConfirmButtons;
        _citizenId = citizenId;
        _onConfirm = onConfirm;

        _proposedAccountId = BankSystem.Instance != null
            ? BankSystem.Instance.GenerateUniqueAccountIdInRange(101, 999)
            : UnityEngine.Random.Range(101, 1000);

        // UI - dane konta
        if (valueCitizenId) valueCitizenId.text = string.IsNullOrWhiteSpace(_citizenId) ? "---" : _citizenId;
        if (valueAccountId) valueAccountId.text = _proposedAccountId.ToString();
        if (processingText) processingText.gameObject.SetActive(false);

        BuildPinSlots(Mathf.Max(1, pinLength));

        // warianty: muszą się zgadzać z liczbą definicji
        // warianty: źródło prawdy = BankSystem

        int count = 6; // fallback dev
        if (BankSystem.Instance != null)
            count = Mathf.Max(1, BankSystem.Instance.VariantCount);

        BuildVariantSlots(count);

        for (int i = 0; i < _variantSlots.Count; i++)
        {
            var col = (BankSystem.Instance != null)
                ? BankSystem.Instance.GetVariantColor(i)
                : Color.white;

            _variantSlots[i].SetPreviewColor(col);
        }

        ResetDraft();
        Show(true);

        // focus na Confirm (Enter/Space działa na button)
        if (EventSystem.current && confirmBtn)
            EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);

        RefreshAll();
    }
    private void RefreshFeeText()
    {
        if (valueFee)
            valueFee.text = $"{TotalFee()}$";
    }
    public void Close()
    {
        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }

        _finalizing = false;
        _suppressSubmitFrames = 0;

        if (processingText)
        {
            processingText.text = "";
            processingText.gameObject.SetActive(false);
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (confirmBtn) confirmBtn.interactable = true;
        if (cancelBtn) cancelBtn.interactable = true;
        if (cardToggleSlider) cardToggleSlider.interactable = true;
        if (pinSaveBtn) pinSaveBtn.interactable = true;
        if (pinCancelBtn) pinCancelBtn.interactable = true;
        if (variantSaveBtn) variantSaveBtn.interactable = true;
        if (variantCancelBtn) variantCancelBtn.interactable = true;

        Show(false);
        _onConfirm = null;
        _citizenId = null;
    }

    // === Money helpers ===

    private PlayerStats GetPS() => FindFirstObjectByType<PlayerStats>();

    private int TotalFee()
    {
        return accountFeeCash + (_issueCard ? cardFeeCash : 0);
    }

    private bool HasEnoughMoneyForCardOnly()
    {
        var ps = GetPS();
        if (!ps) return true;
        // tu tylko sprawdzenie czy stać Cię na DODATEK do karty (bo konto i tak kupujesz)
        return ps.money >= (accountFeeCash + cardFeeCash);
    }

    // === Slider / Toggle ===

    private void OnCardToggleSliderChanged(float v)
    {
        bool on = v >= 1f;

        if (on && !HasEnoughMoneyForCardOnly())
        {
            ForceCardToggle(false);
            ShowNoCashHint(true);
            return;
        }

        ShowNoCashHint(false);
        SetIssueCard(on, setSlider: false);
    }

    private void TryToggleCardIssue(bool on)
    {
        if (on && !HasEnoughMoneyForCardOnly())
        {
            ForceCardToggle(false);
            ShowNoCashHint(true);
            return;
        }

        ShowNoCashHint(false);
        SetIssueCard(on, setSlider: true);
    }

    private void ForceCardToggle(bool on)
    {
        if (!cardToggleSlider) return;
        cardToggleSlider.SetValueWithoutNotify(on ? 1f : 0f);
        SetIssueCard(on, setSlider: false);
    }

    private void ShowNoCashHint(bool show)
    {
        if (!notEnoughCashText) return;

        notEnoughCashText.gameObject.SetActive(show);

        if (show)
            notEnoughCashText.text = "NOT ENOUGH MONEY";
    }

    private void SetIssueCard(bool on, bool setSlider)
    {
        _issueCard = on;

        if (setSlider && cardToggleSlider)
            cardToggleSlider.SetValueWithoutNotify(on ? 1f : 0f);

        if (panelCard)
            panelCard.SetActive(on);

        if (!on)
        {
            _cardStep = CardStep.None;
            _pinSaved = false;
            _variantSaved = false;
            _pinAwaitingConfirm = false;
            _variantSelectedIndex = -1;

            ResetPinDigitsOnly();
            ResetVariantOnly();
            ResetCardDraftState();

            // zostajemy na sliderze
            _focus = FocusZone.CardSlider;
            ApplyFocus();
            return;
        }

        _cardStep = CardStep.None;
        _activePinIndex = 0;

        _pinSaved = false;
        _variantSaved = false;
        _variantSelectedIndex = -1;
        _pinAwaitingConfirm = false;

        ResetPinDigitsOnly();
        ResetVariantOnly();
        ResetCardDraftState();

        // po włączeniu karty też zostajemy na sliderze
        _focus = FocusZone.CardSlider;
        ApplyFocus();
    }

    // === Build dynamic UI ===

    private void BuildPinSlots(int count)
    {
        if (!pinContainer || !pinSlotPrefab) return;

        // clear
        for (int i = pinContainer.childCount - 1; i >= 0; i--)
            Destroy(pinContainer.GetChild(i).gameObject);

        _pinSlots.Clear();
        _pinDigits = new int[count];

        for (int i = 0; i < count; i++)
        {
            _pinDigits[i] = -1;
            var slot = Instantiate(pinSlotPrefab, pinContainer);
            slot.SetDigit(-1);
            slot.SetActive(false, slotIdleColor, slotActiveColor);
            slot.SetArrow(false);
            _pinSlots.Add(slot);
        }

        _activePinIndex = 0;
    }

    private void BuildVariantSlots(int count)
    {
        if (!variantContainer || !variantSlotPrefab) return;

        // clear
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

    // === Draft reset ===

    private void ResetDraft()
    {
        // ustaw slider bez wywoływania logiki
        if (cardToggleSlider) cardToggleSlider.SetValueWithoutNotify(0f);

        _issueCard = false;
        if (panelCard) panelCard.SetActive(false);

        _cardStep = CardStep.None;
        _pinSaved = false;
        _variantSaved = false;
        _pinAwaitingConfirm = false;

        ResetPinDigitsOnly();
        ResetVariantOnly();
        ResetCardDraftState();

        _focus = FocusZone.ConfirmButtons;
        ApplyFocus();
    }


    private void ResetPinDigitsOnly()
    {
        if (_pinDigits == null) return;
        for (int i = 0; i < _pinDigits.Length; i++) _pinDigits[i] = -1;
        _activePinIndex = 0;

        for (int i = 0; i < _pinSlots.Count; i++)
        {
            _pinSlots[i].SetDigit(-1);
        }
    }

    private void ResetVariantOnly()
    {
        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;
    }

    // === PIN input ===

    private void HandlePinEditingInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _activePinIndex = Mathf.Max(0, _activePinIndex - 1);
            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (IsPinComplete() && _activePinIndex == _pinDigits.Length - 1)
            {
                _pinAwaitingConfirm = true;

                if (EventSystem.current && pinSaveBtn && pinSaveBtn.interactable)
                    EventSystem.current.SetSelectedGameObject(pinSaveBtn.gameObject);

                RefreshAll();
                return;
            }

            _activePinIndex = Mathf.Min(_pinDigits.Length - 1, _activePinIndex + 1);
            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _focus = FocusZone.ConfirmButtons;
            ApplyFocus();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _focus = FocusZone.VariantPicker;
            ApplyFocus();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            bool allEmpty = true;
            for (int i = 0; i < _pinDigits.Length; i++)
            {
                if (_pinDigits[i] != -1)
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty && _activePinIndex == 0)
            {
                _focus = FocusZone.CardSlider;
                ApplyFocus();
                return;
            }

            if (_pinDigits[_activePinIndex] != -1)
            {
                _pinDigits[_activePinIndex] = -1;
            }
            else
            {
                _activePinIndex = Mathf.Max(0, _activePinIndex - 1);
                _pinDigits[_activePinIndex] = -1;
            }

            _pinAwaitingConfirm = false;
            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            ResetPinDigitsOnly();
            _pinAwaitingConfirm = false;
            RefreshAll();
            return;
        }

        int digit = GetDigitDown();
        if (digit >= 0)
        {
            _pinDigits[_activePinIndex] = digit;

            if (_activePinIndex < _pinDigits.Length - 1)
                _activePinIndex++;

            if (IsPinComplete())
            {
                _pinAwaitingConfirm = true;

                if (EventSystem.current && pinSaveBtn)
                    EventSystem.current.SetSelectedGameObject(pinSaveBtn.gameObject);
            }

            RefreshAll();
        }
    }

    private void SavePin()
    {
        if (!_issueCard) return;
        if (!IsPinComplete()) return;

        _pinSaved = true;
        _pinAwaitingConfirm = false;

        _focus = FocusZone.VariantPicker;
        _cardStep = CardStep.VariantEditing;

        if (_variantSelectedIndex < 0)
            _variantHoverIndex = 0;

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshAll();
    }

    private void CancelPin()
    {
        if (!_issueCard) return;

        _pinSaved = false;
        _pinAwaitingConfirm = false;
        _cardStep = CardStep.PinEditing;
        _focus = FocusZone.PinEntry;

        ResetPinDigitsOnly();

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshAll();
    }

    private bool IsPinComplete()
    {
        if (_pinDigits == null || _pinDigits.Length == 0) return false;
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
        if (Input.GetKeyDown(KeyCode.Alpha0)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;

        if (Input.GetKeyDown(KeyCode.Keypad0)) return 0;
        if (Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Keypad4)) return 4;
        if (Input.GetKeyDown(KeyCode.Keypad5)) return 5;
        if (Input.GetKeyDown(KeyCode.Keypad6)) return 6;
        if (Input.GetKeyDown(KeyCode.Keypad7)) return 7;
        if (Input.GetKeyDown(KeyCode.Keypad8)) return 8;
        if (Input.GetKeyDown(KeyCode.Keypad9)) return 9;

        return -1;
    }

    // === Variant input ===

    private void HandleVariantEditingInput()
    {
        if (_variantSlots == null || _variantSlots.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _variantHoverIndex = (_variantHoverIndex - 1 + _variantSlots.Count) % _variantSlots.Count;
            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            _variantHoverIndex = (_variantHoverIndex + 1) % _variantSlots.Count;
            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _focus = FocusZone.PinEntry;
            ApplyFocus();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _focus = FocusZone.ConfirmButtons;
            ApplyFocus();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            _variantSelectedIndex = _variantHoverIndex;
        

            if (EventSystem.current && variantSaveBtn)
                EventSystem.current.SetSelectedGameObject(variantSaveBtn.gameObject);

            RefreshAll();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_variantSelectedIndex >= 0)
            {
                _variantSelectedIndex = -1;
                RefreshAll();
                return;
            }

            _focus = FocusZone.PinEntry;
            ApplyFocus();
            return;
        }
    }
    private void SaveVariant()
    {
        if (!_issueCard) return;
        if (_variantSelectedIndex < 0) return;

        _variantSaved = true;
        _cardStep = CardStep.VariantSaved;
        _focus = FocusZone.ConfirmButtons;
        _suppressSubmitFrames = 2;

        if (EventSystem.current && confirmBtn)
            EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);

        RefreshAll();
    }

    private void CancelVariant()
    {
        if (!_issueCard) return;

        _variantSaved = false;
        _variantSelectedIndex = -1;
        _variantHoverIndex = 0;

        _focus = FocusZone.VariantPicker;
        _cardStep = CardStep.VariantEditing;

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshAll();
    }


    // === FINAL Confirm ===

    private bool _finalizing;

    private void ConfirmFinal()
    {
        if (_finalizing) return;
        if (!CanConfirm()) return;

        StartCoroutine(CoFinalizeCreate());
    }

    private IEnumerator CoFinalizeCreate()
    {
        _finalizing = true;

        // blokuj klikanie i input
        if (confirmBtn) confirmBtn.interactable = false;
        if (cancelBtn) cancelBtn.interactable = false;
        if (cardToggleSlider) cardToggleSlider.interactable = false;
        if (pinSaveBtn) pinSaveBtn.interactable = false;
        if (pinCancelBtn) pinCancelBtn.interactable = false;
        if (variantSaveBtn) variantSaveBtn.interactable = false;
        if (variantCancelBtn) variantCancelBtn.interactable = false;

        // sprawdź i pobierz gotówkę TUTAJ (atomowo)
        int fee = TotalFee();
        var ps = GetPS();
        if (ps != null)
        {
            if (ps.money < fee || !ps.SpendMoney(fee))
            {
                Debug.LogWarning($"[BANK CREATE UI] Brak gotówki: masz {ps.money}$, potrzebujesz {fee}$.");
                ShowNoCashHint(true);

                // odblokuj tylko to co ma sens
                if (cancelBtn) cancelBtn.interactable = true;
                if (cardToggleSlider) cardToggleSlider.interactable = true;

                _finalizing = false;
                RefreshAll();
                yield break;
            }
        }

        // komunikat + delay
        if (processingText)
        {
            processingText.gameObject.SetActive(true);
            if (_processingCo != null) StopCoroutine(_processingCo);
            _processingCo = StartCoroutine(CoAnimateProcessing());
        }

        yield return new WaitForSecondsRealtime(verifyPhaseSeconds + createPhaseSeconds);

        var bank = BankSystem.Instance;
        if (bank == null || string.IsNullOrWhiteSpace(_citizenId))
        {
            Debug.LogWarning("[BANK CREATE UI] Brak BankSystem lub citizenId.");
            Close();
            yield break;
        }

        // create account
        bank.CreateAccountForCitizen(_citizenId, initialBalance: 0, forcedAccountId: _proposedAccountId);
        Debug.Log($"[BANK] Utworzono konto: citizenId={_citizenId}, accountId={_proposedAccountId}, fee={accountFeeCash}$");

        // create card draft log (ID dopiero teraz)
        // po bank.CreateAccountForCitizen(...)

        if (_issueCard)
        {
            if (bankCardItemData == null)
            {
                Debug.LogError("[BANK CREATE UI] bankCardItemData nie jest podpięte w Inspectorze!");
            }
            else
            {
                // PIN jako int 0..9999
                int pinInt = 0;
                int.TryParse(GetPinString(), out pinInt);

                int variant = Mathf.Max(0, _variantSelectedIndex);

                // status Pending + aktywacja po 24h (na razie unix-sekundy; jak masz GameTime – podepniesz później)
                var rec = bank.IssueCard(
                    accountId: _proposedAccountId,
                    pin: pinInt,
                    colorVariant: variant,
                    initialStatus: BankCardStatus.Pending
                );

                bank.RegisterPlayerOwnedCard(_citizenId, rec.cardId);


                // jeśli chcesz “pending 24h”, ustaw activateAt w przyszłość
                // (zależnie od tego czy BankCardRecord jest klasą – wygląda, że jest)

                // Zbuduj meta do instancji inventory
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

                // Dodaj do inventory
                var inv = InventoryUI.Instance;
                if (inv == null)
                {
                    inv.GiveOrDrop(inst);
                    Debug.LogError("[BANK CREATE UI] InventoryUI.Instance == null (nie ma inventory w scenie?)");
                }
                else
                {
                    bool added = inv.TryAddItem(inst);
                    Debug.Log($"[BANK] Wydano kartę: cardId={rec.cardId}, acc={rec.accountId}, pin={rec.pin:0000}, var={rec.colorVariant}, status={rec.status}, addedToInv={added}");

                    if (!added)
                    {
                        Debug.LogWarning("[BANK] Brak miejsca w Inventory – karta NIE została dodana. (Możesz wtedy zespawnować ją na ziemi przy graczu.)");
                        // opcjonalnie fallback: inv.Drop/SpawnBankCard(inst) – ale SpawnBankCard jest private,
                        // więc najlepiej zrobić publiczny helper w InventoryUI: GiveOrDrop(inst).
                    }
                }
            }
        }
        var cb = _onConfirm;
        int createdAccountId = _proposedAccountId;

        Close();
        cb?.Invoke(createdAccountId);
    }


    private bool CanConfirm()
    {
        if (string.IsNullOrWhiteSpace(_citizenId)) return false;
        if (_proposedAccountId <= 0) return false;

        // warunki karty
        if (_issueCard)
        {
            if (!_pinSaved) return false;
            if (!_variantSaved) return false;
            if (_variantSelectedIndex < 0) return false;
        }

        // nie blokuj tworzenia konta jeśli brak PS w scenie (dev),
        // ale jak jest PS - sprawdzaj
        var ps = GetPS();
        if (ps != null && ps.money < TotalFee())
            return false;

        return true;
    }

    private bool HasAnyCardDraftData()
    {
        if (!_issueCard) return false;

        if (_pinSaved) return true;
        if (_variantSaved) return true;
        if (_variantSelectedIndex >= 0) return true;

        if (_pinDigits != null)
        {
            for (int i = 0; i < _pinDigits.Length; i++)
                if (_pinDigits[i] >= 0)
                    return true;
        }

        return false;
    }

    private bool CanCancelCardDraft()
    {
        if (!_issueCard) return false;
        return HasAnyCardDraftData();
    }
    // === UI refresh ===

    private void RefreshAll()
    {
        if (panelCard) panelCard.SetActive(_issueCard);

        RefreshPinUI();
        RefreshVariantUI();
        RefreshFeeText();

        bool canConfirm = CanConfirm();
        bool canCancel = CanCancelCardDraft();

        if (confirmBtn) confirmBtn.interactable = canConfirm;
        if (cancelBtn) cancelBtn.interactable = canCancel;

        if (confirmBlocked) confirmBlocked.SetActive(!canConfirm);
        if (cancelBlocked) cancelBlocked.SetActive(!canCancel);

        var es = EventSystem.current;
        var selected = es ? es.currentSelectedGameObject : null;

        // ===== TOP BUTTONS SELECTED =====
        bool topZoneActive = (_focus == FocusZone.ConfirmButtons);

        bool onTopConfirm = selected == confirmBtn?.gameObject;
        bool onTopCancel = selected == cancelBtn?.gameObject;
        bool onTopBack = selected == backBtn?.gameObject;

        // fallback tylko jeśli jesteśmy w top zone i nic nie jest selected
        if (_focus == FocusZone.ConfirmButtons && !onTopConfirm && !onTopCancel && !onTopBack)
        {
            if (confirmBtn && confirmBtn.interactable)
                onTopConfirm = true;
            else if (cancelBtn && cancelBtn.interactable)
                onTopCancel = true;
            else if (backBtn)
                onTopBack = true;
        }

        if (selectedConfirm) selectedConfirm.SetActive(onTopConfirm);
        if (selectedCancel) selectedCancel.SetActive(onTopCancel);
        if (selectedBack) selectedBack.SetActive(onTopBack);

        // ===== SLIDER SELECTED =====
        bool onSlider =
            cardToggleSlider &&
            (
                selected == cardToggleSlider.gameObject ||
                _focus == FocusZone.CardSlider
            );

        if (sliderSelectedDot) sliderSelectedDot.SetActive(onSlider);

        // ===== PIN SECTION SELECTED =====
        bool onPinSection =
            _issueCard &&
            (
                _focus == FocusZone.PinEntry ||
                selected == pinSaveBtn?.gameObject ||
                selected == pinCancelBtn?.gameObject
            );

        if (pinSelectedDot) pinSelectedDot.SetActive(onPinSection);

        // ===== VARIANT SECTION SELECTED =====
        bool onVariantSection =
            _issueCard &&
            (
                _focus == FocusZone.VariantPicker ||
                selected == variantSaveBtn?.gameObject ||
                selected == variantCancelBtn?.gameObject
            );

        if (variantSelectedDot) variantSelectedDot.SetActive(onVariantSection);

        // ===== BLOCKED OVERLAYS - PIN =====
        bool canSavePin = pinSaveBtn && pinSaveBtn.interactable;
        bool canCancelPin = pinCancelBtn && pinCancelBtn.interactable;

        if (pinSaveBlocked) pinSaveBlocked.SetActive(!canSavePin);
        if (pinCancelBlocked) pinCancelBlocked.SetActive(!canCancelPin);

        // ===== BLOCKED OVERLAYS - VARIANT =====
        bool canSaveVariant = variantSaveBtn && variantSaveBtn.interactable;
        bool canCancelVariant = variantCancelBtn && variantCancelBtn.interactable;

        if (variantSaveBlocked) variantSaveBlocked.SetActive(!canSaveVariant);
        if (variantCancelBlocked) variantCancelBlocked.SetActive(!canCancelVariant);
    }

    private void ResetCardPanelDraft()
    {
        _cardStep = CardStep.None;
        _pinSaved = false;
        _variantSaved = false;
        _pinAwaitingConfirm = false;

        ResetPinDigitsOnly();
        ResetVariantOnly();
        ResetCardDraftState();

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        _focus = FocusZone.CardSlider;
        ApplyFocus();
    }

    private void ResetCardDraftState()
    {
        _cardStep = CardStep.None;
        _pinSaved = false;
        _variantSaved = false;
        _pinAwaitingConfirm = false;
        _variantSelectedIndex = -1;
        _activePinIndex = 0;

        ResetPinDigitsOnly();
        ResetVariantOnly();
    }
    private void RefreshPinUI()
    {
        if (_pinSlots == null) return;

        bool editing = _issueCard && _cardStep == CardStep.PinEditing;
        bool locked = _issueCard && (_pinSaved || _cardStep == CardStep.VariantEditing || _cardStep == CardStep.VariantSaved);

        for (int i = 0; i < _pinSlots.Count; i++)
        {
            var s = _pinSlots[i];
            if (!s) continue;

            s.SetDigit(_pinDigits != null && i < _pinDigits.Length ? _pinDigits[i] : -1);

            if (!_issueCard)
            {
                s.SetArrow(false);
                s.SetActive(false, slotIdleColor, slotActiveColor);
                continue;
            }

            if (locked)
            {
                s.SetArrow(false);
                s.SetBorderColor(_pinSaved ? slotConfirmedColor : slotLockedColor);
                continue;
            }

            bool active = editing && i == _activePinIndex;
            s.SetBorderColor(active ? slotActiveColor : slotIdleColor);
            s.SetArrow(active);

            if (editing && _pinAwaitingConfirm)
                s.SetArrow(false);
        }

        bool canSavePin = _issueCard && IsPinComplete() && !_pinSaved;
        bool canCancelPin = _issueCard && HasAnyPinDraftData();

        if (pinSaveBtn) pinSaveBtn.interactable = canSavePin;
        if (pinCancelBtn) pinCancelBtn.interactable = canCancelPin;

        if (pinSaveBlocked) pinSaveBlocked.SetActive(!canSavePin);
        if (pinCancelBlocked) pinCancelBlocked.SetActive(!canCancelPin);
    }
    private bool HasAnyPinDraftData()
    {
        if (_pinDigits == null) return false;

        for (int i = 0; i < _pinDigits.Length; i++)
        {
            if (_pinDigits[i] >= 0)
                return true;
        }

        return _pinSaved;
    }

    private bool HasAnyVariantDraftData()
    {
        return _variantSelectedIndex >= 0 || _variantSaved;
    }

    private void RefreshVariantUI()
    {
        if (_variantSlots == null) return;

        bool editing = _issueCard && _cardStep == CardStep.VariantEditing;
        bool locked = _issueCard && _variantSaved;

        for (int i = 0; i < _variantSlots.Count; i++)
        {
            var v = _variantSlots[i];
            if (!v) continue;

            if (!_issueCard)
            {
                v.SetHover(false);
                v.SetSelected(false);
                SetVariantBorderVisual(v, i, hover: false, selected: false, locked: false);
                continue;
            }

            if (locked)
            {
                bool selectedLocked = (i == _variantSelectedIndex);
                v.SetHover(false);
                v.SetSelected(selectedLocked);
                SetVariantBorderVisual(v, i, hover: false, selected: selectedLocked, locked: true);
                continue;
            }

            bool hover = editing && i == _variantHoverIndex;
            v.SetHover(hover);
            v.SetSelected(false);
            SetVariantBorderVisual(v, i, hover: hover, selected: false, locked: false);
        }

        bool canSaveVariant = _issueCard && _variantSelectedIndex >= 0 && !_variantSaved;
        bool canCancelVariant = _issueCard && HasAnyVariantDraftData();

        if (variantSaveBtn) variantSaveBtn.interactable = canSaveVariant;
        if (variantCancelBtn) variantCancelBtn.interactable = canCancelVariant;

        if (variantSaveBlocked) variantSaveBlocked.SetActive(!canSaveVariant);
        if (variantCancelBlocked) variantCancelBlocked.SetActive(!canCancelVariant);
    }


    private void SetVariantBorderVisual(VariantSlotView v, int index, bool hover, bool selected, bool locked)
    {
        if (!v || !v.activeBorder) return;

        var img = v.activeBorder.GetComponent<Image>();

        if (locked)
        {
            // po zapisaniu: pokazuj border na WYBRANYM (selected)
            v.activeBorder.SetActive(selected);
            if (img) img.color = _variantSaved ? slotConfirmedColor : slotLockedColor;
            return;
        }

        // w edycji: border podąża za strzałką
        v.activeBorder.SetActive(hover);
        if (img && hover) img.color = slotActiveColor;
    }

    private bool IsOpen()
    {
        if (!root) return gameObject.activeInHierarchy;
        return root.gameObject.activeInHierarchy && root.alpha > 0.01f;
    }

    private void Show(bool v)
    {
        if (!root) { gameObject.SetActive(v); return; }
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
        root.gameObject.SetActive(v);
    }

    private void SetupHorizontalNav(Selectable left, Selectable right)
    {
        if (!left || !right) return;

        var nL = left.navigation;
        nL.mode = Navigation.Mode.Explicit;
        nL.selectOnRight = right;
        left.navigation = nL;

        var nR = right.navigation;
        nR.mode = Navigation.Mode.Explicit;
        nR.selectOnLeft = left;
        right.navigation = nR;
    }

    private void EnsureButtonTint(Button b)
    {
        if (!b) return;

        // Jeśli masz w inspectorze "Transition: None", to nie będzie żadnych kolorów.
        // Wymuszamy ColorTint, żeby było widać selected/pressed.
        b.transition = Selectable.Transition.ColorTint;

        var cb = b.colors;
        cb.fadeDuration = 0.05f;
        b.colors = cb;
    }

    private void ApplyFocus()
    {
        if (!EventSystem.current) return;

        switch (_focus)
        {
            case FocusZone.ConfirmButtons:
                {
                    var current = EventSystem.current.currentSelectedGameObject;

                    bool validCurrent =
                        current == confirmBtn?.gameObject ||
                        current == cancelBtn?.gameObject ||
                        current == backBtn?.gameObject;

                    if (!validCurrent)
                    {
                        if (confirmBtn && confirmBtn.interactable)
                            EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);
                        else if (cancelBtn && cancelBtn.interactable)
                            EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
                        else if (backBtn)
                            EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                        else
                            EventSystem.current.SetSelectedGameObject(null);
                    }
                    break;
                }

            case FocusZone.PinEntry:
                if (_pinSaved)
                {
                    _cardStep = CardStep.PinSaved;
                    if (pinCancelBtn && pinCancelBtn.interactable)
                        EventSystem.current.SetSelectedGameObject(pinCancelBtn.gameObject);
                    else if (pinSaveBtn && pinSaveBtn.interactable)
                        EventSystem.current.SetSelectedGameObject(pinSaveBtn.gameObject);
                    else
                        EventSystem.current.SetSelectedGameObject(null);
                }
                else
                {
                    _cardStep = CardStep.PinEditing;
                    EventSystem.current.SetSelectedGameObject(null);
                }
                break;

            case FocusZone.VariantPicker:
                if (_variantSaved)
                {
                    _cardStep = CardStep.VariantSaved;

                    if (variantCancelBtn && variantCancelBtn.interactable)
                        EventSystem.current.SetSelectedGameObject(variantCancelBtn.gameObject);
                    else if (variantSaveBtn && variantSaveBtn.interactable)
                        EventSystem.current.SetSelectedGameObject(variantSaveBtn.gameObject);
                    else
                        EventSystem.current.SetSelectedGameObject(null);
                }
                else
                {
                    _cardStep = CardStep.VariantEditing;
                    EventSystem.current.SetSelectedGameObject(null);
                }
                break;
        }

        RefreshAll();
    }

    public void ExternalToggleCardIssue(bool on)
    {
        TryToggleCardIssue(on);
    }

    private void CancelFlow()
    {
        if (_issueCard && HasAnyCardDraftData())
        {
            ResetCardPanelDraft();
            RefreshAll();
            return;
        }

        var cb = _onConfirm;
        Close();
        cb?.Invoke(-1);
    }

    private void OnBackClicked()
    {
        var cb = _onConfirm;
        Close();
        cb?.Invoke(-1);
    }

    private IEnumerator CoAnimateProcessing()
    {
        float step = 0.25f;

        float t = 0f;
        while (t < verifyPhaseSeconds)
        {
            if (processingText) processingText.text = "Weryfikacja danych" + Dots(t);
            yield return new WaitForSecondsRealtime(step);
            t += step;
        }

        t = 0f;
        while (t < createPhaseSeconds)
        {
            if (processingText) processingText.text = "Tworzenie konta" + Dots(t);
            yield return new WaitForSecondsRealtime(step);
            t += step;
        }
    }

    private string Dots(float t)
    {
        int k = ((int)(t / 0.25f) % 3) + 1; // 1..3
        return k == 1 ? "." : (k == 2 ? ".." : "...");
    }

    private bool HandleEnterOnButtons()
    {
        if (!EventSystem.current) return false;

        var sel = EventSystem.current.currentSelectedGameObject;
        if (sel == null) return false;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!enter) return false;

        // PIN buttons
        if (pinSaveBtn && sel == pinSaveBtn.gameObject) { pinSaveBtn.onClick.Invoke(); return true; }
        if (pinCancelBtn && sel == pinCancelBtn.gameObject) { pinCancelBtn.onClick.Invoke(); return true; }

        // VARIANT buttons
        if (variantSaveBtn && sel == variantSaveBtn.gameObject) { variantSaveBtn.onClick.Invoke(); return true; }
        if (variantCancelBtn && sel == variantCancelBtn.gameObject) { variantCancelBtn.onClick.Invoke(); return true; }

        // TOP buttons
        if (confirmBtn && sel == confirmBtn.gameObject) { confirmBtn.onClick.Invoke(); return true; }
        if (cancelBtn && sel == cancelBtn.gameObject) { cancelBtn.onClick.Invoke(); return true; }
        if (backBtn && sel == backBtn.gameObject) { backBtn.onClick.Invoke(); return true; }

        return false;
    }

    private void SetupHorizontalNav3(Selectable left, Selectable middle, Selectable right)
    {
        if (!left || !middle || !right) return;

        var nL = left.navigation;
        nL.mode = Navigation.Mode.Explicit;
        nL.selectOnRight = middle;
        left.navigation = nL;

        var nM = middle.navigation;
        nM.mode = Navigation.Mode.Explicit;
        nM.selectOnLeft = left;
        nM.selectOnRight = right;
        middle.navigation = nM;

        var nR = right.navigation;
        nR.mode = Navigation.Mode.Explicit;
        nR.selectOnLeft = middle;
        right.navigation = nR;
    }

    private bool HandleTopButtonsHorizontalNav()
    {
        if (_focus != FocusZone.ConfirmButtons && _focus != FocusZone.CardSlider)
            return false;

        if (EventSystem.current == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;

        // ===== SLIDER =====
        if (_focus == FocusZone.CardSlider)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _focus = FocusZone.ConfirmButtons;

                if (confirmBtn && confirmBtn.interactable)
                    EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);
                else if (cancelBtn && cancelBtn.interactable)
                    EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
                else if (backBtn)
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);

                RefreshAll();
                return true;
            }

            return false;
        }

        // ===== CONFIRM / CANCEL / BACK =====
        if (selected == null)
        {
            if (confirmBtn && confirmBtn.interactable)
                EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);
            else if (cancelBtn && cancelBtn.interactable)
                EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
            else if (backBtn)
                EventSystem.current.SetSelectedGameObject(backBtn.gameObject);

            return true;
        }

        // GÓRA = slider
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _focus = FocusZone.CardSlider;

            if (cardToggleSlider)
                EventSystem.current.SetSelectedGameObject(cardToggleSlider.gameObject);

            RefreshAll();
            return true;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (selected == confirmBtn?.gameObject)
            {
                if (cancelBtn && cancelBtn.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
                }
                else if (backBtn)
                {
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                }

                RefreshAll();
                return true;
            }

            if (selected == cancelBtn?.gameObject)
            {
                if (backBtn)
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);

                RefreshAll();
                return true;
            }

            if (selected == backBtn?.gameObject)
            {
                if (_issueCard)
                {
                    _focus = FocusZone.PinEntry;
                    ApplyFocus();
                }
                else
                {
                    // bez karty zostajemy na BACK
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                    RefreshAll();
                }

                return true;
            }
        }

        // LEWO / PRAWO tylko w obrębie górnych buttonów
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (selected == confirmBtn?.gameObject)
            {
                if (cancelBtn && cancelBtn.interactable)
                    EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
                else if (backBtn)
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                return true;
            }

            if (selected == cancelBtn?.gameObject)
            {
                if (backBtn)
                    EventSystem.current.SetSelectedGameObject(backBtn.gameObject);
                return true;
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (selected == backBtn?.gameObject)
            {
                if (cancelBtn && cancelBtn.interactable)
                    EventSystem.current.SetSelectedGameObject(cancelBtn.gameObject);
                else if (confirmBtn)
                    EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);
                return true;
            }

            if (selected == cancelBtn?.gameObject)
            {
                if (confirmBtn)
                    EventSystem.current.SetSelectedGameObject(confirmBtn.gameObject);
                return true;
            }
        }

        return false;
    }
}
