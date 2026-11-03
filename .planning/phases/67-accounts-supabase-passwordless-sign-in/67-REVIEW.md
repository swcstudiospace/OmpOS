---
phase: 67-accounts-supabase-passwordless-sign-in
reviewed: 2026-10-29
depth: deep (static; nothing was built or run)
status: issues_found
sections:
  - "Stage A: plans 67-01 to 67-03"   # present
  - "Stage B: plan 67-04"
findings_stage_a:
  blocker: 0
  high: 3
  medium: 9
  low: 13
  total: 25
findings_stage_b:
  blocker: 0
  high: 0
  medium: 2
  low: 3
  total: 5
open_after_fix_passes:   # counted after fix passes A1 and A2 and the Stage B review
  blocker: 0
  high: 2                # H-01 parts 2 and 3 (locked decision); H-02 in-process refusal (owner decision)
  medium: 9              # Stage A: M-02, M-04 (RS256 part), M-05 to M-09; Stage B: B-01, B-02
  low: 16                # Stage A: L-01 to L-13; Stage B: B-03 to B-05
  total: 27
---

# Phase 67 code review

Status: **issues_found**. Stage A: no blocker; H-03 fixed; H-02 fixed except the in-process refusal (owner decision); H-01 parts 2 and 3 open (locked decision). Stage B (plan 67-04): no blocker and no HIGH; two MEDIUM and three LOW recorded open. Fix pass A2 changed no code: the H-02 and H-03 fixes and their regression tests were already committed in `4a1cffd2`.

Method: static read of every file in scope, cross-checked against 67-CONTEXT D-01..D-64, the three PLANs and SUMMARYs. Nothing was compiled or executed, so every behavioural claim about a running system is marked `[INFERENCE]`. Line numbers are from the worktree at the time of review.

Scope reviewed:
- 67-01: `src/Autonogrammer.Accounts/*` (GoTrueClient, GoTrueOptions, AccountSessionManager, SignInCoordinator, AuthServerCodes, Pkce, JSON contexts), `src/Autonogrammer.Contracts/Accounts/*`, `tests/Autonogrammer.Accounts.Tests/*`, `docs/runbooks/accounts.md`.
- 67-02: `src/Autonogrammer.Harness.Secrets.Os/*`, `SecretStoreException`/`SecretStoreFailure`, `tests/Autonogrammer.Harness.Secrets.Os.Tests/*`.
- 67-03: `services/Autonogrammer.Api/Security/JwtBearerSetup.cs`, `Options/ApiAuth{Options,ClientOptions,OptionsValidator}.cs`, `OpenApi/*`, `Services/WorkspaceSettings/WorkspaceAccess.cs`, and the tests of commit 23c7c2b2.

Positives worth keeping (verified in code): store-write-before-publish rotation (`AccountSessionManager.cs:574-581`); epoch fence against a refresh that outlives a sign-out for the *store/state* (`:560`); one shared flight bound to the manager lifetime, caller cancellation does not cancel it (`:253`, `:372`); all log lines pass only enums/fixed flags and a 16-code vocabulary (`AuthServerCodes.cs`); native strings are hand-marshalled and zeroed (`NativeUtf8.cs`, `KeychainNativeApi.cs:234-243`); no `AllowUnsafeBlocks`; every DllImport carries the safe-directories attribute; GError message text is dropped (`LibsecretNativeApi.cs:189-213`); libsecret struct layout of `SecretSchema`/`GError`/`GList` and the flag values (`ALL=2`, `UNLOCK=4`, `G_IO_ERROR_CANCELLED=19`) match the libsecret/GLib headers as I know them.

## Stage A: plans 67-01 to 67-03

### HIGH

**H-01 `AccountSessionManager.cs:425` (and `GoTrueClient.cs:20`): a lost refresh response ends the session instead of being retried inside the reuse interval [INFERENCE on GoTrue semantics].**
`bool lostResponseRetried = startup;` disables the 1 s lost-response retry of D-19 for the startup refresh, which D-19 does not exempt. After the 5 s startup budget the next attempt is the offline loop, 96-144 s later (`:635-659`), with the same refresh token. The reuse interval is 10 s (the fake models it at `FakeGoTrueHandler.cs:26,470`; report 05). If the service did process the first request, the retry arrives outside the interval, gets `refresh_token_already_used`, which is in `IsDefinitive` (`:323`) and ends in `DiscardAsync(... SessionEnded)` (`:507`): a forced re-login caused by a slow network. The same happens on demand: `RequestTimeout` is 15 s (`GoTrueClient.cs:20`), so after a timeout the single retry (`:443`, +1 s = at least 16 s after the send) is already outside the 10 s interval.
Fix: (1) remove the startup exemption: allow the one 1 s retry also at startup (give it its own 5 s budget or share the first); (2) use a refresh-specific timeout below the reuse interval (for example 8 s for `Endpoint.Refresh`), so that "lost response" attempts are always retried while the old token is still reusable; (3) when the manager itself caused the ambiguity (timeout or transport error on the previous attempt) and the retry answers `refresh_token_already_used`, treat it as transient once (Offline) before ending the session, and add a test row for each ordering (timeout then retry at +1 s inside the interval; startup timeout then retry).

