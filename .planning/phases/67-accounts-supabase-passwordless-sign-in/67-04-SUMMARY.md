# Plan 67-04 SUMMARY: Accounts.Desktop (loopback PKCE listener, file lock, secret-store selector)

Status: code complete and green; the Desktop test project passes. `tests/Autonogrammer.ArchitectureTests` could not be run to completion (see "Not run"). No live rows exist for this plan.

Requirements covered: AG-67-2 (listener side), AG-67-6 (lock side, with the 67-01 manager), AG-67-11, AG-67-12.

## Results

| Command | Result |
| --- | --- |
| `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"` | total 47, failed 0, skipped 0 (run on the final code, after the last source edit) |
| `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` | NOT RUN to completion: the project build stops on analyzer errors in `tests/Autonogrammer.ArchitectureTests/GateParityTests.cs` lines 109, 115 (MA0006) and 128 (MA0002), a file of commit `0afcd3b9` (phase 68-06). The earlier blocker `HeadServices.cs` MA0051 no longer appears. Not edited here (not this plan's file); lead-68 was told. |
| `CI=true ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode` | NOT RUN (the orchestrator owns the one solution-wide command). Each changed lock file was produced by `restore --force-evaluate` of its own project. |

## Commits

- `1c0bea09 feat(67-04): loopback PKCE listener and link flow with raw-socket fake host; Logging.Abstractions reference and relock`
- `8ddb860c feat(67-04): cross-process file lock as the refresh guard with adopt-if-rotated tests`
- `162abc53 feat(67-04): account secret-store selector with consented file fallback and settings keys`

## Public surface that plan 67-06 consumes

Namespace `Autonogrammer.Accounts.Desktop` (project `src/Autonogrammer.Accounts.Desktop`, L3):

- `LoopbackListener : IAsyncDisposable`. `static LoopbackListener? TryStart(string nonce, TimeProvider, ILogger? = null)` (null when no loopback port binds); `Uri RedirectUri` (`http://127.0.0.1:<port>/cb/<nonce>`), `int Port`, `Task<LoopbackResult> Result` (cancelled when disposed first), `int RejectedRequests`; constants `MaxRequestLineBytes` 2048, `MaxHeaderBytes` 8192, `CallbackPrefix`; static `Lifetime` 10 min, `ReadTimeout` 5 s. `LoopbackResult { Outcome, Code (SecretString?), IsLinkInvalidOrExpired }`, `enum LoopbackOutcome { Code, LinkError, Expired }`.
- `PkceSignInFlow(SignInCoordinator, TimeProvider, ILogger<PkceSignInFlow>? = null) : IAsyncDisposable`: `SendCodeAsync(email, ct)` (new address = new attempt with fresh verifier, nonce and listener; the same address is a resend that keeps all three, exactly as the coordinator defines it), `ResendCodeAsync(ct)`, `VerifyCodeAsync(code, ct)` (success closes the listener), `WaitForLinkAsync(ct)` returning `AuthResult<AuthUnit>?` (null: no link in play or attempt replaced; success: the callback signed in; failure `LinkInvalidOrExpired`: error callback or no callback in ten minutes, so the code is the way in), `ResetAsync()`, `RedirectUri`, constant `LinkOnAnotherDeviceSentence` (the D-09 sentence; 67-06 may carry it in `AccountCopy` instead).
- `CrossProcessFileLock(string lockFilePath, TimeProvider? = null, ILogger<CrossProcessFileLock>? = null) : IRefreshGuard`: `FileShare.None` lock file (created owner-only, directory created when missing), a static per-path in-process gate in front of the file lock, release by disposing the handle, 2 minute `MaximumWait` (then `TimeoutException`), never deletes the file. 67-06 passes it as the `refreshGuard` argument of `AccountSessionManager`.
- `AccountSecretStoreSelector(ISecretStore osStore, ISecretStore fileStore, bool hasStoredSession, bool allowFileSecretStore, ILogger<AccountSecretStoreSelector>? = null) : ISecretStore, IDisposable`: `Mode` (`AccountSecretStoreMode { NotAsked, OperatingSystem, MemoryOnly, OwnerOnlyFile }`), `HasStoredSession`, `AllowFileSecretStore`, `event Changed` (the head stores the two settings then), `RememberWithoutSystemProtectionAsync(ct)` (the consent action: moves the in-memory secrets into the file store and sets the flag), constant `DeviceKeyPrefix = "autonogrammer.dispatch.device-key"`.
- `AccountSettingsKeys.HasStoredSession = "account.hasStoredSession"`, `AccountSettingsKeys.AllowFileSecretStore = "account.allowFileSecretStore"`.
- Log EventIds 3070 to 3087 (`DesktopLogMessages`, internal), no value in any line.

Selector behaviour: with `hasStoredSession` false and no write yet, Get/List/Remove return null/empty/false with no store call (`NotAsked`). A write goes to the system store (`OperatingSystem`, `HasStoredSession` true). `Unavailable`, `Denied` and `Locked` on any call switch to `MemoryOnly`: reads return what memory holds, every write keeps the secret in memory and throws `SecretStoreException` with that failure, which the manager shows as `SessionNotRemembered` and which stays set on every later write until consent. `Corrupt` and `Other` propagate and are not a degrade. A name starting `DeviceKeyPrefix` always goes to the system store, never to memory or the file, does not change the mode or `HasStoredSession`, and fails with the system store's own failure, which Dispatch reports as `KeyStoreUnavailable`.

## Deviations and decisions

1. [Rule 3 - analyzer] Meziantou MA0048 (one top-level type per file) added `LoopbackResult.cs`, `LoopbackOutcome.cs` and `AccountSecretStoreMode.cs` beyond the plan's `files_modified` (all inside the owned project), plus `tests/.../PkceSignInFlowTests.cs` (the flow tests are a separate class; the plan names `LoopbackListenerTests.cs` for the listener rows only).
2. [Rule 1 - interpretation] Plan text says each broken limit "yields LinkInvalidOrExpired". Implemented as: a stray request (wrong Host, nonce, method, size, missing code) is answered with a fixed page and counted in `RejectedRequests`, but does NOT end the wait (a local probe must not be able to cancel a sign-in); the error callback on the right nonce and the 10 minute expiry end the wait with `LinkInvalidOrExpired`. The real callback still works after any number of stray requests (tested).
3. [Rule 1] The first flow version closed the listener before every `SendCodeAsync`; the coordinator treats the same address as a resend that reuses the nonce, so that would have killed the live link. The flow now closes the old listener only when a new attempt started or the new attempt has no link.
4. [Rule 3 - analyzer] VSTHRD003 flags awaiting or returning a task field; `Result` and the accept-loop wait go through `Task.WaitAsync(CancellationToken.None)` (a call result) so no pragma or SUP row was needed. CA2000 against CA1849 forced the lock file stream to be opened in a synchronous helper that hands it to the `Handle` type. No suppression was added anywhere.
5. [Rule 3] The raw-socket fake had to decode `Transfer-Encoding: chunked` request bodies: `JsonContent` has no known length, so `HttpClient` sends it chunked.
6. `FakeGoTrueHost` cannot share state with the in-process `FakeGoTrueHandler` of `Accounts.Tests` (internal to another test project, no csproj edit allowed); it re-implements the six routes with the same semantics (OTP codes, S256 check, refresh rotation with a reuse interval on the supplied clock, scripted errors, dropped response, held refresh). `ManualTimeProvider` is a per-project copy as planned.
7. Relocked with `ag-dotnet restore <project> --force-evaluate` (each lock changed only by the Logging.Abstractions edge): `src/Autonogrammer.Accounts.Desktop`, `tests/Autonogrammer.Accounts.Desktop.Tests`, `apps/Autonogrammer.Native`, `apps/Autonogrammer.Web`, `src/Autonogrammer.Dispatch.Desktop`, `tests/Autonogrammer.ArchitectureTests`, `tests/Autonogrammer.Dispatch.Desktop.Tests`, `tests/Autonogrammer.Native.Tests`, `tests/Autonogrammer.Web.Tests`. The Native lock diff is one line (lead-66 had no uncommitted change in it), committed in `1c0bea09`.
8. Not done because outside this plan: CHANGELOG entry and `docs/architecture.md` row for the new project (the phase checklist assigns them to the closing unit).

## Known limits / hand-offs

- After the consent action the manager still shows `SessionNotRemembered` until its next store write (a refresh or sign-in), because the notice is recomputed on writes [INFERENCE from `AccountSessionManager` lines 576-581 and 226; not exercised]. Plan 67-06 should trigger a write or accept the short delay. The selector itself has moved the secret into the file at consent time.
- The lock was exercised on Linux only (flock semantics of `FileShare.None`); Windows and macOS are untested. Two real processes were not used: a second `FileStream` with `FileShare.None` in the same process stands in for the other process, which is the same kernel mechanism.
- Port 0 binding and `127.0.0.1:*` matching of the real Supabase redirect allow-list remain the ADR-0030 [INFERENCE] items; nothing here contacts the real service.
- A stalled or abusive connection holds one of eight connection slots for at most five seconds, then closes; the drain after a rejection reads at most 64 KiB.

## Not run

`tests/Autonogrammer.ArchitectureTests` (blocked by `GateParityTests.cs`, phase 68-06, see Results); the `--locked-mode` solution restore; any real keyring, Windows or macOS run.
