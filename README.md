# In-Memory Account Ledger Core (.NET)

A deterministic C#/.NET console replay of the six-day E1–E10 event stream. This is an in-memory domain-engine exercise, not a production banking system.

## Scope and rules

- No web layer, persistence, database, or UI.
- Money uses C# decimal; AED is rounded to 2 decimal places and BHD to 3 using MidpointRounding.AwayFromZero.
- Ledger entries are append-only. E9 adds a compensating credit linked to E7; E7 is never mutated or deleted.
- An AED 25.00 fee is assessed once per account/value day when the balance is negative. When E7 arrives on D5 back-valued to D2, the scenario's negative value days are D2, D4, and D5. The later D6 reversal does not erase those fees.
- Daily interest is 0.04% of a positive event-time closing balance. Each day's accrual is rounded to currency precision, accumulated off-ledger, and capitalized as one credit dated D6. Interest does not compound daily in this interpretation.
- Holds affect available balance but are not ledger entries. Authorization is approved only when ledger balance minus existing active holds minus the requested hold is non-negative.
- E6 is rejected because Auth-Z does not exist. Auth-B is rejected because its hold would make the available balance negative.

## Prerequisites

Install the .NET 8 SDK or later. No external services are required.

## Run the six-day replay

    dotnet run --project src/Ledger.Replay/Ledger.Replay.csproj

The console prints per-day account balances, available balances, fees known to date, rounded interest accrued, authorization states, backdated-event diagnostics, errors, and append-only entries with both event and value days.

## Run tests

    dotnet test tests/Ledger.Core.Tests/Ledger.Core.Tests.csproj

One test is intentionally failing: REJECTED_criterion_7_claims_all_three_instalments_are_3_334. It is an annotated counterexample required by the exercise, not an expected production behavior. The correct BHD split is 3.334 + 3.333 + 3.333 = 10.000 BHD. Other tests should pass; run them locally to confirm the current commit.

## Known interpretation

Daily interest uses the balance known at each event-time day; a later backdated event does not rewrite interest that was already accrued for earlier days. The task does not explicitly prescribe retroactive interest adjustments. See AMBIGUITIES.md.
