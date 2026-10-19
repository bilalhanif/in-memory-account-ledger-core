# Work log

- 2026-10-19 10:55 +08:00 — Ported the ledger exercise from Python to C#/.NET using decimal, currency-specific rounding, a class library, a console replay, and xUnit tests.
- 2026-10-19 10:56 +08:00 — Added the E1–E10 scenario, AED 25.00 overdraft fee rule, 0.04% daily interest, authorization states/errors, BHD reconciled instalments, and append-only reversal behavior.
- 2026-10-19 10:57 +08:00 — Added README, NUMBERS, AMBIGUITIES, REJECTED, and an intentionally failing annotated test for the rejected equal-instalment criterion.
- 2026-10-19 11:00 +08:00 — Retargeted all projects to .NET 8 and added separate event-day metadata for backdated entries and reversals.
- 2026-10-19 (time not captured) — Updated the core to produce event-time snapshots, assess one fee per negative value day after the backdated debit arrives, reject Auth-B when its hold would make available balance negative, and capitalize rounded daily interest as one D6 credit.
- 2026-10-19 (time not captured) — Added tests for event-time diagnostics, Auth-B rejection, and single-credit D6 capitalization; corrected acceptance-criteria classification and documented abandoned approaches.
- Validation note — The user previously ran the suite with 5 passing tests and the intentionally failing criterion-7 probe. The latest changes have not yet been run locally; run the commands in README and inspect the replay output before treating the updated commit as verified.
