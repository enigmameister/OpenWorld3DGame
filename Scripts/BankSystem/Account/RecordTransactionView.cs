using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RecordTransactionView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text accountNumberValue;
    [SerializeField] private TMP_Text transactionValue;
    [SerializeField] private TMP_Text transactionDateValue;

    [Header("Colors")]
    [SerializeField] private Color incomingColor = new Color(0.2f, 1f, 0.2f);
    [SerializeField] private Color outgoingColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;

    [Header("Highlight")]
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.6f, 0.1f, 0.25f);

    private TransactionsHistoryPanelUI parent;

    public void Init(TransactionsHistoryPanelUI parentPanel)
    {
        parent = parentPanel;
        SetSelected(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        parent?.Select(this);
    }

    public void SetSelected(bool selected)
    {
        if (background == null) return;
        background.color = selected ? selectedColor : normalColor;
    }

    public void Bind(BankTransactionRecord rec)
    {
        if (rec == null) return;

        if (accountNumberValue)
        {
            if (!string.IsNullOrWhiteSpace(rec.otherPartyName))
                accountNumberValue.text = rec.otherPartyName;
            else if (rec.otherAccountId > 0)
                accountNumberValue.text = rec.otherAccountId.ToString("000");
            else
                accountNumberValue.text = "---";
        }

        if (transactionValue)
        {
            bool incoming =
                rec.type == BankTransactionType.Deposit ||
                rec.type == BankTransactionType.TransferIn ||
                rec.type == BankTransactionType.LoanIn;

            bool outgoing =
                rec.type == BankTransactionType.Withdraw ||
                rec.type == BankTransactionType.TransferOut ||
                rec.type == BankTransactionType.LoanRepay;

            string prefix = incoming ? "+" : outgoing ? "-" : "";
            transactionValue.text = $"{prefix}{rec.amount}$";
            transactionValue.color = incoming ? incomingColor : outgoing ? outgoingColor : neutralColor;
        }

        if (transactionDateValue)
        {
            var dt = rec.utcTicks > 0
                ? new DateTime(rec.utcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;

            transactionDateValue.text = rec.utcTicks > 0
                ? dt.ToString("dd-MM-yyyy")
                : "--";
        }
    }
}