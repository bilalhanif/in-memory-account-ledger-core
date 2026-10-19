using Ledger.Core;

var result = new LedgerEngine().RunScenario();
Console.WriteLine("IN-MEMORY ACCOUNT LEDGER — SIX-DAY REPLAY");
Console.WriteLine("Day | Account | Ledger closing | Available | Fees to date | Interest to date | Authorization states");
foreach (var row in result.DailyRows)
{
    Console.WriteLine($"D{row.Day} | {row.AccountId} | {CurrencyRules.Format(row.Currency, row.LedgerBalance)} | {CurrencyRules.Format(row.Currency, row.AvailableBalance)} | {CurrencyRules.Format(row.Currency, row.Fees)} | {CurrencyRules.Format(row.Currency, row.Interest)} | {row.AuthorizationSummary}");
}
Console.WriteLine("\nERRORS");
foreach (var error in result.Errors) Console.WriteLine($"- {error}");
Console.WriteLine("\nAPPEND-ONLY LEDGER ENTRIES");
foreach (var entry in result.Entries.OrderBy(e => e.ValueDay).ThenBy(e => e.Id))
    Console.WriteLine($"{entry.Id} | value D{entry.ValueDay} | {entry.AccountId} | {entry.Kind} | {CurrencyRules.Format(entry.Currency, entry.Amount)} | {entry.Description}");
