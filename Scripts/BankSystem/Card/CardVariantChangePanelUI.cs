using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardVariantChangePanelUI : MonoBehaviour
{
    private enum FocusStep
    {
        Picker,
        Save,
        SaveCancel,
        Confirm,
        Back
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Preview / Header")]
    [SerializeField] private Image cardPreviewImage;          // Card_IMG
    [SerializeField] private TMP_Text currentVariantText;     // np. CURRENT VARIANT: X
    [SerializeField] private TMP_Text infoText2;              // cena albo cooldown

    [Header("Picker")]
    [SerializeField] private Transform variantContainer;      // Variant_Picker_Container
    [SerializeField] private VariantSlotView variantSlotPrefab;

    [Header("Section Buttons")]
    [SerializeField] private Button saveVariantButton;        // Save_Variant_Btn
    [SerializeField] private Button cancelVariantButton;      // Cancel_Variant_Btn

    [Header("Bottom Buttons")]
    [SerializeField] private Button confirmButton;            // Confirm
    [SerializeField] private Button backButton;               // Back

    [Header("Blocked Overlays")]
    [SerializeField] private GameObject saveBlocked;
    [SerializeField] private GameObject cancelVariantBlocked;
    [SerializeField] private GameObject confirmBlocked;

    [Header("Selected Indicators")]
    [SerializeField] private GameObject pickerSelectedDot;
    [SerializeField] private GameObject selConfirm;
    [SerializeField] private GameObject selBack;

    [Header("Processing")]
    [SerializeField] private TMP_Text processingText;
    [SerializeField] private float processingDelaySeconds = 5f;

    [Header("Colors")]
    [SerializeField] private Color slotActiveColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color slotConfirmedColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color wrongColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color checkingColor = new Color(1f, 0.85f, 0.1f, 1f);

    private BankCardOpsPanel _host;
    private BankCardRecord _card;

    private readonly List<VariantSlotView> _variantSlots = new();
    private readonly List<int> _availableVariantIndices = new();

    private FocusStep _focus = FocusStep.Picker;

    private bool _open;
    private bool _busy;
    private bool _saved;

    private int _hoverIndex;
    private int _draftVariant = -1;     // aktualnie zaznaczony draft
    private int _savedVariant = -1;     // wariant zapisany przyciskiem SAVE

    private int _suppressSubmitFrames;
    private Coroutine _co;

    public bool IsOpen => _open;

    private void Awake()
    {
        Show(false);
        ResetRuntimeOnly();

        if (saveVariantButton) saveVariantButton.onClick.AddListener(SaveDraftVariant);
        if (cancelVariantButton) cancelVariantButton.onClick.AddListener(CancelDraftVariant);

        if (confirmButton) confirmButton.onClick.AddListener(TryConfirmChange);
        if (backButton) backButton.onClick.AddListener(() => Close(true));

        EnsureButtonTint(saveVariantButton);
        EnsureButtonTint(cancelVariantButton);
        EnsureButtonTint(confirmButton);
        EnsureButtonTint(backButton);
    }

    private void Update()
    {
        if (!_open) return;

        RefreshInfoText();

        if (_suppressSubmitFrames > 0)
        {
            _suppressSubmitFrames--;
            return;
        }

        if (_busy) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close(true);
            return;
        }

        if (IsPanelLockedToBackOnly())
        {
            if (_focus != FocusStep.Back)
                SetFocus(FocusStep.Back);

            bool enterLocked = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool upLocked = Input.GetKeyDown(KeyCode.UpArrow);
            bool downLocked = Input.GetKeyDown(KeyCode.DownArrow);
            bool leftLocked = Input.GetKeyDown(KeyCode.LeftArrow);
            bool rightLocked = Input.GetKeyDown(KeyCode.RightArrow);

            if (enterLocked)
            {
                Close(true);
                return;
            }

            if (upLocked || downLocked || leftLocked || rightLocked)
            {
                SetFocus(FocusStep.Back);
                return;
            }

            return;
        }

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close(true);
            return;
        }

        switch (_focus)
        {
            case FocusStep.Picker:
                {
                    if (_availableVariantIndices.Count == 0)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    if (left)
                    {
                        _hoverIndex = (_hoverIndex - 1 + _availableVariantIndices.Count) % _availableVariantIndices.Count;
                        _draftVariant = _availableVariantIndices[_hoverIndex];
                        RefreshAll();
                        return;
                    }

                    if (right)
                    {
                        _hoverIndex = (_hoverIndex + 1) % _availableVariantIndices.Count;
                        _draftVariant = _availableVariantIndices[_hoverIndex];
                        RefreshAll();
                        return;
                    }

                    if (enter)
                    {
                        _draftVariant = _availableVariantIndices[_hoverIndex];
                        SetFocus(FocusStep.Save);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(CanSaveDraft() ? FocusStep.Save : FocusStep.Back);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    return;
                }

            case FocusStep.Save:
                {
                    if (left)
                    {
                        SetFocus(FocusStep.Picker);
                        return;
                    }

                    if (right)
                    {
                        SetFocus(FocusStep.SaveCancel);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Picker);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(CanConfirm() ? FocusStep.Confirm : FocusStep.Back);
                        return;
                    }

                    if (enter)
                    {
                        SaveDraftVariant();
                        return;
                    }

                    return;
                }

            case FocusStep.SaveCancel:
                {
                    if (left)
                    {
                        SetFocus(FocusStep.Save);
                        return;
                    }

                    if (right)
                    {
                        SetFocus(FocusStep.Picker);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Picker);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(CanConfirm() ? FocusStep.Confirm : FocusStep.Back);
                        return;
                    }

                    if (enter)
                    {
                        CancelDraftVariant();
                        return;
                    }

                    return;
                }

            case FocusStep.Confirm:
                {
                    if (up)
                    {
                        SetFocus(FocusStep.SaveCancel);
                        return;
                    }

                    if (down || right)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    if (enter)
                    {
                        TryConfirmChange();
                        return;
                    }

                    return;
                }

            case FocusStep.Back:
                {
                    if (up || left)
                    {
                        if (_saved)
                            SetFocus(FocusStep.Confirm);
                        else if (CanSaveDraft())
                            SetFocus(FocusStep.SaveCancel);
                        else
                            SetFocus(FocusStep.Picker);

                        return;
                    }

                    if (down)
                    {
                        SetFocus(FocusStep.Picker);
                        return;
                    }

                    if (enter)
                    {
                        Close(true);
                        return;
                    }

                    return;
                }
        }
    }

    public void Open(BankCardOpsPanel host, BankCardRecord card)
    {
        _host = host;
        _card = card;

        _open = true;
        _busy = false;
        _suppressSubmitFrames = 2;

        RefreshCurrentCardFromBank();
        ResetRuntimeOnly();
        BuildAvailableVariants();
        BuildVariantSlots();
        Show(true);
        RefreshAll();

        SetFocus(IsPanelLockedToBackOnly() ? FocusStep.Back : FocusStep.Picker);
    }

    public void Close(bool returnToMenu)
    {
        StopCo();
        _open = false;
        _busy = false;

        Show(false);

        if (returnToMenu)
            _host?.ReturnToMenuFromSubPanel();
    }

    private void ResetRuntimeOnly()
    {
        _saved = false;
        _hoverIndex = 0;
        _draftVariant = -1;
        _savedVariant = -1;

        if (processingText)
        {
            processingText.text = "";
            processingText.gameObject.SetActive(false);
        }
    }

    private void RefreshCurrentCardFromBank()
    {
        if (_card == null || BankSystem.Instance == null)
            return;

        if (BankSystem.Instance.TryGetCard(_card.cardId, out var fresh) && fresh != null)
            _card = fresh;
    }

    private void BuildAvailableVariants()
    {
        _availableVariantIndices.Clear();

        var bank = BankSystem.Instance;
        if (bank == null || _card == null)
            return;

        for (int i = 0; i < bank.VariantCount; i++)
        {
            if (i == _card.colorVariant)
                continue;

            _availableVariantIndices.Add(i);
        }

        if (_availableVariantIndices.Count > 0)
            _draftVariant = _availableVariantIndices[0];
    }

    private void BuildVariantSlots()
    {
        if (!variantContainer || !variantSlotPrefab)
            return;

        for (int i = variantContainer.childCount - 1; i >= 0; i--)
            Destroy(variantContainer.GetChild(i).gameObject);

        _variantSlots.Clear();

        var bank = BankSystem.Instance;
        if (bank == null)
            return;

        for (int i = 0; i < _availableVariantIndices.Count; i++)
        {
            int variantIndex = _availableVariantIndices[i];
            var slot = Instantiate(variantSlotPrefab, variantContainer);

            slot.SetPreviewColor(bank.GetVariantColor(variantIndex));
            slot.SetHover(false);
            slot.SetSelected(false);

            if (slot.activeBorder) slot.activeBorder.SetActive(false);
            if (slot.arrow) slot.arrow.SetActive(false);

            _variantSlots.Add(slot);
        }
    }

    private void RefreshAll()
    {
        RefreshCurrentCardPreview();
        RefreshInfoText();
        RefreshPickerUI();
        RefreshButtonsAndBlocked();
        RefreshSelectedVisuals();
    }

    private void RefreshCurrentCardPreview()
    {
        var bank = BankSystem.Instance;
        if (_card == null || bank == null)
            return;

        if (cardPreviewImage)
            cardPreviewImage.color = bank.GetVariantColor(_card.colorVariant);

        if (currentVariantText)
            currentVariantText.text = _card.colorVariant.ToString();
    }

    private void RefreshInfoText()
    {
        if (!infoText2 || BankSystem.Instance == null || _card == null)
            return;

        if (IsCardPending())
        {
            long nowMin = GameTimeSystem.Instance != null ? GameTimeSystem.Instance.TotalMinutesSinceStart : 0L;
            long remainMin = Math.Max(0L, _card.activateAt - nowMin);

            int totalSeconds = Mathf.Max(0, (int)(remainMin * 60));
            TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);

            infoText2.text = $"CARD IS PENDING {ts.Hours + ts.Days * 24:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            infoText2.color = wrongColor;
            return;
        }

        int remain = BankSystem.Instance.GetVariantChangeCooldownRemainingMinutes(_card.cardId);
        if (remain > 0)
        {
            infoText2.text = $"NEXT CHANGE {BankSystem.Instance.GetVariantChangeCooldownFormatted(_card.cardId)}";
            infoText2.color = wrongColor;
            return;
        }

        if (!HasEnoughMoney())
        {
            infoText2.text = $"NOT ENOUGH MONEY ({BankSystem.Instance.VariantChangePrice}$)";
            infoText2.color = wrongColor;
            return;
        }

        infoText2.text = $"NEW VARIANT PRICE {BankSystem.Instance.VariantChangePrice}$";
        infoText2.color = wrongColor;
    }

    private void RefreshPickerUI()
    {
        for (int i = 0; i < _variantSlots.Count; i++)
        {
            var slot = _variantSlots[i];
            if (!slot) continue;

            int variantIndex = _availableVariantIndices[i];

            bool hover = (_focus == FocusStep.Picker && i == _hoverIndex);
            bool isDraft = (_draftVariant >= 0 && variantIndex == _draftVariant);
            bool isSaved = (_saved && variantIndex == _savedVariant);

            slot.SetHover(hover);
            slot.SetSelected(isSaved || isDraft);

            if (slot.arrow)
                slot.arrow.SetActive(hover);

            if (slot.activeBorder)
            {
                bool showBorder = hover || isDraft || isSaved;
                slot.activeBorder.SetActive(showBorder);

                var img = slot.activeBorder.GetComponent<Image>();
                if (img != null)
                {
                    if (isSaved)
                        img.color = slotConfirmedColor;
                    else
                        img.color = slotActiveColor;
                }
            }
        }

        if (pickerSelectedDot)
        {
            pickerSelectedDot.SetActive(
                _focus == FocusStep.Picker ||
                _focus == FocusStep.Save ||
                _focus == FocusStep.SaveCancel
            );
        }
    }

    private void RefreshButtonsAndBlocked()
    {
        bool lockedToBack = IsPanelLockedToBackOnly();

        bool canSave = CanSaveDraft();
        bool canCancelVariant = !_busy && !lockedToBack && (_draftVariant >= 0 || _saved);
        bool canConfirm = CanConfirm();

        if (saveVariantButton) saveVariantButton.interactable = canSave;
        if (cancelVariantButton) cancelVariantButton.interactable = canCancelVariant;
        if (confirmButton) confirmButton.interactable = canConfirm;

        if (confirmBlocked) confirmBlocked.SetActive(!canConfirm);
        if (saveBlocked) saveBlocked.SetActive(!canSave);
        if (cancelVariantBlocked) cancelVariantBlocked.SetActive(!canCancelVariant);
    }

    private void RefreshSelectedVisuals()
    {
        if (selConfirm) selConfirm.SetActive(_focus == FocusStep.Confirm);
        if (selBack) selBack.SetActive(_focus == FocusStep.Back);

        if (!EventSystem.current)
            return;

        GameObject selectedGO = null;

        switch (_focus)
        {
            case FocusStep.Save:
                selectedGO = saveVariantButton ? saveVariantButton.gameObject : null;
                break;
            case FocusStep.SaveCancel:
                selectedGO = cancelVariantButton ? cancelVariantButton.gameObject : null;
                break;
            case FocusStep.Confirm:
                selectedGO = confirmButton ? confirmButton.gameObject : null;
                break;
            case FocusStep.Back:
                selectedGO = backButton ? backButton.gameObject : null;
                break;
            case FocusStep.Picker:
            default:
                selectedGO = null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(selectedGO);
    }

    private bool CanSaveDraft()
    {
        return !_busy &&
               !IsPanelLockedToBackOnly() &&
               _draftVariant >= 0 &&
               _draftVariant != _card.colorVariant &&
               !_saved;
    }

    private bool CanCancelDraft()
    {
        return !_busy && (_draftVariant >= 0 || _saved);
    }

    private bool HasCooldown()
    {
        if (BankSystem.Instance == null || _card == null)
            return false;

        return BankSystem.Instance.GetVariantChangeCooldownRemainingMinutes(_card.cardId) > 0;
    }

    private bool HasEnoughMoney()
    {
        if (BankSystem.Instance == null || _card == null)
            return false;

        if (!BankSystem.Instance.TryGetAccount(_card.accountId, out var acc) || acc == null)
            return false;

        return acc.balance >= BankSystem.Instance.VariantChangePrice;
    }

    private bool CanConfirm()
    {
        return !_busy &&
               !IsPanelLockedToBackOnly() &&
               _saved &&
               _savedVariant >= 0;
    }

    private void SetFocus(FocusStep step)
    {
        _focus = step;

        if (EventSystem.current != null)
        {
            switch (_focus)
            {
                case FocusStep.Back:
                    EventSystem.current.SetSelectedGameObject(backButton ? backButton.gameObject : null);
                    break;

                case FocusStep.Confirm:
                    EventSystem.current.SetSelectedGameObject(confirmButton ? confirmButton.gameObject : null);
                    break;

                case FocusStep.Save:
                    EventSystem.current.SetSelectedGameObject(saveVariantButton ? saveVariantButton.gameObject : null);
                    break;

                case FocusStep.SaveCancel:
                    EventSystem.current.SetSelectedGameObject(cancelVariantButton ? cancelVariantButton.gameObject : null);
                    break;

                case FocusStep.Picker:
                default:
                    EventSystem.current.SetSelectedGameObject(null);
                    break;
            }
        }

        RefreshAll();
    }

    private void SaveDraftVariant()
    {
        if (!CanSaveDraft())
            return;

        _saved = true;
        _savedVariant = _draftVariant;

        SetFocus(FocusStep.Confirm);
    }

    private void CancelDraftVariant()
    {
        if (!CanCancelDraft())
            return;

        _saved = false;
        _savedVariant = -1;

        if (_availableVariantIndices.Count > 0)
        {
            _hoverIndex = 0;
            _draftVariant = _availableVariantIndices[0];
        }
        else
        {
            _draftVariant = -1;
        }

        SetFocus(FocusStep.Picker);
    }
    private void TryConfirmChange()
    {
        if (!CanConfirm())
            return;

        StopCo();
        _co = StartCoroutine(CoConfirmVariantChange());
    }

    private IEnumerator CoConfirmVariantChange()
    {
        _busy = true;
        RefreshButtonsAndBlocked();

        if (processingText)
        {
            processingText.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < processingDelaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                int dots = ((int)(elapsed * 3f)) % 4;
                string d = dots == 0 ? "" : new string('.', dots);
                processingText.text = $"PROCESSING{d}";
                processingText.color = checkingColor;
                yield return null;
            }
        }

        var bank = BankSystem.Instance;
        bool ok = false;
        string reason = "";

        if (bank != null && _card != null)
            ok = bank.TryChangeCardVariant(_card.cardId, _savedVariant, out reason);

        if (ok)
        {
            RefreshCurrentCardFromBank();
            _host?.SetCardRecord(_card);

            BuildAvailableVariants();
            BuildVariantSlots();

            _saved = false;
            _savedVariant = -1;

            if (_availableVariantIndices.Count > 0)
            {
                _hoverIndex = 0;
                _draftVariant = _availableVariantIndices[0];
            }
            else
            {
                _draftVariant = -1;
            }

            if (processingText)
            {
                processingText.text = "READY";
                processingText.color = slotConfirmedColor;
            }

            RefreshAll();
            _busy = false;
            SetFocus(FocusStep.Back);
            yield break;
        }

        if (processingText)
        {
            processingText.text = string.IsNullOrEmpty(reason) ? "FAILED" : reason;
            processingText.color = wrongColor;
        }

        _busy = false;
        RefreshAll();
        SetFocus(FocusStep.Back);
    }

    private void StopCo()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
    }
    private void Show(bool v)
    {
        if (!root)
        {
            gameObject.SetActive(v);
            return;
        }

        root.gameObject.SetActive(v);
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
    }

    private static void EnsureButtonTint(Button b)
    {
        if (!b) return;

        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        cb.fadeDuration = 0.05f;
        b.colors = cb;
    }

    private bool IsCardPending()
    {
        return _card != null && _card.status == BankCardStatus.Pending;
    }

    private bool IsPanelLockedToBackOnly()
    {
        return IsCardPending() || HasCooldown() || !HasEnoughMoney();
    }
}