**H-02 `AccountSessionManager.cs:288` and `:608`: `TryRemoveStoredAsync` failure is swallowed, so a "signed out" device silently signs in again at the next launch.**
`SignOutAsync` clears memory, tries to remove the secret, ignores a `SecretStoreException` (only logs, `:779-790`), publishes `SignedOut` with no notice, and `_knownStored` stays set. If the keyring is `Locked`/`Denied` (the very cases 67-02 now classifies) and the server revoke later fails or is skipped (offline, `ConfigUnavailable`), the still-valid refresh token remains on disk and the next start restores the session. This breaks D-22 ("clears local state and stored secret first") with no signal to the person. `DiscardAsync` has the same blind spot, but there the token is already dead.
Fix: make `TryRemoveStoredAsync` return bool; on failure retry once, and if it still fails raise a notice the head can show (new `AccountNotices.SignOutNotCleared`, "this device may sign in again at the next start; remove it in system settings"), and let `SignOutAsync` return the failure so a head can offer a retry. Remember the failed state in memory and refuse to adopt the stored session at the next `StartAsync` when the previous removal failed in the same process. Add a test with `RecordingSecretStore.FailRemove`.

**H-03 `AccountSessionManager.cs:497` and `:526`: `_live` is written outside `_mutation` and without the epoch check, so a refresh in flight can resurrect a signed-out session in memory.**
`AttemptHeldAsync` checks the epoch at `:466`, then awaits the store re-read (`:471`/`:518`, `:493`) and afterwards assigns `_live = adopted` / `_live = live with {...}` unconditionally. A `SignOutAsync` or `DiscardAsync` that completes between the check and the assignment (it nulls `_live`, bumps the epoch, removes the store, publishes `SignedOut`) is then undone: `_live` is non-null again, still carrying the old `AccessToken`/`ExpiresAt`. `State` says `SignedOut` but `GetAccessTokenAsync` (`:242-251`, `IsFresh`) hands out the old access token and `CurrentAccessToken` is non-null. `ApplyRefreshedAsync` is correct (`:560`); these two writers are not. The existing test (`AccountSessionManagerTests.cs:525-541`) only covers the response-after-sign-out ordering, not this one.
Fix: route both adoptions through one `AdoptAsync(epoch, ...)` that takes `_mutation`, rechecks `Interlocked.Read(ref _epoch) == epoch`, and only then writes `_live`/`_knownStored`; also use `Volatile.Write` for every `_live` write (all readers use `Volatile.Read`). Add a test with a store whose `GetAsync` blocks (the `BeforeSet` hook style in `RecordingSecretStore`) while sign-out runs, then assert `CurrentAccessToken` is null and `GetAccessTokenAsync` fails.

### MEDIUM

**M-01 `AccountSessionManager.cs:374-388` (and `:404`): an exception inside the flight leaves connectivity `Refreshing` forever with no retry.**
`RunFlightAsync` converts any unexpected exception (client provider throwing, `IRefreshGuard.AcquireAsync` failing on an unwritable lock file, `ObjectDisposedException`) into `AuthErrorKind.Unexpected`, but only the `Step.Transient` path calls `SetConnectivity(Offline)` + `EnsureOfflineRetryLoop()`. After a failed startup refresh the head shows "refreshing" for the life of the process and nothing schedules another attempt except a caller of `GetAccessTokenAsync`.
Fix: in both catch blocks of `RunFlightAsync` call `SetConnectivity(AccountConnectivity.Offline)` and `EnsureOfflineRetryLoop()` (the cancellation case only when the manager is not disposed). Test: a provider/guard that throws once.

