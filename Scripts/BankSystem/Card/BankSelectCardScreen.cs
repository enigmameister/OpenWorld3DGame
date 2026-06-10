using System;
using System.Collections.Generic;
using UnityEngine;

public class BankSelectCardScreen : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("List")]
    [SerializeField] private Transform cardList;              // Content
    [SerializeField] private BankCardEntryView entryPrefab;   // prefab wiersza

    private readonly List<BankCardEntryView> _views = new();
    private List<BankCardRecord> _cards = new();

    private int _selectedIndex;
    private bool _isOpen;
    private int _accountId;
    private Action<BankCardRecord> _onSelected;

    public void Open(int accountId, List<BankCardRecord> cards, Action<BankCardRecord> onSelected)
    {
        _accountId = accountId;
        _onSelected = onSelected;
        _isOpen = true;

        if (cards == null)
        {
            var bank = BankSystem.Instance;
            _cards = bank != null
                ? bank.GetCardsForAccount(accountId, includeDeleted: false)
                : new List<BankCardRecord>();
        }
        else
        {
            _cards = cards;
        }

        _selectedIndex = 0;
        RebuildList();
        Show(true);
    }

    public void Close()
    {
        _isOpen = false;
        Show(false);
        _onSelected = null;
    }

    private void RebuildList()
    {
        // clear
        for (int i = cardList.childCount - 1; i >= 0; i--)
            Destroy(cardList.GetChild(i).gameObject);

        _views.Clear();

        if (_cards == null || _cards.Count == 0)
            return;

        for (int i = 0; i < _cards.Count; i++)
        {
            var rec = _cards[i];
            var view = Instantiate(entryPrefab, cardList);

            view.Bind(rec);

            int idx = i;
            view.SetOnClick(() =>
            {
                _selectedIndex = idx;
                ApplySelection();
                ChooseCurrent();
            });

            view.SetSelected(false);
            _views.Add(view);
        }

        ApplySelection();
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (_cards == null || _cards.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(+1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ChooseCurrent();
    }

    private void MoveSelection(int dir)
    {
        int next = Mathf.Clamp(_selectedIndex + dir, 0, _cards.Count - 1);
        if (next == _selectedIndex) return;

        _selectedIndex = next;
        ApplySelection();
    }

    private void ApplySelection()
    {
        for (int i = 0; i < _views.Count; i++)
            _views[i].SetSelected(i == _selectedIndex);
    }

    private void ChooseCurrent()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _cards.Count) return;
        _onSelected?.Invoke(_cards[_selectedIndex]);
    }

    private void Show(bool v)
    {
        if (!root)
        {
            gameObject.SetActive(v);
            return;
        }

        root.alpha = v ? 1 : 0;
        root.interactable = v;
        root.blocksRaycasts = v;
        root.gameObject.SetActive(v);
    }
}
