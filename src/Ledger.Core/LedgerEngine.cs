namespace Ledger.Core;

/// <summary>Deterministic six-day, in-memory ledger replay. Entries are append-only; reversals append compensating entries.</summary>
public sealed class LedgerEngine
{
    private const decimal DailyInterestRate = 0.0004m; // 0.04%
    private const decimal DailyOverdraftFee = 25.00m;
    private readonly List<LedgerEntry> _entries = [];
    private readonly List<Authorization> _authorizations = [];
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, (Currency Currency, decimal Opening)> _accounts = new()
    {
        ["ACC-001"] = (Currency.AED, 0m),
        ["ACC-002"] = (Currency.BHD, 0m)
    };

    public IReadOnlyList<LedgerEntry> Entries => _entries.AsReadOnly();

    private void Append(string id, string account, int day, EntryKind kind, decimal amount, Currency currency, string description, string? reverses = null, int? eventDay = null)
    {
        if (day is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(day));
        if (!_accounts.TryGetValue(account, out var config) || config.Currency != currency)
            throw new InvalidOperationException($"Unknown account/currency combination: {account}/{currency}.");
        _entries.Add(new LedgerEntry(id, account, day, kind, CurrencyRules.Round(currency, amount), currency, description, reverses, eventDay ?? day));
    }

    public ReplayResult RunScenario()
    {
        _entries.Clear(); _authorizations.Clear(); _errors.Clear();
        // Original input stream E1-E10. E7/E9 have D2 value dates despite later event dates.
        Append("E1", "ACC-001", 1, EntryKind.Credit, 1200.00m, Currency.AED, "D1 credit");
        Append("E2", "ACC-001", 1, EntryKind.Debit, 950.00m, Currency.AED, "D1 debit");
        _authorizations.Add(new Authorization("Auth-A", "ACC-001", 200m, Currency.AED, 2, AuthorizationState.Approved));
        Append("E4", "ACC-001", 3, EntryKind.Credit, 400.00m, Currency.AED, "D3 credit");
        Settle("E5", "Auth-A", 185m, 4);
        Settle("E6", "Auth-Z", 180m, 4); // rejected: no prior authorization
        Append("E7", "ACC-001", 2, EntryKind.Debit, 620.00m, Currency.AED, "D5 debit back-valued to D2", eventDay: 5);
        _authorizations.Add(new Authorization("Auth-B", "ACC-001", 90m, Currency.AED, 5, AuthorizationState.Approved));
        // Reversal is an appended compensating entry on D2 value date; prior overdraft fees remain charged.
        Append("E9", "ACC-001", 2, EntryKind.Reversal, 620.00m, Currency.AED, "D6 reversal of E7", "E7", eventDay: 6);
        // Split BHD 10.000 into three equal instalments at currency precision: 3.334, 3.333, 3.333.
        Append("E10-A", "ACC-002", 5, EntryKind.Credit, 3.334m, Currency.BHD, "Instalment 1 of 3");
        Append("E10-B", "ACC-002", 5, EntryKind.Credit, 3.333m, Currency.BHD, "Instalment 2 of 3");
        Append("E10-C", "ACC-002", 5, EntryKind.Credit, 3.333m, Currency.BHD, "Instalment 3 of 3");

        // A backdated debit received on D5 can retrospectively create fees for negative
        // value dates through D5. Those fees are append-only. E9 arrives on D6, so it
        // restores principal for D6 onward but cannot erase fees already assessed.
        foreach (var account in _accounts.Where(a => a.Value.Currency == Currency.AED))
        {
            for (var valueDay = 1; valueDay <= 5; valueDay++)
            {
                var principalAtAssessment = account.Value.Opening + _entries
                    .Where(e => e.AccountId == account.Key && e.ValueDay <= valueDay && e.EventDay <= 5 &&
                        e.Kind is not EntryKind.Fee and not EntryKind.Interest and not EntryKind.Reversal)
                    .Sum(SignedAmount);
                if (principalAtAssessment < 0)
                    Append($"FEE-D{valueDay}", account.Key, valueDay, EntryKind.Fee, DailyOverdraftFee,
                        account.Value.Currency, $"Overdraft fee for D{valueDay}", eventDay: 5);
            }
        }

        var rows = new List<ReplayRow>();
        foreach (var account in _accounts)
        {
            decimal accumulatedInterest = 0m;
            decimal feeTotal = 0m;
            for (var day = 1; day <= 6; day++)
            {
                // Fee entries are assessed above when the backdated D5 debit is received.
                // The D6 reversal restores principal but leaves those earlier fees intact.
                feeTotal = _entries.Where(e => e.AccountId == account.Key && e.ValueDay <= day && e.Kind == EntryKind.Fee)
                    .Sum(e => e.Amount);

                var balanceAfterFee = account.Value.Opening + _entries
                    .Where(e => e.AccountId == account.Key && e.ValueDay <= day)
                    .Sum(SignedAmount);
                if (balanceAfterFee > 0)
                {
                    var interest = CurrencyRules.Round(account.Value.Currency, balanceAfterFee * DailyInterestRate);
                    if (interest != 0)
                    {
                        Append($"INT-{account.Key}-D{day}", account.Key, day, EntryKind.Interest, interest, account.Value.Currency, $"Daily interest for D{day}");
                        accumulatedInterest += interest;
                    }
                }

                var finalBalance = account.Value.Opening + _entries
                    .Where(e => e.AccountId == account.Key && e.ValueDay <= day)
                    .Sum(SignedAmount);
                var holds = _authorizations.Where(a => a.AccountId == account.Key && a.Day <= day &&
                    (a.Id == "Auth-B" || (a.Id == "Auth-A" && day < 4)))
                    .Sum(a => a.HoldAmount);
                var authSummary = string.Join(", ", _authorizations.Where(a => a.AccountId == account.Key && a.Day <= day)
                    .Select(a => $"{a.Id}:{(a.Id == "Auth-A" && day >= 4 ? AuthorizationState.Settled : a.State)}"));
                rows.Add(new ReplayRow(day, account.Key, account.Value.Currency, CurrencyRules.Round(account.Value.Currency, finalBalance), CurrencyRules.Round(account.Value.Currency, finalBalance - holds), feeTotal, accumulatedInterest, authSummary));
            }
        }
        return new ReplayResult(_entries.AsReadOnly(), rows.OrderBy(r => r.Day).ThenBy(r => r.AccountId).ToArray(), _errors.AsReadOnly());
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
        if (amount > auth.HoldAmount)
        {
            _errors.Add($"{entryId} rejected: settlement exceeds authorization hold.");
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
