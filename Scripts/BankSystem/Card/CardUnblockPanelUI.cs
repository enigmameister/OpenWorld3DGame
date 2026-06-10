using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardUnblockPanelUI : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private BankCardOpsPanel owner;

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Select Menu")]
    [SerializeField] private CanvasGroup selectMenuRoot;
    [SerializeField] private Button btnPinFailed;
    [SerializeField] private Button btnOwnerBlocked;
    [SerializeField] private Button btnBackSelect;

    [SerializeField] private GameObject selectedPinMarker;      // Selected#Pin
    [SerializeField] private GameObject selectedBlockedMarker;  // Selected#Blocked
    [SerializeField] private GameObject selectedBackMarker;     // Selected#Back

    [Header("Blocked overlays (gray image)")]
    [SerializeField] private GameObject pinFailedBlockedOverlay;     // szary overlay na 3x FAIL PIN
    [SerializeField] private GameObject ownerBlockedBlockedOverlay;  // szary overlay na I BLOCKED

    [Header("Select Menu UI")]
    [SerializeField] private TMP_Text reasonText;

    [Header("3xPIN_Failed Panel")]
    [SerializeField] private CanvasGroup pinFailedRoot;
    [SerializeField] private TMP_InputField dayInput;
    [SerializeField] private TMP_InputField monthInput;
    [SerializeField] private TMP_InputField hourInput;
    [SerializeField] private TMP_InputField minuteInput;
    [SerializeField] private Button btnConfirmPinFailed;
    [SerializeField] private Button btnCancelPinFailed;
    [SerializeField] private Button btnBackPinFailed;
    [SerializeField] private TMP_Text statusTextPinFailed;

    [Header("Blocked_MySelf Panel")]
    [SerializeField] private CanvasGroup ownerBlockedRoot;
    [SerializeField] private Button btnConfirmOwnerBlocked;
    [SerializeField] private Button btnCancelOwnerBlocked;
    [SerializeField] private Button btnBackOwnerBlocked;
    [SerializeField] private TMP_Text statusTextOwnerBlocked;

    [Header("Rules")]
    [SerializeField] private int toleranceMinutes = 120;

    // ===== OWNER BLOCKED (UNBLOCK) NAV STATE =====
    private enum OwnerRow { Top, Bottom }
    private enum OwnerTopBtn { OwnerBlocked, BackTop }          // I BLOCKED / BACK (górny)
    private enum OwnerBottomBtn { Confirm, Cancel, BackBottom } // CONFIRM / CANCEL / BACK (dolny)

    private OwnerRow _ownerRow = OwnerRow.Bottom;
    private OwnerTopBtn _ownerTop = OwnerTopBtn.OwnerBlocked;
    private OwnerBottomBtn _ownerBottom = OwnerBottomBtn.Confirm;

    // blokada na "Enter trzymany" przy wejściu z klawiatury (żeby nie odpalić CONFIRM od razu)
    private bool _ownerSuppressConfirmUntilEnterUp;

    private BankCardRecord _card;

    private enum Mode { Select, PinFailed, OwnerBlocked }
    private Mode _mode = Mode.Select;

    private Button[] _selectButtons;
    private int _selectIndex;

    private bool _isOpen;
    private bool _prevSendNavEvents;


    private Coroutine _processingCo;
    private bool _isProcessing;

    private void Awake()
    {
        // clicki (myszka może działać, ale klawiatura jest główna)
        if (btnPinFailed) btnPinFailed.onClick.AddListener(OnSelect_PinFailed);
        if (btnOwnerBlocked) btnOwnerBlocked.onClick.AddListener(OnSelect_OwnerBlocked);
        if (btnBackSelect) btnBackSelect.onClick.AddListener(OnSelect_Back);

        if (btnConfirmPinFailed) btnConfirmPinFailed.onClick.AddListener(OnPinFailed_Confirm);
        if (btnCancelPinFailed) btnCancelPinFailed.onClick.AddListener(OnPinFailed_Cancel);
        if (btnBackPinFailed) btnBackPinFailed.onClick.AddListener(OnPinFailed_Back);

        if (btnConfirmOwnerBlocked) btnConfirmOwnerBlocked.onClick.AddListener(OnOwnerBlocked_Confirm);
        if (btnCancelOwnerBlocked) btnCancelOwnerBlocked.onClick.AddListener(OnOwnerBlocked_Cancel);
        if (btnBackOwnerBlocked) btnBackOwnerBlocked.onClick.AddListener(OnOwnerBlocked_Back);

        if (pinFailedRoot) pinFailedRoot.gameObject.SetActive(true);
        if (ownerBlockedRoot) ownerBlockedRoot.gameObject.SetActive(true);

        SetNavNone(btnPinFailed);
        SetNavNone(btnOwnerBlocked);
        SetNavNone(btnBackSelect);

        // Startowo ukryj je alpha
        ShowCG(pinFailedRoot, false);
        ShowCG(ownerBlockedRoot, false);
        EnsureSelectButtons();
        SetupPinInputs();

        // start: nic nie pokazuj i nie słuchaj inputu
        _selectButtons = new[] { btnPinFailed, btnOwnerBlocked, btnBackSelect };

        HardHideAll();
        enabled = false;
    }

    private void OnDisable()
    {
        _isOpen = false;
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = _prevSendNavEvents;
    }

    // -------------------- PUBLIC API --------------------
    public void Open(BankCardOpsPanel ownerPanel, BankCardRecord card)
    {
        owner = ownerPanel;
        _card = card;

        EnsureSelectButtons();

        // od teraz panel ma prawo obsługiwać input
        _isOpen = true;
        enabled = true;

        if (EventSystem.current != null)
        {
            _prevSendNavEvents = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = true; // <-- WŁĄCZ
        }

        // HARD RESET (zawsze)
        ShowRoot(true);
        ShowCG(selectMenuRoot, true);
        ShowCG(pinFailedRoot, false);
        ShowCG(ownerBlockedRoot, false);

        SetStatus(statusTextPinFailed, "");
        SetStatus(statusTextOwnerBlocked, "");

        _mode = Mode.Select;

        UpdateSelectInteractables(); // ustawia interactable + overlay
        RefreshReasonText();

        _selectIndex = GetDefaultIndex();     // <- nie FirstSelectableIndex jeśli chcesz BACK jako domyślny
        UpdateSelectedMarkers();
        ApplySelectFocus();
        DebugSelected("OPEN");

        if (_selectIndex == 2 && btnBackSelect)
            StartCoroutine(SelectNextFrame(btnBackSelect.gameObject));

        ForceButtonVisualState();
    }

    private static void SetExplicitNav(Button b, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (!b) return;
        var nav = new Navigation { mode = Navigation.Mode.Explicit };
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        b.navigation = nav;
    }

    public void Close(bool goBackToOwnerMenu)
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = _prevSendNavEvents;

        _isOpen = false;
        enabled = false;

        // schowaj tylko wizualnie (owner i tak kontroluje CanvasGroup / aktywność)
        ShowRoot(false);
        HardHideAll();

        if (goBackToOwnerMenu && owner != null)
            owner.ReturnToMenuFromSubPanel();
    }

    // -------------------- UPDATE / INPUT --------------------
    private void Update()
    {
        if (!_isOpen) return;
        if (!IsVisible()) return;

        // --- ESC ---
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_mode != Mode.Select)
            {
                OpenSelectMenu();
                return;
            }

            Close(true);
            return;
        }

        bool enter = IsEnterPressed();
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);

        if (enter) DebugSelected("ENTER");

        switch (_mode)
        {
            case Mode.Select:
                {
                    bool left = Input.GetKeyDown(KeyCode.LeftArrow);
                    bool right = Input.GetKeyDown(KeyCode.RightArrow);

                    if (up || left) MoveSelect(-1);
                    if (down || right) MoveSelect(+1);

                    SyncIndexFromEventSystem();
                    UpdateSelectedMarkers();

                    if (enter)
                    {
                        if (_selectIndex == 0) OnSelect_PinFailed();
                        else if (_selectIndex == 1)
                        {
                            // ENTER na I BLOCKED (z klawiatury) -> otwórz UNBLOCK, ale zablokuj confirm dopóki enter puszczony
                            OpenOwnerBlockedPanel(fromKeyboard: true);
                        }
                        else OnSelect_Back();
                    }
                    break;
                }

            case Mode.PinFailed:
                {
                    HandlePinFailedInputNavigation();
                    break;
                }

            case Mode.OwnerBlocked:
                {
                    if (_isProcessing) break;

                    bool left = Input.GetKeyDown(KeyCode.LeftArrow);
                    bool right = Input.GetKeyDown(KeyCode.RightArrow);

                    // zdejmij blokadę dopiero gdy Enter puszczony
                    if (_ownerSuppressConfirmUntilEnterUp)
                    {
                        bool enterHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
                        if (!enterHeld) _ownerSuppressConfirmUntilEnterUp = false;
                    }

                    if (up) OwnerBlocked_MoveUp();
                    if (down) OwnerBlocked_MoveDown();
                    if (left) OwnerBlocked_MoveLeft();
                    if (right) OwnerBlocked_MoveRight();

                    if (enter) OwnerBlocked_Activate();

                    break;
                }
        }
    }

    private static bool IsEnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    // -------------------- FLOW --------------------
    private void OpenSelectMenu()
    {
        _mode = Mode.Select;

        LockSelectMenu(false);     // widoczne i aktywne
        ShowCG(pinFailedRoot, false);
        ShowCG(ownerBlockedRoot, false);

        UpdateSelectInteractablesAndOverlays();
        RefreshReasonText();

        _selectIndex = GetDefaultIndex();
        UpdateSelectedMarkers();
        ApplySelectFocus();
    }

    private int GetDefaultIndex()
    {
        // 0=PinFail, 1=OwnerBlocked, 2=Back
        if (_card == null) return 2;

        if (!IsCardBlockedByStatus()) return 2; // tylko BACK, bo karta NIE jest zablokowana

        if (_card.blockReason == BankCardBlockReason.PinFail3x && btnPinFailed != null && btnPinFailed.interactable)
            return 0;

        if (_card.blockReason == BankCardBlockReason.OwnerBlocked && btnOwnerBlocked != null && btnOwnerBlocked.interactable)
            return 1;

        return 2;
    }

    private void OpenPinFailedPanel()
    {
        _mode = Mode.PinFailed;

        LockSelectMenu(true);          // zostaje widoczne, ale zablokowane
        ShowCG(pinFailedRoot, true);
        ShowCG(ownerBlockedRoot, false);

        if (btnConfirmPinFailed) btnConfirmPinFailed.interactable = false;
        SetStatus(statusTextPinFailed, "");

        // Ustaw nawigację w subpanelu PIN FAILED (Left/Right)
        SetExplicitNav(btnConfirmPinFailed, up: null, down: null, left: null, right: btnCancelPinFailed);
        SetExplicitNav(btnCancelPinFailed, up: null, down: null, left: btnConfirmPinFailed, right: btnBackPinFailed);
        SetExplicitNav(btnBackPinFailed, up: null, down: null, left: btnCancelPinFailed, right: null);

        if (btnPinFailed && btnConfirmPinFailed)
        {
            var navTop = new Navigation { mode = Navigation.Mode.Explicit };
            navTop.selectOnUp = null;
            navTop.selectOnDown = btnConfirmPinFailed;
            btnPinFailed.navigation = navTop;

            var navConfirm = btnConfirmPinFailed.navigation;
            navConfirm.mode = Navigation.Mode.Explicit;
            navConfirm.selectOnUp = btnPinFailed;
            navConfirm.selectOnRight = btnCancelPinFailed;
            btnConfirmPinFailed.navigation = navConfirm;
        }
        if (_card == null || !IsCardBlockedByStatus() || _card.blockReason != BankCardBlockReason.PinFail3x)
        {
            SetStatus(statusTextPinFailed, "CARD NOT PIN-BLOCKED");
            return;
        }

        if (EventSystem.current != null)
        {
            if (dayInput != null) EventSystem.current.SetSelectedGameObject(dayInput.gameObject);
            else if (btnConfirmPinFailed != null) EventSystem.current.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
        }

        if (EventSystem.current != null && dayInput != null)
        {
            dayInput.ActivateInputField();
            EventSystem.current.SetSelectedGameObject(dayInput.gameObject);
        }
        // Auto select CONFIRM (następna klatka)
        if (btnConfirmPinFailed) StartCoroutine(SelectNextFrame(btnConfirmPinFailed.gameObject));
    }

    private void OpenOwnerBlockedPanel(bool fromKeyboard)
    {
        _mode = Mode.OwnerBlocked;

        LockSelectMenu(true);
        ShowCG(pinFailedRoot, false);
        ShowCG(ownerBlockedRoot, true);

        // STATUS domyślnie schowany
        if (statusTextOwnerBlocked)
        {
            statusTextOwnerBlocked.text = "";
            statusTextOwnerBlocked.gameObject.SetActive(false);
        }

        // warunki
        if (_card == null || !IsCardBlockedByStatus() || _card.blockReason != BankCardBlockReason.OwnerBlocked)
        {
            if (statusTextOwnerBlocked)
            {
                statusTextOwnerBlocked.gameObject.SetActive(true);
                statusTextOwnerBlocked.text = "CARD BLOCKED";
            }
            if (btnConfirmOwnerBlocked) btnConfirmOwnerBlocked.interactable = false;
            return;
        }

        // start na dole: CONFIRM
        _ownerRow = OwnerRow.Bottom;
        _ownerBottom = OwnerBottomBtn.Confirm;
        _ownerTop = OwnerTopBtn.OwnerBlocked;

        // KLUCZ: blokuj confirm dopóki Enter puszczony (niezależnie czy fromKeyboard)
        bool enterHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        _ownerSuppressConfirmUntilEnterUp = fromKeyboard || enterHeld;

        RefreshOwnerBlockedStatusPreview();
        OwnerBlocked_ApplyFocus();
    }

    // -------------------- SELECT MENU NAV --------------------
    private void MoveSelect(int delta)
    {
        if (_selectButtons == null || _selectButtons.Length == 0) return;

        int count = _selectButtons.Length;

        for (int tries = 0; tries < count; tries++)
        {
            _selectIndex = (_selectIndex + delta + count) % count;

            var b = _selectButtons[_selectIndex];
            if (b != null && b.interactable && b.gameObject.activeInHierarchy)
                break;
        }

        UpdateSelectedMarkers();
        ApplySelectFocus();
        ForceButtonVisualState();
    }

    private void ApplySelectFocus()
    {
        var es = EventSystem.current;
        if (es == null) return;

        if (_selectButtons == null || _selectButtons.Length == 0) return;
        if (_selectIndex < 0 || _selectIndex >= _selectButtons.Length) return;

        var b = _selectButtons[_selectIndex];
        if (!b) return;

        es.SetSelectedGameObject(b.gameObject);
    }

    private void ForceButtonVisualState()
    {
        if (EventSystem.current == null) return;

        var b = (_selectIndex >= 0 && _selectIndex < _selectButtons.Length) ? _selectButtons[_selectIndex] : null;
        if (b == null) return;

        // bez nullowania!
        StartCoroutine(SelectNextFrame(b.gameObject));
    }

    private IEnumerator SelectNextFrame(GameObject go)
    {
        yield return null;
        if (_isOpen && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private void UpdateSelectedMarkers()
    {
        bool pinActive = btnPinFailed != null && btnPinFailed.interactable;
        bool ownerActive = btnOwnerBlocked != null && btnOwnerBlocked.interactable;

        if (selectedPinMarker) selectedPinMarker.SetActive(pinActive && _selectIndex == 0);
        if (selectedBlockedMarker) selectedBlockedMarker.SetActive(ownerActive && _selectIndex == 1);
        if (selectedBackMarker) selectedBackMarker.SetActive(_selectIndex == 2);
    }

    private void UpdateSelectInteractablesAndOverlays()
    {
        bool isBlocked = IsCardBlockedByStatus();

        bool canPin = isBlocked && _card.blockReason == BankCardBlockReason.PinFail3x;
        bool canOwner = isBlocked && _card.blockReason == BankCardBlockReason.OwnerBlocked;

        if (btnPinFailed) btnPinFailed.interactable = canPin;
        if (btnOwnerBlocked) btnOwnerBlocked.interactable = canOwner;
        if (btnBackSelect) btnBackSelect.interactable = true;

        if (pinFailedBlockedOverlay) pinFailedBlockedOverlay.SetActive(!canPin);
        if (ownerBlockedBlockedOverlay) ownerBlockedBlockedOverlay.SetActive(!canOwner);
    }

    // -------------------- BUTTON HOOKS --------------------
    public void OnSelect_PinFailed()
    {
        if (btnPinFailed != null && btnPinFailed.interactable) OpenPinFailedPanel();
    }

    public void OnSelect_OwnerBlocked()
    {
        if (btnOwnerBlocked != null && btnOwnerBlocked.interactable)
            OpenOwnerBlockedPanel(fromKeyboard: false);
    }

    public void OnSelect_Back() => Close(goBackToOwnerMenu: true);

    // PIN FAILED panel buttons
    public void OnPinFailed_Back()
    {
        StopProcessingIfAny();
        OpenSelectMenu();
    }

    public void OnPinFailed_Cancel()
    {
        StopProcessingIfAny();
        OpenSelectMenu();
    }

    public void OnPinFailed_Confirm()
    {
        if (_isProcessing) return;
        if (btnConfirmPinFailed && !btnConfirmPinFailed.interactable) return;

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        if (_processingCo != null) StopCoroutine(_processingCo);
        _processingCo = StartCoroutine(ProcessPinFailedUnblock());
    }

    // OWNER BLOCKED panel buttons
    public void OnOwnerBlocked_Cancel()
    {
        StopProcessingIfAny();
        OpenSelectMenu();           // wraca do CARD BLOCK
    }

    public void OnOwnerBlocked_Back()
    {
        StopProcessingIfAny();
        Close(goBackToOwnerMenu: true);   // zamyka cały CardUnblockPanelUI
    }

    public void OnOwnerBlocked_Confirm()
    {
        if (_isProcessing) return;
        if (btnConfirmOwnerBlocked && !btnConfirmOwnerBlocked.interactable) return;

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        if (statusTextOwnerBlocked)
            statusTextOwnerBlocked.gameObject.SetActive(true);

        if (_processingCo != null) StopCoroutine(_processingCo);
        _processingCo = StartCoroutine(ProcessOwnerUnblock());
    }

    private void StopProcessingIfAny()
    {
        if (_processingCo != null)
        {
            StopCoroutine(_processingCo);
            _processingCo = null;
        }
        _isProcessing = false;
    }

    private IEnumerator ProcessOwnerUnblock()
    {
        _isProcessing = true;
        SetOwnerBlockedButtonsEnabled(false);

        float t = 0f;
        int dots = 0;

        while (t < 5f)
        {
            dots = (dots + 1) % 4;
            SetStatus(statusTextOwnerBlocked, "PROCESSING" + new string('.', dots));
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
        }

        if (_card == null)
        {
            SetStatus(statusTextOwnerBlocked, "NO CARD");
            _isProcessing = false;
            SetOwnerBlockedButtonsEnabled(true);
            _processingCo = null;
            yield break;
        }

        var bank = BankSystem.Instance;
        if (bank == null)
        {
            SetStatus(statusTextOwnerBlocked, "NO BANK");
            _isProcessing = false;
            SetOwnerBlockedButtonsEnabled(true);
            _processingCo = null;
            yield break;
        }

        bool success = ConfirmOwnerBlocked(out string resultMsg);

        if (success)
        {
            SetStatus(statusTextOwnerBlocked, "OK");
            owner?.SetCardRecord(_card);

            // odśwież rekord (NIE deklaruj drugi raz "bank")
            if (bank.TryGetCard(_card.cardId, out var fresh) && fresh != null)
                _card = fresh;

            yield return new WaitForSeconds(0.25f);

            OpenSelectMenu();  // wróć do CARD BLOCK (z samym BACK jeśli już odblokowana)

            _isProcessing = false;
            _processingCo = null;
            yield break;       // <<< zamiast "break"
        }
        else
        {
            SetStatus(statusTextOwnerBlocked, resultMsg);
            RefreshOwnerBlockedStatusPreview();
            SetOwnerBlockedButtonsEnabled(true);
            ApplyOwnerBlockedFocus();

            _isProcessing = false;
            _processingCo = null;
            yield break;
        }
    }

    private bool CanConfirmPinFailed()
    {
        return TryParseInt(dayInput, out var d) && d >= 1 && d <= 31
            && TryParseInt(monthInput, out var m) && m >= 1 && m <= 12
            && TryParseInt(hourInput, out var h) && h >= 0 && h <= 23
            && TryParseInt(minuteInput, out var min) && min >= 0 && min <= 59;
    }
    // -------------------- PIN FAILED LOGIC --------------------
    public void OnPinFailedInputsChanged()
    {
        if (_isProcessing) return;
        if (btnConfirmPinFailed)
            btnConfirmPinFailed.interactable = CanConfirmPinFailed();
    }

    private bool ConfirmPinFailed(out string msg)
    {
        msg = "ERROR";

        if (_card == null) { msg = "NO CARD"; return false; }
        var bank = BankSystem.Instance;
        if (bank == null) { msg = "NO BANK"; return false; }

        if (!CanConfirmPinFailed()) { msg = "WRONG INPUT"; return false; }

        TryParseInt(dayInput, out int inputDay);
        TryParseInt(monthInput, out int inputMonth);
        TryParseInt(hourInput, out int inputHour);
        TryParseInt(minuteInput, out int inputMinute);

        // 1) dzień + miesiąc muszą być dokładne
        if (_card.blockedDay != inputDay || _card.blockedMonth != inputMonth)
        {
            msg = "WRONG DATE";
            return false;
        }

        // 2) tolerancja czasu w minutach z wrapem 24h
        int realMinutes = Mathf.Clamp(_card.blockedHour, 0, 23) * 60 + Mathf.Clamp(_card.blockedMinute, 0, 59);
        int inputMinutes = Mathf.Clamp(inputHour, 0, 23) * 60 + Mathf.Clamp(inputMinute, 0, 59);

        int diff = Mathf.Abs(realMinutes - inputMinutes);
        int wrappedDiff = Mathf.Min(diff, 1440 - diff); // wrap przez północ

        // toleranceMinutes ustaw na 180 dla ±3h
        if (wrappedDiff > toleranceMinutes)
        {
            msg = "TIME OUT OF RANGE";
            return false;
        }

        // 3) odblokowanie (bank ma własną weryfikację)
        bool success = bank.TryUnblockCard_PinFail3x(
            _card.cardId,
            inputDay, inputMonth, inputHour, inputMinute,
            toleranceMinutes,
            out string reason
        );

        msg = success ? "OK" : reason.ToUpper();
        return success;
    }

    // -------------------- OWNER BLOCKED LOGIC --------------------
    private void RefreshOwnerBlockedStatusPreview()
    {
        if (_card == null) { SetStatus(statusTextOwnerBlocked, "NO CARD"); return; }

        const int LIMIT = 3;
        int remain = Mathf.Max(0, LIMIT - _card.ownerBlockCount);

        SetStatus(statusTextOwnerBlocked, remain > 0 ? "STATUS: OK" : "STATUS: LIMIT");
        if (btnConfirmOwnerBlocked) btnConfirmOwnerBlocked.interactable = remain > 0;
    }

    private void ApplyOwnerBlockedFocus()
    {
        if (EventSystem.current != null && btnConfirmOwnerBlocked != null)
            EventSystem.current.SetSelectedGameObject(btnConfirmOwnerBlocked.gameObject);
    }

    private bool ConfirmOwnerBlocked(out string msg)
    {
        msg = "ERROR";

        if (_card == null) { msg = "NO CARD"; return false; }
        var bank = BankSystem.Instance;
        if (bank == null) { msg = "NO BANK"; return false; }

        bool success = bank.TryUnblockCard_OwnerBlocked(_card.cardId, out var reason, out var remaining);
        msg = success ? "OK" : reason.ToUpper();
        return success;
    }

    // -------------------- TEXT / STATUS --------------------
    private void RefreshReasonText()
    {
        if (!reasonText) return;

        if (_card == null)
        {
            reasonText.text = "NO CARD";
            reasonText.color = Color.red;
            return;
        }

        if (!IsCardBlockedByStatus())
        {
            reasonText.text = "CARD NOT BLOCKED";
            reasonText.color = Color.green;
            return;
        }

        // jeśli status BLOCKED, ale nie wiesz jaki powód – pokaż ogólnie
        reasonText.text = "BLOCKED";
        reasonText.color = Color.red;
    }

    // -------------------- VISIBILITY --------------------
    private void HardHideAll()
    {
        ShowRoot(false);
        ShowCG(selectMenuRoot, false);
        ShowCG(pinFailedRoot, false);
        ShowCG(ownerBlockedRoot, false);

        if (selectedPinMarker) selectedPinMarker.SetActive(false);
        if (selectedBlockedMarker) selectedBlockedMarker.SetActive(false);
        if (selectedBackMarker) selectedBackMarker.SetActive(false);
    }

    private bool IsVisible()
    {
        // Najbezpieczniej dla CanvasGroup:
        if (root != null) return root.alpha > 0.5f && root.blocksRaycasts;
        return gameObject.activeInHierarchy;
    }


    private void ShowRoot(bool v)
    {
        if (!root) return;
        root.alpha = v ? 1 : 0;
        root.interactable = v;
        root.blocksRaycasts = v;
    }

    private static void ShowCG(CanvasGroup cg, bool v)
    {
        if (!cg) return;
        cg.alpha = v ? 1f : 0f;
        cg.interactable = v;
        cg.blocksRaycasts = v;
    }

    // -------------------- HELPERS --------------------
    private void EnsureSelectButtons()
    {
        if (_selectButtons == null || _selectButtons.Length != 3)
            _selectButtons = new[] { btnPinFailed, btnOwnerBlocked, btnBackSelect };

        // Bezpiecznie clampuj index

        _selectIndex = Mathf.Clamp(_selectIndex, 0, 2);
    }

    private void UpdateSelectInteractables()
    {
        bool blocked = (_card != null && _card.status == BankCardStatus.Blocked);

        bool canPin = blocked && _card.blockReason == BankCardBlockReason.PinFail3x;
        bool canOwner = blocked && _card.blockReason == BankCardBlockReason.OwnerBlocked;

        if (btnPinFailed) btnPinFailed.interactable = canPin;
        if (btnOwnerBlocked) btnOwnerBlocked.interactable = canOwner;
        if (btnBackSelect) btnBackSelect.interactable = true;

        if (pinFailedBlockedOverlay) pinFailedBlockedOverlay.SetActive(!canPin);
        if (ownerBlockedBlockedOverlay) ownerBlockedBlockedOverlay.SetActive(!canOwner);

            // jeśli tylko BACK jest aktywny, wymuś index=2
        if (btnPinFailed && !btnPinFailed.interactable &&
                btnOwnerBlocked && !btnOwnerBlocked.interactable)
            {
                _selectIndex = 2;
                UpdateSelectedMarkers();
                ApplySelectFocus();
            }
    }

    private static bool TryParseInt(TMP_InputField f, out int v)
    {
        v = 0;
        if (f == null) return false;
        return int.TryParse(f.text, out v);
    }

    private static void SetStatus(TMP_Text t, string msg)
    {
        if (t) t.text = msg;
    }

    private void SyncIndexFromEventSystem()
    {
        if (EventSystem.current == null) return;

        var go = EventSystem.current.currentSelectedGameObject;
        if (go == null) return;

        if (btnPinFailed && go == btnPinFailed.gameObject) _selectIndex = 0;
        else if (btnOwnerBlocked && go == btnOwnerBlocked.gameObject) _selectIndex = 1;
        else if (btnBackSelect && go == btnBackSelect.gameObject) _selectIndex = 2;
    }

    private bool IsCardBlockedByStatus()
    {
        return _card != null && _card.status == BankCardStatus.Blocked;
    }

    private void LockSelectMenu(bool locked)
    {
        if (!selectMenuRoot) return;

        selectMenuRoot.alpha = 1f;                 // zawsze widoczne
        selectMenuRoot.interactable = true;        // zostaje selectable dla nav!
        selectMenuRoot.blocksRaycasts = !locked;   // klik myszą blokujemy
    }

    private void SetOwnerBlockedButtonsEnabled(bool v)
    {
        if (btnConfirmOwnerBlocked) btnConfirmOwnerBlocked.interactable = v;
        if (btnCancelOwnerBlocked) btnCancelOwnerBlocked.interactable = v;
        if (btnBackOwnerBlocked) btnBackOwnerBlocked.interactable = v;
    }

    private IEnumerator ProcessPinFailedUnblock()
    {
        _isProcessing = true;
        SetPinFailedButtonsEnabled(false);

        float t = 0f;
        int dots = 0;

        while (t < 5f)
        {
            dots = (dots + 1) % 4;
            SetStatus(statusTextPinFailed, "PROCESSING" + new string('.', dots));
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
        }

        bool success = ConfirmPinFailed(out string resultMsg);

        if (success)
        {
            SetStatus(statusTextPinFailed, "OK");
            owner?.SetCardRecord(_card);
            yield return new WaitForSeconds(0.25f);
            Close(goBackToOwnerMenu: true);
        }
        else
        {
            SetStatus(statusTextPinFailed, resultMsg);
            SetPinFailedButtonsEnabled(true);
            if (EventSystem.current != null && btnConfirmPinFailed != null)
                EventSystem.current.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
        }

        _isProcessing = false;
        _processingCo = null;
    }
    private void SetPinFailedButtonsEnabled(bool v)
    {
        if (btnConfirmPinFailed) btnConfirmPinFailed.interactable = v && CanConfirmPinFailed();
        if (btnCancelPinFailed) btnCancelPinFailed.interactable = v;
        if (btnBackPinFailed) btnBackPinFailed.interactable = v;
    }

    private void SetupPinInputs()
    {
        SetupInput(dayInput, 2);
        SetupInput(monthInput, 2);
        SetupInput(hourInput, 2);
        SetupInput(minuteInput, 2);

        // aktualizacja confirm po zmianie
        if (dayInput) dayInput.onValueChanged.AddListener(_ => OnPinFailedInputsChanged());
        if (monthInput) monthInput.onValueChanged.AddListener(_ => OnPinFailedInputsChanged());
        if (hourInput) hourInput.onValueChanged.AddListener(_ => OnPinFailedInputsChanged());
        if (minuteInput) minuteInput.onValueChanged.AddListener(_ => OnPinFailedInputsChanged());
    }

    private static void SetupInput(TMP_InputField f, int maxLen)
    {
        if (!f) return;
        f.characterLimit = maxLen;
        f.contentType = TMP_InputField.ContentType.IntegerNumber;
        f.lineType = TMP_InputField.LineType.SingleLine;
        f.richText = false;
    }

    private void HandlePinFailedInputNavigation()
    {
        if (_isProcessing) return;

        var es = EventSystem.current;
        if (es == null) return;

        GameObject go = es.currentSelectedGameObject;

        // jeśli focus uciekł – wróć na DAY
        if (go == null)
        {
            FocusPinField(dayInput);
            return;
        }

        // jeśli jesteśmy na przyciskach w dolnym panelu
        if (btnConfirmPinFailed && go == btnConfirmPinFailed.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnPinFailed_Confirm();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) es.SetSelectedGameObject(btnBackPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.RightArrow)) es.SetSelectedGameObject(btnCancelPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.UpArrow)) FocusPinField(minuteInput);
            return;
        }
        if (btnCancelPinFailed && go == btnCancelPinFailed.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnPinFailed_Cancel();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) es.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.RightArrow)) es.SetSelectedGameObject(btnBackPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.UpArrow)) FocusPinField(minuteInput);
            return;
        }
        if (btnBackPinFailed && go == btnBackPinFailed.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnPinFailed_Back();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) es.SetSelectedGameObject(btnCancelPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.RightArrow)) es.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
            if (Input.GetKeyDown(KeyCode.UpArrow)) FocusPinField(minuteInput);
            return;
        }

        // jeśli jesteśmy na inputach
        TMP_InputField f =
            (dayInput && go == dayInput.gameObject) ? dayInput :
            (monthInput && go == monthInput.gameObject) ? monthInput :
            (hourInput && go == hourInput.gameObject) ? hourInput :
            (minuteInput && go == minuteInput.gameObject) ? minuteInput :
            null;

        if (f == null) return;

        // Left/Right -> poprzedni/następny input
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { FocusPrevField(f); return; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { FocusNextField(f); return; }

        // Up/Down -> zmiana cyfry (wrap)
        if (Input.GetKeyDown(KeyCode.UpArrow)) { BumpField(f, +1); return; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { BumpField(f, -1); return; }

        // Enter -> jeśli wszystko OK -> przejdź na CONFIRM, inaczej na następny input
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (CanConfirmPinFailed())
            {
                if (btnConfirmPinFailed) es.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
            }
            else
            {
                FocusNextField(f);
            }
        }

        // Cyfry 0-9 -> dopisz i auto-advance
        for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
        {
            if (Input.GetKeyDown(k)) { AppendDigit(f, (char)('0' + (k - KeyCode.Alpha0))); return; }
        }
        for (KeyCode k = KeyCode.Keypad0; k <= KeyCode.Keypad9; k++)
        {
            if (Input.GetKeyDown(k)) { AppendDigit(f, (char)('0' + (k - KeyCode.Keypad0))); return; }
        }
    }

    private void FocusPinField(TMP_InputField f)
    {
        if (!f || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(f.gameObject);
        f.ActivateInputField();
    }

    private void FocusNextField(TMP_InputField current)
    {
        if (current == dayInput) FocusPinField(monthInput);
        else if (current == monthInput) FocusPinField(hourInput);
        else if (current == hourInput) FocusPinField(minuteInput);
        else if (current == minuteInput)
        {
            // po ostatnim polu: jeśli poprawne -> CONFIRM, inaczej zostajemy
            if (CanConfirmPinFailed() && btnConfirmPinFailed)
                EventSystem.current.SetSelectedGameObject(btnConfirmPinFailed.gameObject);
        }
    }

    private void FocusPrevField(TMP_InputField current)
    {
        if (current == minuteInput) FocusPinField(hourInput);
        else if (current == hourInput) FocusPinField(monthInput);
        else if (current == monthInput) FocusPinField(dayInput);
    }

    private void AppendDigit(TMP_InputField f, char digit)
    {
        if (!f) return;

        // tylko cyfry
        if (digit < '0' || digit > '9') return;

        // jeśli zaznaczony cały tekst – nadpisz
        if (f.selectionStringAnchorPosition != f.selectionStringFocusPosition)
            f.text = "";

        // dopisz do limitu
        if (f.text.Length >= f.characterLimit && f.characterLimit > 0) return;

        f.text += digit;
        f.caretPosition = f.text.Length;

        // auto advance gdy pole ma 2 cyfry
        if (f.characterLimit > 0 && f.text.Length >= f.characterLimit)
            FocusNextField(f);
    }

    private void BumpField(TMP_InputField f, int delta)
    {
        if (!f) return;
        int limit = f.characterLimit > 0 ? f.characterLimit : 2;

        // pusty -> start od 0
        if (!int.TryParse(f.text, out int value)) value = 0;

        // bump i wrap na zakres 0..(10^len - 1)
        int max = 1;
        for (int i = 0; i < limit; i++) max *= 10;
        value = (value + delta) % max;
        if (value < 0) value += max;

        f.text = value.ToString().PadLeft(limit, '0');
        f.caretPosition = f.text.Length;

        // opcjonalnie: po bumpie odśwież confirm
        OnPinFailedInputsChanged();
    }

    private void DebugSelected(string tag)
    {
        var es = EventSystem.current;
        var go = es ? es.currentSelectedGameObject : null;
        var btn = go ? go.GetComponentInParent<Button>() : null;
        Debug.Log($"{tag} mode={_mode} open={_isOpen} visible={IsVisible()} selectIndex={_selectIndex} esSelected={(go ? go.name : "null")} btn={(btn ? btn.name : "null")} btnInteractable={(btn ? btn.interactable : false)}");
    }

    private static void SetNavNone(Selectable s)
    {
        if (!s) return;
        var nav = new Navigation { mode = Navigation.Mode.None };
        s.navigation = nav;
    }

    private void OwnerBlocked_MoveLeft()
    {
        if (_ownerRow == OwnerRow.Top)
        {
            _ownerTop = (_ownerTop == OwnerTopBtn.OwnerBlocked) ? OwnerTopBtn.BackTop : OwnerTopBtn.OwnerBlocked;
        }
        else
        {
            if (_ownerBottom == OwnerBottomBtn.Confirm) _ownerBottom = OwnerBottomBtn.BackBottom;
            else if (_ownerBottom == OwnerBottomBtn.Cancel) _ownerBottom = OwnerBottomBtn.Confirm;
            else _ownerBottom = OwnerBottomBtn.Cancel;
        }
        OwnerBlocked_ApplyFocus();
    }

    private void OwnerBlocked_MoveRight()
    {
        if (_ownerRow == OwnerRow.Top)
        {
            _ownerTop = (_ownerTop == OwnerTopBtn.OwnerBlocked) ? OwnerTopBtn.BackTop : OwnerTopBtn.OwnerBlocked;
        }
        else
        {
            if (_ownerBottom == OwnerBottomBtn.Confirm) _ownerBottom = OwnerBottomBtn.Cancel;
            else if (_ownerBottom == OwnerBottomBtn.Cancel) _ownerBottom = OwnerBottomBtn.BackBottom;
            else _ownerBottom = OwnerBottomBtn.Confirm;
        }
        OwnerBlocked_ApplyFocus();
    }

    private void OwnerBlocked_MoveUp()
    {
        _ownerRow = OwnerRow.Top;
        _ownerTop = (_ownerBottom == OwnerBottomBtn.BackBottom) ? OwnerTopBtn.BackTop : OwnerTopBtn.OwnerBlocked;
        OwnerBlocked_ApplyFocus();
    }

    private void OwnerBlocked_MoveDown()
    {
        _ownerRow = OwnerRow.Bottom;
        _ownerBottom = (_ownerTop == OwnerTopBtn.BackTop) ? OwnerBottomBtn.BackBottom : OwnerBottomBtn.Confirm;
        OwnerBlocked_ApplyFocus();
    }

    private void OwnerBlocked_ApplyFocus()
    {
        if (!EventSystem.current) return;

        if (_ownerRow == OwnerRow.Top)
        {
            // TOP: I BLOCKED / BACK (górny)
            if (_ownerTop == OwnerTopBtn.OwnerBlocked && btnOwnerBlocked)
                EventSystem.current.SetSelectedGameObject(btnOwnerBlocked.gameObject);
            else if (_ownerTop == OwnerTopBtn.BackTop && btnBackSelect)
                EventSystem.current.SetSelectedGameObject(btnBackSelect.gameObject);
            return;
        }

        // BOTTOM: CONFIRM / CANCEL / BACK (dolny)
        Button b = null;
        if (_ownerBottom == OwnerBottomBtn.Confirm) b = btnConfirmOwnerBlocked;
        else if (_ownerBottom == OwnerBottomBtn.Cancel) b = btnCancelOwnerBlocked;
        else b = btnBackOwnerBlocked;

        if (b) EventSystem.current.SetSelectedGameObject(b.gameObject);
    }

    private void OwnerBlocked_Activate()
    {
        // Enter na TOP:
        if (_ownerRow == OwnerRow.Top)
        {
            if (_ownerTop == OwnerTopBtn.BackTop)
            {
                // górny BACK = zamyka wszystko do menu
                OnSelect_Back();
            }
            else
            {
                // Enter na I BLOCKED będąc w UNBLOCK -> tylko zejście na CONFIRM
                _ownerRow = OwnerRow.Bottom;
                _ownerBottom = OwnerBottomBtn.Confirm;
                OwnerBlocked_ApplyFocus();
            }
            return;
        }

        // Enter na BOTTOM:
        if (_ownerBottom == OwnerBottomBtn.Confirm)
        {
            // KLUCZ: nie odpal confirm jeśli Enter był “z wejścia”
            if (_ownerSuppressConfirmUntilEnterUp) return;

            OnOwnerBlocked_Confirm();
        }
        else if (_ownerBottom == OwnerBottomBtn.Cancel)
        {
            OnOwnerBlocked_Cancel(); // wraca do CARD BLOCK
        }
        else
        {
            OnOwnerBlocked_Back();   // zamyka cały panel
        }
    }
}