**M-02 `GoTrueClient.cs:264-299`: refresh answers that carry no `error_code` are `Unexpected`, hence "transient", hence never a sign-out.**
`Classify` maps only known codes; a 400/401/403 on `Endpoint.Refresh` without `error_code` (older GoTrue `{"error":"invalid_grant"...}`, a proxy or CDN page, `bad_jwt`) becomes `Unexpected`, `IsDefinitive` is false (`AccountSessionManager.cs:323`), and the session stays `SignedIn`/`Offline`, retrying for 15 min and then on every call, failing every API request while never telling the person. The theory at `GoTrueClientTests.cs:359-366` has no row for it.
Fix: for `Endpoint.Refresh`, treat 400/401/403/404 without a mappable code as `SessionEnded` after one re-read (or at least after N consecutive occurrences); add rows `token:refresh 400 null`, `401 null`, and an HTML body.

**M-03 `GoTrueOptions.cs:132-137` and `ApiAuthOptionsValidator.cs:132-143`: the secret-key refusal is bypassed by leading whitespace.**
Both validators test `StartsWith("sb_secret_")` / `IsServiceRoleJwt` on the raw string; `GoTrueOptions` then stores `publishableKey.Trim()` (`:137`). `" sb_secret_..."` or `"\tey...service_role..."` passes the check and is then sent as `apikey` (and, once 67-05 serves it, to every anonymous caller of `/auth/config`). Misconfiguration only, but it is the stated safety net of D-16/D-46.
Fix: trim first in both places (`key = key.Trim()`), compare the prefix case-insensitively, and add `InlineData(" sb_secret_x")` rows to `GoTrueClientTests` and `ApiAuthOptionsValidatorTests`. Also dedupe the two `IsServiceRoleJwt` copies into one tested helper if the layering allows, or add a cross-reference test.

**M-04 `ApiAuthBoundaryTests.cs:60,74` + `JwtTestHost.cs:115-118`: the "hs256" row cannot prove the `ValidAlgorithms` pin.**
The HS256 token is signed with an unrelated HMAC key published nowhere (same `kid` as the EC key). It is rejected for signature/key-type mismatch even if `ValidAlgorithms` (`JwtBearerSetup.cs:48`) were deleted, so the assertion cannot fail on the thing it names. There is also no row for `alg: none` (`RequireSignedTokens`, `:49`), no RS256 row, and no positive control for `Api:Auth:Algorithms = [ES256, RS256]`.
Fix: publish an RSA key in the static metadata and issue an RS256 token signed by it: 401 with the default configuration, 200 with `Api:Auth:Algorithms:0=ES256`, `:1=RS256` (this also proves the option is read). Add an `alg:none` unsigned token row. Keep the HS256 row but sign it with a key the metadata does publish (HMAC over the same `kid` via `SymmetricSecurityKey`) so removal of the pin turns it green.

**M-05 `ApiAuthBoundaryTests.cs:108-125` / `JwtBearerSetup.cs:44`: the rotated-`kid` test models a refresh the real configuration manager rate-limits [INFERENCE], and a 401 during the window cascades into a local sign-out in the heads.**
`RotatingMetadata.RequestRefresh` (`JwtTestHost.cs:215`) refreshes immediately. The real `ConfigurationManager<OpenIdConnectConfiguration>` that `Authority` creates gates `RequestRefresh` by its `RefreshInterval` (default five minutes) [INFERENCE, library behaviour not exercised here]. After a Supabase signing-key rotation, tokens signed with the new key can therefore be answered 401 for several minutes, and D-42 makes an API 401 after a valid sign-in `AccountRejected` ("signs out locally"). CONTEXT D-27 lists this as "[INFERENCE] proven by a rotated-key test"; the test proves only that JwtBearer calls `RequestRefresh`.
Fix: (a) set `options.RefreshInterval`/`AutomaticRefreshInterval` explicitly in `JwtBearerSetup` (for example `RefreshInterval` 30 s through a configured `ConfigurationManager`, `AutomaticRefreshInterval` 10 min per report 05 line 182) and test with the real manager against a loopback JWKS; (b) tell the 67-05/06/07 authors to sign the person out only after one forced `GetAccessTokenAsync` refresh followed by a second 401, never on the first 401.

