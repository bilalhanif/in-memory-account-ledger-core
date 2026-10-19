# Numbers and rounding

## Currency precision

- AED: 2 decimal places.
- BHD: 3 decimal places.
- \`decimal\` is used for financial arithmetic; binary floating-point types are not used.
- Rounding uses \`MidpointRounding.AwayFromZero\` at the currency's minor-unit precision.

## BHD split

The source amount is 10.000 BHD and must reconcile exactly. Three equal mathematical shares are 3.333333… BHD, which cannot each be represented at 3 decimal places. A reconciled allocation is:

- Instalment 1: 3.334 BHD
- Instalment 2: 3.333 BHD
- Instalment 3: 3.333 BHD
- Total: 10.000 BHD

Three instalments of 3.334 BHD would total 10.002 BHD, overstating the source amount by 0.002 BHD.

## Overdraft fee

Daily fee is AED 25.00 when the value-dated balance is negative at end of day. E7 is back-valued to D2, causing the D2 pre-fee principal balance to be 250.00 - 620.00 = -370.00 AED. A 25.00 AED fee is recorded as a separate append-only debit entry. E9 later compensates the 620.00 AED principal debit, but does not delete the fee.

## Interest

Daily rate: 0.04% = 0.0004. For a positive daily balance \`B\`, unrounded daily interest is \`B × 0.0004\`, then rounded to the currency precision. Daily rounded interest entries are posted to the ledger. The implementation does not discard an unrounded remainder silently; any alternative aggregate/reconciliation method should be explicitly specified before changing this policy.
