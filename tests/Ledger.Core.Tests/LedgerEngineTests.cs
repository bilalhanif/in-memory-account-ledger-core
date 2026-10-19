using Ledger.Core;
using Xunit;

namespace Ledger.Core.Tests;

public sealed class LedgerEngineTests
{
    [Fact]
    public void Currency_rounding_uses_two_decimals_for_AED_and_three_for_BHD()
    {
        Assert.Equal(1.24m, CurrencyRules.Round(Currency.AED, 1.235m));
        Assert.Equal(1.235m, CurrencyRules.Round(Currency.BHD, 1.2346m));
    }

    [Fact]
    public void Unknown_authorization_settlement_is_rejected_without_ledger_entry()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.Errors, e => e.Contains("Auth-Z") && e.Contains("no ledger entry created"));
        Assert.DoesNotContain(result.Entries, e => e.Id == "E6");
    }

    [Fact]
    public void Reversal_is_appended_and_does_not_delete_original_debit()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.Entries, e => e.Id == "E7" && e.Kind == EntryKind.Debit);
        Assert.Contains(result.Entries, e => e.Id == "E9" && e.Kind == EntryKind.Reversal && e.ReversesEntryId == "E7");
    }

    [Fact]
    public void Backdated_debit_assesses_only_negative_value_days_before_reversal_arrives()
    {
        var result = new LedgerEngine().RunScenario();
        var fees = result.Entries.Where(e => e.Kind == EntryKind.Fee).Select(e => e.Id).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { "FEE-D2", "FEE-D4", "FEE-D5" }, fees);
        Assert.DoesNotContain(result.Entries, e => e.Id == "FEE-D6");
        Assert.Contains(result.Entries, e => e.Id == "E7" && e.EventDay == 5 && e.ValueDay == 2);
        Assert.Contains(result.Entries, e => e.Id == "E9" && e.EventDay == 6 && e.ValueDay == 2);
    }

    [Fact]
    public void BHD_instalments_reconcile_exactly_to_ten_units()
    {
        var result = new LedgerEngine().RunScenario();
        var instalments = result.Entries.Where(e => e.Id.StartsWith("E10-")).Select(e => e.Amount).ToArray();
        Assert.Equal(new[] { 3.334m, 3.333m, 3.333m }, instalments);
        Assert.Equal(10.000m, instalments.Sum());
    }

    // Intentionally failing acceptance-criteria probe: equal 3.334 BHD instalments total 10.002 BHD.
    // Kept visible as a red test to document why criterion #7 is rejected; do not use as a production assertion.
    [Fact]
    public void REJECTED_criterion_7_claims_all_three_instalments_are_3_334()
    {
        var result = new LedgerEngine().RunScenario();
        var instalments = result.Entries.Where(e => e.Id.StartsWith("E10-")).Select(e => e.Amount).ToArray();
        Assert.Equal(new[] { 3.334m, 3.334m, 3.334m }, instalments);
    }
}