**M-06 `LibsecretNativeApi.cs:208-211`: every `G_IO_ERROR_CANCELLED` becomes `OperationCanceledException(cancellationToken)` even when the caller's token was not cancelled.**
GIO can report that code when a prompt is dismissed [INFERENCE: libsecret behaviour not exercised; the summary itself calls the prompt paths unexercised]. The OCE then escapes `TryWriteStoredAsync`/`ReadStoredAsync`, which catch only `SecretStoreException`: a dismissed unlock prompt during `SignInAsync` throws out of the sign-in after the one-time code was already consumed, and out of `StartAsync`. D-26 expects `Locked` after dismissal.
Fix: `if (domain == Gio && code == GioErrorCancelled) { if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken); throw LinuxLibsecretStore.Failed(Secret, SecretIsLocked) /* or a new Dismissed mapping to Locked */; }`. Add a unit-testable seam (`ThrowIfError` takes domain/code) so the row exists.

**M-07 `BlockingSecretStore.cs:92`, `KeychainNativeApi.cs:22-82`, `WindowsDpapiStore.cs:259-288`: Keychain (and DPAPI file I/O) ignore the caller's token once started.**
`RunAsync` awaits the `StartNew` task without `WaitAsync(cancellationToken)`; only libsecret receives the token (GCancellable). A Keychain authorization sheet that nobody answers blocks `StartAsync(ct)`/`SignInAsync(ct)` indefinitely on macOS, and the test `ACancelledTokenCancelsTheTask` only covers a token cancelled before the call. `WindowsDpapiStore` also runs `Encode`/`Protect`/`CreateDirectory` synchronously on the caller's thread before its first await (`:180-192`).
Fix: `await task.WaitAsync(cancellationToken)` (the native call finishes in the background, result discarded and zeroed), make DPAPI run its body through `Task.Run`, and add a macOS-fake test with a never-completing `IKeychainApi.Copy`.

**M-08 `AccountSessionManager.cs:804-819`: `StateChanged` is raised outside the lock, so concurrent mutators can deliver events out of order.**
`Mutate` updates `_state` under `_stateGate` but invokes the handlers after releasing it; `SetConnectivity` and the `SignOutNotConfirmed` mutation run outside `_mutation`, so a late `Offline` from a flight can be delivered after a newer `SignedOut`, leaving a view model on a stale state. `StateChangesAreRaisedInOrder` (`AccountSessionManagerTests.cs:544`) is sequential.
Fix: carry a monotonically increasing sequence in `AccountStateChangedEventArgs` and queue raises under one lock (or raise under a dedicated `_raiseGate` taken before `_stateGate`), and add a test with two threads mutating.

**M-09 `ApiAuthOptionsValidator.cs:130-151` vs `GoTrueOptions.cs:118-125`: the server accepts a client block the heads then refuse.**
The API validator rejects non-https and userinfo URLs only; `GoTrueOptions.CreateCore` also rejects query and fragment, and requires both URL and key. A `ProjectUrl` with `?x=y`, or a block with only one of ProjectUrl/PublishableKey (`HasAny` is true), starts cleanly (D-46: failures should name the key) and surfaces later as a `ConfigUnavailable` on every head.
Fix: add rows: query/fragment refused; `HasAny` and exactly one of the two missing refused; `PublishableKey` trimmed.

### LOW

