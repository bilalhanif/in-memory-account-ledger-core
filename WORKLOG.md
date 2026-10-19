# Work log

- 2026-10-19 10:55 +08:00 — Ported the ledger exercise from Python to C#/.NET using \`decimal\`, currency-specific rounding, a class library, a console replay, and xUnit tests.
- 2026-10-19 10:56 +08:00 — Added the E1–E10 scenario, AED 25.00 overdraft fee rule, 0.04% daily interest, authorization states/errors, BHD reconciled instalments, and append-only reversal behavior.
- 2026-10-19 10:57 +08:00 — Added README, NUMBERS, AMBIGUITIES, REJECTED, and an intentionally failing annotated test for the rejected equal-instalment criterion.
- 2026-10-19 11:00 +08:00 — Retargeted all projects to .NET 8 and added separate event-day metadata for backdated entries and reversals. Corrected fee assessment so E7 retrospectively assesses D2/D4/D5 fees, while E9 on D6 restores principal without charging a new D6 fee or erasing earlier fees.
- Validation note — No \`dotnet\` executable is available in the authoring environment; build/test status remains unverified. The test suite intentionally includes one failing probe per the exercise requirement.
