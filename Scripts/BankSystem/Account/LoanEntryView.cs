using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoanEntryView : MonoBehaviour
{
    [SerializeField] private TMP_Text signTimeValue;
    [SerializeField] private TMP_Text installmentsLeftValue;
    [SerializeField] private Button installmentsLeftButton;

    [SerializeField] private TMP_Text monthlyInstallmentValue;
    [SerializeField] private TMP_Text loanAmountValue;
    [SerializeField] private TMP_Text totalLoanValue;
    [SerializeField] private TMP_Text leftLoanValue;
    [SerializeField] private TMP_Text bankInterestValue;

    public void Bind(ActiveLoan loan, Action onInstallmentsClick)
    {
        if (signTimeValue)
        {
            var dt = loan.startedAtUtcTicks > 0
                ? new System.DateTime(loan.startedAtUtcTicks, System.DateTimeKind.Utc)
                : default;

            signTimeValue.text = loan.startedAtUtcTicks > 0
                ? dt.ToString("dd-MM-yy | HH:mm")
                : "--";
        }

        if (installmentsLeftValue)
            installmentsLeftValue.text = loan.installmentsLeft.ToString();

        if (installmentsLeftButton)
        {
            installmentsLeftButton.onClick.RemoveAllListeners();
            installmentsLeftButton.onClick.AddListener(() => onInstallmentsClick?.Invoke());
            installmentsLeftButton.interactable = loan != null && !loan.finished && !loan.defaulted && loan.installmentsLeft > 0;
        }

        int effectiveMonthly = 0;
        if (!loan.finished && !loan.defaulted && loan.remainingToRepay > 0)
            effectiveMonthly = Mathf.Min(loan.monthlyPayment, loan.remainingToRepay);

        if (monthlyInstallmentValue)
            monthlyInstallmentValue.text = $"{effectiveMonthly}$";

        if (loanAmountValue)
            loanAmountValue.text = $"{loan.principal}$";

        if (totalLoanValue)
            totalLoanValue.text = $"{loan.totalToRepay}$";

        if (leftLoanValue)
            leftLoanValue.text = $"{loan.remainingToRepay}$";

        if (bankInterestValue)
        {
            float percent = loan.principal > 0 ? (loan.bankTax / (float)loan.principal) * 100f : 0f;
            bankInterestValue.text = $"{loan.bankTax}$ ({percent:0.#}%)";
        }
    }
}