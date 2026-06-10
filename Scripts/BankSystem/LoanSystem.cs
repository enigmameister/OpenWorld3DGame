using System;
using System.Collections.Generic;
using UnityEngine;

public enum LoanVerdict
{
    None,
    Processing,

    Approved,
    ApprovedHighInterest,
    Conditional,
    Denied
}

[Serializable]
public class ActiveLoan
{
    public string citizenId;
    public int accountId;

    public int principal;
    public int months;
    public int bankTax;
    public int totalToRepay;
    public int monthlyPayment;

    public int installmentsPaid;
    public int installmentsLeft;
    public int remainingToRepay;

    public long startedAtUnix;      // możesz zostawić dla kompatybilności
    public long startedAtUtcTicks;  // dokładny czas gry jako DateTime.Ticks
    public long nextDueAtUtcTicks;  // następna rata
    public long fullyRepaidAtUtcTicks;

    public bool finished;
    public bool defaulted;

    // Restructure Logic
    public int installmentsPaidAtLastRestructure;
    public long lastRestructureUtcTicks;
    public int restructureCount;

    // DeferLogic

    public bool isDeferred;
    public int deferMonths;
    public long deferredAtUtcTicks;
    public long deferUntilUtcTicks;
    public int deferCount;
    public int installmentsPaidAtLastDefer;
    public long lastDeferUtcTicks;

    // DueDate

    public int preferredDueDayOfMonth;
}

public struct LoanQuote
{
    public LoanVerdict verdict;
    public float taxRate;
    public int bankTax;
    public int totalToRepay;
    public int monthlyPayment;
    public string reason;

    // Debug / UI-friendly (opcjonalne)
    public int score;
}

public class LoanSystem : MonoBehaviour
{
    public static LoanSystem Instance { get; private set; }

    [Header("Scoring thresholds")]
    [Tooltip("Od tego score: Approved")]
    public int approveScore = 25;

    [Tooltip("Od tego score: ApprovedHighInterest")]
    public int highInterestScore = 18;

    [Tooltip("Od tego score: Conditional")]
    public int conditionalScore = 12;

    [Header("Score weights (tuning)")]
    [Tooltip("Ile punktów za płynność. Liquidity = clamp(balance / liquidityUnit, 0..liquidityMax).")]
    public int liquidityUnit = 1000;
    public int liquidityMax = 10;
    public int liquidityWeight = 2;

    [Tooltip("Ile punktów za historię: repaidLoans * historyPerRepaid, potem * historyWeight.")]
    public int historyPerRepaid = 3;
    public int historyWeight = 3;

    [Tooltip("Kara za aktywną pożyczkę (realnie bank nie da drugiej).")]
    public int activeDebtPenalty = 999; // ogromna kara, ale i tak blokujemy allowOnlyOneActiveLoan

    [Tooltip("Ryzyko: (amount / riskAmountUnit) + months, potem * riskWeight.")]
    public int riskAmountUnit = 5000;
    public int riskWeight = 2;

    [Header("Tax / Interest (real-life inspired)")]
    [Tooltip("Bazowa stawka dla Approved (np. 3%).")]
    public float baseTaxRate = 0.03f;

    [Tooltip("Dodatkowy koszt za miesiąc (np. 0.15%/mies).")]
    public float taxPerMonth = 0.0015f;

    [Tooltip("Jeśli brak historii (0 spłaconych), dodatkowa premia ryzyka.")]
    public float taxPenaltyIfNoHistory = 0.01f;

    [Tooltip("Dodatkowy narzut dla ApprovedHighInterest.")]
    public float highInterestAdd = 0.02f;

    [Tooltip("Dodatkowy narzut dla Conditional.")]
    public float conditionalAdd = 0.05f;

    [Tooltip("Clamp bezpieczeństwa.")]
    public float minTaxRate = 0.01f;
    public float maxTaxRate = 0.35f;

    // citizenId -> ilość spłaconych pożyczek
    private readonly Dictionary<string, int> _loanHistoryCount = new();

    // citizenId -> aktywna pożyczka
    private readonly Dictionary<string, List<ActiveLoan>> _activeLoans = new();

    [Header("Rules")]
    [Tooltip("Maksymalna liczba aktywnych pożyczek na citizenId.")]
    public int maxActiveLoansPerCitizen = 5;

    private bool _timeHooked;
    private GameTimeSystem _timeSystem;

    [Header("Restructure")]
    public int restructureMinMonthsDelta = 1;
    public int restructureMaxMonths = 48;
    public float restructureBaseTaxRate = 0.03f;
    public float restructureTaxPerMonth = 0.0025f;
    public float restructureTaxPerUseAdd = 0.01f;

