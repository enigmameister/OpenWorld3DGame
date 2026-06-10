using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardBlockPanelUI : MonoBehaviour
{
    private enum Step
    {
        CardNumber,
        CurrentPin,
        ConfirmPin,
        Phrase,
        Buttons
    }

    [Header("Owner")]
    [SerializeField] private BankCardOpsPanel owner;

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Inventory gate")]
    [SerializeField] private TMP_Text inventoryCheckText; // czerwony komunikat
    [SerializeField] private string inventoryMissingMsg = "CARD IN PLAYER INVENTORY IS REQUIRED";

    [Header("Inputs")]
    [SerializeField] private TMP_InputField inputCardNumber;

    [Header("PIN slots")]
    [SerializeField] private Transform currentPinContainer;
    [SerializeField] private Transform confirmPinContainer;
    [SerializeField] private PinSlotView pinSlotPrefab;
    [SerializeField] private int pinLength = 4;

    [Header("Checking TMP")]
    [SerializeField] private TMP_Text checkingCardNumber;
    [SerializeField] private TMP_Text checkingCurrentPin;
    [SerializeField] private TMP_Text checkingConfirmPin;
    [SerializeField] private TMP_Text checkingPhrase;

    [Header("Process")]
    [SerializeField] private TMP_Text processText;
    [SerializeField] private float checkDelay = 5f;
    [SerializeField] private float processDelay = 5f;

    [Header("Selected dots")]
    [SerializeField] private GameObject selectedNumber;
    [SerializeField] private GameObject selectedCurrentPin;
    [SerializeField] private GameObject selectedConfirmPin;
    [SerializeField] private GameObject selectedPhrase;
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Buttons")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Button btnBack;

    [Header("Colors")]
    [SerializeField] private Color colorChecking = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] private Color colorOk = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color colorBad = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Blocked overlays")]
    [SerializeField] private GameObject cardNumberBlocked;
    [SerializeField] private GameObject phraseBlocked;
    [SerializeField] private GameObject confirmBlocked;

    [Header("Phrase (YES I CONFIRM) - stable overlay")]
    [SerializeField] private TMP_Text phraseBaseText; // szary/biały (Inspector)
    [SerializeField] private TMP_Text phraseFillText; // zielony (Inspector)

    private const string PHRASE = "YES I CONFIRM";
    private int _phraseIndex = 0;

    private BankCardRecord _card;
    private bool _isOpen;
    private bool _lockedByInventory;
    private bool _processing;

    private Step _step;

    private int[] _curPin;
    private int[] _confPin;
    private int _curIndex;
    private int _confIndex;

    private bool _okNumber;
    private bool _okCurPin;
    private bool _okConfPin;
    private bool _okPhrase;

    private Coroutine _checkCo;

    [System.Serializable]
    private class ConfirmDot
    {
        public GameObject selectSelected;
        public GameObject selectConfirmed;
    }

    [Header("Confirm dots (circle images next to fields)")]
    [SerializeField] private ConfirmDot dotCardNumber;
    [SerializeField] private ConfirmDot dotCurPin;
    [SerializeField] private ConfirmDot dotConfPin;
    [SerializeField] private ConfirmDot dotPhrase;

    private bool _onConfirmDot; // jesteśmy aktualnie “na kółku”

    public void Open(BankCardOpsPanel ownerPanel, BankCardRecord card)
    {
        PlayerInputHandler.SetGameplayBlocked(true);

        owner = ownerPanel;
        _card = card;

        BuildPins();

        _onConfirmDot = false;
        ResetDot(dotCardNumber);
        ResetDot(dotCurPin);
        ResetDot(dotConfPin);
        ResetDot(dotPhrase);

        SetDotSelected(dotCardNumber, false);
        SetDotSelected(dotCurPin, false);
        SetDotSelected(dotConfPin, false);
        SetDotSelected(dotPhrase, false);

        _step = Step.CardNumber;
        _processing = false;
        _okNumber = _okCurPin = _okConfPin = _okPhrase = false;

        if (inputCardNumber) inputCardNumber.SetTextWithoutNotify("");
        _phraseIndex = 0;

        if (phraseBaseText) phraseBaseText.text = PHRASE;
        if (phraseFillText)
        {
            phraseFillText.text = PHRASE;
            phraseFillText.maxVisibleCharacters = 0;
        }
        UpdatePhraseVisual();

        _okPhrase = false;
        if (processText) processText.gameObject.SetActive(false);

        // inventory gate
        _lockedByInventory = !PlayerHasThisCard();
        if (inventoryCheckText)
        {
            inventoryCheckText.gameObject.SetActive(_lockedByInventory);
            inventoryCheckText.text = inventoryMissingMsg;
        }

        // przyciski
        if (btnConfirm) btnConfirm.interactable = false;
        if (btnCancel) btnCancel.interactable = true;

        HookButtons();
        Show(true);

        // focus na input card number (jeśli można)
        if (!_lockedByInventory && EventSystem.current && inputCardNumber)
        {
            EventSystem.current.SetSelectedGameObject(inputCardNumber.gameObject);
            inputCardNumber.ActivateInputField();
        }
        else if (EventSystem.current && btnBack)
        {
            EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
        }

        // WAŻNE: reset kropek i checkingu przy każdym Open (żeby nie zostawały z poprzedniego razu)

        SetChecking(checkingCardNumber, "");
        SetChecking(checkingCurrentPin, "");
        SetChecking(checkingConfirmPin, "");
        SetChecking(checkingPhrase, "");
        _buttonsIndex = 0;

        RefreshVisuals();
    }

    public void Close(bool goBackToMenu)
    {
        StopAllCoroutines();
        _checkCo = null;

        _isOpen = false;
        Show(false);

        if (goBackToMenu && owner != null)
            owner.ReturnToMenuFromSubPanel();

        PlayerInputHandler.SetGameplayBlocked(true);
    }

    private void HookButtons()
    {
        if (btnCancel)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(() =>
            {
                ResetFormToStart(); // panel zostaje otwarty
            });
        }

        if (btnBack)
        {
            btnBack.onClick.RemoveAllListeners();
            btnBack.onClick.AddListener(() => Close(goBackToMenu: true));
        }

        if (btnConfirm)
        {
            btnConfirm.onClick.RemoveAllListeners();
            btnConfirm.onClick.AddListener(OnConfirmPressed);
        }


    }

    private void Update()
    {
        if (!_isOpen || _processing) return;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (enter)
        {
            PressEnterFallbackTo(btnBack); // albo btnCancel, jak wolisz
        }

        // ESC: 1x czyści, 2x zamyka
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsFormDirty())
            {
                ResetFormToStart();
                return;
            }

            Close(goBackToMenu: true);
            return;
        }

        // blokada przez inventory
        if (_lockedByInventory) return;

        // ===== BUTTONS MODE =====
        if (_step == Step.Buttons)
        {
            bool canConfirm = CanConfirmNow();

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // BACK -> CANCEL -> (CONFIRM jeśli może) -> PHRASE
                if (_buttonsIndex == 2)
                {
                    // BACK -> CANCEL
                    _buttonsIndex = 1;
                    NavigateButtons(0);
                    return;
                }

                if (_buttonsIndex == 1)
                {
                    // CANCEL -> CONFIRM (jeśli można) albo -> PHRASE (jeśli Confirm zablokowany)
                    if (canConfirm)
                    {
                        _buttonsIndex = 0;
                        NavigateButtons(0);
                    }
                    else
                    {
                        _step = Step.Phrase;
                        _onConfirmDot = false;
                        FocusForStep();
                        RefreshActivePinArrows();
                        RefreshVisuals();
                    }
                    return;
                }

                if (_buttonsIndex == 0)
                {
                    // CONFIRM -> PHRASE
                    _step = Step.Phrase;
                    _onConfirmDot = false;
                    FocusForStep();
                    RefreshActivePinArrows();
                    RefreshVisuals();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // CONFIRM -> CANCEL -> BACK -> (wróć do CardNumber)
                if (_buttonsIndex == 0)
                {
                    _buttonsIndex = 1;
                    NavigateButtons(0);
                    return;
                }

                if (_buttonsIndex == 1)
                {
                    _buttonsIndex = 2;
                    NavigateButtons(0);
                    return;
                }

                if (_buttonsIndex == 2)
                {
                    _step = Step.CardNumber;
                    _onConfirmDot = false;
                    FocusForStep();
                    RefreshActivePinArrows();
                    RefreshVisuals();
                    return;
                }
            }

            // Enter na aktywnym przycisku
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
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
                    Close(goBackToMenu: true);
                    return;
                }
            }

            return; // ważne: nie lecimy do logiki Stepów
        }

        // ===== STEP MODE =====

        // UP/DOWN zmienia step
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            // z samej góry (CardNumber) idziemy do Buttons na BACK
            if (_step == Step.CardNumber)
            {
                _step = Step.Buttons;
                _buttonsIndex = 2; // BACK
                FocusForStep();
                RefreshActivePinArrows();
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

        // --- wejście/wyjście z kółka ---
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

            // ENTER w CardNumber/Phrase = wejście na dot (jak RIGHT)
            if (!_onConfirmDot && (_step == Step.CardNumber || _step == Step.Phrase))
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (CanEnterDotNow())
                    {
                        EnterDot();
                        return;
                    }
                }
            }

            // ENTER na dot = confirmed + checking
            if (_onConfirmDot && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                var d = GetDotForStep(_step);
                SetDotConfirmed(d, true);
                RefreshVisuals();
                StartCheckForCurrentStep();
                return;
            }
        }

        // --- wpisywanie danych (tylko gdy NIE jesteś na kółku) ---
        if (!_onConfirmDot)
        {
            if (_step == Step.CurrentPin)
            {
                HandlePinTyping(_curPin, ref _curIndex, currentPinContainer, checkingCurrentPin, ref _okCurPin);
            }
            else if (_step == Step.ConfirmPin)
            {
                HandlePinTyping(_confPin, ref _confIndex, confirmPinContainer, checkingConfirmPin, ref _okConfPin);
            }
            else if (_step == Step.Phrase)
            {
                HandlePhraseTyping();
                return;
            }
        }
    }

    private void PressEnterFallbackTo(Button fallback)
    {
        var es = EventSystem.current;
        if (es == null) return;

        var go = es.currentSelectedGameObject;
        var btn = go ? go.GetComponentInParent<Button>() : null;

        // jeśli EventSystem nie siedzi na buttonie z tego panelu → fallback
        if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy)
            btn = fallback;

        if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy)
            return;

        // to jest najpewniejsze (zawsze odpala OnClick)
        btn.onClick.Invoke();

        // opcjonalnie: utrzymaj focus na tym buttonie
        es.SetSelectedGameObject(btn.gameObject);
    }

    private void PrevStep()
    {
        _step = (Step)Mathf.Max(0, (int)_step - 1);
        FocusForStep();
        RefreshVisuals();
    }

    private void NextStep()
    {
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
                EventSystem.current.SetSelectedGameObject(null);
                break;

            case Step.ConfirmPin:
                EventSystem.current.SetSelectedGameObject(null);
                break;

            case Step.Phrase:
                // nie ma InputField, więc tylko “odznacz” UI (żeby nie klikało NPC)
                EventSystem.current.SetSelectedGameObject(null);
                break;

            case Step.Buttons:
                {
                    if (!EventSystem.current) break;

                    bool allOk = _okNumber && _okCurPin && _okConfPin && _okPhrase;
                    bool canConfirm = !_lockedByInventory && allOk && !_processing;

                    if (canConfirm && btnConfirm)
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
    }
    private void NavigateButtons(int dir)
    {
        bool canConfirm = CanConfirmNow();

        // przesuwamy index
        _buttonsIndex = Mathf.Clamp(_buttonsIndex + dir, 0, 2);

        // jeśli confirm nie może być kliknięty, nie pozwól stanąć na index=0
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
            case Step.Phrase:
                _checkCo = StartCoroutine(CoCheckPhrase());
                break;
            case Step.Buttons:
                // nic
                break;
        }

        if (_okPhrase)
        {
            if (_checkCo != null) StopCoroutine(_checkCo);
            _checkCo = StartCoroutine(CoCheckPhrase());
        }
    }

    private IEnumerator CoCheckCardNumber()
    {
        SetChecking(checkingCardNumber, "CHECKING", colorChecking);
        yield return new WaitForSecondsRealtime(checkDelay);

        string typed = inputCardNumber ? inputCardNumber.text.Trim() : "";
        _okNumber = (!string.IsNullOrWhiteSpace(typed) && _card != null &&
                    string.Equals(typed, _card.cardId, System.StringComparison.OrdinalIgnoreCase));

        SetChecking(checkingCardNumber, _okNumber ? "OK" : "WRONG", _okNumber ? colorOk : colorBad);

        var d = dotCardNumber;

        if (!_okNumber)
        {
            ResetDot(d);
            _onConfirmDot = false;
            _step = Step.CardNumber;
            FocusForStep();
        }
        else
        {
            // zostaw Confirmed ON jeśli chcesz “pamiętać”, albo też ResetDot(d) gdy przechodzisz dalej
            _onConfirmDot = false;
            RefreshPinSlots(currentPinContainer, _curPin, _curIndex, active: (_step == Step.CurrentPin) && !_onConfirmDot);
            RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, active: (_step == Step.ConfirmPin) && !_onConfirmDot);
            _step = Step.CurrentPin;
            FocusForStep();
        }

        RefreshVisuals();
    }

    private IEnumerator CoCheckCurrentPin()
    {
        SetChecking(checkingCurrentPin, "CHECKING", colorChecking);

        yield return new WaitForSecondsRealtime(checkDelay);

        _okCurPin = IsPinComplete(_curPin) && PinToInt(_curPin) == (_card != null ? _card.pin : -999);

        SetChecking(checkingCurrentPin, _okCurPin ? "OK" : "WRONG", _okCurPin ? colorOk : colorBad);

        if (_okCurPin)
        {
            _step = Step.ConfirmPin;

            _onConfirmDot = false;   // ważne
            _confIndex = 0;

            // optional: jeśli chcesz czyścić confirm pin przy każdym wejściu
            for (int i = 0; i < pinLength; i++) _confPin[i] = -1;

            // zgaś cur, zapal confirm
            RefreshActivePinArrows();

            FocusForStep();
        }

        RefreshVisuals();
    }

    private IEnumerator CoCheckConfirmPin()
    {
        SetChecking(checkingConfirmPin, "CHECKING", colorChecking);
        yield return new WaitForSecondsRealtime(checkDelay);

        _okConfPin = IsPinComplete(_confPin) && PinToInt(_confPin) == PinToInt(_curPin);
        SetChecking(checkingConfirmPin, _okConfPin ? "OK" : "WRONG", _okConfPin ? colorOk : colorBad);

        if (_okConfPin)
        {
            _step = Step.Phrase;

            // ważne: wyłącz stan kółka i strzałki pinów
            _onConfirmDot = false;
            SetDotSelected(dotConfPin, false);

            RefreshActivePinArrows();

            // nie trzymaj selekcji UI (żeby typing działał od razu)
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

            FocusForStep();
        }
        RefreshVisuals();
    }

    private IEnumerator CoCheckPhrase()
    {
        SetChecking(checkingPhrase, "CHECKING", colorChecking);

        yield return new WaitForSecondsRealtime(checkDelay);

        _okPhrase = (_phraseIndex >= PHRASE.Length);

        SetChecking(checkingPhrase, _okPhrase ? "OK" : "WRONG", _okPhrase ? colorOk : colorBad);

        if (_okPhrase) { _step = Step.Buttons; FocusForStep(); }
        RefreshVisuals();
    }

    private void OnConfirmPressed()
    {
        if (_processing) return;

        // muszą być wszystkie OK
        bool allOk = _okNumber && _okCurPin && _okConfPin && _okPhrase;
        if (!allOk) return;

        StartCoroutine(CoProcessDelete());
    }

    private IEnumerator CoProcessDelete()
    {
        _processing = true;

        if (processText)
        {
            processText.gameObject.SetActive(true);
            processText.text = "PROCESS";
        }

        float t = 0f;
        while (t < processDelay)
        {
            t += Time.unscaledDeltaTime;
            int k = ((int)(t / 0.25f) % 3) + 1;
            if (processText) processText.text = "PROCESS" + (k == 1 ? "." : k == 2 ? ".." : "...");
            yield return null;
        }

        // final: usuń z banku + inventory
        bool ok = false;

        if (BankSystem.Instance != null && _card != null)
        {
            ok = BankSystem.Instance.TryDeleteCard(_card.cardId, out _);
        }

        if (ok && InventoryUI.Instance != null && _card != null)
        {
            InventoryUI.Instance.RemoveBankCardId(_card.cardId);
        }

        _processing = false;

        if (ok)
        {
            // zamknij panel bez wracania do menu
            Show(false);
            _isOpen = false;

            // przejście: OPS -> CHECK -> SELECT
            if (owner != null)
                owner.ReturnToSelectCardRootAfterDelete();

            yield break;
        }

        // jak fail: po prostu zostaw panel otwarty i pokaż FAILED (opcjonalnie)
        if (processText)
        {
            processText.text = "FAILED";
            yield return new WaitForSecondsRealtime(0.8f);
            processText.gameObject.SetActive(false);
        }
    }

    // ---------- PIN typing ----------
    private void BuildPins()
    {
        _curPin = new int[pinLength];
        _confPin = new int[pinLength];
        for (int i = 0; i < pinLength; i++) { _curPin[i] = -1; _confPin[i] = -1; }
        _curIndex = 0;
        _confIndex = 0;

        BuildPinSlots(currentPinContainer);
        BuildPinSlots(confirmPinContainer);

        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, active: (_step == Step.CurrentPin) && !_onConfirmDot);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, active: (_step == Step.ConfirmPin) && !_onConfirmDot);
    }

    private void BuildPinSlots(Transform container)
    {
        if (!container || !pinSlotPrefab) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        for (int i = 0; i < pinLength; i++)
        {
            var slot = Instantiate(pinSlotPrefab, container);
            slot.SetDigit(-1);
            slot.SetArrow(false);
        }
    }

    private void HandlePinTyping(int[] pin, ref int index, Transform container, TMP_Text checking, ref bool okFlag)
    {
        int digit = GetDigitDown();
        if (digit >= 0)
        {
            pin[index] = digit;

            bool wasLast = (index == pin.Length - 1);
            if (!wasLast) index++;

            okFlag = false;
            SetChecking(checking, "");
            bool isActiveStep = (_step == Step.CurrentPin && container == currentPinContainer) ||
                                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, active: isActiveStep && !_onConfirmDot);
            RefreshVisuals();

            // auto-wejście na kółko dopiero po ostatniej cyfrze i komplecie
            if (wasLast && IsPinComplete(pin))
            {
                EnterDot();
            }

            return;
        }

        // Backspace / Left / Right -> tylko nawigacja slotów, ale Right NIE ma “wchodzić na kółko”
        // (wejście na kółko obsługujemy w Update() przez CanEnterDotNow())
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (pin[index] != -1) pin[index] = -1;
            else if (index > 0) { index--; pin[index] = -1; }

            okFlag = false;
            SetChecking(checking, "");
            bool isActiveStep = (_step == Step.CurrentPin && container == currentPinContainer) ||
                                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, active: isActiveStep && !_onConfirmDot);
            RefreshVisuals();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            index = Mathf.Max(0, index - 1);
            bool isActiveStep = (_step == Step.CurrentPin && container == currentPinContainer) ||
                                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, active: isActiveStep && !_onConfirmDot);
            RefreshVisuals();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            bool isLast = index >= pin.Length - 1;

            // jeśli na ostatnim slocie i PIN kompletny -> wejdź na kółko (i zgaś strzałki)
            if (isLast && IsPinComplete(pin))
            {
                if (!_onConfirmDot && CanEnterDotNow())
                    EnterDot();
                return;
            }

            index = Mathf.Min(pin.Length - 1, index + 1);

            bool isActiveStep = (_step == Step.CurrentPin && container == currentPinContainer) ||
                                (_step == Step.ConfirmPin && container == confirmPinContainer);

            RefreshPinSlots(container, pin, index, active: isActiveStep && !_onConfirmDot);
            RefreshVisuals();
            return;
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

    private static bool IsPinComplete(int[] p)
    {
        if (p == null || p.Length == 0) return false;
        for (int i = 0; i < p.Length; i++) if (p[i] < 0) return false;
        return true;
    }

    private static int PinToInt(int[] p)
    {
        if (!IsPinComplete(p)) return -1;
        int v = 0;
        for (int i = 0; i < p.Length; i++) v = v * 10 + Mathf.Clamp(p[i], 0, 9);
        return v;
    }

    private static string NormalizePhrase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim().ToUpperInvariant();

        // sklej wielokrotne spacje do jednej
        var sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    private bool PlayerHasThisCard()
    {
        if (_card == null) return false;
        if (InventoryUI.Instance == null) return false;
        return InventoryUI.Instance.HasBankCardId(_card.cardId);
    }

    private void RefreshVisuals()
    {
        if (selectedNumber) selectedNumber.SetActive(_step == Step.CardNumber);
        if (selectedCurrentPin) selectedCurrentPin.SetActive(_step == Step.CurrentPin);
        if (selectedConfirmPin) selectedConfirmPin.SetActive(_step == Step.ConfirmPin);
        if (selectedPhrase) selectedPhrase.SetActive(_step == Step.Phrase);

        bool onButtons = (_step == Step.Buttons);

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
                // domyślnie BACK (albo jak nic nie jest zaznaczone)
                if (selectedBack) selectedBack.SetActive(true);
            }
        }

        // BLOCKED overlays
        if (cardNumberBlocked)
            cardNumberBlocked.SetActive(_lockedByInventory || _processing || _step != Step.CardNumber);

        if (phraseBlocked)
            phraseBlocked.SetActive(_lockedByInventory || _processing || _step != Step.Phrase);

        // confirmBlocked = aktywne gdy confirm NIE jest klikalny
        bool allOk = _okNumber && _okCurPin && _okConfPin && _okPhrase;
        bool canConfirm = !_lockedByInventory && allOk && !_processing;

        if (btnConfirm) btnConfirm.interactable = canConfirm;

        if (confirmBlocked)
            confirmBlocked.SetActive(!canConfirm);
    }

    private void SetChecking(TMP_Text t, string msg, Color? col = null)
    {
        if (!t) return;
        t.text = msg;
        if (col.HasValue) t.color = col.Value;
    }

    private void Show(bool v)
    {
        _isOpen = v;
        if (!root) { gameObject.SetActive(v); return; }
        root.gameObject.SetActive(v);
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
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
    private void HandlePhraseTyping()
    {
        // Dozwolone: litery + spacja + backspace + strzałki (nawigacja kroków masz osobno)
        if (_phraseIndex < PHRASE.Length)
        {
            char expected = PHRASE[_phraseIndex];

            // spacja
            if (expected == ' ')
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _phraseIndex++;
                    UpdatePhraseVisual();
                    _okPhrase = false;
                    SetChecking(checkingPhrase, "");
                    RefreshVisuals();
                }
                return;
            }

            // litery (A-Z)
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (Input.GetKeyDown(k))
                {
                    char typed = (char)('A' + (k - KeyCode.A));
                    if (typed == expected)
                    {
                        _phraseIndex++;
                        UpdatePhraseVisual();
                        _okPhrase = false;
                        SetChecking(checkingPhrase, "");
                        RefreshVisuals();
                    }
                    // jak zła litera -> ignoruj (nic nie wpisuj)
                    return;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_phraseIndex > 0)
            {
                _phraseIndex--;
                UpdatePhraseVisual();
                _okPhrase = false;
                SetChecking(checkingPhrase, "");
                RefreshVisuals();
            }
        }
    }

    private void UpdatePhraseVisual()
    {
        if (phraseFillText)
            phraseFillText.maxVisibleCharacters = Mathf.Clamp(_phraseIndex, 0, PHRASE.Length);

        // baseText zostaje cały czas pełny i nie ruszamy go
    }

    private bool IsStepWithDot(Step s)
    {
        return s == Step.CardNumber
            || s == Step.CurrentPin
            || s == Step.ConfirmPin
            || s == Step.Phrase;
    }

    private bool CanEnterDotNow()
    {
        if (_lockedByInventory || _processing) return false;

        switch (_step)
        {
            case Step.CardNumber:
                // nie pozwalaj wejść na kółko jeśli pole jest puste
                return inputCardNumber && !string.IsNullOrWhiteSpace(inputCardNumber.text);

            case Step.CurrentPin:
                // nie pozwól jeśli CardNumber nie przeszedł OK
                if (!_okNumber) return false;
                return IsPinComplete(_curPin) && _curIndex >= (pinLength - 1);

            case Step.ConfirmPin:
                if (!_okNumber || !_okCurPin) return false;
                return IsPinComplete(_confPin) && _confIndex >= (pinLength - 1);

            case Step.Phrase:
                // nie pozwól wejść jeśli wcześniejsze nie są OK
                if (!_okNumber || !_okCurPin || !_okConfPin) return false;

                // opcjonalnie: nie pozwól wejść na kółko jeśli gracz nic nie wpisał
                // (jeśli chcesz, żeby mógł wejść i dostać WRONG bez wpisywania, usuń tę linię)
                return _phraseIndex > 0;

            default:
                return false;
        }
    }

    private ConfirmDot GetDotForStep(Step s)
    {
        return s switch
        {
            Step.CardNumber => dotCardNumber,
            Step.CurrentPin => dotCurPin,
            Step.ConfirmPin => dotConfPin,
            Step.Phrase => dotPhrase,
            _ => null
        };
    }

    private void ResetDot(ConfirmDot d)
    {
        if (d == null) return;
        if (d.selectSelected) d.selectSelected.SetActive(false);
        if (d.selectConfirmed) d.selectConfirmed.SetActive(false);
    }

    private void SetDotSelected(ConfirmDot d, bool v)
    {
        if (d == null) return;
        if (d.selectSelected) d.selectSelected.SetActive(v);
        // confirmed nie ruszamy tu
    }

    private void SetDotConfirmed(ConfirmDot d, bool v)
    {
        if (d == null) return;
        if (d.selectConfirmed) d.selectConfirmed.SetActive(v);
        if (v && d.selectSelected) d.selectSelected.SetActive(false);
    }

    // --- Buttons navigation ---
    private int _buttonsIndex = 0; // 0=Confirm, 1=Cancel, 2=Back

    private bool IsFormDirty()
    {
        if (inputCardNumber && !string.IsNullOrWhiteSpace(inputCardNumber.text)) return true;

        if (_phraseIndex > 0) return true;

        if (!IsPinEmpty(_curPin)) return true;
        if (!IsPinEmpty(_confPin)) return true;

        if (_okNumber || _okCurPin || _okConfPin || _okPhrase) return true;

        // jeśli kółka świecą
        if (IsDotActive(dotCardNumber)) return true;
        if (IsDotActive(dotCurPin)) return true;
        if (IsDotActive(dotConfPin)) return true;
        if (IsDotActive(dotPhrase)) return true;

        // jeśli są jakiekolwiek napisy w checking
        if (checkingCardNumber && !string.IsNullOrEmpty(checkingCardNumber.text)) return true;
        if (checkingCurrentPin && !string.IsNullOrEmpty(checkingCurrentPin.text)) return true;
        if (checkingConfirmPin && !string.IsNullOrEmpty(checkingConfirmPin.text)) return true;
        if (checkingPhrase && !string.IsNullOrEmpty(checkingPhrase.text)) return true;

        return false;
    }

    private static bool IsPinEmpty(int[] p)
    {
        if (p == null) return true;
        for (int i = 0; i < p.Length; i++)
            if (p[i] >= 0) return false;
        return true;
    }

    private bool IsDotActive(ConfirmDot d)
    {
        if (d == null) return false;
        return (d.selectSelected && d.selectSelected.activeSelf) ||
               (d.selectConfirmed && d.selectConfirmed.activeSelf);
    }

    // Czyści cały formularz, ale nie zamyka panelu
    private void ResetFormToStart()
    {
        StopAllCoroutines();
        _checkCo = null;

        _processing = false;
        _onConfirmDot = false;

        _okNumber = _okCurPin = _okConfPin = _okPhrase = false;

        if (inputCardNumber) inputCardNumber.SetTextWithoutNotify("");

        // reset PIN
        for (int i = 0; i < pinLength; i++)
        {
            _curPin[i] = -1;
            _confPin[i] = -1;
        }
        _curIndex = 0;
        _confIndex = 0;

        // reset PHRASE
        _phraseIndex = 0;
        UpdatePhraseVisual();

        // reset checking
        SetChecking(checkingCardNumber, "");
        SetChecking(checkingCurrentPin, "");
        SetChecking(checkingConfirmPin, "");
        SetChecking(checkingPhrase, "");
        if (processText) processText.gameObject.SetActive(false);

        // reset dots
        ResetDot(dotCardNumber);
        ResetDot(dotCurPin);
        ResetDot(dotConfPin);
        ResetDot(dotPhrase);

        // wróć na start
        _step = Step.CardNumber;
        _buttonsIndex = 0;

        // odśwież sloty (ważne: usunie strzałki z poprzednich)
        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, active: false);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, active: false);

        FocusForStep();
        RefreshVisuals();
    }

    private void RefreshActivePinArrows()
    {
        // strzałka tylko wtedy gdy jesteś na danym steppie I nie jesteś na kółku
        RefreshPinSlots(currentPinContainer, _curPin, _curIndex, active: (_step == Step.CurrentPin) && !_onConfirmDot);
        RefreshPinSlots(confirmPinContainer, _confPin, _confIndex, active: (_step == Step.ConfirmPin) && !_onConfirmDot);
    }

    private void EnterDot()
    {
        _onConfirmDot = true;

        // zgaś strzałki pinów natychmiast
        RefreshActivePinArrows();

        SetDotSelected(GetDotForStep(_step), true);
        RefreshVisuals();
    }

    private void ExitDot()
    {
        _onConfirmDot = false;

        SetDotSelected(GetDotForStep(_step), false);

        // przywróć strzałkę tylko na aktywnym steppie
        RefreshActivePinArrows();

        FocusForStep();
        RefreshVisuals();
    }

    private bool CanConfirmNow()
    {
        bool allOk = _okNumber && _okCurPin && _okConfPin && _okPhrase;
        return !_lockedByInventory && allOk && !_processing;
    }
}