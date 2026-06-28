using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemAmountDialog : MonoBehaviour
{
    public static ItemAmountDialog Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Amount")]
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Slider slider;

    [Header("Buttons")]
    [SerializeField] private Button moreButton;
    [SerializeField] private Button lessButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Step")]
    [SerializeField] private int buttonStep = 1;

    private CanvasGroup canvasGroup;

    private int minValue;
    private int maxValue;
    private int currentValue;

    private Action<int> onConfirm;
    private Action onCancel;

    public bool IsOpen { get; private set; }

    private bool suppressCallbacks;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (root == null)
            root = gameObject;

        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        moreButton?.onClick.AddListener(() => Add(buttonStep));
        lessButton?.onClick.AddListener(() => Add(-buttonStep));

        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(Cancel);

        if (amountInput != null)
            amountInput.onValueChanged.AddListener(OnInputChanged);

        if (slider != null)
            slider.onValueChanged.AddListener(OnSliderChanged);

        Show(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool esc = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        bool enter = keyboard != null &&
                     (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        bool up = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
        bool down = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
#else
        bool esc = Input.GetKeyDown(KeyCode.Escape);
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
#endif

        if (esc)
        {
            Cancel();
            return;
        }

        if (enter)
        {
            Confirm();
            return;
        }

        if (up) Add(buttonStep);
        if (down) Add(-buttonStep);
    }

    public void Open(string title, int min, int max, int startValue, Action<int> confirm, Action cancel = null)
    {
        if (root == null)
            root = gameObject;

        transform.SetAsLastSibling();
        root.transform.SetAsLastSibling();

        minValue = Mathf.Max(0, min);
        maxValue = Mathf.Max(minValue, max);
        currentValue = Mathf.Clamp(startValue, minValue, maxValue);

        onConfirm = confirm;
        onCancel = cancel;

        if (titleText != null)
            titleText.text = title;

        if (slider != null)
        {
            slider.wholeNumbers = true;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
        }

        SetValue(currentValue, true);
        Show(true);

        if (amountInput != null)
        {
            amountInput.interactable = true;
            amountInput.Select();
            amountInput.ActivateInputField();
        }

        if (EventSystem.current != null && amountInput != null)
            EventSystem.current.SetSelectedGameObject(amountInput.gameObject);
    }

    public void Close()
    {
        Show(false);

        onConfirm = null;
        onCancel = null;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Confirm()
    {
        if (!IsOpen) return;

        int value = Mathf.Clamp(currentValue, minValue, maxValue);

        var callback = onConfirm;
        Close();

        callback?.Invoke(value);
    }

    private void Cancel()
    {
        if (!IsOpen) return;

        var callback = onCancel;
        Close();

        callback?.Invoke();
    }

    private void Add(int delta)
    {
        SetValue(currentValue + delta, true);
    }

    private void OnInputChanged(string txt)
    {
        if (suppressCallbacks) return;

        if (!int.TryParse(txt, out int parsed))
            parsed = minValue;

        SetValue(parsed, false);
    }

    private void OnSliderChanged(float value)
    {
        if (suppressCallbacks) return;

        SetValue(Mathf.RoundToInt(value), true);
    }

    private void SetValue(int value, bool updateInput)
    {
        currentValue = Mathf.Clamp(value, minValue, maxValue);

        suppressCallbacks = true;

        if (slider != null)
            slider.value = currentValue;

        if (updateInput && amountInput != null)
            amountInput.text = currentValue.ToString();

        suppressCallbacks = false;
    }

    private void Show(bool show)
    {
        IsOpen = show;

        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.ignoreParentGroups = false;
        }
    }
}