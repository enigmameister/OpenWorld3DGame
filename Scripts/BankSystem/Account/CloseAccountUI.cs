using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CloseAccountUI : MonoBehaviour
{
    private enum FocusStep
    {
        Sign,
        Save,
        Cancel,
        Confirm,
        Back
    }

    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Owner")]
    [SerializeField] private AccountOperationsUI owner;

    [Header("Checks TMP")]
    [SerializeField] private TMP_Text currentLoansValue;
    [SerializeField] private TMP_Text accountBalanceValue;
    [SerializeField] private TMP_Text returnCardsValue;
    [SerializeField] private TMP_Text allConditionsValue;

    [Header("Sign")]
    [SerializeField] private GameObject signRoot;
    [SerializeField] private GameObject signSelected;
    [SerializeField] private GameObject signBlocked;
    [SerializeField] private TMP_Text phraseTodoText;   // bia³y/szary
    [SerializeField] private TMP_Text phraseDoneText;   // zielony

    [Header("Buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("Blocked Overlays")]
    [SerializeField] private GameObject saveBlocked;
    [SerializeField] private GameObject cancelBlocked;
    [SerializeField] private GameObject confirmBlocked;

    [Header("Selected Indicators")]
    [SerializeField] private GameObject selConfirm;
    [SerializeField] private GameObject selBack;

    [Header("Processing")]
    [SerializeField] private GameObject processingRoot;
    [SerializeField] private TMP_Text processingText;
    [SerializeField] private float deletingPhaseSeconds = 3f;
    [SerializeField] private float confirmingPhaseSeconds = 3f;
    [SerializeField] private float finalizingPhaseSeconds = 4f;

    [Header("Colors")]
    [SerializeField] private Color okColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color deniedColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color processingColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Header("Phrase")]
    [SerializeField] private string requiredPhrase = "YES I CONFIRM";

    private AccountOpsPanel _host;
    private int _accountId;
    private bool _open;
    private bool _busy;
    private bool _signSaved;
    private int _phraseIndex;
    private int _suppressSubmitFrames;
    private FocusStep _focus = FocusStep.Back;
    private Coroutine _processingCo;

    private CloseAccountCheckResult _check;

    public bool IsOpen => _open;

    private void Awake()
    {
        Show(false);

        if (saveButton) saveButton.onClick.AddListener(SavePhrase);
        if (cancelButton) cancelButton.onClick.AddListener(CancelPhrase);
        if (confirmButton) confirmButton.onClick.AddListener(ConfirmCloseAccount);
        if (backButton) backButton.onClick.AddListener(() => Close(true));

        EnsureButtonTint(saveButton);
        EnsureButtonTint(cancelButton);
        EnsureButtonTint(confirmButton);
        EnsureButtonTint(backButton);

        if (phraseTodoText)
            phraseTodoText.text = requiredPhrase;

        if (phraseDoneText)
        {
            phraseDoneText.text = requiredPhrase;
            phraseDoneText.maxVisibleCharacters = 0;
        }
    }

    private void Update()
    {
        if (!_open) return;

        if (_suppressSubmitFrames > 0)
        {
            _suppressSubmitFrames--;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close(true);
            return;
        }

        if (_busy) return;

        RefreshCheckState();
        RefreshAll();

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        if (IsLockedToBackOnly())
        {
            if (_focus != FocusStep.Back)
                SetFocus(FocusStep.Back);

            if (enter)
            {
                Close(true);
                return;
            }

            if (up || down || left || right)
            {
                SetFocus(FocusStep.Back);
                return;
            }

            return;
        }

        switch (_focus)
        {
            case FocusStep.Sign:
                {
                    HandlePhraseTyping();

                    if (down)
                    {
                        if (_signSaved)
                            SetFocus(FocusStep.Confirm);
                        else if (IsPhraseComplete())
                            SetFocus(FocusStep.Save);
                        else if (HasAnyPhraseProgress())
                            SetFocus(FocusStep.Cancel);
                        else
                            SetFocus(FocusStep.Back);

                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    if (enter && CanSavePhrase())
                    {
                        SavePhrase();
                        return;
                    }

                    return;
                }

            case FocusStep.Save:
                {
                    if (left)
                    {
                        SetFocus(FocusStep.Sign);
                        return;
                    }

                    if (right)
                    {
                        SetFocus(FocusStep.Cancel);
                        return;
                    }

                    if (up)
                    {
                        SetFocus(FocusStep.Sign);
                        return;
                    }

                    if (down)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    if (enter)
                    {
                        SavePhrase();
                        return;
                    }

                    return;
                }

            case FocusStep.Cancel:
                {
                    if (_signSaved)
                    {
                        if (left || up)
                        {
                            SetFocus(FocusStep.Sign);
                            return;
                        }

                        if (right || down)
                        {
                            SetFocus(FocusStep.Confirm);
                            return;
                        }
                    }
                    else
                    {
                        if (left || up)
                        {
                            if (IsPhraseComplete())
                                SetFocus(FocusStep.Save);
                            else
                                SetFocus(FocusStep.Sign);

                            return;
                        }

                        if (right || down)
                        {
                            SetFocus(FocusStep.Back);
                            return;
                        }
                    }

                    if (enter)
                    {
                        CancelPhrase();
                        return;
                    }

                    return;
                }

            case FocusStep.Confirm:
                {
                    if (left || up)
                    {
                        SetFocus(FocusStep.Cancel);
                        return;
                    }

                    if (right || down)
                    {
                        SetFocus(FocusStep.Back);
                        return;
                    }

                    if (enter)
                    {
                        ConfirmCloseAccount();
                        return;
                    }

                    return;
                }

            case FocusStep.Back:
                {
                    if (left || up)
                    {
                        if (_signSaved)
                            SetFocus(FocusStep.Confirm);
                        else if (IsPhraseComplete())
                            SetFocus(FocusStep.Save);
                        else if (HasAnyPhraseProgress())
                            SetFocus(FocusStep.Cancel);
                        else
                            SetFocus(FocusStep.Sign);

                        return;
                    }

                    if (down)
                    {
                        SetFocus(FocusStep.Sign);
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

    public void Open(AccountOpsPanel host, AccountOperationsUI ownerUi, int accountId)
    {
        _host = host;
        owner = ownerUi;
        _accountId = accountId;

        _open = true;
        _busy = false;
        _signSaved = false;
        _suppressSubmitFrames = 2;

        _phraseIndex = 0;

        if (phraseTodoText)
        {
            phraseTodoText.text = requiredPhrase;
            phraseTodoText.gameObject.SetActive(true);
        }

        if (phraseDoneText)
        {
            phraseDoneText.text = requiredPhrase;
            phraseDoneText.gameObject.SetActive(true);
            phraseDoneText.maxVisibleCharacters = 0;
        }

        UpdatePhraseVisual();

        if (processingRoot) processingRoot.SetActive(false);
        if (processingText) processingText.text = "";

        Show(true);
        RefreshCheckState();
        RefreshAll();

        SetFocus(IsLockedToBackOnly() ? FocusStep.Back : FocusStep.Sign);
    }

    public void Close(bool returnToMenu)
    {
        StopProcessing();

        _open = false;
        _busy = false;
        Show(false);

        if (returnToMenu)
            _host?.OpenMenu();
    }

    private void RefreshCheckState()
    {
        if (BankSystem.Instance == null)
            return;

        _check = BankSystem.Instance.EvaluateCloseAccount(_accountId);
    }

    private bool IsLockedToBackOnly()
    {
        return !_check.allConditionsMet;
    }

    private bool IsPhraseValid()
    {
        return IsPhraseComplete();
    }

    private bool CanSavePhrase()
    {
        return !_busy && _check.allConditionsMet && !_signSaved && IsPhraseValid();
    }

    private bool CanCancelPhrase()
    {
        return !_busy && _check.allConditionsMet && (_signSaved || HasAnyPhraseProgress());
    }

    private bool CanConfirmClose()
    {
        return !_busy && _check.allConditionsMet && _signSaved;
    }

    private void SavePhrase()
    {
        if (!CanSavePhrase()) return;

        _signSaved = true;
        SetFocus(FocusStep.Confirm);
    }

    private void CancelPhrase()
    {
        if (!CanCancelPhrase()) return;

        _signSaved = false;
        _phraseIndex = 0;
        UpdatePhraseVisual();

        SetFocus(FocusStep.Sign);
    }

    private void ConfirmCloseAccount()
    {
        if (!CanConfirmClose()) return;

        StopProcessing();
        _processingCo = StartCoroutine(CoCloseAccount());
    }

    private IEnumerator CoCloseAccount()
    {
        _busy = true;
        RefreshAll();

        if (processingRoot) processingRoot.SetActive(true);

        yield return RunProcessingPhase("PROCESSING", deletingPhaseSeconds);
        yield return RunProcessingPhase("DELETING", confirmingPhaseSeconds);
        yield return RunProcessingPhase("CONFIRMING", finalizingPhaseSeconds);

        string reason = "";
        bool ok = false;

        var bank = BankSystem.Instance;
        if (bank != null)
        {
            var cardsToReturn = bank.GetReturnableCardsForAccount(_accountId);
            if (InventoryUI.Instance != null && cardsToReturn != null)
            {
                for (int i = 0; i < cardsToReturn.Count; i++)
                {
                    var rec = cardsToReturn[i];
                    if (rec == null || string.IsNullOrWhiteSpace(rec.cardId))
                        continue;

                    InventoryUI.Instance.RemoveBankCardId(rec.cardId);
                }
            }

            ok = bank.TryCloseAccount(_accountId, out reason);
        }
        else
        {
            reason = "BANK SYSTEM MISSING";
        }

        if (!ok)
        {
            _busy = false;

            if (processingText)
            {
                processingText.text = string.IsNullOrEmpty(reason) ? "FAILED" : reason;
                processingText.color = deniedColor;
            }

            RefreshCheckState();
            RefreshAll();
            SetFocus(FocusStep.Back);
            yield break;
        }

        _open = false;
        Show(false);

        if (owner != null)
            owner.ReturnToDialogueStart();
    }

    private IEnumerator RunProcessingPhase(string label, float duration)
    {
        if (processingText == null)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        processingText.color = processingColor;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            int dots = ((int)(elapsed * 3f)) % 4;
            string suffix = dots == 0 ? "" : new string('.', dots);
            processingText.text = $"{label}{suffix}";
            yield return null;
        }
    }

    private void RefreshAll()
    {
        RefreshValues();
        RefreshBlocks();
        RefreshSelecteds();
    }

    private void RefreshValues()
    {
        SetCheckValue(currentLoansValue, _check.hasCurrentLoans ? "DENIED" : "NONE", !_check.hasCurrentLoans);
        SetCheckValue(accountBalanceValue, _check.balanceNotZero ? "DENIED" : "NONE", !_check.balanceNotZero);
        SetCheckValue(returnCardsValue, _check.hasActiveCards ? "DENIED" : "NONE", !_check.hasActiveCards);
        SetCheckValue(allConditionsValue, _check.allConditionsMet ? "NONE" : "DENIED", _check.allConditionsMet);

        if (signBlocked)
            signBlocked.SetActive(!_check.allConditionsMet || _busy || _signSaved);
    }

    private void RefreshBlocks()
    {
        if (saveButton) saveButton.interactable = CanSavePhrase();
        if (cancelButton) cancelButton.interactable = CanCancelPhrase();
        if (confirmButton) confirmButton.interactable = CanConfirmClose();

        if (saveBlocked) saveBlocked.SetActive(!CanSavePhrase());
        if (cancelBlocked) cancelBlocked.SetActive(!CanCancelPhrase());
        if (confirmBlocked) confirmBlocked.SetActive(!CanConfirmClose());
    }

    private void RefreshSelecteds()
    {
        if (signSelected) signSelected.SetActive(_focus == FocusStep.Sign);
        if (selConfirm) selConfirm.SetActive(_focus == FocusStep.Confirm);
        if (selBack) selBack.SetActive(_focus == FocusStep.Back);

        if (EventSystem.current == null)
            return;

        GameObject go = null;

        switch (_focus)
        {
            case FocusStep.Save:
                go = saveButton ? saveButton.gameObject : null;
                break;
            case FocusStep.Cancel:
                go = cancelButton ? cancelButton.gameObject : null;
                break;
            case FocusStep.Confirm:
                go = confirmButton ? confirmButton.gameObject : null;
                break;
            case FocusStep.Back:
                go = backButton ? backButton.gameObject : null;
                break;
            case FocusStep.Sign:
            default:
                go = null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(go);
    }

    private void SetFocus(FocusStep step)
    {
        _focus = step;
        RefreshAll();
    }

    private static void SetCheckValue(TMP_Text label, string text, bool ok)
    {
        if (!label) return;
        label.text = text;
        label.color = ok ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
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

    private void HandlePhraseTyping()
    {
        if (_signSaved || _busy || !_check.allConditionsMet)
            return;

        // BACKSPACE zawsze ma dzia³aæ, nawet gdy aktualny expected znak to spacja
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_phraseIndex > 0)
            {
                _phraseIndex--;
                UpdatePhraseVisual();
                RefreshAll();
            }
            return;
        }

        if (_phraseIndex >= requiredPhrase.Length)
            return;

        char expected = requiredPhrase[_phraseIndex];

        if (expected == ' ')
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _phraseIndex++;
                UpdatePhraseVisual();
                RefreshAll();
            }
            return;
        }

        for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
        {
            if (Input.GetKeyDown(k))
            {
                char typed = (char)('A' + (k - KeyCode.A));
                if (typed == expected)
                {
                    _phraseIndex++;
                    UpdatePhraseVisual();
                    RefreshAll();
                }
                return;
            }
        }
    }

    private void UpdatePhraseVisual()
    {
        if (phraseDoneText)
            phraseDoneText.maxVisibleCharacters = Mathf.Clamp(_phraseIndex, 0, requiredPhrase.Length);
    }

    private bool HasAnyPhraseProgress()
    {
        return _phraseIndex > 0;
    }

    private bool IsPhraseComplete()
    {
        return _phraseIndex >= requiredPhrase.Length;
    }
}