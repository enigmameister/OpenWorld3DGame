using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TransactionsHistoryPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("State")]
    [SerializeField] private GameObject recordRoot;
    [SerializeField] private TMP_Text emptyInfo;

    [Header("List")]
    [SerializeField] private Transform recordContainer;
    [SerializeField] private RecordTransactionView recordTransactionPrefab;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [SerializeField] private ScrollRect scrollRect;

    private readonly List<RecordTransactionView> _rows = new();

    private AccountOperationsUI _host;
    private bool _open;
    private int _accountId;

    private RecordTransactionView selected;

    public bool IsOpen => _open;

    private void Awake()
    {
        Show(false);

        if (backButton)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void Update()
    {
        if (!_open) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackClicked();
            return;
        }
    }

    public void Open(AccountOperationsUI host, int accountId)
    {
        _host = host;
        _accountId = accountId;
        _open = true;

        Rebuild();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        Show(true);

        if (backButton)
            backButton.Select();
    }

    public void Hide()
    {
        _open = false;
        ClearRows();
        Show(false);
    }

    public void Close(bool goBackToMenu)
    {
        Hide();
        if (goBackToMenu)
            _host?.ShowMainMenu();
    }

    private void OnBackClicked()
    {
        _host?.ConsumeEscapeThisFrame();
        Close(true);
    }

    private void Rebuild()
    {
        ClearRows();

        var bank = BankSystem.Instance;
        var items = bank != null ? bank.GetTransactionHistory(_accountId) : new List<BankTransactionRecord>();

        bool hasAny = items != null && items.Count > 0;

        if (recordRoot) recordRoot.SetActive(hasAny);
        if (emptyInfo) emptyInfo.gameObject.SetActive(!hasAny);

        if (!hasAny)
            return;

        int max = Mathf.Min(items.Count, 50);

        for (int i = 0; i < max; i++)
        {
            var row = Instantiate(recordTransactionPrefab, recordContainer);
            row.Bind(items[i]);
            row.Init(this);

            _rows.Add(row);
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }

        _rows.Clear();
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

    public void Select(RecordTransactionView item)
    {
        if (selected == item) return;

        if (selected != null)
            selected.SetSelected(false);

        selected = item;

        if (selected != null)
            selected.SetSelected(true);
    }

    public void ClearSelection()
    {
        Select(null);
    }
}