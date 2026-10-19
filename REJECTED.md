# Rejected acceptance criteria

The following supplied criteria are intentionally not treated as correct assertions because they conflict with the scenario's arithmetic or ledger semantics.

1. **“Day 2 balance is −AED 370.00.” — REJECTED as a final closing-balance assertion.** −AED 370.00 is the principal balance after E7's backdated debit and before the overdraft fee and later reversal. It is a useful intermediate calculation, not the final replay balance once fee and reversal entries are included.
2. **“E7 causes exactly one Day 2 fee.” — ACCEPTED only with qualification.** One fee is assessed for the single negative value date D2. The general rule is one fee per negative account/day, not one fee per debit event; multiple qualifying debits on the same day must not multiply the daily fee.
3. **“Auth-A settlement is accepted.” — ACCEPTED.** Auth-A exists with a 200.00 AED hold; the 185.00 AED settlement is within that hold.
4. **“Unknown Auth-Z settlement is rejected and posts no funds.” — ACCEPTED.** No authorization exists, so E6 is rejected and creates no ledger entry.
5. **“Auth-B hold affects available balance, not ledger balance.” — ACCEPTED.** The hold is not a ledger posting and remains outstanding through D6.
6. **“E9 restores pre-E7 balances and fees.” — REJECTED as written.** E9 compensates E7's principal effect, but the fee triggered by the historical overdraft is not automatically erased. Restoring principal does not imply restoring fees.
7. **“Each BHD instalment is 3.334.” — REJECTED.** Three such instalments total 10.002 BHD, not 10.000 BHD. A reconciled 3-decimal split is 3.334, 3.333, 3.333 BHD.
8. **“Discard the interest remainder.” — REJECTED.** Silently discarding accrued fractions can cause reconciliation drift. This implementation rounds and posts daily interest at currency precision; any separate end-of-period remainder treatment must be an explicit business rule.

The intentionally failing test \`REJECTED_criterion_7_claims_all_three_instalments_are_3_334\` preserves a visible counterexample for criterion #7.
