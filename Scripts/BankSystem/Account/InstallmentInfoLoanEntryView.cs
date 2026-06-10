using TMPro;
using UnityEngine;

public class InstallmentInfoLoanEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text installmentAmountText;
    [SerializeField] private TMP_Text repayDateText;
    [SerializeField] private TMP_Text statusText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color nextColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color dueNowColor = new Color(1f, 0.2f, 0.2f);

    public void Bind(int amount, System.DateTime due, string status)
    {
        if (installmentAmountText)
            installmentAmountText.text = $"{amount}$";

        if (repayDateText)
            repayDateText.text = due.ToString("dd-MM-yyyy | HH:mm");

        if (statusText)
            statusText.text = status;

        ApplyColors(status);
    }

    private void ApplyColors(string status)
    {
        Color c = normalColor;

        switch (status)
        {
            case "NEXT":
                c = nextColor;
                break;

            case "DUE NOW":
                c = dueNowColor;
                break;

            default:
                c = normalColor;
                break;
        }

        if (installmentAmountText) installmentAmountText.color = c;
        if (repayDateText) repayDateText.color = c;
        if (statusText) statusText.color = c;
    }
}