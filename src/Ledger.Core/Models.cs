namespace Ledger.Core;

public enum Currency { AED, BHD }
public enum EntryKind { Credit, Debit, Fee, Interest, Settlement, Reversal }
public enum AuthorizationState { Approved, Settled, Rejected, Open }

public sealed record LedgerEntry(
    string Id,
    string AccountId,
    int ValueDay,
    EntryKind Kind,
    decimal Amount,
    Currency Currency,
    string Description,
    string? ReversesEntryId = null,
    int EventDay = 0);

public sealed record Authorization(string Id, string AccountId, decimal HoldAmount, Currency Currency, int Day, AuthorizationState State);
public sealed record ReplayRow(int Day, string AccountId, Currency Currency, decimal LedgerBalance, decimal AvailableBalance, decimal Fees, decimal Interest, string AuthorizationSummary);
public sealed record ReplayResult(IReadOnlyList<LedgerEntry> Entries, IReadOnlyList<ReplayRow> DailyRows, IReadOnlyList<string> Errors);

public static class CurrencyRules
{
    public static int Scale(Currency currency) => currency switch { Currency.AED => 2, Currency.BHD => 3, _ => throw new ArgumentOutOfRangeException(nameof(currency)) };
    public static decimal Round(Currency currency, decimal amount) => decimal.Round(amount, Scale(currency), MidpointRounding.AwayFromZero);
    public static string Format(Currency currency, decimal amount) => $"{Round(currency, amount).ToString($"F{Scale(currency)}", System.Globalization.CultureInfo.InvariantCulture)} {currency}";
}
