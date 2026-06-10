using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BankCardOpsPanel : MonoBehaviour
{
    [Header("Card Header (TMP)")]
    [SerializeField] private TMP_Text cardNumberValue;
    [SerializeField] private TMP_Text cardVariantValue;
    [SerializeField] private TMP_Text cardStatusValue;
    [SerializeField] private TMP_Text ownerIdValue;
    [SerializeField] private TMP_Text cardPinValue;

    [Header("Variant Visual")]
    [SerializeField] private Image variantPreview;

    [Header("Menu Buttons (order matters: Up/Down)")]
    [SerializeField] private List<Button> menuButtons = new();

    [Header("Highlight Fade")]
    [SerializeField] private float normalAlpha = 0.10f;
    [SerializeField] private float selectedAlpha = 0.55f;
    [SerializeField] private float fadeSpeed = 10f;

    [Header("SubPanels (CanvasGroup)")]
    [SerializeField] private CanvasGroup pinChangePanel;
    [SerializeField] private CanvasGroup cardUnblockPanel;
    [SerializeField] private CanvasGroup cardBlockPanel;
    [SerializeField] private CanvasGroup cardStolenPanel;
    [SerializeField] private CanvasGroup variantChangePanel;

    [Header("SubPanels (Script Panels)")]
    [SerializeField] private CardPinChangePanelUI pinChangePanelUI;
    [SerializeField] private CardUnblockPanelUI cardUnblockPanelUI;
    [SerializeField] private CardBlockPanelUI cardBlockPanelUI;
    [SerializeField] private CardStolenPanelUI cardStolenPanelUI;
    [SerializeField] private CardVariantChangePanelUI variantChangePanelUI;

    private bool _subPanelOpen;

    // runtime
    private CreditCardOperationsUI _owner;
    private int _accountId;
    private BankCardRecord _card;

    private bool _isOpen;
    private int _selectedIndex;
    private int _suppressSubmitFrames;

    private readonly List<Image> _buttonImages = new();
    private readonly List<float> _targetAlphas = new();

    private enum Mode
    {
        Menu,
        PinChange,
        CardUnblock,
        CardBlock,
        CardStolen,
        VariantChange
    }

    private Mode _mode = Mode.Menu;

    public bool IsSubPanelOpen => _subPanelOpen;

    // =========================
    // Public API
    // =========================
    public void Open(CreditCardOperationsUI owner, int accountId, BankCardRecord rec)
    {
        _owner = owner;
        _accountId = accountId;
        _card = rec;

        RefreshHeader();
        Show(true);

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, menuButtons.Count - 1));
        CacheButtonImages();
        OpenMenu();
        RefreshMenuAvailability();
    }

    public void Close()
    {
        Show(false);
        _isOpen = false;
        _owner = null;
    }

    // =========================
    // Unity loop (keyboard nav)
    // =========================
    private void Update()
    {
        if (!_isOpen) return;
        if (!gameObject.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BankDialogueUI.SuppressEscapeFrames = 2;

            if (_subPanelOpen)
            {
                if (pinChangePanelUI != null && pinChangePanelUI.gameObject.activeInHierarchy)
                    pinChangePanelUI.Close(true);
                else if (cardUnblockPanelUI != null && cardUnblockPanelUI.gameObject.activeInHierarchy)
                    cardUnblockPanelUI.Close(true);
                else if (cardBlockPanelUI != null && cardBlockPanelUI.gameObject.activeInHierarchy)
                    cardBlockPanelUI.Close(true);
                else if (cardStolenPanelUI != null && cardStolenPanelUI.gameObject.activeInHierarchy)
                    cardStolenPanelUI.Close(true);
                else if (variantChangePanelUI != null && variantChangePanelUI.gameObject.activeInHierarchy)
                    variantChangePanelUI.Close(true);
                else
                    ReturnToMenuFromSubPanel();

                return;
            }

            ExitToDialogueStart();
            return;
        }

        // ✅ blokuj Enter na 1-2 klatki po zamknięciu subpanelu
        if (_suppressSubmitFrames > 0)
        {
            _suppressSubmitFrames--;
            TickFade();
            return;
        }
        // 🔥 KLUCZ: jak subpanel otwarty – ZERO Enter/Up/Down/ClickSelected
        if (_subPanelOpen)
        {
            TickFade();
            return;
        }


        if (_mode != Mode.Menu) return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(+1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ClickSelected();

        TickFade();
    }


    // =========================
    // Buttons (OnClick w Inspectorze)
    // =========================
    public void OnPinChangeButton()
    {
        if (!_isOpen) return;

        OpenSubPanel(Mode.PinChange, pinChangePanel);

        // <<< TO JEST KLUCZ
        if (pinChangePanelUI != null)
            pinChangePanelUI.Open(this, _card);
    }
    public void OnUnblockCardButton()
    {
        if (!_isOpen) return;

        if (BankSystem.Instance != null && _card != null)
        {
            if (BankSystem.Instance.TryGetCard(_card.cardId, out var fresh) && fresh != null)
                _card = fresh;
        }

        RefreshHeader();

        OpenSubPanel(Mode.CardUnblock, cardUnblockPanel);

        if (cardUnblockPanelUI != null)
            cardUnblockPanelUI.Open(this, _card);
    }

    public void OnStolenButton()
    {
        if (!_isOpen) return;
        if (!CanUseStolenOption()) return;

        OpenSubPanel(Mode.CardStolen, cardStolenPanel);

        if (cardStolenPanelUI != null)
            cardStolenPanelUI.Open(this, _card);
    }

    private bool CanUseStolenOption()
    {
        if (_card == null) return false;
        if (_card.status != BankCardStatus.Active) return false;
        if (InventoryUI.Instance == null) return false;

        return !InventoryUI.Instance.HasBankCardId(_card.cardId);
    }

    public void OnVariantChangeButton()
    {
        if (!_isOpen) return;

        if (BankSystem.Instance != null && _card != null)
        {
            if (BankSystem.Instance.TryGetCard(_card.cardId, out var fresh) && fresh != null)
                _card = fresh;
        }

        RefreshHeader();

        OpenSubPanel(Mode.VariantChange, variantChangePanel);

        if (variantChangePanelUI != null)
            variantChangePanelUI.Open(this, _card);
    }

    public void OnAccountOperationsButton()
    {
        if (!_isOpen) return;

        if (_owner != null)
            _owner.Close();
    }

    public void OnExitButton()
    {
        ExitToDialogueStart();
    }

    // =========================
    // Menu/SubPanels flow
    // =========================
    private void OpenMenu()
    {
        _mode = Mode.Menu;
        _subPanelOpen = false;
        HideAllSubPanels();
        SetMenuInteractable(true);
        ApplySelection(immediate: true);
    }

    private void OpenSubPanel(Mode mode, CanvasGroup panel)
    {
        _mode = mode;
        _subPanelOpen = true;
        HideAllSubPanels();
        SetMenuInteractable(false);

        ShowPanel(panel, true);
    }


    private void HideAllSubPanels()
    {
        ShowPanel(pinChangePanel, false);
        ShowPanel(cardUnblockPanel, false);
        ShowPanel(cardStolenPanel, false);
        ShowPanel(variantChangePanel, false);
        ShowPanel(cardBlockPanel, false);
    }

    private void SetMenuInteractable(bool v)
    {
        for (int i = 0; i < menuButtons.Count; i++)
        {
            if (menuButtons[i]) menuButtons[i].interactable = v;
        }
    }

    private static void ShowPanel(CanvasGroup cg, bool v)
    {
        if (!cg) return;
        cg.alpha = v ? 1f : 0f;
        cg.interactable = v;
        cg.blocksRaycasts = v;
        cg.gameObject.SetActive(v);
    }

    // =========================
    // Header refresh (card info)
    // =========================
    private void RefreshHeader()
    {
        if (cardNumberValue) cardNumberValue.text = _card.cardId;
        if (cardVariantValue) cardVariantValue.text = _card.colorVariant.ToString();
        if (cardStatusValue) cardStatusValue.text = _card.status.ToString().ToUpper();
        if (cardPinValue) cardPinValue.text = _card.pin.ToString("0000");

        var bank = BankSystem.Instance;
        if (bank != null && bank.TryGetAccount(_accountId, out var acc))
        {
            if (ownerIdValue) ownerIdValue.text = acc.citizenId;
        }

        if (variantPreview && bank != null)
            variantPreview.color = bank.GetVariantColor(_card.colorVariant);
    }

    // =========================
    // Menu selection visuals (jak w AccountOperationsUI)
    // =========================
    private void CacheButtonImages()
    {
        _buttonImages.Clear();
        _targetAlphas.Clear();

        for (int i = 0; i < menuButtons.Count; i++)
        {
            var b = menuButtons[i];
            if (!b) { _buttonImages.Add(null); _targetAlphas.Add(normalAlpha); continue; }

            var img = b.GetComponent<Image>();
            _buttonImages.Add(img);
            _targetAlphas.Add(normalAlpha);

            if (img != null)
            {
                var c = img.color;
                c.a = normalAlpha;
                img.color = c;
            }
        }
    }

    private void MoveSelection(int dir)
    {
        if (menuButtons.Count == 0) return;

        int next = Mathf.Clamp(_selectedIndex + dir, 0, menuButtons.Count - 1);

        // skip null / non-interactable
        int guard = 0;
        while (guard++ < 50)
        {
            var b = menuButtons[next];
            if (b != null && b.interactable) break;

            next += dir >= 0 ? 1 : -1;
            if (next < 0 || next >= menuButtons.Count) return;
        }

        _selectedIndex = next;
        ApplySelection(immediate: false);
    }

    private void ApplySelection(bool immediate)
    {
        for (int i = 0; i < _targetAlphas.Count; i++)
            _targetAlphas[i] = (i == _selectedIndex) ? selectedAlpha : normalAlpha;

        // focus w EventSystem
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

    private static void SetAlpha(Image img, float a)
    {
        if (!img) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    private void ClickSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= menuButtons.Count) return;
        var b = menuButtons[_selectedIndex];
        if (b == null || !b.interactable) return;

        b.onClick?.Invoke();
    }

    // =========================
    // Exit → back to NPC dialogue start (jak AccountOperationsUI)
    // =========================
    private void ExitToDialogueStart()
    {
        // EXIT ma wracać do początku rozmowy z NPC, bez otwierania AccountOperationsUI
        if (_owner != null)
            _owner.ReturnToDialogueStart();
    }

    private void Show(bool v)
    {
        gameObject.SetActive(v);
        _isOpen = v;
    }
    public void SetCardRecord(BankCardRecord rec)
    {
        if (rec == null) return;
        _card = rec;
        RefreshHeader();
    }

    public void ReturnToMenuFromSubPanel()
    {
        _suppressSubmitFrames = 2;
        OpenMenu();
        RefreshHeader();
        RefreshMenuAvailability();
    }

    public void ReturnToSelectCardRootAfterDelete()
    {
        _subPanelOpen = false;
        OpenMenu(); // wróć do menu UI w tym panelu (opcjonalnie)

        if (_owner != null)
            _owner.ReturnToSelectCardRoot(); // <- to musisz mieć w CreditCardOperationsUI
    }

    public void OnBlockCardButton()
    {
        if (!_isOpen) return;

        if (BankSystem.Instance != null && _card != null)
        {
            if (BankSystem.Instance.TryGetCard(_card.cardId, out var fresh) && fresh != null)
                _card = fresh;
        }

        RefreshHeader();

        OpenSubPanel(Mode.CardBlock, cardBlockPanel);

        if (cardBlockPanelUI != null)
            cardBlockPanelUI.Open(this, _card);
    }

    private const int STOLEN_BUTTON_INDEX = 3;

    private void RefreshMenuAvailability()
    {
        if (menuButtons == null || menuButtons.Count == 0)
            return;

        bool canUseStolen = CanUseStolenOption();

        if (STOLEN_BUTTON_INDEX >= 0 &&
            STOLEN_BUTTON_INDEX < menuButtons.Count &&
            menuButtons[STOLEN_BUTTON_INDEX] != null)
        {
            menuButtons[STOLEN_BUTTON_INDEX].interactable = canUseStolen;
        }

        if (_selectedIndex >= 0 && _selectedIndex < menuButtons.Count)
        {
            var selected = menuButtons[_selectedIndex];
            if (selected == null || !selected.interactable)
            {
                for (int i = 0; i < menuButtons.Count; i++)
                {
                    if (menuButtons[i] != null && menuButtons[i].interactable)
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }
        }

        ApplySelection(immediate: true);
    }
}
