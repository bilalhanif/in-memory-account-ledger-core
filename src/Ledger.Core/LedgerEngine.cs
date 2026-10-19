namespace Ledger.Core;

/// <summary>
/// Deterministic six-day, in-memory ledger replay. Entries are append-only;
/// a reversal appends a compensating entry and never mutates its original.
/// </summary>
public sealed class LedgerEngine
{
    private const decimal DailyInterestRate = 0.0004m; // 0.04% per day, specified by the exercise.
    private const decimal DailyOverdraftFee = 25.00m; // AED 25.00 per negative account/value day.
    private readonly List<LedgerEntry> _entries = [];
    private readonly List<Authorization> _authorizations = [];
    private readonly List<string> _errors = [];
    private readonly List<string> _diagnostics = [];
    private readonly Dictionary<string, (Currency Currency, decimal Opening)> _accounts = new()
    {
        ["ACC-001"] = (Currency.AED, 0m),
        ["ACC-002"] = (Currency.BHD, 0m)
    };

    public IReadOnlyList<LedgerEntry> Entries => _entries.AsReadOnly();

    private void Append(
        string id, string account, int valueDay, EntryKind kind, decimal amount,
        Currency currency, string description, string? reverses = null, int? eventDay = null)
    {
        if (valueDay is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(valueDay));
        if (!_accounts.TryGetValue(account, out var config) || config.Currency != currency)
            throw new InvalidOperationException($"Unknown account/currency combination: {account}/{currency}.");

        _entries.Add(new LedgerEntry(
            id, account, valueDay, kind, CurrencyRules.Round(currency, amount),
            currency, description, reverses, eventDay ?? valueDay));
    }

    public ReplayResult RunScenario()
    {
        _entries.Clear();
        _authorizations.Clear();
        _errors.Clear();
        _diagnostics.Clear();

        // E1-E2: Day 1 booked transactions.
        Append("E1", "ACC-001", 1, EntryKind.Credit, 1200.00m, Currency.AED, "D1 credit");
        Append("E2", "ACC-001", 1, EntryKind.Debit, 950.00m, Currency.AED, "D1 debit");

        // E3: Auth-A is approved on D2 because 250 - 200 remains non-negative.
        _authorizations.Add(new Authorization("Auth-A", "ACC-001", 200m, Currency.AED, 2, AuthorizationState.Approved));

        // E4-E6: D3 credit, valid Auth-A settlement, invalid Auth-Z settlement.
        Append("E4", "ACC-001", 3, EntryKind.Credit, 400.00m, Currency.AED, "D3 credit");
        Settle("E5", "Auth-A", 185m, 4);
        Settle("E6", "Auth-Z", 180m, 4);

        // E7 arrives on D5 but is value-dated D2.
        Append("E7", "ACC-001", 2, EntryKind.Debit, 620.00m, Currency.AED,
            "D5 debit back-valued to D2", eventDay: 5);

        // On arrival of E7, reassess every value day through D5. The D2 balance
        // before any fee is -370.00 AED; the negative value days are D2, D4, D5.
        var d2BeforeFees = Balance("ACC-001", valueDay: 2, knownByEventDay: 5, includeFees: false, includeInterest: false);
        _diagnostics.Add($"E7 backdating check: D2 balance known at end of D5 before fees = {CurrencyRules.Format(Currency.AED, d2BeforeFees)}.");

        for (var valueDay = 1; valueDay <= 5; valueDay++)
        {
            var balanceIncludingPriorFees = Balance("ACC-001", valueDay, knownByEventDay: 5, includeFees: true, includeInterest: false);
            var feeAlreadyExists = _entries.Any(e => e.AccountId == "ACC-001" && e.ValueDay == valueDay && e.Kind == EntryKind.Fee);
            if (balanceIncludingPriorFees < 0 && !feeAlreadyExists)
            {
                Append($"FEE-D{valueDay}", "ACC-001", valueDay, EntryKind.Fee, DailyOverdraftFee,
                    Currency.AED, $"Overdraft fee assessed on D{valueDay}", eventDay: 5);
            }
        }

        // E8: authorization approval must check the actual available balance after hold.
        const decimal authBHold = 90m;
        var authBLedgerBalance = Balance("ACC-001", valueDay: 5, knownByEventDay: 5, includeFees: true, includeInterest: false);
        var authBAvailableAfterHold = authBLedgerBalance - authBHold;
        var authBState = authBAvailableAfterHold >= 0 ? AuthorizationState.Approved : AuthorizationState.Rejected;
        _authorizations.Add(new Authorization("Auth-B", "ACC-001", authBHold, Currency.AED, 5, authBState));
        if (authBState == AuthorizationState.Rejected)
            _errors.Add($"E8 rejected: Auth-B hold of {CurrencyRules.Format(Currency.AED, authBHold)} would make available balance {CurrencyRules.Format(Currency.AED, authBAvailableAfterHold)}; no hold was applied.");

        // E9 arrives D6 and appends a compensating credit value-dated D2.
        Append("E9", "ACC-001", 2, EntryKind.Reversal, 620.00m, Currency.AED,
            "D6 reversal of E7", reverses: "E7", eventDay: 6);

        // E10 is BHD 10.000 split into currency-precision instalments that reconcile exactly.
        Append("E10-A", "ACC-002", 5, EntryKind.Credit, 3.334m, Currency.BHD, "Instalment 1 of 3", eventDay: 5);
        Append("E10-B", "ACC-002", 5, EntryKind.Credit, 3.333m, Currency.BHD, "Instalment 2 of 3", eventDay: 5);
        Append("E10-C", "ACC-002", 5, EntryKind.Credit, 3.333m, Currency.BHD, "Instalment 3 of 3", eventDay: 5);

        // Interest is accrued for each event-time closing balance, rounded each day,
        // but is not posted daily. One aggregate credit is capitalized at end of D6.
        var dailyInterest = new Dictionary<string, decimal> { ["ACC-001"] = 0m, ["ACC-002"] = 0m };
        for (var day = 1; day <= 6; day++)
        {
            foreach (var account in _accounts)
            {
                var balance = Balance(account.Key, day, knownByEventDay: day, includeFees: true, includeInterest: false);
                if (balance <= 0) continue;
                var roundedDailyAccrual = CurrencyRules.Round(account.Value.Currency, balance * DailyInterestRate);
                dailyInterest[account.Key] += roundedDailyAccrual;
            }
        }

        foreach (var account in _accounts)
        {
            var total = CurrencyRules.Round(account.Value.Currency, dailyInterest[account.Key]);
            if (total != 0)
                Append($"INT-{account.Key}-D6", account.Key, 6, EntryKind.Interest, total,
                    account.Value.Currency, "Capitalized total of rounded daily accruals at end of D6", eventDay: 6);
        }

        var rows = new List<ReplayRow>();
        foreach (var account in _accounts)
        {
            for (var day = 1; day <= 6; day++)
            {
                // Event-time snapshot: only events known by this day are visible.
                var ledgerBalance = Balance(account.Key, day, knownByEventDay: day, includeFees: true, includeInterest: true);
                var fees = _entries
                    .Where(e => e.AccountId == account.Key && e.EventDay <= day && e.ValueDay <= day && e.Kind == EntryKind.Fee)
                    .Sum(e => e.Amount);

                var activeHolds = _authorizations
                    .Where(a => a.AccountId == account.Key && a.Day <= day && IsActive(a, day))
                    .Sum(a => a.HoldAmount);
                var authSummary = string.Join(", ", _authorizations
                    .Where(a => a.AccountId == account.Key && a.Day <= day)
                    .Select(a => $"{a.Id}:{StateAt(a.Id, day, a.State)}"));

                var accruedToDate = dailyInterest[account.Key];
                if (day < 6)
                {
                    accruedToDate = 0m;
                    for (var interestDay = 1; interestDay <= day; interestDay++)
                    {
                        var interestBalance = Balance(account.Key, interestDay, knownByEventDay: interestDay, includeFees: true, includeInterest: false);
                        if (interestBalance > 0)
                            accruedToDate += CurrencyRules.Round(account.Value.Currency, interestBalance * DailyInterestRate);
                    }
                }

                rows.Add(new ReplayRow(day, account.Key, account.Value.Currency,
                    CurrencyRules.Round(account.Value.Currency, ledgerBalance),
                    CurrencyRules.Round(account.Value.Currency, ledgerBalance - activeHolds),
                    CurrencyRules.Round(account.Value.Currency, fees),
                    CurrencyRules.Round(account.Value.Currency, accruedToDate),
                    authSummary));
            }
        }

        return new ReplayResult(
            _entries.AsReadOnly(),
            rows.OrderBy(r => r.Day).ThenBy(r => r.AccountId).ToArray(),
            _errors.AsReadOnly(),
            _diagnostics.AsReadOnly());
    }

