# In-Memory Account Ledger Core (.NET)

A deterministic C#/.NET console replay of a six-day, append-only account ledger. This is a domain-engine exercise, not a production banking system.

## Constraints

- In-memory only: no database, persistence, web layer, API, or UI.
- C# \`decimal\` for money; AED rounded to 2 decimals and BHD to 3 decimals.
- Append-only ledger: reversals append compensating entries and never delete the original debit.
- Daily overdraft fee: AED 25.00 for each negative value date discovered when the backdated D5 debit arrives. The later D6 reversal restores principal but does not erase fees already assessed; D6 itself is not charged.
- Daily interest: 0.04% on positive balances, rounded to the currency's minor-unit precision each day.
- Authorization holds affect available balance, not the ledger balance. An unknown authorization settlement is rejected without creating a ledger entry.

## Requirements

Install the .NET 8 SDK or later. No external runtime services are needed.

## Run the six-day replay

\`\`\`bash
dotnet run --project src/Ledger.Replay/Ledger.Replay.csproj
\`\`\`

## Run tests

\`\`\`bash
dotnet test tests/Ledger.Core.Tests/Ledger.Core.Tests.csproj
\`\`\`

One test is intentionally failing: it encodes rejected criterion #7 (three BHD 3.334 instalments). It is annotated in the test source and exists as a visible counterexample. The correct split is 3.334 + 3.333 + 3.333 = 10.000 BHD. Build/test status has not been independently verified in the authoring environment because the .NET SDK is unavailable.
