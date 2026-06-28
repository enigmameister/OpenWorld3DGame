    using TMPro;
    using UnityEngine;
    public class ATMScreenAccountInfo : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI accWalletText;
        public TextMeshProUGUI playerWalletText;
        public TextMeshProUGUI cardIdText;
        public TextMeshProUGUI accIdText;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI variantText;

        [Header("Buttons (left->right)")]
    public ATMMenuButtonView operationsBtn;
    public ATMMenuButtonView accountSettingsBtn; // zamiast changePin/changeStatus

    private ATMMenuButtonView[] _buttons;
        private int _index;

        private InventoryItemInstance _card;
        private ATMUIController _ui;

        public void Open(InventoryItemInstance card, ATMUIController ui)
        {
            _card = card;
            _ui = ui;

            _buttons = new[] { operationsBtn, accountSettingsBtn };
            _index = 0;

            BindTexts();
            RefreshSelection();
        }

        void OnEnable()
        {
            // jeœli screen jest aktywowany bez Open() - nie wywalaj nullrefów
            if (_buttons == null || _buttons.Length == 0)
                _buttons = new[] { operationsBtn, accountSettingsBtn };

            RefreshSelection();
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _ui?.BackToSelectCardFromAccountInfo();
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                Move(-1);

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                Move(+1);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Submit();
        }

        private void Move(int dir)
        {
            if (_buttons == null || _buttons.Length == 0) return;

            _index = (_index + dir + _buttons.Length) % _buttons.Length;
            RefreshSelection();
        }

    private void Submit()
    {
        if (_ui == null) return;

        if (_index == 0) _ui.OpenOperations();
        else if (_index == 1) _ui.OpenAccountSettings();
    }

    private void RefreshSelection()
        {
            if (_buttons == null) return;
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null)
                    _buttons[i].SetSelected(i == _index);
        }

        private void BindTexts()
        {
            // Player wallet (gotówka u gracza)
            var ps = Object.FindFirstObjectByType<PlayerStats>();
            int playerCash = ps != null ? ps.money : 0;
            if (playerWalletText) playerWalletText.text = $"PLAYER WALLET: {playerCash}";

            // Card data
            if (_card != null && _card.hasBankCardMeta)
            {
                if (cardIdText) cardIdText.text = $"CARD ID: {_card.bankCard.cardId}";
                if (accIdText) accIdText.text = $"ACC ID: {_card.bankCard.accountId}";
                if (statusText) statusText.text = $"STATUS: {_card.bankCard.status.ToString().ToUpperInvariant()}";
                if (variantText) variantText.text = $"VARIANT: {_card.bankCard.colorVariant}";
            }
            else
            {
                if (cardIdText) cardIdText.text = "CARD ID: —";
                if (accIdText) accIdText.text = "ACC ID: —";
                if (statusText) statusText.text = "STATUS: —";
                if (variantText) variantText.text = "VARIANT: —";
            }

            // Account wallet (na razie placeholder)
            // Docelowo tu zrobisz ATMAccountService.GetBalance(accountId)
            if (_ui != null && accWalletText)
                accWalletText.text = $"ACC WALLET: {_ui.GetAccountWallet()}";

            if (_ui != null && playerWalletText)
                playerWalletText.text = $"PLAYER WALLET: {_ui.GetPlayerWallet()}";

    }
}
