using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RestructureLoanPanelUI : MonoBehaviour
{
    public enum Focus
    {
        Installment,
        Request,
        Suggest,
        Confirm,
        Cancel,
        Back
    }

    [Header("Owner")]
    [SerializeField] private LoanMenuNavigation owner;
    [SerializeField] private CanvasGroup root;

    [Header("Current Loan Summary")]
    [SerializeField] private GameObject resumeRootCurrent;
    [SerializeField] private TMP_Text currentAmountValue;
    [SerializeField] private TMP_Text currentInstallmentValue;
    [SerializeField] private TMP_Text currentTaxValue;
    [SerializeField] private TMP_Text currentLoanPlayerValue;
    [SerializeField] private TMP_Text currentLoanTotalValue;

    [Header("Top Limit / Status")]
    [SerializeField] private TMP_Text limitText;

    [Header("Sections - Selected")]
    [SerializeField] private GameObject selectedInstallment;
    [SerializeField] private GameObject selectedRequest;
    [SerializeField] private GameObject selectedSuggest;
    [SerializeField] private Image suggestBorderImage;
    [SerializeField] private Color suggestNormalColor = Color.white;
    [SerializeField] private Color suggestSelectedColor = Color.green;

    [Header("Sections - Blocked")]
    [SerializeField] private GameObject installmentBlocked;
    [SerializeField] private GameObject requestBlocked;
    [SerializeField] private GameObject statusBlocked;
    [SerializeField] private GameObject confirmBlockedOverlay;
    [SerializeField] private GameObject cancelBlockedOverlay;

    [Header("Installment")]
    [SerializeField] private TMP_Text installmentValueText;
    [SerializeField] private Button installmentLessButton;
    [SerializeField] private Button installmentMoreButton;

    [Header("Request")]
    [SerializeField] private Button requestButton;
    [SerializeField] private TMP_Text bankProcessing;

    [Header("Status")]
    [SerializeField] private TMP_Text resultText;

    [Header("Suggestions")]
    [SerializeField] private GameObject resumeRootSuggest;
    [SerializeField] private CanvasGroup loadingSuggest;
    [SerializeField] private TMP_Text loadingSuggestText;
    [SerializeField] private Button leftSlideButton;
    [SerializeField] private Button rightSlideButton;
    [SerializeField] private RectTransform bankSuggestContainer;
    [SerializeField] private RestructureLoanSuggestPageView suggestPagePrefab;

    [Header("Bottom Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button backButton;

    [Header("Bottom Selected")]
    [SerializeField] private GameObject selectedConfirm;
    [SerializeField] private GameObject selectedCancel;
    [SerializeField] private GameObject selectedBack;

    [Header("Bottom Processing")]
    [SerializeField] private TMP_Text resumeProcessing;

    [Header("Timing")]
    [SerializeField] private float requestDelaySeconds = 5f;
    [SerializeField] private float confirmDelaySeconds = 5f;
    [SerializeField] private float suggestSwitchDelaySeconds = 0.5f;

    private Coroutine _loadingSuggestAnimCo;

    private ActiveLoan _loan;
    private Focus _focus;

    private bool _open;
    private bool _lockedByRule;
    private bool _isProcessing;
    private bool _hasOffers;

    private int _requestedMonths;
    private int _selectedOfferIndex;

    private List<LoanSystem.LoanRestructureOffer> _offers = new();

    private Coroutine _requestCo;
    private Coroutine _processingAnimCo;
    private Coroutine _confirmCo;
    private Coroutine _switchSuggestCo;

    [Header("Suggest Arrow Flash")]
    [SerializeField] private Image leftArrowImage;
    [SerializeField] private Image rightArrowImage;
    [SerializeField] private float arrowFlashDuration = 0.12f;

    private Coroutine _leftArrowFlashCo;
    private Coroutine _rightArrowFlashCo;
    private Color _leftArrowBaseColor;
    private Color _rightArrowBaseColor;

    public bool IsOpen => _open;

    private void Awake()
    {
        if (leftArrowImage != null)
            _leftArrowBaseColor = leftArrowImage.color;

        if (rightArrowImage != null)
            _rightArrowBaseColor = rightArrowImage.color;

        Show(false);
    }

    public void Open(LoanMenuNavigation nav, ActiveLoan loan)
    {
        owner = nav;
        _loan = loan;
        _open = true;

        _isProcessing = false;
        _hasOffers = false;
        _offers.Clear();
        _selectedOfferIndex = 0;

        if (resumeRootSuggest) resumeRootSuggest.SetActive(false);
        SetLoadingSuggestVisible(false);

        if (bankProcessing) bankProcessing.gameObject.SetActive(false);
        if (resumeProcessing) resumeProcessing.gameObject.SetActive(false);

        BindCurrentLoan();

        ReevaluateAvailability();

        _requestedMonths = LoanSystem.Instance != null
            ? LoanSystem.Instance.GetMinRestructureMonths(_loan)
            : Mathf.Max((_loan != null ? _loan.installmentsLeft : 1) + 1, 2);

        if (installmentValueText)
            installmentValueText.text = _requestedMonths.ToString();

        _focus = _lockedByRule ? Focus.Back : Focus.Installment;

        resultText.text = "RESULT";
        RefreshAll();
        Show(true);
    }

    public void Hide()
    {
        _open = false;
        StopAllProcessing();
        ClearSuggestions();
        Show(false);
    }

    private void Update()
    {
        if (!_open || root == null || root.alpha < 0.5f)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BankDialogueUI.SuppressEscapeFrames = 2;
            OnBack();
            return;
        }

        if (_isProcessing)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveFocus(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveFocus(+1);

        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.01f) right = true;
        if (wheel < -0.01f) left = true;

        if (left) OnAdjust(-1);
        if (right) OnAdjust(+1);

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            ActivateFocused();
        }
    }

    private void ReevaluateAvailability()
    {
        _lockedByRule = true;

        if (LoanSystem.Instance == null)
        {
            if (limitText) limitText.text = "LOAN SYSTEM MISSING";
            return;
        }

        if (!LoanSystem.Instance.CanRestructureLoan(_loan, out string reason))
        {
            if (limitText) limitText.text = reason;
            _lockedByRule = true;
            return;
        }

        if (limitText) limitText.text = "";
        _lockedByRule = false;
    }

    private void BindCurrentLoan()
    {
        if (_loan == null) return;

        if (resumeRootCurrent) resumeRootCurrent.SetActive(true);

        if (currentAmountValue) currentAmountValue.text = $"{_loan.principal:n0}$";
        if (currentInstallmentValue) currentInstallmentValue.text = $"{_loan.installmentsLeft} DAYS";

        if (currentTaxValue)
        {
            float percent = _loan.principal > 0
                ? (_loan.bankTax / (float)_loan.principal) * 100f
                : 0f;

            currentTaxValue.text = $"{_loan.bankTax:n0}$ ({percent:0.#}%)";
        }

        if (currentLoanPlayerValue) currentLoanPlayerValue.text = $"{_loan.remainingToRepay:n0}$";
        if (currentLoanTotalValue) currentLoanTotalValue.text = $"{_loan.totalToRepay:n0}$";
    }

    private void MoveFocus(int dir)
    {
        if (_lockedByRule)
        {
            _focus = Focus.Back;
            RefreshAll();
            return;
        }

        Focus[] order = _hasOffers
            ? new[] { Focus.Installment, Focus.Request, Focus.Suggest, Focus.Confirm, Focus.Cancel, Focus.Back }
            : new[] { Focus.Installment, Focus.Request, Focus.Back };

        int idx = System.Array.IndexOf(order, _focus);
        if (idx < 0) idx = 0;

        idx = Mathf.Clamp(idx + dir, 0, order.Length - 1);
        _focus = order[idx];

        RefreshAll();
    }

    private void OnAdjust(int delta)
    {
        if (_lockedByRule || _isProcessing)
            return;

        switch (_focus)
        {
            case Focus.Installment:
                {
                    if (LoanSystem.Instance == null || _loan == null) return;

                    int minMonths = LoanSystem.Instance.GetMinRestructureMonths(_loan);
                    int maxMonths = LoanSystem.Instance.restructureMaxMonths;

                    _requestedMonths = Mathf.Clamp(_requestedMonths + delta, minMonths, maxMonths);
                    if (installmentValueText) installmentValueText.text = _requestedMonths.ToString();

                    InvalidateOffers();
                    break;
                }

            case Focus.Suggest:
                {
                    if (!_hasOffers || _offers.Count == 0)
                        return;

                    if (delta < 0)
                        FlashSuggestArrow(false);
                    else if (delta > 0)
                        FlashSuggestArrow(true);

                    int next = Mathf.Clamp(_selectedOfferIndex + delta, 0, _offers.Count - 1);
                    if (next == _selectedOfferIndex)
                        return;

                    StartSwitchSuggestion(next);
                    break;
                }
        }

        RefreshAll();
    }

    private void ActivateFocused()
    {
        if (_lockedByRule)
        {
            OnBack();
            return;
        }

        switch (_focus)
        {
            case Focus.Request:
                StartRequest();
                break;

            case Focus.Confirm:
                OnConfirm();
                break;

            case Focus.Cancel:
                OnCancel();
                break;

            case Focus.Back:
                OnBack();
                break;
        }
    }

    private void StartRequest()
    {
        if (_isProcessing || _lockedByRule || LoanSystem.Instance == null || _loan == null)
            return;

        StopAllProcessing();
        _requestCo = StartCoroutine(CoRequestOffers());
    }

    private IEnumerator CoRequestOffers()
    {
        _isProcessing = true;

        StartBankProcessingAnim();
        resultText.text = "RESULT";
        InvalidateOffers();

        RefreshAll();

        yield return new WaitForSecondsRealtime(requestDelaySeconds);

        _offers = LoanSystem.Instance.BuildRestructureOffers(_loan, _requestedMonths);
        _selectedOfferIndex = 0;

        _isProcessing = false;
        StopBankProcessingAnim();

        if (_offers != null && _offers.Count > 0)
        {
            _hasOffers = true;
            resultText.text = "APPROVED";
            BuildSuggestionPages();

            if (resumeRootSuggest) resumeRootSuggest.SetActive(true);
            _focus = Focus.Suggest;
        }
        else
        {
            _hasOffers = false;
            resultText.text = "DENIED";
            if (resumeRootSuggest) resumeRootSuggest.SetActive(false);
            _focus = Focus.Request;
        }

        RefreshAll();
    }

    private void BuildSuggestionPages()
    {
        ClearSuggestions();

        if (bankSuggestContainer == null || suggestPagePrefab == null || _offers == null || _loan == null)
            return;

        for (int i = 0; i < _offers.Count; i++)
        {
            var page = Instantiate(suggestPagePrefab, bankSuggestContainer);
            page.gameObject.SetActive(i == _selectedOfferIndex);
            page.Bind(_offers[i], _loan);
        }
    }

    private void ClearSuggestions()
    {
        if (bankSuggestContainer == null)
            return;

        for (int i = bankSuggestContainer.childCount - 1; i >= 0; i--)
            Destroy(bankSuggestContainer.GetChild(i).gameObject);
    }

    private void HideAllSuggestionPages()
    {
        if (bankSuggestContainer == null)
            return;

        for (int i = 0; i < bankSuggestContainer.childCount; i++)
            bankSuggestContainer.GetChild(i).gameObject.SetActive(false);
    }

    private void StartSwitchSuggestion(int targetIndex)
    {
        if (_switchSuggestCo != null)
            StopCoroutine(_switchSuggestCo);

        _switchSuggestCo = StartCoroutine(CoSwitchSuggestion(targetIndex));
    }

    private IEnumerator CoSwitchSuggestion(int targetIndex)
    {
        HideAllSuggestionPages();
        SetLoadingSuggestVisible(true);

        yield return new WaitForSecondsRealtime(suggestSwitchDelaySeconds);

        _selectedOfferIndex = targetIndex;
        RefreshSuggestionPages();

        SetLoadingSuggestVisible(false);
        RefreshAll();
    }

    private void RefreshSuggestionPages()
    {
        if (bankSuggestContainer == null)
            return;

        for (int i = 0; i < bankSuggestContainer.childCount; i++)
        {
            var child = bankSuggestContainer.GetChild(i);
            child.gameObject.SetActive(i == _selectedOfferIndex);
        }
    }

    private void SetLoadingSuggestVisible(bool visible)
    {
        if (loadingSuggest == null)
            return;

        loadingSuggest.alpha = visible ? 1f : 0f;
        loadingSuggest.interactable = visible;
        loadingSuggest.blocksRaycasts = visible;
        loadingSuggest.gameObject.SetActive(visible);

        if (_loadingSuggestAnimCo != null)
        {
            StopCoroutine(_loadingSuggestAnimCo);
            _loadingSuggestAnimCo = null;
        }

        if (visible)
        {
            if (loadingSuggestText != null)
                _loadingSuggestAnimCo = StartCoroutine(CoDots(loadingSuggestText, "LOADING"));
        }
        else
        {
            if (loadingSuggestText != null)
                loadingSuggestText.text = "";
        }
    }

    private void OnConfirm()
    {
        if (_isProcessing || !_hasOffers || _offers == null || _offers.Count == 0)
            return;

        if (_selectedOfferIndex < 0 || _selectedOfferIndex >= _offers.Count)
            return;

        StopAllProcessing();
        _confirmCo = StartCoroutine(CoConfirmOffer());
    }

    private IEnumerator CoConfirmOffer()
    {
        _isProcessing = true;
        StartResumeProcessingAnim();
        RefreshAll();

        yield return new WaitForSecondsRealtime(confirmDelaySeconds);

        bool success = false;
        string reason = "";

        if (LoanSystem.Instance != null && _loan != null)
        {
            success = LoanSystem.Instance.ApplyRestructureOffer(
                _loan,
                _offers[_selectedOfferIndex],
                out reason
            );
        }

        _isProcessing = false;
        StopResumeProcessingAnim();

        if (success)
        {
            owner?.Owner?.Refresh();
            ReevaluateAvailability();
            BindCurrentLoan();

            resultText.text = "SUCCESS";
            InvalidateOffers();
            _focus = Focus.Back;
        }
        else
        {
            resultText.text = string.IsNullOrWhiteSpace(reason) ? "DENIED" : reason;
        }

        RefreshAll();
    }

    private void OnCancel()
    {
        if (_isProcessing)
            return;

        InvalidateOffers();
        resultText.text = "RESULT";
        _focus = _lockedByRule ? Focus.Back : Focus.Installment;
        RefreshAll();
    }

    private void InvalidateOffers()
    {
        _hasOffers = false;
        _offers.Clear();
        _selectedOfferIndex = 0;

        if (resumeRootSuggest) resumeRootSuggest.SetActive(false);
        SetLoadingSuggestVisible(false);
        ClearSuggestions();
    }

    private void OnBack()
    {
        if (owner != null)
            owner.ConsumeEscapeThisFrame();

        BankDialogueUI.SuppressEscapeFrames = 2;

        var dlg = FindFirstObjectByType<BankDialogueUI>(FindObjectsInactive.Include);
        if (dlg != null && dlg.IsOpen)
            dlg.Close(unlockPlayer: false);

        Hide();

        if (owner != null)
            owner.CloseRestructureLoan();
    }

    private void StopAllProcessing()
    {
        if (_requestCo != null)
        {
            StopCoroutine(_requestCo);
            _requestCo = null;
        }

        if (_processingAnimCo != null)
        {
            StopCoroutine(_processingAnimCo);
            _processingAnimCo = null;
        }

        if (_confirmCo != null)
        {
            StopCoroutine(_confirmCo);
            _confirmCo = null;
        }

        if (_switchSuggestCo != null)
        {
            StopCoroutine(_switchSuggestCo);
            _switchSuggestCo = null;
        }

        if (_loadingSuggestAnimCo != null)
        {
            StopCoroutine(_loadingSuggestAnimCo);
            _loadingSuggestAnimCo = null;
        }

        _isProcessing = false;

        StopBankProcessingAnim();
        StopResumeProcessingAnim();
        SetLoadingSuggestVisible(false);
    }

    private void RefreshAll()
    {
        bool canInstallment = !_lockedByRule && !_isProcessing;
        bool canRequest = !_lockedByRule && !_isProcessing;
        bool canSuggest = !_lockedByRule && !_isProcessing && _hasOffers && _offers.Count > 0;
        bool canConfirm = canSuggest;
        bool canCancel = _hasOffers && !_isProcessing;
        bool canBack = !_isProcessing;

        if (installmentValueText)
            installmentValueText.text = _requestedMonths.ToString();

        if (installmentBlocked) installmentBlocked.SetActive(!canInstallment || _focus != Focus.Installment);
        if (requestBlocked) requestBlocked.SetActive(!canRequest || _focus != Focus.Request);
        if (statusBlocked) statusBlocked.SetActive(!_isProcessing);

        if (requestButton) requestButton.interactable = canRequest;
        if (confirmButton) confirmButton.interactable = canConfirm;
        if (cancelButton) cancelButton.interactable = canCancel;
        if (backButton) backButton.interactable = canBack;

        if (installmentLessButton) installmentLessButton.interactable = canInstallment;
        if (installmentMoreButton) installmentMoreButton.interactable = canInstallment;
        if (leftSlideButton) leftSlideButton.interactable = canSuggest;
        if (rightSlideButton) rightSlideButton.interactable = canSuggest;

        if (confirmBlockedOverlay) confirmBlockedOverlay.SetActive(!canConfirm);
        if (cancelBlockedOverlay) cancelBlockedOverlay.SetActive(!canCancel);

        if (selectedInstallment) selectedInstallment.SetActive(_focus == Focus.Installment && canInstallment);
        if (selectedRequest) selectedRequest.SetActive(_focus == Focus.Request && canRequest);
        if (selectedSuggest) selectedSuggest.SetActive(_focus == Focus.Suggest && canSuggest);

        if (selectedConfirm) selectedConfirm.SetActive(_focus == Focus.Confirm && canConfirm);
        if (selectedCancel) selectedCancel.SetActive(_focus == Focus.Cancel && canCancel);
        if (selectedBack) selectedBack.SetActive(_focus == Focus.Back);

        if (suggestBorderImage != null)
            suggestBorderImage.color = (_focus == Focus.Suggest && canSuggest)
                ? suggestSelectedColor
                : suggestNormalColor;

        RefreshFocusEventSystem();
    }

    private void RefreshFocusEventSystem()
    {
        if (EventSystem.current == null)
            return;

        GameObject go = null;

        switch (_focus)
        {
            case Focus.Request:
                go = requestButton ? requestButton.gameObject : null;
                break;

            case Focus.Confirm:
                go = confirmButton ? confirmButton.gameObject : null;
                break;

            case Focus.Cancel:
                go = cancelButton ? cancelButton.gameObject : null;
                break;

            case Focus.Back:
                go = backButton ? backButton.gameObject : null;
                break;

            case Focus.Suggest:
                go = null;
                break;
        }

        EventSystem.current.SetSelectedGameObject(go);
    }

    private void Show(bool visible)
    {
        if (!root)
        {
            gameObject.SetActive(visible);
            return;
        }

        root.gameObject.SetActive(visible);
        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    private IEnumerator CoDots(TMP_Text target, string prefix)
    {
        if (target == null) yield break;

        int step = 0;
        while (true)
        {
            step = (step + 1) % 4;
            string dots = new string('.', step);
            target.text = $"{prefix}{dots}";
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    private void StartBankProcessingAnim()
    {
        if (bankProcessing == null) return;

        bankProcessing.gameObject.SetActive(true);

        if (_processingAnimCo != null)
            StopCoroutine(_processingAnimCo);

        _processingAnimCo = StartCoroutine(CoDots(bankProcessing, "PROCESSING"));
    }

    private void StopBankProcessingAnim()
    {
        if (_processingAnimCo != null)
        {
            StopCoroutine(_processingAnimCo);
            _processingAnimCo = null;
        }

        if (bankProcessing != null)
        {
            bankProcessing.text = "";
            bankProcessing.gameObject.SetActive(false);
        }
    }

    private void StartResumeProcessingAnim()
    {
        if (resumeProcessing == null) return;

        resumeProcessing.gameObject.SetActive(true);

        if (_confirmCo != null)
            StopCoroutine(_confirmCo);

        _confirmCo = StartCoroutine(CoDots(resumeProcessing, "PROCESSING"));
    }

    private void StopResumeProcessingAnim()
    {
        if (_confirmCo != null)
        {
            StopCoroutine(_confirmCo);
            _confirmCo = null;
        }

        if (resumeProcessing != null)
        {
            resumeProcessing.text = "";
            resumeProcessing.gameObject.SetActive(false);
        }
    }

    // ===== Inspector OnClick handlers =====

    public void OnClickInstallmentLess()
    {
        _focus = Focus.Installment;
        OnAdjust(-1);
    }

    public void OnClickInstallmentMore()
    {
        _focus = Focus.Installment;
        OnAdjust(+1);
    }

    public void OnClickRequest()
    {
        _focus = Focus.Request;
        StartRequest();
    }

    public void OnClickLeftSuggest()
    {
        _focus = Focus.Suggest;
        OnAdjust(-1);
    }

    public void OnClickRightSuggest()
    {
        _focus = Focus.Suggest;
        OnAdjust(+1);
    }

    public void OnClickConfirm()
    {
        _focus = Focus.Confirm;
        OnConfirm();
    }

    public void OnClickCancel()
    {
        _focus = Focus.Cancel;
        OnCancel();
    }

    public void OnClickBack()
    {
        _focus = Focus.Back;
        OnBack();
    }

    private void FlashSuggestArrow(bool right)
    {
        if (right)
        {
            if (rightArrowImage == null) return;

            if (_rightArrowFlashCo != null)
                StopCoroutine(_rightArrowFlashCo);

            _rightArrowFlashCo = StartCoroutine(CoFlashArrow(rightArrowImage, _rightArrowBaseColor));
        }
        else
        {
            if (leftArrowImage == null) return;

            if (_leftArrowFlashCo != null)
                StopCoroutine(_leftArrowFlashCo);

            _leftArrowFlashCo = StartCoroutine(CoFlashArrow(leftArrowImage, _leftArrowBaseColor));
        }
    }

    private IEnumerator CoFlashArrow(Image img, Color baseColor)
    {
        if (img == null) yield break;

        Color pressedColor = Color.green;
        img.color = pressedColor;

        yield return new WaitForSecondsRealtime(arrowFlashDuration);

        if (img != null)
            img.color = baseColor;
    }
}