    [Header("Defer")]
    public int deferMinMonths = 1;
    public int deferMaxMonths = 36;
    public float deferBaseTaxRate = 0.01f;
    public float deferTaxPerMonth = 0.005f;
    public float deferTaxPerUseAdd = 0.01f;
    public float deferMaxTaxRate = 0.25f;

    [Header("Change Due Date")]
    public int dueDateMinDay = 1;
    public int dueDateMaxDay = 31;
    public int dueDateCooldownDays = 30;

    [Serializable]
    public class LoanDueDatePreference
    {
        public string citizenId;
        public int preferredDayOfMonth = 1;     // 1..31
        public long lastChangedAtUtcTicks = 0;  // cooldown 30 dni
    }

    public struct LoanRestructureOffer
    {
        public int months;
        public int monthlyPayment;
        public int bankTax;
        public int totalToRepay;
        public int remainingToRepay;
        public string label;
    }

    public struct LoanDeferOffer
    {
        public int deferMonths;
        public int addedBankTax;
        public int totalToRepay;
        public int remainingToRepay;
        public int monthlyPayment;
        public string label;
    }

    public struct LoanInstallmentPreview
    {
        public int index;
        public int amount;
        public long dueAtUtcTicks;
        public string status; // np. "PLANNED"
    }

    private readonly Dictionary<string, LoanDueDatePreference> _dueDatePrefs = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        HookGameTimeIfNeeded();
    }

    public bool HasActiveLoan(string citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return false;
        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null) return false;

        for (int i = 0; i < loans.Count; i++)
        {
            var loan = loans[i];
            if (loan != null && !loan.finished && !loan.defaulted)
                return true;
        }

        return false;
    }

    public int GetActiveLoanCount(string citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return 0;
        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null) return 0;

        int count = 0;
        for (int i = 0; i < loans.Count; i++)
        {
            var loan = loans[i];
            if (loan != null && !loan.finished && !loan.defaulted)
                count++;
        }

        return count;
    }

    public List<ActiveLoan> GetLoansForCitizen(string citizenId, bool includeFinished = false)
    {
        var result = new List<ActiveLoan>();

        if (string.IsNullOrWhiteSpace(citizenId)) return result;
        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null) return result;

        for (int i = 0; i < loans.Count; i++)
        {
            var loan = loans[i];
            if (loan == null) continue;

            if (includeFinished || (!loan.finished && !loan.defaulted))
                result.Add(loan);
        }

        return result;
    }

    private int CalculateScore(int balance, int repaidLoans, bool hasActiveLoan, int amount, int months)
    {
        int liquidity = Mathf.Clamp(balance / Mathf.Max(1, liquidityUnit), 0, liquidityMax);
        int history = repaidLoans * historyPerRepaid;

        int debtPenalty = hasActiveLoan ? activeDebtPenalty : 0;
        int risk = (amount / Mathf.Max(1, riskAmountUnit)) + months;

        int score =
            liquidity * liquidityWeight +
            history * historyWeight -
            risk * riskWeight -
            debtPenalty;

        return score;
    }

    private (LoanVerdict verdict, string reason) DecideFromScore(int score, bool hasActiveLoan)
    {
        if (score >= approveScore) return (LoanVerdict.Approved, "Approved");
        if (score >= highInterestScore) return (LoanVerdict.ApprovedHighInterest, "Approved (high interest)");
        if (score >= conditionalScore) return (LoanVerdict.Conditional, "Conditional offer");
        return (LoanVerdict.Denied, "Denied (risk too high)");
    }

    public LoanQuote RequestQuote(string citizenId, int amount, int months)
    {
        var q = new LoanQuote
        {
            verdict = LoanVerdict.Denied,
            reason = "Unknown",
            taxRate = 0f,
            bankTax = 0,
            totalToRepay = 0,
            monthlyPayment = 0,
            score = 0
        };

        // Zgodnie z Twoim założeniem: tu nie sprawdzamy citizenId/account/amount/months
        // (bo panel LOAN i tak dostępny tylko dla konta, a wartości są presetami).

        bool hasActive = HasActiveLoan(citizenId);
        int activeCount = GetActiveLoanCount(citizenId);

        if (activeCount >= maxActiveLoansPerCitizen)
        {
            q.verdict = LoanVerdict.Denied;
            q.reason = "Maximum active loans reached";
            return q;
        }

        var bank = BankSystem.Instance;
        if (bank == null)
        {
            q.verdict = LoanVerdict.Denied;
            q.reason = "BankSystem missing";
            return q;
        }

        // Zakładamy, że konto istnieje (ale nadal defensywnie: jeśli nie — DENIED)
        if (!bank.TryGetAccountForCitizen(citizenId, out var acc))
        {
            q.verdict = LoanVerdict.Denied;
            q.reason = "Account not found";
            return q;
        }

        int balance = bank.GetBalance(acc.accountId);
        _loanHistoryCount.TryGetValue(citizenId, out int repaidLoans);

        int score = CalculateScore(balance, repaidLoans, hasActive, amount, months);
        q.score = score;

        var (verdict, reason) = DecideFromScore(score, hasActive);
        q.verdict = verdict;
        q.reason = reason;

        // Jeśli denied — nie licz podatku
        if (verdict == LoanVerdict.Denied)
            return q;

        // TAX RATE (real-life-ish)
        float tax = baseTaxRate + taxPerMonth * months;

        bool hasHistory = repaidLoans > 0;
        if (!hasHistory) tax += taxPenaltyIfNoHistory;

        if (verdict == LoanVerdict.ApprovedHighInterest) tax += highInterestAdd;
        if (verdict == LoanVerdict.Conditional) tax += conditionalAdd;

        tax = Mathf.Clamp(tax, minTaxRate, maxTaxRate);

        int bankTax = Mathf.Max(1, Mathf.RoundToInt(amount * tax));
        int total = amount + bankTax;
        int monthly = Mathf.Max(1, Mathf.CeilToInt((float)total / months));

        q.taxRate = tax;
        q.bankTax = bankTax;
        q.totalToRepay = total;
        q.monthlyPayment = monthly;

        return q;
    }

    public bool ConfirmLoan(string citizenId, int amount, int months, LoanQuote quote, out ActiveLoan created)
    {
        created = null;

        if (quote.verdict == LoanVerdict.Denied)
            return false;

        var bank = BankSystem.Instance;
        if (bank == null) return false;
        if (!bank.TryGetAccountForCitizen(citizenId, out var acc)) return false;

        var ts = GameTimeSystem.Instance;
        if (ts == null) return false;

        if (GetActiveLoanCount(citizenId) >= maxActiveLoansPerCitizen)
            return false;

        acc.balance += amount;
        bank.AddLoanTransactionIn(acc.accountId, amount);

        DateTime now = ts.CurrentTime;
        int preferredDay = GetPreferredDueDay(citizenId);
        DateTime nextDue = CalculateNextDueDate(now, preferredDay);

        created = new ActiveLoan
        {
            citizenId = citizenId,
            accountId = acc.accountId,
            principal = amount,
            months = months,
            bankTax = quote.bankTax,
            totalToRepay = quote.totalToRepay,
            monthlyPayment = quote.monthlyPayment,

            installmentsPaid = 0,
            installmentsLeft = months,
            remainingToRepay = quote.totalToRepay,

            startedAtUnix = new DateTimeOffset(now).ToUnixTimeSeconds(),
            startedAtUtcTicks = now.Ticks,
            nextDueAtUtcTicks = nextDue.Ticks,
            fullyRepaidAtUtcTicks = 0,

            preferredDueDayOfMonth = preferredDay,

            finished = false,
            defaulted = false
        };

        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null)
        {
            loans = new List<ActiveLoan>();
            _activeLoans[citizenId] = loans;
        }

        loans.Add(created);
        return true;
    }

    public bool TryGetLoanAt(string citizenId, int index, out ActiveLoan loan)
    {
        loan = null;

        if (string.IsNullOrWhiteSpace(citizenId)) return false;
        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null) return false;
        if (index < 0 || index >= loans.Count) return false;

        loan = loans[index];
        return loan != null;
    }

    // KONTROFERTA BANKU 

    public struct LoanSuggestion
    {
        public bool hasSuggestion;
        public int suggestedAmount;
        public int suggestedMonths;
        public int suggestedScore;
        public LoanVerdict suggestedVerdict;
        public string reason;
    }

    public LoanSuggestion SuggestTerms(
        string citizenId,
        int[] amountOptions,
        int minMonths,
        int maxMonths,
        int preferredAmount,
        int preferredMonths)
    {
        var s = new LoanSuggestion
        {
            hasSuggestion = false,
            suggestedAmount = preferredAmount,
            suggestedMonths = preferredMonths,
            suggestedScore = int.MinValue,
            suggestedVerdict = LoanVerdict.Denied,
            reason = "No offer"
        };

        if (GetActiveLoanCount(citizenId) >= maxActiveLoansPerCitizen)
        {
            s.reason = "Maximum active loans reached";
            return s;
        }

        var bank = BankSystem.Instance;
        if (bank == null) { s.reason = "BankSystem missing"; return s; }
        if (!bank.TryGetAccountForCitizen(citizenId, out var acc)) { s.reason = "Account not found"; return s; }

        int balance = bank.GetBalance(acc.accountId);
        _loanHistoryCount.TryGetValue(citizenId, out int repaidLoans);

        // szukamy najlepszego dopuszczalnego wariantu możliwie blisko preferencji gracza
        int bestScore = int.MinValue;
        float bestDistance = float.MaxValue;
        int bestA = 0;
        int bestM = 0;
        LoanVerdict bestV = LoanVerdict.Denied;

        for (int i = 0; i < amountOptions.Length; i++)
        {
            int a = amountOptions[i];
            for (int m = minMonths; m <= maxMonths; m++)
            {
                // scoring
                int score = CalculateScore(balance, repaidLoans, hasActiveLoan: false, amount: a, months: m);
                var (v, _) = DecideFromScore(score, hasActiveLoan: false);

                if (v == LoanVerdict.Denied) continue; // musi przechodzić

                // dystans od preferencji: wolimy trzymać się blisko tego co gracz chciał
                // (mniejsza kwota i/lub krótszy okres zwykle łatwiej przechodzi — to i tak wyjdzie w score)
                float dAmount = Mathf.Abs(a - preferredAmount) / (float)Mathf.Max(1, preferredAmount);
                float dMonths = Mathf.Abs(m - preferredMonths) / (float)Mathf.Max(1, preferredMonths);
                float dist = dAmount * 0.75f + dMonths * 0.25f;

                // wybór: najpierw najmniejszy dystans, potem większy score jako tie-break
                if (dist < bestDistance - 0.0001f || (Mathf.Abs(dist - bestDistance) < 0.0001f && score > bestScore))
                {
                    bestDistance = dist;
                    bestScore = score;
                    bestA = a;
                    bestM = m;
                    bestV = v;
                }
            }
        }

        if (bestScore == int.MinValue)
        {
            s.reason = "No acceptable terms found";
            return s;
        }

        s.hasSuggestion = true;
        s.suggestedAmount = bestA;
        s.suggestedMonths = bestM;
        s.suggestedScore = bestScore;
        s.suggestedVerdict = bestV;
        s.reason = "Counter-offer available";
        return s;
    }

    void OnEnable()
    {
        HookGameTimeIfNeeded();
    }

    void OnDisable()
    {
        UnhookGameTime();
    }

    private void HookGameTimeIfNeeded()
    {
        if (_timeHooked) return;

        _timeSystem = GameTimeSystem.Instance;
        if (_timeSystem == null) return;

        _timeSystem.OnMinuteChanged += OnGameMinuteChanged;
        _timeHooked = true;
    }

    private void UnhookGameTime()
    {
        if (!_timeHooked) return;

        if (_timeSystem != null)
            _timeSystem.OnMinuteChanged -= OnGameMinuteChanged;

        _timeSystem = null;
        _timeHooked = false;
    }

    private void OnGameMinuteChanged(int nowMin)
    {
        ProcessLoanInstallments();
    }

    public bool TryGetActiveLoan(string citizenId, out ActiveLoan loan)
    {
        loan = null;

        if (string.IsNullOrWhiteSpace(citizenId)) return false;
        if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null) return false;

        for (int i = 0; i < loans.Count; i++)
        {
            var l = loans[i];
            if (l != null && !l.finished && !l.defaulted)
            {
                loan = l;
                return true;
            }
        }

        return false;
    }

    private void ProcessLoanInstallments()
    {
        if (GameTimeSystem.Instance == null) return;
        if (BankSystem.Instance == null) return;

        DateTime now = GameTimeSystem.Instance.CurrentTime;

        var citizens = new List<string>(_activeLoans.Keys);

        foreach (var citizenId in citizens)
        {
            if (!_activeLoans.TryGetValue(citizenId, out var loans) || loans == null)
                continue;

            for (int i = 0; i < loans.Count; i++)
            {
                var loan = loans[i];
                if (loan == null) continue;
                if (loan.finished || loan.defaulted) continue;

                if (loan.isDeferred)
                {
                    if (loan.deferUntilUtcTicks > 0 && now.Ticks < loan.deferUntilUtcTicks)
                        continue;

                    ClearDeferredState(loan);
                }

                while (loan.nextDueAtUtcTicks > 0 && now.Ticks >= loan.nextDueAtUtcTicks && loan.remainingToRepay > 0)
                {
                    int installment = Mathf.Min(loan.monthlyPayment, loan.remainingToRepay);

                    bool debited = BankSystem.Instance.ApplyLoanInstallment(loan.accountId, installment);
                    if (!debited)
                        break;

                    loan.remainingToRepay -= installment;
                    loan.installmentsPaid++;

                    loan.installmentsLeft = loan.remainingToRepay > 0
                        ? Mathf.CeilToInt(loan.remainingToRepay / (float)Mathf.Max(1, loan.monthlyPayment))
                        : 0;

                    DateTime due = new DateTime(loan.nextDueAtUtcTicks, DateTimeKind.Utc);
                    int preferredDay = loan.preferredDueDayOfMonth > 0 ? loan.preferredDueDayOfMonth : due.Day;
                    loan.nextDueAtUtcTicks = CalculateNextDueDateFromBase(due, preferredDay).Ticks;

                    if (loan.remainingToRepay <= 0)
                    {
                        loan.remainingToRepay = 0;
                        loan.installmentsLeft = 0;
                        loan.finished = true;
                        loan.fullyRepaidAtUtcTicks = now.Ticks;

                        ClearDeferredState(loan);
                        MarkLoanRepaid(loan);
                        break;
                    }
                }
            }
        }
    }

    public void MarkLoanRepaid(ActiveLoan loan)
    {
        if (loan == null || string.IsNullOrWhiteSpace(loan.citizenId)) return;

        _loanHistoryCount.TryGetValue(loan.citizenId, out int c);
        _loanHistoryCount[loan.citizenId] = c + 1;
    }

    public bool TryRepayLoan(ActiveLoan loan, int amount, out int repaidAmount, out string reason)
    {
        repaidAmount = 0;
        reason = "";

        if (loan == null)
        {
            reason = "NO LOAN";
            return false;
        }

        if (loan.finished || loan.defaulted)
        {
            reason = "LOAN CLOSED";
            return false;
        }

        if (amount <= 0)
        {
            reason = "INVALID AMOUNT";
            return false;
        }

        var bank = BankSystem.Instance;
        if (bank == null)
        {
            reason = "BANK MISSING";
            return false;
        }

        int repay = Mathf.Min(amount, loan.remainingToRepay);

        if (!bank.TryRepayLoanFromBalance(loan.accountId, repay))
        {
            reason = "NO MONEY";
            return false;
        }

        bank.AddLoanRepayTransaction(loan.accountId, repay);
        int oldInstallmentsLeft = loan.installmentsLeft;

        loan.remainingToRepay -= repay;
        repaidAmount = repay;

        loan.installmentsLeft = loan.remainingToRepay > 0
            ? Mathf.CeilToInt(loan.remainingToRepay / (float)Mathf.Max(1, loan.monthlyPayment))
            : 0;

        int paidInstallmentsDelta = Mathf.Max(0, oldInstallmentsLeft - loan.installmentsLeft);
        loan.installmentsPaid += paidInstallmentsDelta;

        if (loan.remainingToRepay <= 0)
        {
            loan.remainingToRepay = 0;
            loan.installmentsLeft = 0;
            loan.finished = true;
            loan.fullyRepaidAtUtcTicks = GameTimeSystem.Instance != null
                ? GameTimeSystem.Instance.CurrentTime.Ticks
                : 0;

            ClearDeferredState(loan);
            MarkLoanRepaid(loan);
        }

        return true;
    }
    
    // RestructureLoan System
    public bool CanRestructureLoan(ActiveLoan loan, out string reason) // Limit_Text TMP Behaviour
    {
        reason = "";

        if (loan == null)
        {
            reason = "NO ACTIVE LOAN";
            return false;
        }

        if (loan.finished)
        {
            reason = "LOAN CLOSED";
            return false;
        }

        if (loan.defaulted)
        {
            reason = "LOAN DEFAULTED";
            return false;
        }

        if (loan.remainingToRepay <= 0)
        {
            reason = "NOTHING TO RESTRUCTURE";
            return false;
        }

        if (loan.installmentsPaid <= loan.installmentsPaidAtLastRestructure)
        {
            reason = "LOAN ALREADY MODIFIED. PAY CURRENT INSTALLMENT.";
            return false;
        }

        return true;
    }

    public List<LoanRestructureOffer> BuildRestructureOffers(ActiveLoan loan, int requestedMonths) // Bank Suggest Logic
    {
        var offers = new List<LoanRestructureOffer>();

        if (!CanRestructureLoan(loan, out _))
            return offers;

        int minMonths = Mathf.Max(
            loan.installmentsLeft + restructureMinMonthsDelta,
            2
        );

        int maxMonths = Mathf.Max(minMonths, restructureMaxMonths);

        int[] candidates =
        {
        requestedMonths - 6,
        requestedMonths - 3,
        requestedMonths,
        requestedMonths + 3,
        requestedMonths + 6
    };

        HashSet<int> unique = new HashSet<int>();

        for (int i = 0; i < candidates.Length; i++)
        {
            int months = Mathf.Clamp(candidates[i], minMonths, maxMonths);
            if (!unique.Add(months))
                continue;

            float taxRate =
                restructureBaseTaxRate +
                months * restructureTaxPerMonth +
                loan.restructureCount * restructureTaxPerUseAdd;

            int bankTax = Mathf.Max(1, Mathf.RoundToInt(loan.remainingToRepay * taxRate));
            int total = loan.remainingToRepay + bankTax;
            int monthly = Mathf.Max(1, Mathf.CeilToInt((float)total / months));

            offers.Add(new LoanRestructureOffer
            {
                months = months,
                monthlyPayment = monthly,
                bankTax = bankTax,
                totalToRepay = total,
                remainingToRepay = total,
                label = $"{months} MONTHS"
            });
        }

        offers.Sort((a, b) => a.months.CompareTo(b.months));
        return offers;
    }

    public bool ApplyRestructureOffer(ActiveLoan loan, LoanRestructureOffer offer, out string reason)
    {
        reason = "";

        if (!CanRestructureLoan(loan, out reason))
            return false;

        int minMonths = GetMinRestructureMonths(loan);
        if (offer.months < minMonths)
        {
            reason = "INVALID MONTHS";
            return false;
        }

        if (offer.months <= 0)
        {
            reason = "INVALID OFFER";
            return false;
        }

        loan.months = loan.installmentsPaid + offer.months;
        loan.bankTax += offer.bankTax;
        loan.totalToRepay = loan.principal + loan.bankTax;
        loan.remainingToRepay = offer.remainingToRepay;
        loan.monthlyPayment = offer.monthlyPayment;
        loan.installmentsLeft = offer.months;

        loan.installmentsPaidAtLastRestructure = loan.installmentsPaid;
        loan.lastRestructureUtcTicks = GameTimeSystem.Instance != null
            ? GameTimeSystem.Instance.CurrentTime.Ticks
            : 0;

        loan.restructureCount++;

        return true;
    }

    public int GetMinRestructureMonths(ActiveLoan loan)
    {
        if (loan == null) return 2;
        return Mathf.Max(loan.installmentsLeft + restructureMinMonthsDelta, 2);
    }

    // DeferLoan System

    public bool CanDeferLoan(ActiveLoan loan, out string reason)
    {
        reason = "";

        if (loan == null)
        {
            reason = "NO ACTIVE LOAN";
            return false;
        }

        if (loan.finished)
        {
            reason = "LOAN CLOSED";
            return false;
        }

        if (loan.defaulted)
        {
            reason = "LOAN DEFAULTED";
            return false;
        }

        if (loan.remainingToRepay <= 0)
        {
            reason = "NOTHING TO DEFER";
            return false;
        }

        if (loan.installmentsLeft <= 0)
        {
            reason = "NO INSTALLMENTS LEFT";
            return false;
        }

        if (loan.isDeferred)
        {
            reason = "LOAN IS DEFERRED. UNBLOCK TO CONTINUE";
            return false;
        }

        if (loan.installmentsPaid <= 0)
        {
            reason = "PAY AT LEAST ONE INSTALLMENT FIRST";
            return false;
        }

        // jeden defer na cykl między spłatami
        if (loan.installmentsPaid <= loan.installmentsPaidAtLastDefer)
        {
            reason = "DEFER ALREADY USED. PAY CURRENT INSTALLMENT.";
            return false;
        }

        reason = "OK";
        return true;
    }

    public LoanDeferOffer BuildDeferOffer(ActiveLoan loan, int deferMonths)
    {
        if (loan == null)
            return default;

        deferMonths = Mathf.Clamp(deferMonths, deferMinMonths, deferMaxMonths);

        float taxRate =
            deferBaseTaxRate +
            deferMonths * deferTaxPerMonth +
            loan.deferCount * deferTaxPerUseAdd;

        taxRate = Mathf.Clamp(taxRate, 0f, deferMaxTaxRate);

        int addedTax = Mathf.Max(1, Mathf.RoundToInt(loan.remainingToRepay * taxRate));
        int newRemaining = loan.remainingToRepay + addedTax;

        int monthly = Mathf.Max(
            1,
            Mathf.CeilToInt(newRemaining / (float)Mathf.Max(1, loan.installmentsLeft))
        );

        return new LoanDeferOffer
        {
            deferMonths = deferMonths,
            addedBankTax = addedTax,
            totalToRepay = loan.principal + loan.bankTax + addedTax,
            remainingToRepay = newRemaining,
            monthlyPayment = monthly,
            label = $"{deferMonths} MONTHS"
        };
    }

    public bool ApplyDeferOffer(ActiveLoan loan, LoanDeferOffer offer, out string reason)
    {
        reason = "";

        if (!CanDeferLoan(loan, out reason))
            return false;

        if (offer.deferMonths <= 0)
        {
            reason = "INVALID DEFER";
            return false;
        }

        var ts = GameTimeSystem.Instance;
        if (ts == null)
        {
            reason = "TIME SYSTEM MISSING";
            return false;
        }

        int deferMonths = Mathf.Clamp(offer.deferMonths, deferMinMonths, deferMaxMonths);
        var validatedOffer = BuildDeferOffer(loan, deferMonths);

        DateTime currentDue = new DateTime(loan.nextDueAtUtcTicks, DateTimeKind.Utc);
        DateTime newDue = currentDue.AddMonths(deferMonths);

        loan.isDeferred = true;
        loan.deferMonths = deferMonths;
        loan.deferredAtUtcTicks = ts.CurrentTime.Ticks;
        loan.deferUntilUtcTicks = newDue.Ticks;

        loan.bankTax += Mathf.Max(0, validatedOffer.addedBankTax);
        loan.totalToRepay = loan.principal + loan.bankTax;
        loan.remainingToRepay = Mathf.Max(0, validatedOffer.remainingToRepay);
        loan.monthlyPayment = Mathf.Max(1, validatedOffer.monthlyPayment);

        loan.nextDueAtUtcTicks = newDue.Ticks;

        loan.deferCount++;
        loan.installmentsPaidAtLastDefer = loan.installmentsPaid;
        loan.lastDeferUtcTicks = ts.CurrentTime.Ticks;

        reason = "OK";
        return true;
    }

    private void ClearDeferredState(ActiveLoan loan)
    {
        if (loan == null) return;

        loan.isDeferred = false;
        loan.deferMonths = 0;
        loan.deferredAtUtcTicks = 0;
        loan.deferUntilUtcTicks = 0;
    }

    public bool TryGetDeferBlockReason(ActiveLoan loan, out string reason)
    {
        return !CanDeferLoan(loan, out reason);
    }

    public string GetDeferInfoText(ActiveLoan loan)
    {
        if (CanDeferLoan(loan, out var reason))
            return "SELECT PERIOD AND PRESS CHECK";

        return reason;
    }

    private LoanDueDatePreference GetOrCreateDueDatePreference(string citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId))
            return null;

        if (_dueDatePrefs.TryGetValue(citizenId, out var pref) && pref != null)
            return pref;

        pref = new LoanDueDatePreference
        {
            citizenId = citizenId,
            preferredDayOfMonth = 1,
            lastChangedAtUtcTicks = 0
        };

        _dueDatePrefs[citizenId] = pref;
        return pref;
    }

    public bool CanChangeDueDate(string citizenId, out string reason)
    {
        reason = "";

        if (string.IsNullOrWhiteSpace(citizenId))
        {
            reason = "NO CITIZEN ID";
            return false;
        }

        var ts = GameTimeSystem.Instance;
        if (ts == null)
        {
            reason = "TIME SYSTEM MISSING";
            return false;
        }

        var pref = GetOrCreateDueDatePreference(citizenId);
        if (pref == null)
        {
            reason = "PREFERENCE MISSING";
            return false;
        }

        if (pref.lastChangedAtUtcTicks <= 0)
        {
            reason = "OK";
            return true;
        }

        DateTime now = ts.CurrentTime;
        DateTime last = new DateTime(pref.lastChangedAtUtcTicks, DateTimeKind.Utc);
        DateTime unlockAt = last.AddDays(dueDateCooldownDays);

        if (now < unlockAt)
        {
            reason = "ONLY ONCE PER MONTH";
            return false;
        }

        reason = "OK";
        return true;
    }

    public bool TryGetDueDateCooldownRemaining(
    string citizenId,
    out int days,
    out int hours,
    out int minutes)
    {
        days = 0;
        hours = 0;
        minutes = 0;

        if (string.IsNullOrWhiteSpace(citizenId))
            return false;

        var ts = GameTimeSystem.Instance;
        if (ts == null)
            return false;

        var pref = GetOrCreateDueDatePreference(citizenId);
        if (pref == null || pref.lastChangedAtUtcTicks <= 0)
            return false;

        DateTime now = ts.CurrentTime;
        DateTime last = new DateTime(pref.lastChangedAtUtcTicks, DateTimeKind.Utc);
        DateTime unlockAt = last.AddDays(dueDateCooldownDays);

        if (now >= unlockAt)
            return false;

        TimeSpan remain = unlockAt - now;
        days = Mathf.Max(0, remain.Days);
        hours = Mathf.Max(0, remain.Hours);
        minutes = Mathf.Max(0, remain.Minutes);
        return true;
    }

    public bool ApplyChangeDueDate(string citizenId, int newDayOfMonth, out string reason)
    {
        reason = "";

        if (!CanChangeDueDate(citizenId, out reason))
            return false;

        var ts = GameTimeSystem.Instance;
        if (ts == null)
        {
            reason = "TIME SYSTEM MISSING";
            return false;
        }

        int clampedDay = Mathf.Clamp(newDayOfMonth, dueDateMinDay, dueDateMaxDay);

        var pref = GetOrCreateDueDatePreference(citizenId);
        if (pref == null)
        {
            reason = "PREFERENCE MISSING";
            return false;
        }

        if (pref.preferredDayOfMonth == clampedDay)
        {
            reason = "SAME DAY ALREADY SET";
            return false;
        }

        pref.preferredDayOfMonth = clampedDay;
        pref.lastChangedAtUtcTicks = ts.CurrentTime.Ticks;

        reason = "OK";
        return true;
    }

    public int GetPreferredDueDay(string citizenId)
    {
        var pref = GetOrCreateDueDatePreference(citizenId);
        if (pref == null) return 1;

        return Mathf.Clamp(pref.preferredDayOfMonth, dueDateMinDay, dueDateMaxDay);
    }

    public string GetDueDateInfoText(string citizenId)
    {
        if (CanChangeDueDate(citizenId, out var reason))
            return "SELECT NEW DAY AND PRESS CHECK";

        return reason;
    }

    public string GetDueDateValueText(string citizenId)
    {
        if (TryGetDueDateCooldownRemaining(citizenId, out int d, out int h, out int m))
            return $"REMAIN: {d} DAYS | {h:00}:{m:00}";

        return $"CURRENT: DAY {GetPreferredDueDay(citizenId):00}";
    }

    private DateTime CalculateNextDueDate(DateTime now, int preferredDay)
    {
        preferredDay = Mathf.Clamp(preferredDay, dueDateMinDay, dueDateMaxDay);

        int year = now.Year;
        int month = now.Month + 1;

        if (month > 12)
        {
            month = 1;
            year++;
        }

        int maxDay = DateTime.DaysInMonth(year, month);
        int day = Mathf.Clamp(preferredDay, 1, maxDay);

        return new DateTime(year, month, day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
    }

    private DateTime CalculateNextDueDateFromBase(DateTime baseDate, int preferredDay)
    {
        preferredDay = Mathf.Clamp(preferredDay, dueDateMinDay, dueDateMaxDay);

        int year = baseDate.Year;
        int month = baseDate.Month + 1;

        if (month > 12)
        {
            month = 1;
            year++;
        }

        int maxDay = DateTime.DaysInMonth(year, month);
        int day = Mathf.Clamp(preferredDay, 1, maxDay);

        return new DateTime(year, month, day, baseDate.Hour, baseDate.Minute, 0, DateTimeKind.Utc);
    }

    public List<LoanInstallmentPreview> BuildUpcomingInstallments(ActiveLoan loan)
    {
        var result = new List<LoanInstallmentPreview>();

        if (loan == null)
            return result;

        if (loan.finished || loan.defaulted)
            return result;

        if (loan.remainingToRepay <= 0 || loan.installmentsLeft <= 0)
            return result;

        int remaining = loan.remainingToRepay;
        int monthly = Mathf.Max(1, loan.monthlyPayment);

        long dueTicks = loan.nextDueAtUtcTicks;
        int preferredDay = loan.preferredDueDayOfMonth > 0
            ? loan.preferredDueDayOfMonth
            : 1;

        DateTime now = GameTimeSystem.Instance != null
            ? GameTimeSystem.Instance.CurrentTime
            : DateTime.UtcNow;

        int index = 1;

        while (remaining > 0)
        {
            int amount = Mathf.Min(monthly, remaining);

            DateTime due = dueTicks > 0
                ? new DateTime(dueTicks, DateTimeKind.Utc)
                : now;

            string status;
            if (due <= now)
                status = "DUE NOW";
            else if (index == 1)
                status = "NEXT";
            else
                status = "PLANNED";

            result.Add(new LoanInstallmentPreview
            {
                index = index,
                amount = amount,
                dueAtUtcTicks = due.Ticks,
                status = status
            });

            remaining -= amount;
            dueTicks = CalculateNextDueDateFromBase(due, preferredDay).Ticks;
            index++;
        }

        return result;
    }

    public bool HasActiveLoanForAccount(int accountId)
    {
        if (accountId <= 0) return false;

        foreach (var kv in _activeLoans)
        {
            var loans = kv.Value;
            if (loans == null) continue;

            for (int i = 0; i < loans.Count; i++)
            {
                var loan = loans[i];
                if (loan == null) continue;
                if (loan.accountId != accountId) continue;
                if (loan.finished || loan.defaulted) continue;

                return true;
            }
        }

        return false;
    }

    public int GetActiveLoanCountForAccount(int accountId)
    {
        if (accountId <= 0) return 0;

        int count = 0;

        foreach (var kv in _activeLoans)
        {
            var loans = kv.Value;
            if (loans == null) continue;

            for (int i = 0; i < loans.Count; i++)
            {
                var loan = loans[i];
                if (loan == null) continue;
                if (loan.accountId != accountId) continue;
                if (loan.finished || loan.defaulted) continue;

                count++;
            }
        }

        return count;
    }
}
