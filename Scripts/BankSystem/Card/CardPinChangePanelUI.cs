using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPinChangePanelUI : MonoBehaviour
{
    private enum Row
    {
        CurrentPin = 0,
        NewPin = 1,
        ConfirmPin = 2,
        Buttons = 3
    }

    private enum Btn
    {
        Save = 0,
        Cancel = 1,
        Back = 2
    }

    private enum VerifyState
    {
        None,
        Checking,
        Ok,
        Not
    }

    [System.Serializable]
    private class ConfirmDot
    {
        public Button button;
        public GameObject selectSelected;
        public GameObject selectConfirmed;
        public TMP_Text checkingText;
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Info")]
    [SerializeField] private GameObject infoNormal;
    [SerializeField] private GameObject infoAfterChanged;
    [SerializeField] private TMP_Text infoAfterChangedText;

    [Header("Section Selected")]
    [SerializeField] private GameObject selectedCurrentPinGO;
    [SerializeField] private GameObject selectedNewPinGO;
    [SerializeField] private GameObject selectedConfirmPinGO;

    [Header("Confirm Checking Dots")]
    [SerializeField] private ConfirmDot currentDot;
    [SerializeField] private ConfirmDot newDot;
    [SerializeField] private ConfirmDot confirmDot;

    [Header("PIN Containers")]
    [SerializeField] private Transform currentPinContainer;
    [SerializeField] private Transform newPinContainer;
    [SerializeField] private Transform confirmPinContainer;

    [Header("PIN Slot Prefab")]
    [SerializeField] private PinSlotView pinSlotPrefab;
    [SerializeField, Range(4, 4)] private int slotsPerPin = 4;

    [Header("Slot Colors")]
    [SerializeField] private Color slotIdleBorder = Color.white;
    [SerializeField] private Color slotSelectedBorder = Color.yellow;

    [Header("Buttons")]
    [SerializeField] private Button saveBtn;
    [SerializeField] private GameObject saveBlockedGO;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private GameObject cancelBlockedGO;
    [SerializeField] private Button backBtn;

    [Header("Processing")]
    [SerializeField] private GameObject processingTextGO;
    [SerializeField] private TMP_Text processingText;
    [SerializeField] private float verifyDelaySeconds = 5f;
    [SerializeField] private float saveProcessingSeconds = 5f;

    [Header("Rules")]
    [SerializeField] private int pinChangeCooldownMinutes = 1440;

    private BankCardOpsPanel _owner;
    private BankCardRecord _card;

    private readonly List<PinSlotView> _curSlots = new();
    private readonly List<PinSlotView> _newSlots = new();
    private readonly List<PinSlotView> _conSlots = new();

    private int[] _curDigits;
    private int[] _newDigits;
    private int[] _conDigits;

    private Row _row = Row.CurrentPin;
    private Btn _btn = Btn.Cancel;
    private int _slotIndex;
    private bool _onCheckDot;

    private VerifyState _vCur = VerifyState.None;
    private VerifyState _vNew = VerifyState.None;
    private VerifyState _vCon = VerifyState.None;

    private Coroutine _verifyCo;
    private Coroutine _saveCo;

    private int _cooldownRemain;

    private void Awake()
    {
        if (saveBtn)
        {
            saveBtn.onClick.RemoveAllListeners();
            saveBtn.onClick.AddListener(OnSave);
        }

        if (cancelBtn)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(OnCancel);
        }

        if (backBtn)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(OnBack);
        }

        BindConfirmDot(currentDot, Row.CurrentPin);
        BindConfirmDot(newDot, Row.NewPin);
        BindConfirmDot(confirmDot, Row.ConfirmPin);
    }

    private void BindConfirmDot(ConfirmDot dot, Row row)
    {
        if (dot == null || dot.button == null) return;

        dot.button.onClick.RemoveAllListeners();
        dot.button.onClick.AddListener(() => OnCheckDotPressed(row));
    }

    public void Open(BankCardOpsPanel owner, BankCardRecord card)
    {
        _owner = owner;
        _card = card;

        EnsureDigitArrays();
        EnsureBuilt();
        ResetAllInputs();

        Show(true);

        RefreshCooldownUI();

        if (_cooldownRemain > 0)
        {
            _row = Row.Buttons;
            _btn = Btn.Back;
        }
        else
        {
            _row = Row.CurrentPin;
            _btn = Btn.Cancel;
        }

        _slotIndex = 0;
        _onCheckDot = false;

        RefreshAll();
    }

    public void Close(bool goBackToMenu)
    {
        StopRunningCoroutines();
        Show(false);

        if (goBackToMenu && _owner != null)
            _owner.ReturnToMenuFromSubPanel();
    }

    private void Update()
    {
        if (!root || !root.gameObject.activeInHierarchy || root.alpha < 0.5f)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBack();
            return;
        }

        if (_verifyCo != null || _saveCo != null)
            return;

        if (_row == Row.Buttons)
        {
            HandleButtonsInput();
            return;
        }

        HandlePinSectionInput();
    }

    // =========================
    // Build / reset
    // =========================
    private void EnsureDigitArrays()
    {
        if (_curDigits == null || _curDigits.Length != slotsPerPin) _curDigits = new int[slotsPerPin];
        if (_newDigits == null || _newDigits.Length != slotsPerPin) _newDigits = new int[slotsPerPin];
        if (_conDigits == null || _conDigits.Length != slotsPerPin) _conDigits = new int[slotsPerPin];
    }

    private void EnsureBuilt()
    {
        EnsureContainerSlots(currentPinContainer, _curSlots);
        EnsureContainerSlots(newPinContainer, _newSlots);
        EnsureContainerSlots(confirmPinContainer, _conSlots);
    }

    private void EnsureContainerSlots(Transform container, List<PinSlotView> list)
    {
        list.Clear();

        if (!container)
        {
            Debug.LogError("[CardPinChangePanelUI] Missing PIN container reference.");
            return;
        }

        var existing = new List<PinSlotView>(container.GetComponentsInChildren<PinSlotView>(true));

        if (existing.Count != slotsPerPin)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            if (!pinSlotPrefab)
            {
                Debug.LogError("[CardPinChangePanelUI] Missing pinSlotPrefab.");
                return;
            }

            existing.Clear();

            for (int i = 0; i < slotsPerPin; i++)
            {
                var slot = Instantiate(pinSlotPrefab, container);
                slot.name = $"Pin_Slot ({i})";
                existing.Add(slot);
            }
        }

        existing.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        for (int i = 0; i < existing.Count && i < slotsPerPin; i++)
            list.Add(existing[i]);
    }

    private void ResetAllInputs()
    {
        EnsureDigitArrays();

        for (int i = 0; i < slotsPerPin; i++)
        {
            _curDigits[i] = -1;
            _newDigits[i] = -1;
            _conDigits[i] = -1;
        }

        _vCur = VerifyState.None;
        _vNew = VerifyState.None;
        _vCon = VerifyState.None;

        _row = Row.CurrentPin;
        _slotIndex = 0;
        _btn = Btn.Cancel;
        _onCheckDot = false;

        ClearDotTMP(currentDot);
        ClearDotTMP(newDot);
        ClearDotTMP(confirmDot);

        if (processingTextGO) processingTextGO.SetActive(false);
    }

    private void ClearDotTMP(ConfirmDot dot)
    {
        if (dot == null || dot.checkingText == null) return;

        dot.checkingText.text = "";
        dot.checkingText.color = Color.white;
    }

    private void StopRunningCoroutines()
    {
        if (_verifyCo != null)
        {
            StopCoroutine(_verifyCo);
            _verifyCo = null;
        }

        if (_saveCo != null)
        {
            StopCoroutine(_saveCo);
            _saveCo = null;
        }

        if (processingTextGO)
            processingTextGO.SetActive(false);
    }

    // =========================
    // Input
    // =========================
    private void HandlePinSectionInput()
    {
        var digits = GetDigitsForRow(_row);
        if (digits == null) return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _onCheckDot = false;
            MoveRow(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _onCheckDot = false;
            MoveRow(+1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (_onCheckDot)
            {
                _onCheckDot = false;
                RefreshAll();
                return;
            }

            MoveSlot(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (_onCheckDot)
            {
                OnCheckDotPressed(_row);
                return;
            }

            if (_slotIndex == slotsPerPin - 1 && IsRowComplete(_row))
            {
                _onCheckDot = true;
                RefreshAll();
                return;
            }

            MoveSlot(+1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_onCheckDot)
            {
                _onCheckDot = false;
                RefreshAll();
                return;
            }

            if (digits[_slotIndex] != -1)
            {
                digits[_slotIndex] = -1;
            }
            else if (_slotIndex > 0)
            {
                _slotIndex--;
                digits[_slotIndex] = -1;
            }

            SetVerifyState(_row, VerifyState.None);
            RefreshAll();
            return;
        }

        if (TryGetDigitDown(out int d))
        {
            SetDigit(_row, _slotIndex, d);

            if (_slotIndex < slotsPerPin - 1)
            {
                _slotIndex++;
            }
            else if (IsRowComplete(_row))
            {
                _onCheckDot = true;
            }

            RefreshAll();
            return;
        }

        if (IsConfirmPressed())
        {
            if (_onCheckDot)
            {
                OnCheckDotPressed(_row);
                return;
            }

            if (IsRowComplete(_row))
            {
                _onCheckDot = true;
                RefreshAll();
            }
        }
    }

    private void HandleButtonsInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveButton();
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveButton();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_btn == Btn.Back)
            {
                if (cancelBtn != null && cancelBtn.interactable)
                {
                    _btn = Btn.Cancel;
                    RefreshAll();
                    return;
                }

                if (CanSave())
                {
                    _btn = Btn.Save;
                    RefreshAll();
                    return;
                }
            }

            if (_btn == Btn.Cancel)
            {
                if (CanSave())
                {
                    _btn = Btn.Save;
                    RefreshAll();
                    return;
                }

                _row = Row.ConfirmPin;
                _slotIndex = Mathf.Clamp(_slotIndex, 0, slotsPerPin - 1);
                _onCheckDot = false;
                RefreshAll();
                return;
            }

            if (_btn == Btn.Save)
            {
                _row = Row.ConfirmPin;
                _slotIndex = Mathf.Clamp(_slotIndex, 0, slotsPerPin - 1);
                _onCheckDot = false;
                RefreshAll();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            return;
        }

        if (IsConfirmPressed())
        {
            ClickFocusedButton();
        }
    }

    // =========================
    // Verify
    // =========================
    private void OnCheckDotPressed(Row row)
    {
        if (_verifyCo != null || _saveCo != null) return;
        if (_cooldownRemain > 0) return;
        if (!IsRowComplete(row)) return;

        StartVerify(row);
        RefreshAll();
    }

    private void StartVerify(Row row)
    {
        if (_verifyCo != null)
            StopCoroutine(_verifyCo);

        _verifyCo = StartCoroutine(CoVerify(row));
    }

    private IEnumerator CoVerify(Row row)
    {
        SetVerifyState(row, VerifyState.Checking);
        RefreshAll();

        float t = 0f;
        int dots = 0;

        while (t < verifyDelaySeconds)
        {
            dots = (dots + 1) % 4;
            SetCheckingTMP(row, "CHECKING" + new string('.', dots));
            yield return new WaitForSecondsRealtime(0.5f);
            t += 0.5f;
        }

        bool ok = false;

        if (row == Row.CurrentPin)
        {
            int pin = DigitsToPin(_curDigits);
            ok = (_card != null && pin == _card.pin);
        }
        else if (row == Row.NewPin)
        {
            int currentPin = DigitsToPin(_curDigits);
            int newPin = DigitsToPin(_newDigits);
            ok = newPin != currentPin;
        }
        else if (row == Row.ConfirmPin)
        {
            int newPin = DigitsToPin(_newDigits);
            int confirmPin = DigitsToPin(_conDigits);
            ok = newPin == confirmPin;
        }

        SetVerifyState(row, ok ? VerifyState.Ok : VerifyState.Not);

        _verifyCo = null;
        RefreshAll();
    }

    // =========================
    // Buttons
    // =========================
    public void OnSave()
    {
        if (_saveCo != null) return;
        if (!CanSave()) return;

        _saveCo = StartCoroutine(CoSave());
    }

    public void OnCancel()
    {
        if (_cooldownRemain > 0)
        {
            _btn = Btn.Back;
            RefreshAll();
            return;
        }

        ResetAllInputs();
        RefreshAll();
    }

    public void OnBack()
    {
        Close(true);
    }

    private IEnumerator CoSave()
    {
        if (processingTextGO) processingTextGO.SetActive(true);

        float t = 0f;
        int dots = 0;

        while (t < saveProcessingSeconds)
        {
            dots = (dots + 1) % 4;

            if (processingText)
                processingText.text = "PROCESSING" + new string('.', dots);

            yield return new WaitForSecondsRealtime(0.5f);
            t += 0.5f;
        }

        bool success = false;

        var bank = BankSystem.Instance;
        if (bank != null && _card != null)
        {
            int currentPin = DigitsToPin(_curDigits);
            int newPin = DigitsToPin(_newDigits);

            if (bank.TryChangeCardPin(_card.cardId, currentPin, newPin, pinChangeCooldownMinutes, out _))
            {
                if (bank.TryGetCard(_card.cardId, out var rec) && rec != null)
                {
                    _card = rec;
                    _owner?.SetCardRecord(_card);
                }

                success = true;
            }
        }

        if (processingTextGO) processingTextGO.SetActive(false);
        _saveCo = null;

        if (success)
        {
            RefreshCooldownUI();
            ResetAllInputs();

            _row = Row.Buttons;
            _btn = Btn.Back;
            _onCheckDot = false;

            RefreshAll();
            yield break;
        }

        RefreshAll();
    }

    // =========================
    // Refresh
    // =========================
    private void RefreshAll()
    {
        RefreshCooldownUI();

        bool canAttempt = _cooldownRemain <= 0;

        if (infoNormal) infoNormal.SetActive(canAttempt);
        if (infoAfterChanged) infoAfterChanged.SetActive(!canAttempt);

        RefreshSectionSelected();

        RefreshSlots(_curSlots, _curDigits, _row == Row.CurrentPin && !_onCheckDot);
        RefreshSlots(_newSlots, _newDigits, _row == Row.NewPin && !_onCheckDot);
        RefreshSlots(_conSlots, _conDigits, _row == Row.ConfirmPin && !_onCheckDot);

        RefreshConfirmDot(
            currentDot,
            _vCur,
            _row == Row.CurrentPin,
            canAttempt && IsRowComplete(Row.CurrentPin)
        );

        RefreshConfirmDot(
            newDot,
            _vNew,
            _row == Row.NewPin,
            canAttempt && IsRowComplete(Row.NewPin)
        );

        RefreshConfirmDot(
            confirmDot,
            _vCon,
            _row == Row.ConfirmPin,
            canAttempt && IsRowComplete(Row.ConfirmPin)
        );

        bool canSave = CanSave();
        bool canCancel = HasAnyInputOrState() && _cooldownRemain <= 0;

        if (saveBtn) saveBtn.interactable = canSave;
        if (saveBlockedGO) saveBlockedGO.SetActive(!canSave);

        if (cancelBtn) cancelBtn.interactable = canCancel;
        if (cancelBlockedGO) cancelBlockedGO.SetActive(!canCancel);

        ApplyFocusToEventSystem();
    }

    private void RefreshSectionSelected()
    {
        if (selectedCurrentPinGO)
            selectedCurrentPinGO.SetActive(_row == Row.CurrentPin);

        if (selectedNewPinGO)
            selectedNewPinGO.SetActive(_row == Row.NewPin);

        if (selectedConfirmPinGO)
            selectedConfirmPinGO.SetActive(_row == Row.ConfirmPin);
    }

    private void RefreshSlots(List<PinSlotView> slots, int[] digits, bool isActiveRow)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (!s) continue;

            s.SetDigit(digits[i]);

            bool selected = isActiveRow && i == _slotIndex;
            s.SetActive(selected, slotIdleBorder, slotSelectedBorder);
            s.SetArrow(selected);
        }
    }

    private void RefreshConfirmDot(ConfirmDot dot, VerifyState state, bool activeRow, bool canPress)
    {
        if (dot == null) return;

        if (dot.selectSelected)
            dot.selectSelected.SetActive(activeRow && _onCheckDot && state != VerifyState.Ok);

        if (dot.selectConfirmed)
            dot.selectConfirmed.SetActive(state == VerifyState.Ok);

        if (dot.button)
            dot.button.interactable = canPress;

        if (dot.checkingText)
        {
            if (state == VerifyState.None)
            {
                dot.checkingText.text = "";
                dot.checkingText.color = Color.white;
            }
            else if (state == VerifyState.Ok)
            {
                dot.checkingText.text = "OK";
                dot.checkingText.color = Color.green;
            }
            else if (state == VerifyState.Not)
            {
                dot.checkingText.text = "NOT";
                dot.checkingText.color = Color.red;
            }
        }
    }

    private void RefreshCooldownUI()
    {
        var bank = BankSystem.Instance;
        _cooldownRemain = (bank != null && _card != null)
            ? bank.GetPinChangeCooldownRemainingMinutes(_card.cardId, pinChangeCooldownMinutes)
            : 0;

        if (infoAfterChangedText)
        {
            int mm = Mathf.Max(0, _cooldownRemain);
            int hh = mm / 60;
            int rem = mm % 60;
            infoAfterChangedText.text = $"{hh:00}:{rem:00}";
        }
    }

    private void ApplyFocusToEventSystem()
    {
        if (!EventSystem.current) return;

        if (_row == Row.Buttons)
        {
            Button b = null;

            if (_btn == Btn.Save) b = saveBtn;
            else if (_btn == Btn.Cancel) b = cancelBtn;
            else if (_btn == Btn.Back) b = backBtn;

            if (b) EventSystem.current.SetSelectedGameObject(b.gameObject);
            return;
        }

        if (_onCheckDot)
        {
            if (_row == Row.CurrentPin && currentDot != null && currentDot.button)
            {
                EventSystem.current.SetSelectedGameObject(currentDot.button.gameObject);
                return;
            }

            if (_row == Row.NewPin && newDot != null && newDot.button)
            {
                EventSystem.current.SetSelectedGameObject(newDot.button.gameObject);
                return;
            }

            if (_row == Row.ConfirmPin && confirmDot != null && confirmDot.button)
            {
                EventSystem.current.SetSelectedGameObject(confirmDot.button.gameObject);
                return;
            }
        }

        EventSystem.current.SetSelectedGameObject(null);
    }

    // =========================
    // Navigation helpers
    // =========================
    private void MoveRow(int dir)
    {
        int next = Mathf.Clamp((int)_row + dir, 0, 3);
        _row = (Row)next;
        _onCheckDot = false;

        if (_row == Row.Buttons)
        {
            if (CanSave()) _btn = Btn.Save;
            else if (cancelBtn != null && cancelBtn.interactable) _btn = Btn.Cancel;
            else _btn = Btn.Back;
        }
        else
        {
            _slotIndex = Mathf.Clamp(_slotIndex, 0, slotsPerPin - 1);
        }

        RefreshAll();
    }

    private void MoveSlot(int dir)
    {
        _slotIndex = Mathf.Clamp(_slotIndex + dir, 0, slotsPerPin - 1);
        RefreshAll();
    }

    private void MoveButton()
    {
        if (_btn == Btn.Save)
        {
            _btn = Btn.Cancel;
        }
        else if (_btn == Btn.Cancel)
        {
            _btn = Btn.Back;
        }
        else if (_btn == Btn.Back)
        {
            _btn = Btn.Cancel;
        }

        if (_btn == Btn.Save && !CanSave())
            _btn = Btn.Cancel;

        if (_btn == Btn.Cancel && (cancelBtn == null || !cancelBtn.interactable))
            _btn = Btn.Back;

        RefreshAll();
    }

    private void ClickFocusedButton()
    {
        if (_btn == Btn.Save) OnSave();
        else if (_btn == Btn.Cancel) OnCancel();
        else if (_btn == Btn.Back) OnBack();
    }

    // =========================
    // State helpers
    // =========================
    private int[] GetDigitsForRow(Row row)
    {
        if (row == Row.CurrentPin) return _curDigits;
        if (row == Row.NewPin) return _newDigits;
        if (row == Row.ConfirmPin) return _conDigits;
        return null;
    }

    private void SetVerifyState(Row row, VerifyState state)
    {
        if (row == Row.CurrentPin) _vCur = state;
        if (row == Row.NewPin) _vNew = state;
        if (row == Row.ConfirmPin) _vCon = state;
    }

    private void SetCheckingTMP(Row row, string msg)
    {
        if (row == Row.CurrentPin && currentDot != null && currentDot.checkingText)
            currentDot.checkingText.text = msg;

        if (row == Row.NewPin && newDot != null && newDot.checkingText)
            newDot.checkingText.text = msg;

        if (row == Row.ConfirmPin && confirmDot != null && confirmDot.checkingText)
            confirmDot.checkingText.text = msg;
    }

    private void SetDigit(Row row, int index, int digit)
    {
        digit = Mathf.Clamp(digit, 0, 9);

        if (row == Row.CurrentPin) _curDigits[index] = digit;
        if (row == Row.NewPin) _newDigits[index] = digit;
        if (row == Row.ConfirmPin) _conDigits[index] = digit;

        SetVerifyState(row, VerifyState.None);
    }

    private bool IsRowComplete(Row row)
    {
        if (row == Row.CurrentPin) return IsDigitsComplete(_curDigits);
        if (row == Row.NewPin) return IsDigitsComplete(_newDigits);
        if (row == Row.ConfirmPin) return IsDigitsComplete(_conDigits);
        return false;
    }

    private bool IsDigitsComplete(int[] digits)
    {
        if (digits == null || digits.Length < slotsPerPin) return false;

        for (int i = 0; i < slotsPerPin; i++)
        {
            if (digits[i] < 0)
                return false;
        }

        return true;
    }

    private bool HasAnyInputOrState()
    {
        return HasAnyDigits(_curDigits) ||
               HasAnyDigits(_newDigits) ||
               HasAnyDigits(_conDigits) ||
               _vCur != VerifyState.None ||
               _vNew != VerifyState.None ||
               _vCon != VerifyState.None;
    }

    private bool HasAnyDigits(int[] digits)
    {
        if (digits == null) return false;

        for (int i = 0; i < digits.Length; i++)
        {
            if (digits[i] >= 0)
                return true;
        }

        return false;
    }

    private bool CanSave()
    {
        if (_cooldownRemain > 0) return false;
        if (!IsRowComplete(Row.CurrentPin)) return false;
        if (!IsRowComplete(Row.NewPin)) return false;
        if (!IsRowComplete(Row.ConfirmPin)) return false;
        if (_vCur != VerifyState.Ok) return false;
        if (_vNew != VerifyState.Ok) return false;
        if (_vCon != VerifyState.Ok) return false;
        return true;
    }

    private int DigitsToPin(int[] d)
    {
        int a = Mathf.Clamp(d[0], 0, 9);
        int b = Mathf.Clamp(d[1], 0, 9);
        int c = Mathf.Clamp(d[2], 0, 9);
        int e = Mathf.Clamp(d[3], 0, 9);
        return a * 1000 + b * 100 + c * 10 + e;
    }

    private static bool TryGetDigitDown(out int d)
    {
        d = -1;

        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { d = 0; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { d = 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { d = 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { d = 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { d = 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { d = 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { d = 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { d = 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { d = 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { d = 9; return true; }

        return false;
    }

    private static bool IsConfirmPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
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
}