using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardStolenPanelUI : MonoBehaviour
{
    private enum Step
    {
        CardNumber,
        CurrentPin,
        ConfirmPin,
        IssueNewCard,
        VariantPicker,
        Buttons
    }

    [System.Serializable]
    private class ConfirmDot
    {
        public GameObject selectSelected;
        public GameObject selectConfirmed;
        public TMP_Text checkingText;
    }

    [Header("Owner")]
    [SerializeField] private BankCardOpsPanel owner;

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField inputCardNumber;
    [SerializeField] private GameObject cardNumberBlocked;

    [Header("PIN slots")]
    [SerializeField] private Transform currentPinContainer;
    [SerializeField] private Transform confirmPinContainer;
    [SerializeField] private PinSlotView pinSlotPrefab;
    [SerializeField] private int pinLength = 4;

    [Header("Selected dots")]
    [SerializeField] private GameObject selectedCardNumber;
    [SerializeField] private GameObject selectedCurrentPin;
    [SerializeField] private GameObject selectedConfirmPin;
    [SerializeField] private GameObject selectedSlider;
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Confirm dots")]
    [SerializeField] private ConfirmDot dotCardNumber;
    [SerializeField] private ConfirmDot dotCurrentPin;
    [SerializeField] private ConfirmDot dotConfirmPin;

    [Header("Issue new card")]
    [SerializeField] private Slider issueNewCardSlider;
    [SerializeField] private TMP_Text checkMoneyText;
    [SerializeField] private GameObject newCardIssueRoot;

    [Header("Variant picker")]
    [SerializeField] private Transform variantContainer;
    [SerializeField] private VariantSlotView variantSlotPrefab;
    [SerializeField] private Button btnSaveVariant;
    [SerializeField] private Button btnCancelVariant;
    [SerializeField] private GameObject saveBlocked;
    [SerializeField] private GameObject cancelVariantBlocked;

    [Header("Status")]
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private float checkDelay = 5f;
    [SerializeField] private float processDelay = 5f;
    [SerializeField] private int replacementFee = 5;

    [Header("Buttons")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Button btnBack;
    [SerializeField] private GameObject confirmBlocked;
    [SerializeField] private GameObject cancelBlocked;

    [Header("Inventory")]
    [SerializeField] private BankCardItemData bankCardItemData;

    [Header("Colors")]
    [SerializeField] private Color colorChecking = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] private Color colorOk = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color colorBad = new Color(1f, 0.25f, 0.25f, 1f);

    private BankCardRecord _card;
    private bool _isOpen;
    private bool _processing;

    private Step _step;
    private bool _onConfirmDot;

    private int[] _curPin;
    private int[] _confPin;
    private int _curIndex;
    private int _confIndex;

    private bool _okNumber;
    private bool _okCurPin;
    private bool _okConfPin;

    private bool _issueReplacement;
    private bool _replacementAllowed;
    private bool _variantSaved;
    private int _variantHoverIndex;
    private int _variantSelectedIndex = -1;

    private int _buttonsIndex = 0; // 0 confirm, 1 cancel, 2 back

    private readonly System.Collections.Generic.List<VariantSlotView> _variantSlots = new();
    private Coroutine _checkCo;
    private Coroutine _processCo;

    public void Open(BankCardOpsPanel ownerPanel, BankCardRecord card)
    {
        owner = ownerPanel;
        _card = card;

        BuildPins();
        BuildVariantSlots();

        _isOpen = true;
        _processing = false;
        _step = Step.CardNumber;
        _buttonsIndex = 0;
        _onConfirmDot = false;

        _okNumber = false;
        _okCurPin = false;
        _okConfPin = false;

        _issueReplacement = false;
        _replacementAllowed = false;
        _variantSaved = false;
        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;

        if (inputCardNumber) inputCardNumber.SetTextWithoutNotify("");
        if (issueNewCardSlider)
        {
            issueNewCardSlider.minValue = 0f;
            issueNewCardSlider.maxValue = 1f;
            issueNewCardSlider.wholeNumbers = true;
            issueNewCardSlider.SetValueWithoutNotify(0f);
        }

        if (newCardIssueRoot) newCardIssueRoot.SetActive(false);
        if (checkMoneyText) checkMoneyText.gameObject.SetActive(false);

        if (readyText)
        {
            readyText.text = "";
            readyText.gameObject.SetActive(false);
        }

        ResetDot(dotCardNumber);
        ResetDot(dotCurrentPin);
        ResetDot(dotConfirmPin);

        HookButtons();
        Show(true);
        FocusForStep();
        RefreshVisuals();
    }

    public void Close(bool goBackToMenu)
    {
        StopAllCoroutines();
        _checkCo = null;
        _processCo = null;

        _isOpen = false;
        Show(false);

        if (goBackToMenu && owner != null)
            owner.ReturnToMenuFromSubPanel();
    }

    private void HookButtons()
    {
        if (btnSaveVariant)
        {
            btnSaveVariant.onClick.RemoveAllListeners();
            btnSaveVariant.onClick.AddListener(OnSaveVariantPressed);
        }

        if (btnCancelVariant)
        {
            btnCancelVariant.onClick.RemoveAllListeners();
            btnCancelVariant.onClick.AddListener(OnCancelVariantPressed);
        }

        if (btnConfirm)
        {
            btnConfirm.onClick.RemoveAllListeners();
            btnConfirm.onClick.AddListener(OnConfirmPressed);
        }

        if (btnCancel)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(ResetFormToStart);
        }

        if (btnBack)
        {
            btnBack.onClick.RemoveAllListeners();
            btnBack.onClick.AddListener(() => Close(true));
        }

        if (issueNewCardSlider)
        {
            issueNewCardSlider.onValueChanged.RemoveAllListeners();
            issueNewCardSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void Update()
    {
        if (!_isOpen || _processing) return;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsFormDirty())
            {
                ResetFormToStart();
                return;
            }

            Close(true);
            return;
        }

        if (_step == Step.Buttons)
        {
            HandleButtonsInput(enter);
            return;
        }

        if (_step == Step.VariantPicker)
        {
            HandleVariantPickerInput(enter);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_step == Step.VariantPicker)
            {
                _step = Step.IssueNewCard;
                FocusForStep();
                RefreshVisuals();
                return;
            }

            if (_step == Step.Buttons)
            {
                if (_issueReplacement && _replacementAllowed)
                {
                    _step = Step.VariantPicker;
                    FocusForStep();
                    RefreshVisuals();
                    return;
                }
            }

            if (_step == Step.CardNumber)
            {
                _step = Step.Buttons;
                _buttonsIndex = 2;
                FocusForStep();
                RefreshVisuals();
                return;
            }

            if (_onConfirmDot) ExitDot();
            PrevStep();
            RefreshActivePinArrows();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (_onConfirmDot) ExitDot();
            NextStep();
            RefreshActivePinArrows();
            return;
        }

        if (IsStepWithDot(_step))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (!_onConfirmDot && CanEnterDotNow())
                {
                    EnterDot();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_onConfirmDot)
                {
                    ExitDot();
                    return;
                }
            }

            if (_onConfirmDot && enter)
            {
                SetDotConfirmed(GetDotForStep(_step), true);
                RefreshVisuals();
                StartCheckForCurrentStep();
                return;
            }
        }

        if (_step == Step.IssueNewCard)
        {
            HandleSliderInput(enter);
            return;
        }

        if (!_onConfirmDot)
        {
            if (_step == Step.CurrentPin)
            {
                HandlePinTyping(_curPin, ref _curIndex, currentPinContainer, ref _okCurPin);
                return;
            }

            if (_step == Step.ConfirmPin)
            {
                HandlePinTyping(_confPin, ref _confIndex, confirmPinContainer, ref _okConfPin);
                return;
            }
        }
    }

    private void HandleButtonsInput(bool enter)
    {
        bool canConfirm = CanConfirmNow();

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_buttonsIndex == 2)
            {
                _buttonsIndex = 1;
                FocusButtonByIndex(canConfirm);
                RefreshVisuals();
                return;
            }

            if (_buttonsIndex == 1)
            {
                if (canConfirm)
                {
                    _buttonsIndex = 0;
                    FocusButtonByIndex(canConfirm);
                    RefreshVisuals();
                    return;
                }

                if (_issueReplacement && _replacementAllowed)
                {
                    if (btnCancelVariant && btnCancelVariant.interactable)
                        EventSystem.current?.SetSelectedGameObject(btnCancelVariant.gameObject);
                    else if (btnSaveVariant && btnSaveVariant.interactable)
                        EventSystem.current?.SetSelectedGameObject(btnSaveVariant.gameObject);
                    else
                        EventSystem.current?.SetSelectedGameObject(null);

                    _step = Step.VariantPicker;
                    RefreshVisuals();
                    return;
                }

                _step = Step.IssueNewCard;
                FocusForStep();
                RefreshVisuals();
                return;
            }

            if (_buttonsIndex == 0)
            {
                if (_issueReplacement && _replacementAllowed)
                {
                    if (btnCancelVariant && btnCancelVariant.interactable)
                        EventSystem.current?.SetSelectedGameObject(btnCancelVariant.gameObject);
                    else if (btnSaveVariant && btnSaveVariant.interactable)
                        EventSystem.current?.SetSelectedGameObject(btnSaveVariant.gameObject);
                    else
                        EventSystem.current?.SetSelectedGameObject(null);

                    _step = Step.VariantPicker;
                    RefreshVisuals();
                    return;
                }

                _step = Step.IssueNewCard;
                FocusForStep();
                RefreshVisuals();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (_buttonsIndex == 0)
            {
                _buttonsIndex = 1;
                FocusButtonByIndex(canConfirm);
                RefreshVisuals();
                return;
            }

            if (_buttonsIndex == 1)
            {
                _buttonsIndex = 2;
                FocusButtonByIndex(canConfirm);
                RefreshVisuals();
                return;
            }

            if (_buttonsIndex == 2)
            {
                _step = Step.CardNumber;
                FocusForStep();
                RefreshVisuals();
                return;
            }
        }

        if (!enter) return;

        var cur = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        if (btnConfirm && cur == btnConfirm.gameObject && canConfirm)
        {
            OnConfirmPressed();
            return;
        }

        if (btnCancel && cur == btnCancel.gameObject)
        {
            ResetFormToStart();
            return;
        }

        if (btnBack && cur == btnBack.gameObject)
        {
            Close(true);
        }
    }
    private void FocusButtonByIndex(bool canConfirm)
    {
        if (!EventSystem.current) return;

        if (_buttonsIndex == 0 && canConfirm && btnConfirm)
            EventSystem.current.SetSelectedGameObject(btnConfirm.gameObject);
        else if (_buttonsIndex == 1 && btnCancel)
            EventSystem.current.SetSelectedGameObject(btnCancel.gameObject);
        else if (_buttonsIndex == 2 && btnBack)
            EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
    }
    private void HandleSliderInput(bool enter)
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SetIssueReplacement(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SetIssueReplacement(true);
            return;
        }

        if (enter)
        {
            SetIssueReplacement(!_issueReplacement);
        }
    }

    private void HandleVariantPickerInput(bool enter)
    {
        if (_variantSlots.Count == 0) return;

        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        bool onSaveCancel =
            selected == (btnSaveVariant ? btnSaveVariant.gameObject : null) ||
            selected == (btnCancelVariant ? btnCancelVariant.gameObject : null);

        if (!onSaveCancel)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _variantHoverIndex = (_variantHoverIndex - 1 + _variantSlots.Count) % _variantSlots.Count;
                RefreshVariantVisuals();
                RefreshVisuals();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _variantHoverIndex = (_variantHoverIndex + 1) % _variantSlots.Count;
                RefreshVariantVisuals();
                RefreshVisuals();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _step = Step.IssueNewCard;
                FocusForStep();
                RefreshVisuals();
                return;
            }

            if (enter)
            {
                _variantSelectedIndex = _variantHoverIndex;

                if (EventSystem.current && btnSaveVariant)
                    EventSystem.current.SetSelectedGameObject(btnSaveVariant.gameObject);

                RefreshVariantVisuals();
                RefreshVisuals();
                return;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (selected == (btnCancelVariant ? btnCancelVariant.gameObject : null) && btnSaveVariant)
                {
                    EventSystem.current.SetSelectedGameObject(btnSaveVariant.gameObject);
                    RefreshVisuals();
                    return;
                }

                if (selected == (btnSaveVariant ? btnSaveVariant.gameObject : null))
                {
                    EventSystem.current?.SetSelectedGameObject(null);
                    RefreshVisuals();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (selected == (btnSaveVariant ? btnSaveVariant.gameObject : null) && btnCancelVariant)
                {
                    EventSystem.current.SetSelectedGameObject(btnCancelVariant.gameObject);
                    RefreshVisuals();
                    return;
                }

                if (selected == (btnCancelVariant ? btnCancelVariant.gameObject : null))
                {
                    EventSystem.current?.SetSelectedGameObject(null);
                    RefreshVisuals();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (selected == (btnCancelVariant ? btnCancelVariant.gameObject : null) && btnSaveVariant)
                {
                    EventSystem.current.SetSelectedGameObject(btnSaveVariant.gameObject);
                    RefreshVisuals();
                    return;
                }

                if (selected == (btnSaveVariant ? btnSaveVariant.gameObject : null))
                {
                    EventSystem.current?.SetSelectedGameObject(null);
                    RefreshVisuals();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _step = Step.Buttons;
                _buttonsIndex = CanConfirmNow() ? 0 : 1;
                FocusForStep();
                RefreshVisuals();
                return;
            }

            if (enter)
            {
                if (selected == (btnSaveVariant ? btnSaveVariant.gameObject : null))
                {
                    OnSaveVariantPressed();
                    return;
                }

                if (selected == (btnCancelVariant ? btnCancelVariant.gameObject : null))
                {
                    OnCancelVariantPressed();
                    return;
                }
            }
        }
    }

    private void BuildPins()
    {
        _curPin = new int[pinLength];
        _confPin = new int[pinLength];

        for (int i = 0; i < pinLength; i++)
        {
            _curPin[i] = -1;
            _confPin[i] = -1;
        }

        _curIndex = 0;
        _confIndex = 0;

        BuildPinSlots(currentPinContainer);
        BuildPinSlots(confirmPinContainer);

        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, false);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, false);
    }

    private void BuildPinSlots(Transform container)
    {
        if (!container || !pinSlotPrefab) return;

        if (container.childCount == 0)
        {
            for (int i = 0; i < pinLength; i++)
            {
                var slot = Instantiate(pinSlotPrefab, container);
                slot.SetDigit(-1);
                slot.SetArrow(false);
            }
            return;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            var slot = container.GetChild(i).GetComponent<PinSlotView>();
            if (!slot) continue;
            slot.SetDigit(-1);
            slot.SetArrow(false);
        }
    }

    private void BuildVariantSlots()
    {
        if (!variantContainer || !variantSlotPrefab) return;

        for (int i = variantContainer.childCount - 1; i >= 0; i--)
            Destroy(variantContainer.GetChild(i).gameObject);

        _variantSlots.Clear();

        int count = 0;
        if (BankSystem.Instance != null)
            count = Mathf.Max(0, BankSystem.Instance.VariantCount);

        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(variantSlotPrefab, variantContainer);
            slot.SetHover(false);
            slot.SetSelected(false);
            slot.SetPreviewColor(BankSystem.Instance.GetVariantColor(i));

            if (slot.activeBorder) slot.activeBorder.SetActive(false);
            if (slot.arrow) slot.arrow.SetActive(false);

            _variantSlots.Add(slot);
        }

        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;
        _variantSaved = false;

        RefreshVariantVisuals();
    }

    private void HandlePinTyping(int[] pin, ref int index, Transform container, ref bool okFlag)
    {
        int digit = GetDigitDown();
        if (digit >= 0)
        {
            pin[index] = digit;

            bool wasLast = index == pin.Length - 1;
            if (!wasLast) index++;

            okFlag = false;

            bool isActiveStep =
                (_step == Step.CurrentPin && container == currentPinContainer) ||
                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, isActiveStep && !_onConfirmDot);
            RefreshVisuals();

            if (wasLast && IsPinComplete(pin))
                EnterDot();

            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (pin[index] != -1) pin[index] = -1;
            else if (index > 0)
            {
                index--;
                pin[index] = -1;
            }

            okFlag = false;

            bool isActiveStep =
                (_step == Step.CurrentPin && container == currentPinContainer) ||
                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, isActiveStep && !_onConfirmDot);
            RefreshVisuals();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            index = Mathf.Max(0, index - 1);

            bool isActiveStep =
                (_step == Step.CurrentPin && container == currentPinContainer) ||
                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, isActiveStep && !_onConfirmDot);
            RefreshVisuals();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            bool isLast = index >= pin.Length - 1;

            if (isLast && IsPinComplete(pin))
            {
                if (!_onConfirmDot && CanEnterDotNow())
                    EnterDot();
                return;
            }

            index = Mathf.Min(pin.Length - 1, index + 1);

            bool isActiveStep =
                (_step == Step.CurrentPin && container == currentPinContainer) ||
                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, isActiveStep && !_onConfirmDot);
            RefreshVisuals();
        }
    }

    private void RefreshPinSlots(Transform container, int[] pin, int activeIndex, bool active)
    {
        if (!container) return;

        for (int i = 0; i < container.childCount; i++)
        {
            var slot = container.GetChild(i).GetComponent<PinSlotView>();
            if (!slot) continue;

            int d = (pin != null && i < pin.Length) ? pin[i] : -1;
            slot.SetDigit(d);

            bool isActive = active && i == activeIndex && !_processing;
            slot.SetArrow(isActive);
        }
    }

    private void RefreshVariantVisuals()
    {
        for (int i = 0; i < _variantSlots.Count; i++)
        {
            var slot = _variantSlots[i];
            if (!slot) continue;

            bool hover = (_step == Step.VariantPicker) && i == _variantHoverIndex;
            bool selected = i == _variantSelectedIndex;

            slot.SetHover(hover);
            slot.SetSelected(selected);

            if (slot.arrow) slot.arrow.SetActive(hover);
            if (slot.activeBorder) slot.activeBorder.SetActive(hover || selected);
        }
    }
    private void OnSliderChanged(float value)
    {
        SetIssueReplacement(value >= 1f);
    }
    private void SetIssueReplacement(bool on)
    {
        _issueReplacement = on;

        if (issueNewCardSlider)
            issueNewCardSlider.SetValueWithoutNotify(on ? 1f : 0f);

        if (!on)
        {
            _replacementAllowed = false;
            _variantSaved = false;
            _variantSelectedIndex = -1;

            if (checkMoneyText) checkMoneyText.gameObject.SetActive(false);
            if (newCardIssueRoot) newCardIssueRoot.SetActive(false);

            RefreshVariantVisuals();
            RefreshVisuals();
            return;
        }

        if (_card == null || BankSystem.Instance == null)
        {
            _replacementAllowed = false;
            if (checkMoneyText)
            {
                checkMoneyText.gameObject.SetActive(true);
                checkMoneyText.text = "ACCOUNT NOT FOUND";
            }
            if (newCardIssueRoot) newCardIssueRoot.SetActive(false);
            RefreshVisuals();
            return;
        }

        int balance = BankSystem.Instance.GetBalance(_card.accountId);
        _replacementAllowed = balance >= replacementFee;

        if (checkMoneyText)
        {
            checkMoneyText.gameObject.SetActive(!_replacementAllowed);
            if (!_replacementAllowed)
                checkMoneyText.text = $"NOT ENOUGH MONEY\n{replacementFee}$ NEEDED";
        }

        if (newCardIssueRoot)
            newCardIssueRoot.SetActive(_replacementAllowed);

        if (!_replacementAllowed)
        {
            _variantSaved = false;
            _variantSelectedIndex = -1;
        }

        RefreshVariantVisuals();
        RefreshVisuals();
    }

    private void OnSaveVariantPressed()
    {
        if (!_issueReplacement) return;
        if (!_replacementAllowed) return;
        if (_variantSelectedIndex < 0) return;

        _variantSaved = true;
        _step = Step.Buttons;
        _buttonsIndex = CanConfirmNow() ? 0 : 1;
        FocusForStep();
        RefreshVisuals();
    }
    private void OnCancelVariantPressed()
    {
        _variantSaved = false;
        _variantSelectedIndex = -1;

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        _step = Step.VariantPicker;
        FocusForStep();
        RefreshVariantVisuals();
        RefreshVisuals();
    }

    private void PrevStep()
    {
        _step = (Step)Mathf.Max(0, (int)_step - 1);
        FocusForStep();
        RefreshVisuals();
    }

    private void NextStep()
    {
        if (_step == Step.ConfirmPin)
        {
            _step = Step.IssueNewCard;
            FocusForStep();
            RefreshVisuals();
            return;
        }

        if (_step == Step.IssueNewCard)
        {
            _step = _issueReplacement && _replacementAllowed ? Step.VariantPicker : Step.Buttons;
            FocusForStep();
            RefreshVisuals();
            return;
        }

        _step = (Step)Mathf.Min((int)Step.Buttons, (int)_step + 1);
        FocusForStep();
        RefreshVisuals();
    }

    private void FocusForStep()
    {
        if (!EventSystem.current) return;

        switch (_step)
        {
            case Step.CardNumber:
                EventSystem.current.SetSelectedGameObject(inputCardNumber ? inputCardNumber.gameObject : null);
                inputCardNumber?.ActivateInputField();
                break;

            case Step.CurrentPin:
            case Step.ConfirmPin:
                EventSystem.current.SetSelectedGameObject(null);
                break;

            case Step.IssueNewCard:
                EventSystem.current.SetSelectedGameObject(issueNewCardSlider ? issueNewCardSlider.gameObject : null);
                break;

            case Step.VariantPicker:
                EventSystem.current.SetSelectedGameObject(null);
                break;

            case Step.Buttons:
                if (CanConfirmNow() && btnConfirm)
                {
                    _buttonsIndex = 0;
                    EventSystem.current.SetSelectedGameObject(btnConfirm.gameObject);
                }
                else if (btnCancel)
                {
                    _buttonsIndex = 1;
                    EventSystem.current.SetSelectedGameObject(btnCancel.gameObject);
                }
                else if (btnBack)
                {
                    _buttonsIndex = 2;
                    EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
                }
                break;
        }
    }

    private void NavigateButtons(int dir)
    {
        bool canConfirm = CanConfirmNow();

        _buttonsIndex = Mathf.Clamp(_buttonsIndex + dir, 0, 2);

        if (!canConfirm && _buttonsIndex == 0)
            _buttonsIndex = 1;

        if (!EventSystem.current) return;

        if (_buttonsIndex == 0 && btnConfirm) EventSystem.current.SetSelectedGameObject(btnConfirm.gameObject);
        else if (_buttonsIndex == 1 && btnCancel) EventSystem.current.SetSelectedGameObject(btnCancel.gameObject);
        else if (_buttonsIndex == 2 && btnBack) EventSystem.current.SetSelectedGameObject(btnBack.gameObject);

        RefreshVisuals();
    }

    private void StartCheckForCurrentStep()
    {
        if (_checkCo != null) StopCoroutine(_checkCo);

        switch (_step)
        {
            case Step.CardNumber:
                _checkCo = StartCoroutine(CoCheckCardNumber());
                break;
            case Step.CurrentPin:
                _checkCo = StartCoroutine(CoCheckCurrentPin());
                break;
            case Step.ConfirmPin:
                _checkCo = StartCoroutine(CoCheckConfirmPin());
                break;
        }
    }

    private IEnumerator CoCheckCardNumber()
    {
        yield return StartCoroutine(CoRunChecking(dotCardNumber));

        string typed = inputCardNumber ? inputCardNumber.text.Trim() : "";
        _okNumber = !string.IsNullOrWhiteSpace(typed) &&
                    _card != null &&
                    string.Equals(typed, _card.cardId, System.StringComparison.OrdinalIgnoreCase);

        if (!_okNumber)
        {
            SetCheckingResult(dotCardNumber, false);
            _onConfirmDot = false;
            _step = Step.CardNumber;
            FocusForStep();
        }
        else
        {
            SetCheckingResult(dotCardNumber, true);
            _onConfirmDot = false;
            _step = Step.CurrentPin;
            FocusForStep();
        }

        RefreshVisuals();
    }

    private IEnumerator CoCheckCurrentPin()
    {
        yield return StartCoroutine(CoRunChecking(dotCurrentPin));

        _okCurPin = IsPinComplete(_curPin) && PinToInt(_curPin) == (_card != null ? _card.pin : -999);

        if (!_okCurPin)
        {
            SetCheckingResult(dotCurrentPin, false);
            _onConfirmDot = false;
            _step = Step.CurrentPin;
            FocusForStep();
        }
        else
        {
            SetCheckingResult(dotCurrentPin, true);
            _onConfirmDot = false;
            _step = Step.ConfirmPin;
            _confIndex = 0;

            for (int i = 0; i < pinLength; i++)
                _confPin[i] = -1;

            RefreshActivePinArrows();
            FocusForStep();
        }

        RefreshVisuals();
    }

    private IEnumerator CoCheckConfirmPin()
    {
        yield return StartCoroutine(CoRunChecking(dotConfirmPin));

        _okConfPin = IsPinComplete(_confPin) && PinToInt(_confPin) == PinToInt(_curPin);

        if (!_okConfPin)
        {
            SetCheckingResult(dotConfirmPin, false);
            _onConfirmDot = false;
            _step = Step.ConfirmPin;
            FocusForStep();
        }
        else
        {
            SetCheckingResult(dotConfirmPin, true);
            _onConfirmDot = false;
            _step = Step.IssueNewCard;
            FocusForStep();
        }

        RefreshVisuals();
    }

    private IEnumerator CoRunChecking(ConfirmDot dot)
    {
        SetDotSelected(dot, true);

        float t = 0f;
        while (t < checkDelay)
        {
            t += Time.unscaledDeltaTime;
            int k = ((int)(t / 0.35f) % 3) + 1;
            SetChecking(dot.checkingText, "CHECKING" + (k == 1 ? "." : k == 2 ? ".." : "..."), colorChecking);
            yield return null;
        }
    }

    private void SetCheckingResult(ConfirmDot dot, bool ok)
    {
        if (dot == null) return;

        if (ok)
        {
            SetDotSelected(dot, false);
            SetDotConfirmed(dot, true);

            if (dot.checkingText)
            {
                dot.checkingText.text = "OK";
                dot.checkingText.color = colorOk;
            }
        }
        else
        {
            if (dot.selectSelected) dot.selectSelected.SetActive(false);
            if (dot.selectConfirmed) dot.selectConfirmed.SetActive(false);

            if (dot.checkingText)
            {
                dot.checkingText.text = "NOT";
                dot.checkingText.color = colorBad;
            }
        }
    }

    private void OnConfirmPressed()
    {
        if (_processing) return;
        if (!CanConfirmNow()) return;

        if (_processCo != null) StopCoroutine(_processCo);
        _processCo = StartCoroutine(CoProcessStolen());
    }

    private IEnumerator CoProcessStolen()
    {
        _processing = true;
        RefreshVisuals();

        if (readyText)
        {
            readyText.gameObject.SetActive(true);
            readyText.text = "PROCESSING";
        }

        float t = 0f;
        while (t < processDelay)
        {
            t += Time.unscaledDeltaTime;
            int k = ((int)(t / 0.35f) % 3) + 1;
            if (readyText)
                readyText.text = "PROCESSING" + (k == 1 ? "." : k == 2 ? ".." : "...");
            yield return null;
        }

        bool ok = false;
        BankCardRecord newCard = null;

        if (BankSystem.Instance != null && _card != null)
        {
            ok = BankSystem.Instance.TryReportCardStolen(
                _card.cardId,
                _issueReplacement,
                Mathf.Max(0, _variantSelectedIndex),
                replacementFee,
                out newCard,
                out _
            );
        }

        if (ok && InventoryUI.Instance != null && _card != null)
        {
            InventoryUI.Instance.RemoveBankCardId(_card.cardId);
        }

        if (ok && newCard != null)
        {
            var ps = FindFirstObjectByType<PlayerStats>();
            if (ps != null)
                BankSystem.Instance.RegisterPlayerOwnedCard(ps.citizenId, newCard.cardId);

            if (InventoryUI.Instance != null && bankCardItemData != null)
            {
                var inst = new InventoryItemInstance(bankCardItemData) { count = 1 };
                inst.hasBankCardMeta = true;
                inst.bankCard = new BankCardMeta
                {
                    cardId = newCard.cardId,
                    accountId = newCard.accountId,
                    pin = newCard.pin,
                    status = newCard.status,
                    colorVariant = newCard.colorVariant,
                    activateAt = newCard.activateAt
                };

                if (!InventoryUI.Instance.TryAddItem(inst))
                    InventoryUI.Instance.GiveOrDrop(inst);
            }
        }

        if (readyText)
        {
            readyText.text = ok ? "READY" : "FAILED";
        }

        yield return new WaitForSecondsRealtime(0.8f);

        _processing = false;

        if (ok)
        {
            Show(false);
            _isOpen = false;

            if (owner != null)
                owner.ReturnToSelectCardRootAfterDelete();

            yield break;
        }

        if (readyText)
        {
            readyText.text = "";
            readyText.gameObject.SetActive(false);
        }

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (selectedCardNumber) selectedCardNumber.SetActive(_step == Step.CardNumber);
        if (selectedCurrentPin) selectedCurrentPin.SetActive(_step == Step.CurrentPin);
        if (selectedConfirmPin) selectedConfirmPin.SetActive(_step == Step.ConfirmPin);
        if (selectedSlider) selectedSlider.SetActive(_step == Step.IssueNewCard);

        bool onButtons = _step == Step.Buttons;

        if (selectedConfirm) selectedConfirm.SetActive(false);
        if (selectedCancel) selectedCancel.SetActive(false);
        if (selectedBack) selectedBack.SetActive(false);

        if (onButtons && EventSystem.current != null)
        {
            var cur = EventSystem.current.currentSelectedGameObject;

            if (btnConfirm && cur == btnConfirm.gameObject)
            {
                if (selectedConfirm) selectedConfirm.SetActive(true);
            }
            else if (btnCancel && cur == btnCancel.gameObject)
            {
                if (selectedCancel) selectedCancel.SetActive(true);
            }
            else
            {
                if (selectedBack) selectedBack.SetActive(true);
            }
        }

        if (cardNumberBlocked)
            cardNumberBlocked.SetActive(_processing || _step != Step.CardNumber);

        bool canSave = _issueReplacement && _replacementAllowed && _variantSelectedIndex >= 0 && !_variantSaved;
        if (btnSaveVariant) btnSaveVariant.interactable = canSave;
        if (saveBlocked) saveBlocked.SetActive(!canSave);

        bool canCancelVariant = _issueReplacement && (_variantSelectedIndex >= 0 || _variantSaved);
        if (btnCancelVariant) btnCancelVariant.interactable = canCancelVariant;
        if (cancelVariantBlocked) cancelVariantBlocked.SetActive(!canCancelVariant);

        bool canConfirm = CanConfirmNow();
        if (btnConfirm) btnConfirm.interactable = canConfirm;
        if (confirmBlocked) confirmBlocked.SetActive(!canConfirm);

        if (btnCancel) btnCancel.interactable = !_processing;
        if (cancelBlocked) cancelBlocked.SetActive(_processing);

        RefreshActivePinArrows();
        RefreshVariantVisuals();
    }

    private void RefreshActivePinArrows()
    {
        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, _step == Step.CurrentPin && !_onConfirmDot);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, _step == Step.ConfirmPin && !_onConfirmDot);
    }

    private void ResetFormToStart()
    {
        StopAllCoroutines();
        _checkCo = null;
        _processCo = null;

        _processing = false;
        _onConfirmDot = false;

        _okNumber = false;
        _okCurPin = false;
        _okConfPin = false;

        if (inputCardNumber) inputCardNumber.SetTextWithoutNotify("");

        for (int i = 0; i < pinLength; i++)
        {
            _curPin[i] = -1;
            _confPin[i] = -1;
        }

        _curIndex = 0;
        _confIndex = 0;

        _issueReplacement = false;
        _replacementAllowed = false;
        _variantSaved = false;
        _variantHoverIndex = 0;
        _variantSelectedIndex = -1;

        if (issueNewCardSlider) issueNewCardSlider.SetValueWithoutNotify(0f);
        if (newCardIssueRoot) newCardIssueRoot.SetActive(false);
        if (checkMoneyText) checkMoneyText.gameObject.SetActive(false);

        if (readyText)
        {
            readyText.text = "";
            readyText.gameObject.SetActive(false);
        }

        ResetDot(dotCardNumber);
        ResetDot(dotCurrentPin);
        ResetDot(dotConfirmPin);

        _step = Step.CardNumber;
        _buttonsIndex = 0;

        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, false);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, false);
        RefreshVariantVisuals();

        FocusForStep();
        RefreshVisuals();
    }

    private bool IsFormDirty()
    {
        if (inputCardNumber && !string.IsNullOrWhiteSpace(inputCardNumber.text)) return true;
        if (!IsPinEmpty(_curPin)) return true;
        if (!IsPinEmpty(_confPin)) return true;
        if (_issueReplacement) return true;
        if (_variantSelectedIndex >= 0 || _variantSaved) return true;
        if (_okNumber || _okCurPin || _okConfPin) return true;
        if (IsDotActive(dotCardNumber) || IsDotActive(dotCurrentPin) || IsDotActive(dotConfirmPin)) return true;
        return false;
    }

    private bool CanConfirmNow()
    {
        if (_processing) return false;
        if (!_okNumber || !_okCurPin || !_okConfPin) return false;

        if (!_issueReplacement)
            return true;

        if (!_replacementAllowed)
            return false;

        return _variantSaved && _variantSelectedIndex >= 0;
    }

    private bool IsStepWithDot(Step s)
    {
        return s == Step.CardNumber || s == Step.CurrentPin || s == Step.ConfirmPin;
    }

    private bool CanEnterDotNow()
    {
        if (_processing) return false;

        switch (_step)
        {
            case Step.CardNumber:
                return inputCardNumber && !string.IsNullOrWhiteSpace(inputCardNumber.text);

            case Step.CurrentPin:
                if (!_okNumber) return false;
                return IsPinComplete(_curPin) && _curIndex >= pinLength - 1;

            case Step.ConfirmPin:
                if (!_okNumber || !_okCurPin) return false;
                return IsPinComplete(_confPin) && _confIndex >= pinLength - 1;

            default:
                return false;
        }
    }

    private ConfirmDot GetDotForStep(Step s)
    {
        return s switch
        {
            Step.CardNumber => dotCardNumber,
            Step.CurrentPin => dotCurrentPin,
            Step.ConfirmPin => dotConfirmPin,
            _ => null
        };
    }

    private void EnterDot()
    {
        _onConfirmDot = true;
        RefreshActivePinArrows();
        SetDotSelected(GetDotForStep(_step), true);
        RefreshVisuals();
    }

    private void ExitDot()
    {
        _onConfirmDot = false;
        SetDotSelected(GetDotForStep(_step), false);
        RefreshActivePinArrows();
        FocusForStep();
        RefreshVisuals();
    }

    private void ResetDot(ConfirmDot d)
    {
        if (d == null) return;
        if (d.selectSelected) d.selectSelected.SetActive(false);
        if (d.selectConfirmed) d.selectConfirmed.SetActive(false);
        if (d.checkingText) d.checkingText.text = "";
    }

    private void SetDotSelected(ConfirmDot d, bool value)
    {
        if (d == null) return;
        if (d.selectSelected) d.selectSelected.SetActive(value);
    }

    private void SetDotConfirmed(ConfirmDot d, bool value)
    {
        if (d == null) return;
        if (d.selectConfirmed) d.selectConfirmed.SetActive(value);
        if (value && d.selectSelected) d.selectSelected.SetActive(false);
    }

    private void SetChecking(TMP_Text t, string msg, Color? col = null)
    {
        if (!t) return;
        t.text = msg;
        if (col.HasValue) t.color = col.Value;
    }

    private void Show(bool value)
    {
        if (!root)
        {
            gameObject.SetActive(value);
            return;
        }

        root.gameObject.SetActive(value);
        root.alpha = value ? 1f : 0f;
        root.interactable = value;
        root.blocksRaycasts = value;
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

    private static bool IsPinComplete(int[] pin)
    {
        if (pin == null || pin.Length == 0) return false;
        for (int i = 0; i < pin.Length; i++)
            if (pin[i] < 0) return false;
        return true;
    }

    private static int PinToInt(int[] pin)
    {
        if (!IsPinComplete(pin)) return -1;
        int value = 0;
        for (int i = 0; i < pin.Length; i++)
            value = value * 10 + Mathf.Clamp(pin[i], 0, 9);
        return value;
    }

    private static bool IsPinEmpty(int[] pin)
    {
        if (pin == null) return true;
        for (int i = 0; i < pin.Length; i++)
            if (pin[i] >= 0) return false;
        return true;
    }

    private bool IsDotActive(ConfirmDot d)
    {
        if (d == null) return false;
        return (d.selectSelected && d.selectSelected.activeSelf) ||
               (d.selectConfirmed && d.selectConfirmed.activeSelf);
    }

    public void OnClickConfirmCardNumber()
    {
        if (!_isOpen || _processing) return;
        if (_step != Step.CardNumber) return;
        if (!CanEnterDotNow()) return;

        _onConfirmDot = true;
        RefreshActivePinArrows();
        SetDotSelected(dotCardNumber, true);
        RefreshVisuals();
        StartCheckForCurrentStep();
    }

    public void OnClickConfirmCurrentPin()
    {
        if (!_isOpen || _processing) return;
        if (_step != Step.CurrentPin) return;
        if (!CanEnterDotNow()) return;

        _onConfirmDot = true;
        RefreshActivePinArrows();
        SetDotSelected(dotCurrentPin, true);
        RefreshVisuals();
        StartCheckForCurrentStep();
    }

    public void OnClickConfirmConfirmPin()
    {
        if (!_isOpen || _processing) return;
        if (_step != Step.ConfirmPin) return;
        if (!CanEnterDotNow()) return;

        _onConfirmDot = true;
        RefreshActivePinArrows();
        SetDotSelected(dotConfirmPin, true);
        RefreshVisuals();
        StartCheckForCurrentStep();
    }
}