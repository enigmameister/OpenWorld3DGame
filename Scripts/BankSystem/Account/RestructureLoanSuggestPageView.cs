using TMPro;
using UnityEngine;

public class RestructureLoanSuggestPageView : MonoBehaviour
{
    [SerializeField] public TMP_Text amountValue;
    [SerializeField] public TMP_Text installmentValue;
    [SerializeField] public TMP_Text taxValue;
    [SerializeField] public TMP_Text loanPlayerValue;
    [SerializeField] public TMP_Text loanTotalValue;

    public void Bind(LoanSystem.LoanRestructureOffer offer, ActiveLoan loan)
    {
        if (amountValue) amountValue.text = $"{loan.principal:n0}$";
        if (installmentValue) installmentValue.text = $"{offer.months} MONTHS";
        if (taxValue)
        {
            float percent = loan.remainingToRepay > 0
                ? (offer.bankTax / (float)loan.remainingToRepay) * 100f
                : 0f;

            taxValue.text = $"{offer.bankTax:n0}$ ({percent:0.#}%)";
        }

        if (loanPlayerValue) loanPlayerValue.text = $"{offer.remainingToRepay:n0}$";
        if (loanTotalValue) loanTotalValue.text = $"{(loan.principal + (loan.bankTax + offer.bankTax)):n0}$";
    }
}