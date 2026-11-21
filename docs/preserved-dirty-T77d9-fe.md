# Preserved dirty files — T77d9-fe conflict record (AC-119)

Status: Record (conflict record for T77d9-fe, AC-119)

Baseline: branch `swarm/T77d9-fe` from `origin/main` at `434e214e`.
Local `main` was 93 behind with 11 tracked dirty files. Tracked changes were
stashed, the branch was created from `origin/main`, and the stash was applied.
Seven files conflicted: `origin/main` already carries a newer, more complete
version of the same hardening. Per the requirements assumption A1, this branch
ships the preserved new content plus this conflict record, and keeps the
upstream (newer) versions for the superseded files instead of regressing them.

## Kept onto the branch (new vs origin/main)

- `docs/architecture.md` — A03 v0.16.0 blueprint addendum (phases 66–78).
- `services/Autonogrammer.Api/Security/JwtBearerSetup.cs` — explicit 30 s
  metadata refresh / 10 min automatic refresh, bounding the post-rotation 401
  window. No equivalent existed on `origin/main`.

## Kept at upstream (dirty draft superseded by origin/main)

- `src/Autonogrammer.Accounts/AccountSessionManager.cs` — upstream adds the
  raise gate held across update and delivery with the state gate not held
  during handlers; the dirty draft only reordered the state lock.
- `src/Autonogrammer.Harness.Secrets.Os/BlockingSecretStore.cs` and
  `WindowsDpapiStore.cs` — upstream additionally observes the abandoned
  native call's fault via a continuation, avoiding an unobserved exception;
  the dirty draft dropped it.
- `src/Autonogrammer.Harness.Secrets.Os/LibsecretNativeApi.cs` — upstream
  renames the helper to `ThrowForError`, documents the dismissed-prompt
  `Locked` mapping, and uses the named constant instead of the dirty draft's
  magic `2`.
- `tests/Autonogrammer.Accounts.Tests/AccountSessionManagerTests.cs`,
  `tests/Autonogrammer.Api.IntegrationTests/Security/ApiAuthOptionsValidatorTests.cs`,
  `tests/Autonogrammer.Harness.Secrets.Os.Tests/OsSecretStoreContractTests.cs` —
  upstream contains the equivalent or fuller tests (concurrent mutation
  ordering, prompt-blocked cancellation, DPAPI-blocked cancellation).
- `services/Autonogrammer.Api/Options/ApiAuthOptionsValidator.cs` — the dirty
  hunk duplicated the providers/wallet check outside the method; upstream
  already has the single correct check inside the method.
- `tests/Autonogrammer.Harness.Secrets.Os.Tests/LinuxLibsecretTests.cs` — the
  dirty tests called the old `ThrowForDomainCode` name; upstream renamed it
  to `ThrowForError` and already tests the cancelled/uncancelled cases.

No preserved content was lost: every dirty behaviour is present on this branch,
either as the kept change above or as the upstream implementation.

`main` was left clean: no commit was made on `main`, and the preserved changes
live only on `swarm/T77d9-fe`.
