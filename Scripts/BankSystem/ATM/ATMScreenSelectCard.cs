using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ATMScreenSelectCard : MonoBehaviour
{
    [Header("UI")]
    public Transform cardListRoot;
    public ATMCardEntryView cardEntryPrefab;
    public ATMUIController controller; // przypnij w Inspectorze

    private readonly List<ATMCardEntryView> _views = new();
    private readonly List<InventoryItemInstance> _cards = new();
    private readonly List<bool> _selectable = new(); // ACTIVE + registered

    private int _index = 0;

    public void OpenAndRebuild()
    {
        BuildListFromInventory();

        // jeśli nie ma w ogóle kart w inventory -> NoCard
        if (_cards.Count == 0)
        {
            controller?.ShowNoCardScreen();
            return;
        }

        // wybierz pierwszy wpis (nawet jeśli Pending), żeby UI miało highlight
        _index = 0;
        RefreshSelection();
    }


    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            Move(+1);

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            Move(-1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            var selected = GetSelectedCardOrNull();
            if (selected != null)
                controller.SelectCard(selected);
        }
    }

    InventoryItemInstance GetSelectedCardOrNull()
    {
        if (_cards.Count == 0) return null;
        if (_index < 0 || _index >= _cards.Count) return null;
        if (_selectable[_index] == false) return null;
        return _cards[_index];
    }

    void Move(int dir)
    {
        if (_cards.Count == 0) return;

        int start = _index;
        for (int tries = 0; tries < _cards.Count; tries++)
        {
            _index = (_index + dir + _cards.Count) % _cards.Count;
            if (_selectable[_index]) break;
        }

        if (start != _index) RefreshSelection();
    }

    void SelectFirstValid()
    {
        _index = 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_selectable[i])
            {
                _index = i;
                return;
            }
        }
    }

    void RefreshSelection()
    {
        for (int i = 0; i < _views.Count; i++)
            _views[i].SetSelected(i == _index);
    }

    void BuildListFromInventory()
    {
        // clear UI
        if (cardListRoot)
        {
            foreach (Transform c in cardListRoot)
                Destroy(c.gameObject);
        }

        _views.Clear();
        _cards.Clear();
        _selectable.Clear();

        var inv = InventoryUI.Instance;
        if (inv == null) return;

        var all = inv.GetAllInstancesDistinct();

        var cards = all
            .Where(i => i != null && i.data is BankCardItemData && i.hasBankCardMeta && !string.IsNullOrWhiteSpace(i.bankCard.cardId))
            .ToList();

        _cards.AddRange(cards);

        var bank = BankSystem.Instance;
        bool bankOk = (bank != null);

        foreach (var inst in _cards)
        {
            // 1) Registered? + "moment użycia" -> sync status z banku (tylko gdy bank istnieje)
            bool registered = false;

            if (bankOk && bank.TryGetCardRecordEffective(inst.bankCard.cardId, out var rec))
            {
                registered = true;

                // Aktualizujemy tylko to co bank jest w stanie zweryfikować
                inst.bankCard.status = rec.status;
                inst.bankCard.activateAt = rec.activateAt;
                inst.bankCard.colorVariant = rec.colorVariant;
                inst.bankCard.accountId = rec.accountId;
            }

            // 2) Selectable tylko gdy registered + Active
            bool isActive = (inst.bankCard.status == BankCardStatus.Active);
            bool selectable = registered && isActive;
            _selectable.Add(selectable);

            // 3) View
            var v = Instantiate(cardEntryPrefab, cardListRoot);
            v.Bind(inst);

            // zablokowane wizualnie, jeśli nie da się wybrać
            v.SetBlocked(!selectable);

            // status text: INVALID jeśli nie istnieje w banku, inaczej pokazuj realny status (Pending/Revoked/Active)
            if (!registered)
            {
                if (v.statusText != null) v.statusText.text = "STATUS: INVALID";
            }
            else
            {
                if (v.statusText != null)
                    v.statusText.text = $"STATUS: {inst.bankCard.status.ToString().ToUpperInvariant()}";    
            }

            v.SetSelected(false);
            _views.Add(v);
        }
    }

    void OnEnable() => OpenAndRebuild();
}