- **L-01 `AccountSessionManager.cs:316-321`:** `Dispose` cancels, disposes `_lifetime` and `_mutation` without waiting for flights; a later `GetAccessTokenAsync`/`SignOutAsync` throws `ObjectDisposedException` from `_mutation.WaitAsync` (or `_lifetime.Token`). Track a `_disposed` flag, return `SessionEnded`/no-op after dispose, and await the running flight with a short bound.
- **L-02 `AccountSessionManager.cs:159-166`:** `_started` is set before the store is read; a cancelled or failed first `StartAsync` can never be retried. Set it after success, or reset it in a `catch`.
- **L-03 `AccountSessionManager.cs:617-659`:** the offline loop ignores `AuthFailure.RetryAfter` (429), and `EnsureOfflineRetryLoop` can skip scheduling when the previous loop decided to return but has not completed (`IsCompleted` false). Honour `RetryAfter` as a minimum delay; make loop completion and scheduling share `_flightGate`.
- **L-04 `AccountSessionManager.cs:728-760`:** a stored secret that fails the strict shape (for example `schema:2` written by a newer build, or an extra member) is deleted (`:176`). Downgrading the app therefore signs the person out of the newer build too. Only delete on `JsonException`/missing members; leave a higher `schema` untouched and report `ServiceChanged`-like "stored by a newer version".
- **L-05 `SignInCoordinator.cs:101-117`:** two concurrent `SendCodeAsync` calls (double click) both pass the cooldown check before either records `_attempt`, and send two emails against the shared 2/h quota. Serialise with a `SemaphoreSlim` around check + send + record.
- **L-06 `SignInCoordinator.cs:279-289`:** `linkRedirect(nonce)` hands the head a nonce (it binds a loopback listener) but there is no hook to release it when the send fails, the address changes, or `Reset()` runs; a failed `SendEmailOtpAsync` (`:117-122`) leaves the listener orphaned. Return an `IDisposable`/callback from `linkRedirect` and dispose it in those three paths (relevant to 67-04).
- **L-07 `SignInCoordinator.cs:179-184`:** the wrong-code budget is a snapshot read; concurrent `VerifyCodeAsync` calls can exceed five requests. Count under `_gate` before sending.
- **L-08 `WindowsDpapiStore.cs:237-257`:** `ListNamesAsync` decrypts every secret file to learn names (values land in unzeroed `SecretString`s) and one corrupt file fails the whole list with `Corrupt`. Keep a name index in clear-text hash-named files or skip and log unreadable entries. Also: `File.Move(overwrite)` over a file another process is reading can raise `IOException` that is classified `Other` (`:278-281`), not retried.
- **L-09 `LibsecretNativeMethods.cs:17-31`, `KeychainNativeMethods.cs`:** `DefaultDllImportSearchPaths(SafeDirectories)` is a Windows search-path concept; it gives no protection for the relative sonames `libsecret-1.so.0` etc. on Linux (`dlopen` default search incl. `LD_LIBRARY_PATH`). Not a defect against D-25 as written, but do not count it as hijack hardening; macOS uses absolute framework paths (good).
- **L-10 67-02 coverage:** Keychain and DPAPI stores were compiled but never run on their platforms (SUMMARY says so); `OwnerOnlyFileSecretStore` (unchanged) throws untyped `SecretStoreException` (`Failure == Other`), so the 67-04 selector cannot tell `Denied` from `Corrupt` on the fallback.
- **L-11 tests (`AccountSessionManagerTests.cs`):** `:525-541` discards the pending `GetAccessTokenAsync` result (`_ =`) and never asserts it failed or that `CurrentAccessToken` is null; `:41-59` asserts the stored JSON does not contain the *old* refresh token but never that it contains the *new* one; `:206-234` relies on `Task.Delay(5)` pumping and wide bounds (`ShouldBeLessThan(14)`), a flake risk; `DefaultJitter` (`:327`) is never exercised (the harness replaces it). No row for: store removal failing, flight exception, a second sign-in during a flight, `Dispose` during a flight.
- **L-12 `LogHygieneTests.cs:58`:** the link `auth_code` passed to `CompleteLinkAsync` is not added to `secrets`, and at 24 characters it is under the 30-character opaque-string regex, so a leak of it would pass. Add it to `secrets`.
- **L-13 `67-03-SUMMARY.md`, `docs/api/openapi.json`:** the committed document was generated in a sandbox copy and `GuardTests.OpenApi_ServedEqualsCommitted` was satisfied against that sandbox's own regeneration; re-run the real-tree guard once the lock-file state allows (the 68-03 regeneration commit 25d75784 landed later and may already have fixed it).

### Requirement and decision conformance (spot checks)

| Item | Result |
| --- | --- |
| D-13/D-15 six calls, closed taxonomy, no server text | Met (`GoTrueClient.cs`, `AuthServerCodes.cs`); gap M-02 |
| D-16 key refusal | Met except whitespace bypass (M-03) |
| D-17 `TimeProvider`, 15 s timeout, no retry on otp/verify/pkce, log ids 3000-3099 | Met; EventIds 3000-3036 (Accounts), 3050-3053 (Os) do not collide |
| D-18 single flight, margin `max(60 s, 20 %)` | Met (`:333-342`, `:344-365`) |
| D-19 store before publish; retry once after 1 s then every 2 min, 15 min | Store order met; 1 s retry skipped at startup (H-01) |
| D-20 guard, re-read, adopt, definitive failures, no exception | Met; adoption race H-03 |
| D-21 startup never waits on network, issuer mismatch discard | Met (`:159-200`, `:414-423`) |
| D-22 local first, then `POST /logout?scope=`, `SignOutNotConfirmed` | Met except store-removal failure (H-02) |
| D-25 additive `SecretStoreFailure`, value-free messages, no unsafe | Met |
| D-27 ES256 pin, lifetime, skew 30 s, `is_anonymous`, `role`, `ValidIssuer` required | Met in code; pin test weak (M-04) |
| D-30/D-50 empty owners deny except OwnerToken/dev principal | Met (`WorkspaceAccess.cs`, rows in `WorkspaceSettingsAccessTests.cs`) |
| D-33/D-51 metadata-driven OpenAPI security, anonymous list locked by test | Met (`SecuritySchemeTransformer.cs`, `OpenApiSecurityTests.cs`) |
| D-42 error taxonomy of 16 kinds | Met (`AuthErrorKind.cs`, `TheErrorCatalogueIsClosed...`) |

