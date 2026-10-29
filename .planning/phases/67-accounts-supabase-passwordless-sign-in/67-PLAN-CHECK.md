## ISSUES FOUND

**Phase:** 67-accounts-supabase-passwordless-sign-in (revised on the integration base, D-45..D-64)
**Plans checked:** 7 (67-01 .. 67-07), round 2 against the current tree (HEAD 6589db88; the shared worktree also carries another session's uncommitted source edits, see I7)
**Issues:** 1 blocker, 6 warning(s), 7 info

All 24 requirement ids AG-67-1..24 appear in a plan's `requirements`; every `<automated>` has a `<fails_when>`; all filters use an allowed form (`--filter-not-trait "Category=Live"` alone, or `-- --filter-class "*X*"` alone, or no filter for ArchitectureTests); waves are consistent (01/02/03 wave 1, 04 wave 2 deps 01+02, 05 wave 3 deps 01+03 (accepted), 06 wave 3 deps 01+04, 07 wave 4 deps 01+05+06); declared files_modified overlaps are only the sequenced ones (03->05: openapi.json, deploy/railway/README.md; 05->07: BearerTokenUnauthorizedTests.cs, declared at 05:228); every non-NEW path in files_modified is git-tracked.

### Round 1 status
| R1 | Status | Evidence |
|---|---|---|
| B1 logging reference | PARTIAL | Carve-out present in 01:57,92, 02:36, 04:41 and CONTEXT D-62; `Directory.Packages.props:65`, `Sandbox.csproj:18`, `Harness.Acp.csproj:5` verified. Lock scope still open, see W1. |
| B2 Kestrel fake | FIXED | 04:41,76,152 raw `TcpListener`, D-61; test csproj untouched. Residual stale text, see I1; plain-HTTP vs https options, see W2. |
| B3 lock never wired | FIXED | 01:109 `IRefreshGuard` plus deferred provider; 04:92 `CrossProcessFileLock`, test with two managers, AG-67-6 in 04 `requirements`; 06:104 composes it. |
| B4 owners deny breaks tests | FIXED in text, premise WRONG | My R1 evidence was an error: `GET /workspace` is `CatalogEndpoints.WorkspaceAsync` (CatalogEndpoints.cs:258) and never calls `WorkspaceAccess`; see W4. |
| W1 verify targets | FIXED | Second `<automated>` in 01 T4, 05 T4, 06 T3, 07 T2/T3; 05 T1 runs the whole Api project; changelog check at 07 T3. |
| W2 file counts | ACCEPTED | Now 01=24, 03=21, 05=44 (4 tasks), 07=33 files. |
| W3 adapter and features | MOSTLY | 05 T4 `GetAccountAsync`, Demo fixture, `ClientRouteSurface`; 06:104 `DesktopAccountHost.cs`. Residual W5. |
| W4 line numbers | FIXED | `AuthConfigEndpoints.cs` Handle 35-49, no-store 39, AllowAnonymous 28 match 05 T1. |
| W5 GetUserAsync | FIXED | `GetAuthUserAsync` absent from all plans. |
| W6 shared test file | FIXED | 05:228 names it. |
| W7 AccountSessionItem | FIXED | 01:140 and D-64: `{Session: SessionSummary, Surface: string}`; `Contracts/Agents/SessionSummary.cs` exists. |
| W8 deferred provider | FIXED | 01:109 (Func of CancellationToken to ValueTask of GoTrueClient or null), 06:104. |
| W9 render-web-config test | FIXED | `RenderWebConfigScriptTests.cs` in 07 files and T3. |
| INFO 2/4/5 | FIXED | 03:159 deviation note; 01 T4 states no Live row; 07:143 build-web.sh comment only (`scripts/vercel/build-web.sh:44` is the render-web-config call); `FileSettingsStoreTests` row at 06:168. INFO 3 partly (I3). |

### Blockers
**B1. [key_links / AG-67-11, AG-67-12] The Native head never composes the secret-store selector or the consent action.**
- Plan 06 T2 (and 06:168). Required property: the desktop head decides the token store through `AccountSecretStoreSelector` (zero keyring calls on first launch of a Code-only user, memory-only plus `SessionNotRemembered` on OS failure, file store only after the "Remember sign-in on this device" consent), and maintains `account.hasStoredSession`.
- Evidence: plan 04 T3 builds the selector and `AccountSettingsKeys` and tests them in isolation; plan 04 key_links say plan 06 consumes the keys. Plan 06 never names `AccountSecretStoreSelector`, `OsSecretStore`, `OwnerOnlyFileSecretStore` or any `ISecretStore` (grep), only adds the two keys to `FileSettingsStore.cs`; no task sets `hasStoredSession` after sign-in or clears it on sign-out; `IAccountHost` (06 T1) lists sign-in, sign-out, resend, state only, so the D-26 consent action has no host method, copy sentence, view control or test. As planned the selector is an orphan class and AG-67-11/12 hold only in the library.
- Fix (non-binding): in 06 T2 state that `DesktopAccountHost` builds the manager over the selector (settings read through `FileSettingsStore`), writes `hasStoredSession` on sign-in success and clears it on sign-out or SessionEnded, add a consent action to `IAccountHost`/`AccountViewModel`/`AccountCopy`/`AccountView`, and add rows to `AccountStateTests` or `HeadServicesWiringTests` (first launch: zero store calls; consent flips `account.allowFileSecretStore`).

### Warnings
**W1. [scope / ownership] B1-round-1 residual: lock files of dependents are not declared and 01 and 02 overlap undeclared.**
- Plans 01, 02, 04. Evidence: lock files record each project reference's package dependencies (`src/Autonogrammer.Accounts.Desktop/packages.lock.json` lists `autonogrammer.accounts` and `autonogrammer.harness.secrets.os` with their dependencies). Adding Logging.Abstractions to Accounts changes ten locks that list `"autonogrammer.accounts"` (Native, Web, Dispatch.Desktop, Accounts.Desktop, and tests: Accounts.Tests, Accounts.Desktop.Tests, ArchitectureTests, Dispatch.Desktop.Tests, Native.Tests, Web.Tests); the Os reference changes six (Native, Accounts.Desktop, Os.Tests, Accounts.Desktop.Tests, ArchitectureTests, Native.Tests). Each plan lists only its own `src/*/packages.lock.json` in files_modified; wave-1 plans 01 and 02 both rewrite the Accounts.Desktop, Accounts.Desktop.Tests, Native, Native.Tests and ArchitectureTests locks. AGENTS.md:99 says commit every lock; commits use explicit paths.
- Required property: files_modified of 01, 02 and 04 enumerate every lock the relock rewrites, and the shared ones are declared as sequenced (one plan relocks them last) or both wave-1 relocks run after both csproj edits.

**W2. [data_contract] The raw-socket `FakeGoTrueHost` is plain HTTP but `GoTrueOptions.Create` requires https (01 T1 test 2, D-16).**
- Plan 04 T1:76. The PKCE exchange test "runs end to end against a real socket" yet no mechanism reaches `http://127.0.0.1:port`. Required property: the test reaches the fake without loosening `GoTrueOptions` (keeps T-67-01), for example an HttpClient handler that rewrites the https project URL to the loopback port [INFERENCE]; state it.

**W3. [verification] Api.IntegrationTests commands cannot go green on a host without test services.**
- Plans 03 T1 (`*Auth*` includes `ApiAuthStartupTests`, which calls `JwtTestHost.ProductionConfig()`), 03 T3, 05 T1, T3, T4. Evidence: `JwtTestHost.cs:66-75` throws when `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` is unset; `ProductionConfig()` is used at ApiAuthStartupTests.cs:17-57, CoordinationConfigurationTests.cs:46, Harness/EngineIsolationStartupTests.cs:30-64; report 09 line 106 and VALIDATION.md:41 say the project fails without `scripts/test-services.sh up` (podman). Required property: each such task states the env prerequisite, or names the service-dependent classes as pre-existing red so a failure is attributable.

**W4. [facts] Plan 03 T3 rationale for the owners default is wrong and conflicts with the new rows.**
- 03:115,122 cite `WebAppAuthenticationTests` (alice, `/workspace` 200 at :63) and `WebAppDisclosureTests` (:25, :56); `GET /workspace` is not owner-gated, and :25 is a token issue, not a request. The real casualties are the WorkspaceSettings Jwt-host rows (`WorkspaceSettingsAccessTests.cs:20,35` build `WorkspaceHost.Jwt(workspace)` with no owners and expect pass). A default owner in the shared `WebAppTestHost.Jwt` (03:26) makes the new "empty owners deny" rows need a way to unset it, which a config merge cannot express [INFERENCE]. Also 05:228 says plan 03 touched `WebAppAuthenticationTests.cs` and `WebAppDisclosureTests.cs`; 03 files_modified does not list them. Required property: the default-owner mechanism names the real tests and a way to run with an empty list.

**W5. [key_links] The account features read is untested in both heads (T-67-25).**
- 06:104 (`DesktopAccountHost` reads GET account "through the same API client") and 07:124 (`WebAccountSession` via `IOmpApiClient.GetAccountAsync`). `IOmpApiClient` is `internal` in `apps/Autonogrammer.Web/Services/IOmpApiClient.cs:13`; Native has only an `HttpClient` (`HeadServices.CreateCloud`, HEAD 311-318). Only a fake-host view-model row (06 T1 test 4) covers the Devices gate. Required property: a test per head proves the Devices row stays closed without `dispatch` and the Native read names its client (HttpClient plus `AccountsJsonContext`).

**W6. [scope_sanity] Plans 01 (4 tasks, 24 files), 05 (4 tasks, 44 files), 07 (33 files), 03 (21 files) exceed thresholds; accepted by the planner (milestone unit split). 05 T3/T4 (mechanical cutover) could be reordered so each commit is green.**

### Advisories (info)
1. 01:191 still says "the Kestrel FakeGoTrueHost per D-43"; CONTEXT D-43 text and RESEARCH.md:90 also keep "Kestrel" (amended by D-61). Align.
2. Stale `SessionRegistry` mentions not covered: `docs/open-questions.md:122,591` (05:227 covers backup-and-restore.md:30 and railway README:31, both verified).
3. 06:47 prohibits a menu "instead of the SurfaceHost slot" while D-56 places nothing in the chrome; reword to "nothing placed".
4. 01 and 04 never say `IRefreshGuard`, `AccountSessionManager`, `GoTrueClient`, `SignInCoordinator` are `public` (Accounts has InternalsVisibleTo only for Accounts.Tests); Desktop and Native consume them.
5. 06 T2 test 4 needs "a signed-in fake account session" in `HeadServices`; no `HeadServicesOptions` seam is named. `CssContractTests` (06 T3 test 2) lives in UI.Tests, which 06 never runs.
6. 03:122 cites `WebAppDisclosureTests` line 25; 04 AG-67-6 composition on the head (06:104) has no test row.
7. Plan 06 anchors in `HeadServices.cs` (302-320, 311-318, 439-447, 449-453) match `git show HEAD`, but the working copy has another session's uncommitted hunks (+8 lines before `CreateCloud`, which is then at 311, `ApiAddress` 462, `Token` 472; also `Native.csproj` and `Native.Tests.csproj` modified). Executors must re-anchor by symbol, not line, if that change lands first.

### Requirement coverage
| Id | Plans | Id | Plans | Id | Plans |
|---|---|---|---|---|---|
| AG-67-1 | 01 (06 head) | AG-67-9 | 01 | AG-67-17 | 03 |
| AG-67-2 | 04 (06 head) | AG-67-10 | 02 | AG-67-18 | 05 |
| AG-67-3 | 01 | AG-67-11 | 04 (head gap B1) | AG-67-19 | 05 |
| AG-67-4 | 07 | AG-67-12 | 04 (head gap B1) | AG-67-20 | 05 |
| AG-67-5 | 01 | AG-67-13 | 03 | AG-67-21 | 05 |
| AG-67-6 | 01, 04 (06 composes) | AG-67-14 | 03, 05 | AG-67-22 | 05 |
| AG-67-7 | 01, 06 | AG-67-15 | 03, 05 | AG-67-23 | 06, 07 |
| AG-67-8 | 01 (06 VM) | AG-67-16 | 03 | AG-67-24 | 07 |

### Facts re-verified in the tree
`AuthConfigEndpoints.cs` 24-49; `Directory.Packages.props:65,71`; `Sandbox.csproj:18`; `Harness.Acp.csproj:5`; `WebAppTestHost.cs:50` (Jwt); `DemoWorld.cs:340` (Board), `DemoApiHandler.cs:281`; `FileSettingsStoreTests.cs` at `tests/Autonogrammer.Native.Tests/Settings/Head/`; `scripts/ci/check-changelog.sh` (optional path arg); `scripts/vercel/build-web.sh:44`; `render-csp.sh` 18-23, 62; `index.html:15`; `service-worker.published.js:35`; `TopBar.razor:24`; `HeadServices.cs` 44, 47 and, at HEAD, 303-320, 439-453; `JwtTestHost.cs` 30, 66-75; `ApiAuthenticationExtensions.cs:62`; `ApiJsonContext.cs:23`; `AutomationServiceRegistration.cs:33`; `docs/reference/api.md:39`; `e2e-api.mjs` 284-289; `Directory.Build.props:36-39` (locked mode only when CI=true).

### Recommendation
1 blocker (B1) and 6 warning(s) require revision. Returning to planner with feedback; plans are not fixed here. I withdraw the factual premise of round-1 B4 (W4).

---

## Planner revision after round 2 (2026-10-29, not re-checked: the two-round cap was reached)

| Round 2 item | Disposition |
|---|---|
| B1 selector and consent orphaned | Fixed in 67-06: `IAccountHost.RememberWithoutSystemProtectionAsync`, `AccountViewModel` shows it only with the `SessionNotRemembered` notice, `AccountCopy` sentences, `AccountView` control; `DesktopAccountHost` builds the manager over `AccountSecretStoreSelector`, writes and clears `account.hasStoredSession`, wires the consent to `account.allowFileSecretStore`; new rows in `AccountViewModelTests` (Test 5) and `AccountStateTests` (Test 4: zero store calls on first launch, consent flips the setting). |
| W1 lock scope | 67-01 and 67-02 commit only their own csproj, own lock and own test-project lock; 67-04 relocks every dependent once (Native, Web, Dispatch.Desktop, ArchitectureTests, Dispatch.Desktop.Tests, Native.Tests, Web.Tests, Accounts.Desktop.Tests, each verified tracked) and runs `CI=true ... restore Autonogrammer.Linux.slnf --locked-mode` as a second automated block. |
| W2 https vs plain fake | 67-04: a `DelegatingHandler` rewrites the https project URL to the fake's loopback port; `GoTrueOptions.Create` is not loosened. |
| W3 services | 67-03, 67-05 verification text and 67-VALIDATION state the service prerequisite and require a baseline run so pre-existing red classes are attributable; 67-03 T1 now runs `*Boundary*` and `*OptionsValidator*` (no longer `*Auth*`, which pulled in `ApiAuthStartupTests`). |
| W4 premise of the owners default | Withdrawn premise adopted: `WebAppTestHost` and the Web-app tests are no longer touched by 67-03; `WorkspaceHost` gets a nullable owners parameter (null = the subjects its tests use, empty list = no owners) so the deny rows can run; CONTEXT D-50 and RESEARCH corrected; the 67-05 shared-file note no longer claims 67-03 touched those tests. |
| W5 features read untested | 67-06 T2 Test 5 (`DesktopAccountHost` reads `GET /account` through the head's `HttpClient`, Devices closed without `dispatch` or on a failed read) and 67-07 T2 Test 6 (`WebAccountSession` through a fake `IOmpApiClient.GetAccountAsync`). |
| W6 file counts | Accepted (milestone unit split). |
| I1, I3, I4, I5, I7 | Kestrel wording aligned (67-01, RESEARCH); chrome wording in 67-06 prohibitions; public visibility of `IAccountSession`, `AccountSessionManager`, `SignInCoordinator`, `GoTrueClient`, `GoTrueOptions`, `AuthResult`, `IRefreshGuard` stated in 67-01; `HeadServicesOptions` test seams and `UI.Tests` run added in 67-06; 67-06 objective says to anchor `HeadServices.cs` edits by symbol. I2 (`docs/open-questions.md` mentions of `SessionRegistry`) left: they are decision records. I6 left. |
