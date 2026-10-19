# Acceptance-criteria decisions

The assignment warns that some supplied criteria are incorrect. This file distinguishes the criteria accepted as stated from those rejected.

1. **“The Day 2 closing ledger balance, evaluated at end of Day 5 and before any fee is assessed, is AED −370.00.” — ACCEPTED.** E7 is known by D5 but value-dated D2: AED 1,200 − AED 950 − AED 620 = −AED 370 before fees.
2. **“E7 causes exactly one overdraft fee to be assessed, on Day 2.” — REJECTED.** The rule is one fee per negative account/value day, not one fee per transaction. Once E7 is received, the negative value days are D2, D4, and D5, so three fees are assessed.
3. **“The Day 4 settlement of Auth-A must be accepted.” — ACCEPTED.** Auth-A exists with an AED 200 hold; AED 185 is within that amount.
4. **“Any settlement referencing an authorization ID not present in the ledger must be rejected and the funds must not leave the account.” — ACCEPTED.** E6 is rejected because Auth-Z does not exist; no E6 ledger entry is appended.
5. **“If Auth-B is approved, its hold reduces available balance but not ledger balance.” — ACCEPTED as a conditional rule.** Holds do not create ledger entries. In this scenario Auth-B is rejected because applying AED 90 would make available balance negative, so no active hold is applied.
6. **“After E9, all balances and fees return to their pre-E7 values.” — REJECTED.** E9 compensates E7's principal effect but does not reverse fees already assessed. A later reversal also must not alter event-time snapshots for D1–D5.
7. **“The three BHD instalments in E10 must each be BHD 3.334.” — REJECTED.** That totals BHD 10.002, not BHD 10.000. The reconciled split is 3.334 + 3.333 + 3.333 = 10.000 BHD.
8. **“If the rounded daily interest accruals do not sum to the capitalized total, the remainder is discarded.” — REJECTED.** The requirements explicitly say rounded daily accruals must sum exactly to the capitalized total. The engine sums the rounded daily amounts and posts one D6 credit; it does not discard a remainder.

## Approaches abandoned during the build

- **Posting daily interest credits:** abandoned because the requirement calls for one capitalized credit at the end of D6. Daily accruals are now calculation values only.
- **Applying a backdated reversal to all historical snapshots:** abandoned because E9 is not known before D6. The replay retains event-time snapshots and records value dates separately.
- **Approving Auth-B unconditionally:** abandoned because authorization must be declined when the hold would make available balance negative.
- **Splitting BHD 10.000 into three 3.334 instalments:** rejected because it creates BHD 10.002 and fails reconciliation.

## Intentionally failing test

REJECTED_criterion_7_claims_all_three_instalments_are_3_334 is deliberately red. It demonstrates that the rejected assertion conflicts with currency precision and reconciliation; it is not a failing test of the intended ledger behavior.
