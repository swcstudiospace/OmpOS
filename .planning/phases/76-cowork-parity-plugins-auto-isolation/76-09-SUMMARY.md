---
phase: 76-cowork-parity-plugins-auto-isolation
plan: '09'
subsystem: harness-isolation
tags: [bubblewrap, isolation, probe, argv-builder, live-test, security]

requires: []
provides:
  - Corrected Bubblewrap availability probe (self-test mounts `/` read-only and runs `/bin/true`)
  - Read-only file grants preserved by the argv adapter when the target exists
affects: [SEC-77-08]
requirements: [AG-76-10]
status: closed
commit: 980d2012
key-files:
  modified:
    - src/Autonogrammer.Harness.Isolation/BubblewrapArgvBuilder.cs
    - tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs
    - docs/runbooks/cowork-isolation.md
    - CHANGELOG.md
    - docs/quality/v0.16.0-security-review.md
key-decisions:
  - "Plan 76-09 is closed on the plain live pass, per owner decision 2026-11-15."
  - "The protected outer-bubblewrap run is an environment limit on this host, not a pass."
  - "Phase 76 is not claimed complete by this summary."
patterns-established:
  - "Absent read-only grants are skipped rather than emitted as binds with missing targets."
---

# Phase 76 Plan 09: Bubblewrap probe and wrapper defects Summary

**Bubblewrap probe self-test corrected; six session-wrapper defects found by the protected live attempt, five already fixed in source and one fixed in `BubblewrapArgvBuilder.cs`; plain live control passes on the host.**

## Status

- Plan 76-09 is **closed** on the plain live pass, per owner decision 2026-11-15.
- The protected outer-bubblewrap run is **not done**. It is an environment limit on this host: the .NET CLI fails inside the nested sandbox (Guid.NewGuid cryptographic error).
- Phase 76 is **not** claimed complete.

## Accomplishments

- Six wrapper defects were identified in 76-09. Five were already fixed in source before this commit.
- The sixth, an existing read-only file grant silently dropped by Adapt, is fixed in `BubblewrapArgvBuilder.cs` (one line). Absent grants still skip.
- Commit `980d2012` also carries the 75-11 CHANGELOG bullet and the SEC-77-06 row update in the security review (DefaultConfigTests 17/17 observed on a run-owned Postgres).

## Verification

- Non-live Isolation suite: 24/24 (build 0/0 under warnaserror).
- Live `IsolationLiveTests`: 1/1 with `AUTONOGRAMMER_LIVE_ISOLATION=1` on the host.
- Residual: the `ungranted` denial passes by absence (the target path does not exist inside the sandbox), not by a mount rule. This is recorded as a residual risk, not as a verified mount denial.

## Residual Risk

- The protected outer-bubblewrap run has not been performed on this host.
- The `ungranted` denial is established by absence, not by a mount rule.
- SEC-77-08 is recorded as fixed, with owner sign-off 15/11/2026 by SomeRandmGuyy, in `docs/quality/v0.16.0-security-review.md`; its residual sentence is kept there.

## Files Created/Modified

- `src/Autonogrammer.Harness.Isolation/BubblewrapArgvBuilder.cs` - read-only file grant fix (one line; absent grants still skip)
- `tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs` - live control test update
- `docs/runbooks/cowork-isolation.md` - runbook update with observed results
- `CHANGELOG.md` - bullet
- `docs/quality/v0.16.0-security-review.md` - SEC-77-06 row evidence (commit content)

## Self-Check

- Commit `980d2012` exists and touches the five files above.
- Status claims limited to the observed host results listed under Verification.
