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
    public void Auth_A_is_approved_only_when_available_balance_covers_the_hold()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.DailyRows, r => r.AccountId == "ACC-001" && r.Day == 2 && r.AuthorizationSummary.Contains("Auth-A:Approved"));
    }

    [Fact]
    public void Auth_B_is_rejected_when_hold_would_make_available_balance_negative()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.Errors, e => e.Contains("Auth-B") && e.Contains("no hold was applied"));
        Assert.DoesNotContain(result.DailyRows.Where(r => r.AccountId == "ACC-001"), r => r.Day == 5 && r.AuthorizationSummary.Contains("Auth-B:Approved"));
        Assert.DoesNotContain(result.DailyRows, r => r.AccountId == "ACC-001" && r.Day == 5 && r.AvailableBalance < r.LedgerBalance);
    }

    [Fact]
    public void Reversal_is_appended_and_does_not_delete_original_debit()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.Entries, e => e.Id == "E7" && e.Kind == EntryKind.Debit && e.EventDay == 5 && e.ValueDay == 2);
        Assert.Contains(result.Entries, e => e.Id == "E9" && e.Kind == EntryKind.Reversal && e.ReversesEntryId == "E7" && e.EventDay == 6 && e.ValueDay == 2);
    }

    [Fact]
    public void Backdated_debit_reports_pre_fee_D2_balance_and_assesses_one_fee_per_negative_value_day()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Contains(result.Diagnostics, d => d.Contains("D2 balance known at end of D5 before fees = -370.00 AED"));
        var fees = result.Entries.Where(e => e.Kind == EntryKind.Fee).Select(e => e.Id).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { "FEE-D2", "FEE-D4", "FEE-D5" }, fees);
        Assert.DoesNotContain(result.Entries, e => e.Id == "FEE-D6");
    }

    [Fact]
    public void Daily_interest_is_rounded_but_capitalized_as_one_D6_credit()
    {
        var result = new LedgerEngine().RunScenario();
        Assert.Single(result.Entries, e => e.AccountId == "ACC-001" && e.Kind == EntryKind.Interest);
        Assert.Single(result.Entries, e => e.AccountId == "ACC-002" && e.Kind == EntryKind.Interest);
        Assert.Contains(result.Entries, e => e.Id == "INT-ACC-001-D6" && e.ValueDay == 6 && e.Amount == 0.81m);
        Assert.Contains(result.Entries, e => e.Id == "INT-ACC-002-D6" && e.ValueDay == 6 && e.Amount == 0.008m);
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
    // This red test is required by the exercise and documents why criterion #7 is rejected.
    [Fact]
    public void REJECTED_criterion_7_claims_all_three_instalments_are_3_334()
    {
        var result = new LedgerEngine().RunScenario();
        var instalments = result.Entries.Where(e => e.Id.StartsWith("E10-")).Select(e => e.Amount).ToArray();
        Assert.Equal(new[] { 3.334m, 3.334m, 3.334m }, instalments);
    }
}