## Fix pass A1

Scope: all HIGH findings, security F-01 and F-02, and the cheap one-file MEDIUM findings. Tests were written first for every behaviour fix. **Nothing was built or run in this pass**: free disk was 1.9 to 3.7 GB, under the 5 GB floor of the safety contract, so the code was checked by reading only. The main agent must run the checks listed at the end before treating any row below as verified. Commits: `4a1cffd2` (manager), `f0dcc251` (key refusal), `8e8cb642` (boundary test).

| Finding | Result | Commit | Notes |
| --- | --- | --- | --- |
| H-01 part 1: startup retry | fixed | `4a1cffd2` | The startup exemption `lostResponseRetried = startup` is removed: D-19 does not exempt startup and D-21 only bounds the first request. Each startup request keeps its own 5 s budget, so a startup refresh may now last up to about 11 s (5 s, 1 s, 5 s) with connectivity `Refreshing`; the retry at +6 s is inside the 10 s reuse interval. Tests: `AStartupRefreshWhoseResponseWasLostIsRetriedOnceAfterOneSecondInsideTheReuseInterval` (new), `FiveSecondBudgetsEndAStartupRefreshAsOfflineNotSignedOutAfterTheOneSecondRetry` and `OfflineStartIsASignedInNoticeAndTheRetryLoopBringsItBackOnline` (updated to the new request count: two failures, then the loop). |
| H-01 part 2: refresh timeout below 10 s | not fixed (locked decision) | none | D-17 fixes the per-request timeout at 15 s. After an on-demand 15 s timeout the single retry at about 16 s is still outside the 10 s reuse interval [INFERENCE on GoTrue semantics]. Needs a decision amendment (for example a manager-level refresh budget of 8 s next to the D-21 startup budget); not done without it. |
| H-01 part 3: treat `refresh_token_already_used` as transient once after a self-caused ambiguity | not fixed (locked decision) | none | D-20 lists `refresh_token_already_used` as definitive: re-read once, else clear and sign out. Same amendment as part 2 is needed. |
| H-02 / F-02 sign-out ignores a failed secret removal | fixed | `4a1cffd2` | `TryRemoveStoredAsync` returns bool and retries once. New flag `AccountNotices.SignOutNotCleared`. `SignOutAsync` never throws; it sets the notice when the removal failed and the service did not confirm the revoke (a confirmed revoke kills the stored token, so no notice), or when there was no live session to revoke. The return value of `SignOutAsync` still reports the revoke result (D-42 has no kind for this). `RecordingSecretStore.FailRemovals(int)` added. Tests: failed removal plus unconfirmed revoke (notice, two remove attempts, secret still stored, no throw), failed removal plus confirmed revoke (no notice), one-off failure retried (no notice). Not done: refusing the stored session at the next start after a failed removal (`StartAsync` runs once per process, and a new process has no memory of it; a tombstone would be a new stored shape, D-23). `DiscardAsync` still ignores a failed removal on purpose (the token is already dead). The heads (67-04 to 67-07) must show `SignOutNotCleared` in the copy catalogue. |
| H-03 `_live` outside the mutex and epoch | fixed | `4a1cffd2` | Both adoptions go through `AdoptAsync(epoch, ...)`, which takes `_mutation`, rechecks the epoch and the live session, and only then writes `_live`/`_knownStored`; a stale adoption ends the refresh with `SessionEnded` and makes no request. Every `_live` write now uses `Volatile.Write`. Test: `AStoredTokenReadBeforeASignOutIsNeverAdoptedAfterItAndTheOldAccessTokenIsNotHandedOut` (a store read held with `RecordingSecretStore.HoldGets` while sign-out runs). |
| F-01 / M-03 whitespace bypass of the key refusal | fixed | `f0dcc251` | Both `GoTrueOptions.CreateCore` and `ApiAuthOptionsValidator.ValidateClient` trim first and compare `sb_secret_` case-insensitively; the client JWT decode now accepts padded base64url (secondary finding of F-01). Rows: leading space, tab and newline, upper case, padded and whitespace-wrapped service-role JWT in `GoTrueClientTests` and `ApiAuthOptionsValidatorTests`. Not done: dedupe of the two `IsServiceRoleJwt` copies (layering: Api does not reference Accounts). |
| M-01 flight exception leaves `Refreshing` | fixed | `4a1cffd2` | Both catch blocks of `RunFlightAsync` call `ScheduleRetryAfterFailure` (offline plus retry loop, skipped once the manager is stopped). Test: `AnExceptionInsideTheRefreshLeavesTheSessionOfflineAndRetriesInsteadOfStayingRefreshing` (client provider throws once). |
| M-02 refresh answers without `error_code` | not fixed (locked decision) | none | D-20 enumerates the definitive codes; treating code-less 4xx as `SessionEnded` needs an amendment. |
| M-04 hs256 row cannot fail | fixed | `8e8cb642` | The test metadata now publishes an HMAC key under its own `kid`; the hs256 token is signed with it, so only the `ValidAlgorithms` pin can reject it [INFERENCE: not run, and not mutation-tested by deleting the pin]. Added an `alg: none` row. Not done: RS256 row and the `Algorithms = [ES256, RS256]` positive control (needs an RSA key in the metadata and a larger change). |
| M-05, M-06, M-07, M-08, M-09 | not fixed (outside this pass) | none | Not one-file fixes or not requested. |
| L-01 to L-13 | not fixed | none | Not requested. |