    private decimal Balance(string account, int valueDay, int knownByEventDay, bool includeFees, bool includeInterest)
    {
        var opening = _accounts[account].Opening;
        return opening + _entries
            .Where(e => e.AccountId == account && e.ValueDay <= valueDay && e.EventDay <= knownByEventDay)
            .Where(e => includeFees || e.Kind != EntryKind.Fee)
            .Where(e => includeInterest || e.Kind != EntryKind.Interest)
            .Sum(SignedAmount);
    }

    private bool IsActive(Authorization authorization, int day)
    {
        if (authorization.Id == "Auth-A") return day >= 2 && day < 4;
        if (authorization.Id == "Auth-B") return authorization.State == AuthorizationState.Approved && day >= 5;
        return authorization.State == AuthorizationState.Open && day >= authorization.Day;
    }

    private static AuthorizationState StateAt(string id, int day, AuthorizationState finalState)
    {
        if (id == "Auth-A") return day >= 4 ? AuthorizationState.Settled : AuthorizationState.Approved;
        if (id == "Auth-B" && day < 5) return AuthorizationState.Open;
        return finalState;
    }

    private void Settle(string entryId, string authId, decimal amount, int day)
    {
        var index = _authorizations.FindIndex(a => a.Id == authId);
        if (index < 0)
        {
            _errors.Add($"{entryId} rejected: authorization '{authId}' does not exist; no ledger entry created.");
            return;
        }

        var auth = _authorizations[index];
        if (auth.State != AuthorizationState.Approved || amount > auth.HoldAmount)
        {
            _errors.Add($"{entryId} rejected: authorization is not active or settlement exceeds its hold.");
            return;
        }

        _authorizations[index] = auth with { State = AuthorizationState.Settled };
        Append(entryId, auth.AccountId, day, EntryKind.Settlement, amount, auth.Currency, $"Settlement for {authId}");
    }

    private static decimal SignedAmount(LedgerEntry entry) => entry.Kind switch
    {
        EntryKind.Credit or EntryKind.Reversal or EntryKind.Interest => entry.Amount,
        EntryKind.Debit or EntryKind.Fee or EntryKind.Settlement => -entry.Amount,
        _ => 0m
    };
}
