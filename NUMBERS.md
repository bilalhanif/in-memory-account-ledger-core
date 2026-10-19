# Numbers, constants, and rounding

The event amounts, rates, fee, currency precision, and six-day window below are supplied by the exercise. They are not tunable business defaults.

## Required constants

| Constant | Value | Reason; why not half? |
|---|---:|---|
| Replay window | D1–D6 | The scenario explicitly defines six days; using three days would omit required events. |
| ACC-001 opening balance | AED 0.00 | Specified opening balance; half is not the specified starting state. |
| ACC-002 opening balance | BHD 0.000 | Specified opening balance; half is not the specified starting state. |
| Overdraft fee | AED 25.00 | Specified fee per negative account/value day; AED 12.50 would undercharge. |
| Daily interest rate | 0.04% = 0.0004 | Specified rate; 0.02% would accrue half the required interest. |
| AED scale | 2 decimal places | Required currency precision; one decimal place loses valid minor units. |
| BHD scale | 3 decimal places | Required currency precision; two decimal places loses valid fils. |
| Rounding mode | MidpointRounding.AwayFromZero | Explicit deterministic policy for midpoint values; changing to another mode can change monetary postings. |

## Money representation

- Use decimal, not binary floating-point.
- Round amounts to the currency scale when they become ledger entries.
- Format amounts using invariant culture to keep decimal output stable.

## BHD 10.000 split

Three mathematical equal shares are 3.333333… BHD, which cannot all be represented exactly at 3 decimals. Preserve the source total by assigning the rounding remainder to one instalment:

- Instalment 1: 3.334 BHD
- Instalment 2: 3.333 BHD
- Instalment 3: 3.333 BHD
- Reconciled total: 10.000 BHD

Three 3.334 instalments total 10.002 BHD, overstating the credit by 0.002 BHD. Halving any instalment would fail to allocate the source amount.

## Overdraft fees

E7 is received on D5 but value-dated D2. Before any fee, D2 principal is AED 1,200.00 − AED 950.00 − AED 620.00 = **−AED 370.00**. The event-time replay assesses AED 25.00 for each negative value day discovered on receipt: D2, D4, and D5. E9 later compensates E7's principal but does not erase those already assessed fees.

## Daily interest and capitalisation

- Rate: 0.0004 per day.
- Daily raw accrual: positive eligible closing balance × 0.0004.
- Each daily accrual is rounded to currency precision before it is accumulated.
- The sum of those rounded daily accruals is posted as one interest credit at D6. No rounded daily remainder is discarded.
- In this implementation, balances are event-time snapshots; a backdated event received later does not rewrite earlier accrued interest. This is an explicit interpretation, documented in AMBIGUITIES.md.