Checks the main agent must run (nothing below was run here): `ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"`; `ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*ApiAuthOptionsValidator*"` and `--filter-class "*ApiAuthBoundary*"`. Expected to need a look on first run: the three updated startup tests rely on `ManualTimeProvider` stepping through the new 1 s retry, and the `hs256`/`alg-none` rows depend on JwtBearer rejecting by algorithm before signature work (if the `hs256` row returns 200, the `ValidAlgorithms` pin is broken, which is the point of the row).

## Stage B: plan 67-04 (Accounts.Desktop)

Scope: `src/Autonogrammer.Accounts.Desktop/*` (LoopbackListener, LoopbackResult, LoopbackOutcome, PkceSignInFlow, CrossProcessFileLock, AccountSecretStoreSelector, AccountSecretStoreMode, AccountSettingsKeys, DesktopLogMessages, AssemblyMarker), plus `apps/Autonogrammer.Native/Composition/DesktopAccountHost.cs` read only for how the stored-session flag is persisted. Static read only; nothing was built, tested or run, so every runtime claim is `[INFERENCE]`.

Verified clean by reading: the listener binds `IPAddress.Loopback` only, requires `Host` to equal `127.0.0.1:<port>`, compares the nonce with `FixedTimeEquals`, answers with `Referrer-Policy: no-referrer`, `Cache-Control: no-store` and a CSP, and logs only fixed reason words; a stalled client holds one of eight slots for at most 5 s; the lock uses `FileShare.None` with an owner-only mode and never deletes the file; a device key name is routed to the system store only and never reaches memory or the file; the PKCE verifier stays in memory.

### HIGH

None.

### MEDIUM

**B-01 `AccountSecretStoreSelector.cs:294-310` and `:361-366` (with `AccountSessionManager.cs:811-813`): a degraded selector abandons a keyring entry it cannot remove, and sign-out then reports the secret cleared.**
A system-store failure on set, get or remove calls `Degrade`, which sets `HasStoredSession` to false and the mode to MemoryOnly. The keyring entry from an earlier run stays, and nothing reads it again because the head persists `false` (`DesktopAccountHost.cs:194`, `:399`, `:422`). In MemoryOnly, `RemoveCoreAsync` removes from memory only and returns false when nothing was in memory, which the `ISecretStore.RemoveAsync` contract defines as "nothing stored". `TryRemoveStoredAsync` ignores that boolean and returns true on its retry. So a sign-out whose server revoke was not confirmed raises no `SignOutNotCleared` notice, and a live refresh token stays in the keyring. No automatic sign-in follows, so the harm is a lingering credential, not the H-02 re-sign-in, and the next sign-in overwrites the name. Fix (owner decision, because it changes the D-26 degrade behaviour): keep an "entry may exist" bit set by `Degrade`; while it is set, MemoryOnly removal retries the system store and, if that still fails, throws the degraded failure so the manager raises `SignOutNotCleared`. Test: system-store remove fails with Locked, then sign-out with an unconfirmed revoke, expecting `SignOutNotConfirmed | SignOutNotCleared`.

