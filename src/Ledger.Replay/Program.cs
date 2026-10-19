using Ledger.Core;

var result = new LedgerEngine().RunScenario();
Console.WriteLine("IN-MEMORY ACCOUNT LEDGER — SIX-DAY EVENT-TIME REPLAY");
Console.WriteLine("Day | Account | Ledger closing | Available | Fees known to date | Rounded interest accrued | Authorization states");
foreach (var row in result.DailyRows)
{
    Console.WriteLine($"D{row.Day} | {row.AccountId} | {CurrencyRules.Format(row.Currency, row.LedgerBalance)} | {CurrencyRules.Format(row.Currency, row.AvailableBalance)} | {CurrencyRules.Format(row.Currency, row.Fees)} | {CurrencyRules.Format(row.Currency, row.Interest)} | {row.AuthorizationSummary}");
}

Console.WriteLine("\nVALUE-DATE RESTATEMENT / BACKDATED-EVENT DIAGNOSTICS");
var d2EventTime = result.DailyRows.Single(row => row.Day == 2 && row.AccountId == "ACC-001").LedgerBalance;
Console.WriteLine($"- D2 closing balance as originally known on D2: {CurrencyRules.Format(Currency.AED, d2EventTime)}.");
foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"- {diagnostic}");

Console.WriteLine("\nERRORS");
foreach (var error in result.Errors) Console.WriteLine($"- {error}");

Console.WriteLine("\nAPPEND-ONLY LEDGER ENTRIES (ordered by event day, then value day)");
foreach (var entry in result.Entries.OrderBy(e => e.EventDay).ThenBy(e => e.ValueDay).ThenBy(e => e.Id))
{
    Console.WriteLine($"{entry.Id} | event D{entry.EventDay} | value D{entry.ValueDay} | {entry.AccountId} | {entry.Kind} | {CurrencyRules.Format(entry.Currency, entry.Amount)} | {entry.Description}");
}
