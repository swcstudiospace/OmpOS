---
phase: 76-cowork-parity-plugins-auto-isolation
plan: "09"
subsystem: harness
tags: [isolation, bubblewrap, probe]
status: partial_blocked
---

# 76-09 Summary

Plan `76-09-PLAN.md` (correct the Bubblewrap availability probe: mount `/` read-only for the self-test and
execute `/bin/true`; strengthened live test must fail until a real allowed write and a denied outside
write both work; nested-sandbox limitation reported, never inferred) is **partially landed** on
`origin/main` @ `c04095ba`.

## Present on the tree (probe fix landed)

- `src/Autonogrammer.Harness.Isolation/IsolationProbe.cs` line 10:
  `SelfTestArguments = ["--die-with-parent", "--unshare-user", "--unshare-pid", "--ro-bind", "/", "/", "/bin/true"]`
  — the plan's probe correction is in the shipped source (root mounted read-only, `/bin/true` executed,
  existing namespace flags and degraded-state handling retained).
- `tests/Autonogrammer.Harness.Isolation.Tests/IsolationProbeTests.cs` — present.
- `tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs` — present; gated by
  `AUTONOGRAMMER_LIVE_ISOLATION=1` (line 14). Without the variable the test skips, it does not fail.
- `docs/runbooks/cowork-isolation.md` — present (runbook referenced by the plan).

## Still blocked (plan's own terms)

- The strengthened live test demands **both** a successful allowed write and a denied outside write from
  a single child inside an outer Bubblewrap environment with a disposable `/etc`. The plan itself records
  that the wrapper fails for reasons beyond the probe (writable binds lack destination arguments, `/tmp`
  mount ordering, denied targets including absent directories and socket files, deny-mask precedence,
  nested read-only grants). Those wrapper defects are **not addressed** by the probe fix, and the live
  positive control therefore **still fails** on the current tree. Per the plan, do not fix arity alone and
  declare isolation working.
- The live test was **not executed** by this doc lane: it needs Bubblewrap with user-namespace rights and
  an outer disposable-`/etc` environment. Recorded as blocked, never as passed.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Harness.Isolation.Tests --filter-not-trait "Category=Live"` —
  30 succeeded, 0 failed, 0 skipped (IsolationProbeTests + planner/argv/profile suites).
- `AUTONOGRAMMER_LIVE_ISOLATION=1` execution — **not run** (see blocked above).

## Next step for the owning lane

A separately verified wrapper design (deny-mask precedence, missing-target and socket-alias handling,
nested read-only grants, `/tmp` ordering) must land before `IsolationLiveTests` can pass its positive
control. Route to A05. This doc lane made no code change.
