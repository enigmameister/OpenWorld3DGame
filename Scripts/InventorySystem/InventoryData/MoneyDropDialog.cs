using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using System.Text;
using UnityEngine.EventSystems;

public class MoneyDropDialog : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button closeXButton;

    [Header("Config")]
    [SerializeField] private int step = 10; // krok strzałek
    [SerializeField] private bool clampToPlayerMoney = true;

    private PlayerStats _player;
    private MoneyDropSpawner _spawner;

    private bool _isOpen;
    public bool IsOpen { get; private set; }

    private int _currentAmount;

    [Header("Window")]
    [SerializeField] private RectTransform windowRoot; // DropCashDialog RectTransform
    private Vector2 _defaultAnchoredPos;
    private bool _defaultPosSaved;


    void Awake()
    {
        // Canvas Group
        if (!group) group = GetComponent<CanvasGroup>();

        // Window root (CashDialogRoot)
        if (!windowRoot)
        {
            Debug.LogError("MoneyDropDialog: WindowRoot not assigned!");
        }
        else
        {
            _defaultAnchoredPos = windowRoot.anchoredPosition;
            _defaultPosSaved = true;
        }

        // Buttons
        upButton?.onClick.AddListener(() => AddStep(+step));
        downButton?.onClick.AddListener(() => AddStep(-step));
        dropButton?.onClick.AddListener(DoDrop);
        exitButton?.onClick.AddListener(Close);
        closeXButton?.onClick.AddListener(Close);

        // Input
        if (amountInput != null)
        {
            amountInput.contentType = TMP_InputField.ContentType.Custom;
            amountInput.characterValidation = TMP_InputField.CharacterValidation.None;

            amountInput.onValueChanged.AddListener(OnInputChanged);

            // Enter = Drop (TMP wywołuje EndEdit przy Enter)
            amountInput.onEndEdit.AddListener(_ =>
            {
                if (!_isOpen) return;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    DoDrop();
            });
        }


        // Start hidden
        CloseImmediate();
    }

    void Update()
    {
        if (!_isOpen) return;

        // Enter / NumpadEnter = Drop
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // opcjonalnie: jeśli fokus jest na inpucie albo ogólnie dialog ma fokus
            DoDrop();
        }

        // Esc = Close (opcjonalnie, ale fajne)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    private void HideImmediate()
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void Open(PlayerStats player, MoneyDropSpawner spawner)
    {
        IsOpen = true;
        _isOpen = true;
        _player = player;
        _spawner = spawner;

        _currentAmount = 0;
        SyncInput();

        // ✅ RESET pozycji zawsze
        if (windowRoot != null && _defaultPosSaved)
            windowRoot.anchoredPosition = _defaultAnchoredPos;

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        amountInput?.ActivateInputField();
    }

    public void Close()
    {
        IsOpen = false;
        _isOpen = false;

        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // ✅ reset pozycji po zamknięciu (żeby kolejne otwarcie zawsze startowało z default)
        if (windowRoot != null && _defaultPosSaved)
            windowRoot.anchoredPosition = _defaultAnchoredPos;
    }

    private void CloseImmediate()
    {
        _isOpen = false;
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private void OnInputChanged(string raw)
    {
        if (!_isOpen) return;

        int val = ParseDigitsToInt(raw);
        SetAmount(val);

        // sformatuj z przecinkami bez skakania kursora na początek
        string formatted = FormatInt(_currentAmount);
        if (amountInput != null && amountInput.text != formatted)
        {
            int caret = amountInput.caretPosition;
            amountInput.SetTextWithoutNotify(formatted);
            amountInput.caretPosition = Mathf.Min(caret, amountInput.text.Length);
        }
    }

    private void AddStep(int delta)
    {
        if (!_isOpen) return;
        SetAmount(_currentAmount + delta);
        SyncInput();
    }

    private void SetAmount(int value)
    {
        value = Mathf.Max(0, value);

        if (clampToPlayerMoney && _player != null)
            value = Mathf.Min(value, Mathf.Max(0, _player.money));

        _currentAmount = value;
    }

    private void SyncInput()
    {
        if (amountInput == null) return;
        amountInput.SetTextWithoutNotify(FormatInt(_currentAmount));
    }

    private void DoDrop()
    {
        if (_player == null || _spawner == null) { Close(); return; }

        int amount = _currentAmount;
        if (amount <= 0) { Close(); return; }

        // clamp final
        amount = Mathf.Min(amount, Mathf.Max(0, _player.money));
        if (amount <= 0) { Close(); return; }

        // odejmij z konta
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.ApplyMoneyChange(-amount);
        else
            _player.SetMoney(_player.money - amount);

        // spawn cash na ziemi
        _spawner.SpawnCash(amount);

        Close();
    }

    private static readonly CultureInfo CommaCulture = CultureInfo.GetCultureInfo("en-US");

    private int ParseDigitsToInt(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;

        // zostaw tylko cyfry
        int result = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c < '0' || c > '9') continue;

            int digit = c - '0';

            // overflow-safe (int)
            if (result > (int.MaxValue - digit) / 10)
                return int.MaxValue;

            result = result * 10 + digit;
        }
        return result;
    }

    private string FormatInt(int value)
    {
        return value.ToString("N0", CommaCulture); // 1,000,000
    }

}
