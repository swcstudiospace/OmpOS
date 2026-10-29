---
phase: 67-accounts-supabase-passwordless-sign-in
reviewed: 2026-10-29
depth: deep (static; nothing was built or run)
status: issues_found
sections:
  - "Stage A: plans 67-01 to 67-03"   # present
  - "Stage B: plan 67-04"            # pending, added by a later pass
findings_stage_a:
  blocker: 0
  high: 3
  medium: 9
  low: 13
  total: 25
---

# Phase 67 code review

Status: **issues_found** for Stage A (no blocker, 3 high). Stage B (plan 67-04, Accounts.Desktop) is not reviewed yet.

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

### Not reviewed here

Plan 67-04 (`Autonogrammer.Accounts.Desktop`: file-lock `IRefreshGuard`, loopback listener, selector) and the consumers of `IRefreshGuard`/`linkRedirect` (findings H-03, L-06 and M-05(b) hand requirements to it). Nothing was built, tested or run for this report.