**B-02 `LoopbackListener.cs:19-20` and `:363-368`, test `LoopbackListenerTests.cs:75`: a stray request does not end the wait, which differs from plan 67-04 Test 2.**
Plan 67-04 Test 2 says each broken limit "yields LinkInvalidOrExpired". The implementation answers the probe with a fixed page and keeps waiting, so a local probe cannot cancel a sign-in. That is a sound choice and 67-04-SUMMARY deviation 2 discloses it, but the plan's acceptance text is not met as written. Needs owner acceptance or a plan amendment. No code changed.

### LOW

**B-03 `PkceSignInFlow.cs:82` and `:135-140`: a listener can outlive a failed send, and a call after dispose throws.**
If `_coordinator.SendCodeAsync` throws (for example on cancellation) after `TryStart` bound a listener, `started` is never stored or disposed, so the port stays open until the listener's ten-minute timer ends it. `_lifetime.Token` at line 82 also throws `ObjectDisposedException` after `DisposeAsync` has disposed `_lifetime`. Fix: dispose `started` in a catch around the coordinator call, and check a disposed flag before using `_lifetime`.

**B-04 `AccountSecretStoreSelector.cs:172-206`: consent in OperatingSystem mode leaves the keyring session behind.**
`RememberWithoutSystemProtectionAsync` publishes `OwnerOnlyFile` without moving a keyring session and keeps `HasStoredSession` true, so the next start reads an empty file. The head offers consent from the `SessionNotRemembered` notice, which the selector only produces in MemoryOnly, so the UI path is not reachable; the public method is. Fix: return early unless the mode is MemoryOnly.

**B-05 `DesktopLogMessages.cs:56-57` and `:110`, `AssemblyMarker.cs:5`: an unused log method and stale text.**
`DeviceKeyRefused` is never called, so the selector never logs a device-key refusal and EventId 3086 is unused. The header comment says EventIds 3070 to 3089, but the highest used is 3087. `AssemblyMarker` still says "Behaviour arrives in Phase 67." Fix: delete the unused method and update the marker summary.

Checks the main agent must run (none of them was run in this pass): `ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"` and `ag-dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"`.

## Fix pass A2 (H-02 and H-03 re-check, Stage B review)

Scope: H-02 and H-03 as named in this review, and the Stage B review. Result: no code change in this pass. Both fixes are on HEAD in `4a1cffd2` (an ancestor of HEAD; the working tree was clean when checked), and their regression tests are already in the tree. Verified by reading only.

| Finding | Result | Evidence |
| --- | --- | --- |
| H-02 failed secret removal ignored by sign-out | fixed, residual open | `TryRemoveStoredAsync` returns a bool after one retry (`AccountSessionManager.cs:804-822`); `SignOutAsync` adds `SignOutNotCleared` when both the removal and the revoke failed (`:298-313`). Tests: `AStoreThatCannotRemoveTheSecretWithAnUnconfirmedRevokeRaisesTheNoticeAfterOneRetryAndNeverThrows` (`AccountSessionManagerTests.cs:608`), `AStoreThatCannotRemoveTheSecretWithAConfirmedRevokeNeedsNoNotice` (`:627`), `AOneOffRemovalFailureIsRetriedAndLeavesNoClearNotice` (`:641`). Residual: refusing to adopt the stored session at the next `StartAsync` is not implemented. `StartAsync` runs once per manager (`:162`) and a new process has no memory of an earlier failure, so an in-process form cannot cover a restart. Covering a restart needs a persisted tombstone, a new stored shape that D-23 does not allow. Owner decision. Stage B B-01 is a related gap. |
| H-03 `_live` written outside the mutex and epoch | fixed | Every `_live` write is under `_mutation` and uses `Volatile.Write` (`:187`, `:222`, `:288`, `:545`, `:600`, `:631`). `AdoptAsync` rechecks the epoch and the live session before writing (`:532-558`). Test `AStoredTokenReadBeforeASignOutIsNeverAdoptedAfterItAndTheOldAccessTokenIsNotHandedOut` (`AccountSessionManagerTests.cs:585`) holds a store read with `HoldGets` while sign-out runs. |

Frontmatter `open_after_fix_passes` counts the H-02 residual as open high.
