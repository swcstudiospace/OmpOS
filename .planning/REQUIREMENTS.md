# Requirements: OmpOS

**Milestone:** v0.9.2 Hardening and hosted API
**Defined:** 2026-10-21
**Core Value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.

## Sources and conventions

- Sources: `.planning/milestones/v0.9.1-MILESTONE-AUDIT.md` (front matter `tech_debt`, `owner_decided_deferrals`, `human_only`, `gaps`; sections 2, 9, 11, 12), `docs/handoff.md`, `docs/open-questions.md`. Research was skipped for this milestone.
- REQ-ID source mapping: `TD-nn` = entries of the audit front matter `tech_debt` block in order; `OD-nn` = audit `OD-n`; `HO-nn` = audit `HO-n`. Each entry gives its audit line (front matter line numbers) or open-question ID.
- Count note: the audit states 43 tech-debt items, but its `01-foundation` block lists 11 entries (M-01 to M-04, L-01 to L-04, IN-01, IN-02, 01-VALIDATION) against a stated count of 10. All 11 are carried, so the TD group has 44 entries.
- Supersession: tech debt is programmed into phases, not parked. This supersedes owner decision 7 in `docs/handoff.md`.
- Gate: every HOST requirement runs after the owner decides the sign-in method (OD-03). That decision is not recorded in any source for this milestone and is not assumed here.

## v1 Requirements

### Tech Debt (TD)

Source for each line: audit front matter `tech_debt` block, `phase` name and line number.

- [ ] **TD-01**: Temporal dev ports in `deploy/compose.yaml` bind to loopback or require auth; they are not reachable on all host interfaces. Source: audit tech_debt 01-foundation M-01 (`deploy/compose.yaml:9-11`), line 68. **Note 2026-10-21:** the Temporal dev ports go with the Temporal removal (`docs/handoff.md`); the compose cutover that removes them is still in progress, so their removal is not yet verified.
- [ ] **TD-02**: CI action major versions are verified and pinned in `.github/workflows/ci.yml`. Source: audit M-02 (`ci.yml:28,31,39,78,79`), line 69.
- [ ] **TD-03**: `bootstrap.sh` verifies the `dotnet-install.sh` download integrity; OQ-27 is resolved in `docs/open-questions.md:33`. Source: audit M-03 (`bootstrap.sh:16-17`), line 70.
- [ ] **TD-04**: The compose gate validates `deploy/compose.yaml` with `podman-compose config`, not a YAML parse only. Source: audit M-04 (`ci.yml:70-71`), line 71.
- [ ] **TD-05**: 01-REVIEW finding L-01 (hygiene or layer-map gap) is closed. Source: audit L-01 (`01-REVIEW.md`, listed as "L-01 to L-04"), line 72.
- [ ] **TD-06**: 01-REVIEW finding L-02 is closed. Source: audit L-02 (`01-REVIEW.md`), line 72.
- [ ] **TD-07**: 01-REVIEW finding L-03 is closed. Source: audit L-03 (`01-REVIEW.md`), line 72.
- [ ] **TD-08**: 01-REVIEW finding L-04 is closed; `tests/OmpOS.ArchitectureTests/LayerMap.cs:37-41` is among the L items. Source: audit L-04 (`01-REVIEW.md`; `LayerMap.cs:37-41`), line 72.
- [ ] **TD-09**: The semver assertion in `tests/OmpOS.Contracts.Tests/ContractVersionTests.cs:16` checks a real semver value instead of passing vacuously. Source: audit IN-01, line 73.
- [ ] **TD-10**: The duplicate `.env` entry in `.gitignore` (lines 7 and 485) is removed. Source: audit IN-02, line 74.
- [ ] **TD-11**: `01-VALIDATION.md` rows 1-01-03 and 1-01-09 are exercised, or the `nyquist_compliant` flag matches the evidence. Source: audit 01-VALIDATION (`01-VALIDATION.md:55,61`), line 75.
- [ ] **TD-12**: Inbound `x-correlation-id` is validated (length and charset) before use and before persistence. Source: audit F-05 (`services/OmpOS.Api/Middleware/CorrelationIdMiddleware.cs:36`), line 79.
- [ ] **TD-13**: Resume positions `since` and `Last-Event-ID` are validated (malformed or negative rejected, bounded). Source: audit F-06 (`services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs:46-69`), line 80.
- [ ] **TD-14**: `HarnessEventMapper` maps unknown subtypes to the unknown fallback instead of throwing. Source: audit F-07 (`services/OmpOS.Api/Harness/HarnessEventMapper.cs:32`), line 81.
- [ ] **TD-15**: `PromptRunner` keeps tracking newer runs; completion of an older run does not evict them. Source: audit F-08 (`services/OmpOS.Api/Services/PromptRunner.cs:109`), line 82.
- [ ] **TD-16**: `CreateSessionRequest.OwnerId` length and format are bounded in `SessionEndpoints.cs` (CreateSessionAsync). Source: audit F-09 (info), line 83.
- [ ] **TD-17**: `ps` output is validated before it is used as a container id. Source: audit WR-01 (`src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:322`), line 87.
- [ ] **TD-18**: The caller `SessionId` is escaped or validated before it is used in the podman filter. Source: audit WR-02 (`LocalPodmanComposeProvider.cs:315-316`), line 88.
- [ ] **TD-19**: `AllowHostSecrets` and `PerSandboxCredentials` are enforced, not ignored. Source: audit WR-03 (`LocalPodmanComposeProvider.cs:340-356`), line 89.
- [ ] **TD-20**: Descriptor labels do not embed the host temp path. Source: audit WR-04 (`LocalPodmanComposeProvider.cs:279-280`), line 90.
- [ ] **TD-21**: Raw stderr is not placed in exception messages; it goes to a bounded, redacted diagnostic. Source: audit WR-05 (`LocalPodmanComposeProvider.cs:303`), line 91.
- [ ] **TD-22**: The TTL boundary in `SandboxLifecycle.cs` has no off-by-one error. Source: audit WR-06 (`src/OmpOS.Sandbox/SandboxLifecycle.cs:231`), line 92.
- [ ] **TD-23**: Jittered backoff has a positive floor. Source: audit WR-07 (`SandboxLifecycle.cs:277-278`), line 93.
- [ ] **TD-24**: OpenShell YAML list values render as YAML values, not CLR type names. Source: audit WR-08 (`src/OmpOS.Sandbox/OpenShellProvider.cs:221-228`), line 94.
- [ ] **TD-25**: A production `IProcessRunner` is registered by a host (no caller today), and `RealProcessRunner` is no longer confined to the tests project. Source: audit WR-09 (`tests/OmpOS.Sandbox.Tests/RealProcessRunner.cs:1-13`; `src/OmpOS.Sandbox/SandboxServiceCollectionExtensions.cs:15-18`), line 95.
- [ ] **TD-26**: `RealProcessRunner` starts processes from an argument list, not hand-rolled quoting. Source: audit WR-11 (`RealProcessRunner.cs:19,116-124`), line 96.
- [ ] **TD-27**: A session-store failure in `SandboxLifecycle` surfaces as a typed error, not an untyped exception. Source: audit WR-12 (`SandboxLifecycle.cs:80`), line 97.
- [ ] **TD-28**: 03-REVIEW finding IN-01 is closed. Source: audit IN-01 to IN-04 (`03-REVIEW.md`, including DeleteComposeFile exception filtering in `LocalPodmanComposeProvider.cs`), line 98.
- [ ] **TD-29**: 03-REVIEW finding IN-02 is closed. Source: audit IN-02 (`03-REVIEW.md`), line 98.
- [ ] **TD-30**: 03-REVIEW finding IN-03 is closed. Source: audit IN-03 (`03-REVIEW.md`), line 98.
- [ ] **TD-31**: 03-REVIEW finding IN-04 is closed. Source: audit IN-04 (`03-REVIEW.md`), line 98.
- [ ] **TD-32**: The reconnect policy in the web head has a recorded closure: bounded reconnect with backoff implemented and tested, or the policy decision written down. Source: audit 04-surfaces WR-02 (`04-REVIEW.md:140`), line 102.
- [ ] **TD-33**: Terminal SSE detection checks the event type, not only an event field. Source: audit WR-03 (`apps/OmpOS.Web/Services/SseEventStream.cs:157-158`), line 103.
- [ ] **TD-34**: Seq dedup sets are bounded. Source: audit WR-04 (`src/OmpOS.EngineHost/RemoteEngineHost.cs:27,315-355`; `apps/OmpOS.Web/Services/SseEventStream.cs:62`), line 104.
- [ ] **TD-35**: A2UI image URLs are restricted to an allowed scheme list. Source: audit WR-06 (`src/OmpOS.GenUi/A2uiSurfaceCatalog.cs:110-117`), line 105.
- [ ] **TD-36**: `EngineEventMapper` maps unknown events to a fallback instead of throwing. Source: audit WR-07 (`apps/OmpOS.App/Components/EngineEventMapper.cs:56`), line 106.
- [ ] **TD-37**: The accepted `MarkupString` sink (handoff decision 3) has a regression test proving its only input is `MarkdownRenderer` output. Source: audit IN-01 (`src/OmpOS.UI/Components/Blocks/MarkdownBlockView.razor:4`), line 107.
- [ ] **TD-38**: Whitespace-only drafts are not sent. Source: audit IN-02 (`src/OmpOS.UI/Components/Composer.razor:36-45`), line 108.
- [ ] **TD-39**: History replay deduplicates by Seq in `SelectSessionAsync`. Source: audit IN-03 (`apps/OmpOS.Web/Pages/Index.razor`), line 109.
- [ ] **TD-40**: `03-VERIFICATION.md` frontmatter status and criteria row 4c agree with its Re-verification section (MET). Source: audit records-and-docs (`03-VERIFICATION.md:4`, `:61`, `:125-161`), line 113.
- [ ] **TD-41**: `04-VERIFICATION.md` score and UI-03 row agree with its Re-verification section (VERIFIED). Source: audit records-and-docs (`04-VERIFICATION.md:5`, `:28`), line 114.
- [x] **TD-42**: `04-03-SUMMARY.md` frontmatter lists DESK-01 and MOB-01 as well as SHELL-01. Source: audit records-and-docs (`04-03-SUMMARY.md:2`), line 115. Closed: commit 882444f; frontmatter at `.planning/milestones/v0.9.1-phases/04-surfaces/04-03-SUMMARY.md:3` lists `[DESK-01, MOB-01, SHELL-01]`.
- [ ] **TD-43**: The VERIFY-02 TODO-linkage gate has an in-repo positive case and negative case, not only a recorded negative control in commit a6fcadd. Source: audit test-evidence (`05-VALIDATION.md:42`; `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:121`), line 119.
- [x] **TD-44**: The two `incomplete` axe items on the icon-only send control are resolved or documented with evidence, and the unexplained skip-link screenshot is explained or reproduced. Source: audit test-evidence (`docs/runbooks/local-dev.md:96-97`), line 120. Closed: commit 924ca78; evidence `docs/runbooks/evidence/web-*-924ca78-2026-10-21.*` and the dated entry 2026-10-21 (commit 924ca78) in `docs/runbooks/local-dev.md`.

### Owner-Deferred Items (OD)

Source for each line: audit `owner_decided_deferrals` (OD-n) and the open-question ID.

- [ ] **OD-01**: Interactive tool approval: a UI decision reaches a pending ACP `session/request_permission`; `RequestToolApprovalAsync` and `ResolveToolApprovalAsync` (`src/OmpOS.Sessions/SessionService.cs:286`, currently internal) get a production caller; deny-by-policy default is preserved and approval may only deny earlier. Source: audit OD-1; `docs/open-questions.md:42` (Q-HARN-PERMISSION); handoff decision 2.
- [ ] **OD-02**: ~~Temporal worker composition: real provider and activity implementations are composed into the worker, and the worker is wired into the API session-run path behind `Workflows:Enabled` (`services/OmpOS.Api/Program.cs:54-63` uses FakeSandboxProvider; `services/OmpOS.Workflows.Worker/WorkerHostConfiguration.cs:91-97` guards activities). Source: audit OD-2; `docs/open-questions.md:41` (Q-SB-WORKER-COMPOSE); handoff decision 1.~~ **Composition half superseded 2026-10-21:** the Temporal worker is removed and there is no worker service. Session runs execute in-process in the API through `DirectSessionRunner` (`docs/handoff.md`, "Temporal removal and Railway topology (2026-10-21)", supersession row OD-02). Q-SB-WORKER-COMPOSE is resolved by removal. The persistence half (Phase 7 in ROADMAP.md) stays open, and its session log is shared by the API only.
- [ ] **OD-03**: The single dev principal (`services/OmpOS.Api/Security/SingleDevPrincipal.cs:8,11`) is replaced by an authentication provider using the sign-in method the owner selects; unauthenticated cross-origin requests are denied. Gate: owner decision on sign-in method. Source: audit OD-3; `docs/open-questions.md:32` (OQ-26); handoff decision 4 and deploy blocker 1.
- [ ] **OD-04**: Per-sandbox credential issuance: each sandbox receives its own scoped credential, revoked at teardown, beyond duplicate env-target rejection. Source: audit OD-4; `docs/open-questions.md:40` (Q-SB-CREDENTIALS).
- [ ] **OD-05**: The direct-path retry policy is decided and implemented: bounded retries for idempotent activities as on the Temporal path, or an explicit fail-fast setting with tests; the decision is recorded in the open question. Source: audit OD-5; `docs/open-questions.md:39` (Q-WF-RETRY); handoff known deferrals. **Note 2026-10-21:** the "Temporal path" comparison is historical. Temporal is removed; retry lives in `DirectSessionRunner` and the run lease only (Q-WF-RETRY, `docs/handoff.md`). **Superseded 2026-10-21:** Temporal removed; see docs/handoff.md, Temporal removal and Railway topology (2026-10-21), item 1.
- [ ] **OD-06**: The remote WebSocket ACP transport stub is replaced by a transport that passes `AcpTransportContractTests<T>`, using framing and authentication verified against the ACP or omp specification; any unverified part stays in the open question. Source: audit OD-6; `docs/open-questions.md:34` (Q-HARN-WS).
- [ ] **OD-07**: Unseen `session/update` variants are recorded from live `omp acp` and omp RPC turns, frozen as fixtures, and mapped; the unknown fallback applies only to variants still unseen. Source: audit OD-7; `docs/open-questions.md:43` (Q-HARN-UPDATE).
- [ ] **OD-08**: `AgentOsProvider` implements the sandbox operations against a verified agentOS interface, or the open question records the verified blocker. No vendor API is invented. Source: audit OD-8; `docs/open-questions.md:35` (Q-SB-AGENTOS); `VendorStubTests`.
- [ ] **OD-09**: `RailwaySwarmProvider` implements the sandbox operations against verified Railway endpoints and authentication, or the open question records the verified blocker. Source: audit OD-9; `docs/open-questions.md:36` (Q-SB-RAILWAY).
- [ ] **OD-10**: The attested-enclave verifier produces a verdict from a verified report format (dstack attestation shape) instead of always `Unverified`, or the open question records the verified blocker. Source: audit OD-10; `docs/open-questions.md:37` (Q-SB-ENCLAVE).
- [ ] **OD-11**: `OpenShellProvider` (`src/OmpOS.Sandbox/OpenShellProvider.cs:11`) is exercised through the podman compute driver on Podman 5.x against the verified gateway and policy contract, or the open question records the verified blocker. Source: audit OD-11; `docs/open-questions.md:38` (Q-SB-OPENSHELL).
- [ ] **OD-12**: The Blazor reflection override for the web and MAUI heads is resolved: the head runs with `JsonSerializerIsReflectionEnabledByDefault=false` under tests, or a scoped override is recorded with its justification. Source: audit OD-12; `docs/open-questions.md:45` (Q-WEB-REFLECTION).
- [ ] **OD-13**: The authoritative AC_1..AC_11 enumeration is recovered from the ultrathink plan (Linear SPE-17749 to SPE-17798; Notion task 3ffbc1a0c7ae8112aa70d2e15b81929f), or recorded as unrecoverable; the AC verdicts in `docs/handoff.md` are re-checked against the recovered list. Source: audit OD-13; `docs/open-questions.md:44` (Q-AC-LIST); handoff provenance; PROJECT.md Context (Process).

### Human-Only Items on This Host (HO)

- [ ] **HO-02**: `podman` and `podman-compose` are installed on this host; `podman-compose config` validates `deploy/compose.yaml` and the compose stack boots (FOUND-08). Source: audit HO-2 (`01-VERIFICATION.md:50-55`; `deploy/compose.yaml`). Podman was absent at scaffold time (PROJECT.md Context).
- [ ] **HO-03**: `RealPodmanComposeConfig_SmokeTest` runs and passes on this host's podman, not skipped. Source: audit HO-3 (`tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs:136`; `03-VERIFICATION.md` human verification 2).
- [ ] **HO-04**: ~~The replay fixture `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` is re-validated against a live Temporal dev server on this host. Source: audit HO-4 (`03-VERIFICATION.md` human verification 1). The Temporal CLI was absent at scaffold time (PROJECT.md Context).~~ **Superseded 2026-10-21:** the Temporal history fixture is retired, so there is no replay to re-validate. The evidence file `docs/runbooks/evidence/ho-04-replay-revalidation-2026-10-21.md` is kept and marked superseded (`docs/handoff.md`, supersession row HO-04).

### Hosting (HOST)

Gate for every HOST requirement: owner decision on the sign-in method (OD-03).

- [ ] **HOST-01**: **Wording updated 2026-10-21 (Temporal removal):** the API alone deploys to Railway; there is no worker service (`docs/handoff.md`, decision 2). The API deploys from committed config-as-code built with podman-compatible OCI build files, configured through the variables in `.env.example`; `/health/live` and `/health/ready` respond on the deployed URL. Source: PROJECT.md Constraints (Hosting split); PROJECT.md Out of Scope (Railway deployment, superseded by this milestone); handoff "Before any public deployment".
- [ ] **HOST-02**: The hosted API is not publicly exposed until the OD-03 sign-in method is in place; the deploy checklist verifies that unauthenticated cross-origin requests are denied by the locked CORS default. Source: handoff decision 4; handoff "Before any public deployment" item 1; audit OD-3.
- [ ] **HOST-03**: ~~The API and worker connect to Temporal Cloud with credentials supplied only through environment variables (none committed), and readiness reflects Temporal Cloud connectivity.~~ **Superseded 2026-10-21:** replaced by the Railway services in `docs/handoff.md` decision 2 (TimescaleDB session log, Dragonfly coordination, GreptimeDB telemetry over OTLP). No Temporal Cloud connection exists. Source: PROJECT.md Out of Scope (Temporal Cloud setup, superseded by this milestone); PROJECT.md Key Decisions (Temporal optional behind `Workflows:Enabled`, superseded 2026-10-21); audit OD-2.
- [ ] **HOST-04**: The hosted API path runs the real omp harness instead of only the in-memory adapter (`services/OmpOS.Api/Program.cs`). Source: handoff decision 9; audit integration INT-OMP-IN-API (`services/OmpOS.Api/Program.cs:51`).
- [ ] **HOST-05**: The web head's `connect-src` allows the production API origin. `vercel.ts` generates it at build time from the validated `OMPOS_API_ORIGIN` (`scripts/vercel/render-csp.sh` validates the origin), and the web head reads that origin from `Api:BaseUrl` or `apiBaseUrl`. Source: audit HO-7 (superseded reference `vercel.json:31`, which no longer exists); handoff "Before any public deployment" item 2.
- [ ] **HOST-06**: The static publish output is deployed to Vercel, and the runbook browser smoke on the deployed URL shows the same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar) with no CSP violations. Source: audit HO-7; handoff "Before any public deployment" item 4.

### Mac Builds (MAC)

- [ ] **MAC-01**: `apps/OmpOS.App` builds for `net10.0-ios` with warnings as errors on a macOS host with Xcode 27.1. Source: audit HO-6; handoff decision 5 and "Before any public deployment" item 3; `docs/open-questions.md:23` (OQ-17). **Superseded 2026-10-21 (ADR-0004):** replaced by XP-05 (iOS) and XP-04 (macOS) in v0.10.0.
- [ ] **MAC-02**: `apps/OmpOS.App` builds for `net10.0-maccatalyst` with warnings as errors on the same macOS host. Source: audit HO-6; handoff "Before any public deployment" item 3. **Superseded 2026-10-21 (ADR-0004):** replaced by XP-05 (iOS) and XP-04 (macOS) in v0.10.0.
- [ ] **MAC-03**: The macOS CI `shells` job (`.github/workflows/ci.yml:74-88`) runs green on a macOS runner and records DESK-01, MOB-01 and SHELL-01 evidence; they stay Partial until it does. Source: audit HO-6; handoff decision 5; `docs/open-questions.md:23` (OQ-17).
- [ ] **MAC-04**: Runtime spot-check on the macOS build: desktop streams against the remote backend; mobile resumes remote-only and never spawns a process. Source: handoff "Before any public deployment" item 3 (verify line).

### Records and Evidence (REC)

- [x] **REC-01**: Blocking record BR-1 is closed: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:121`) runs and passes at HEAD; `05-VERIFICATION.md` is re-run to replace its gaps_found status; VERIFY-02 is added to `05-01-SUMMARY.md` requirements-completed only if the re-run passes. Source: audit section 2.2 and front matter `gaps.blocking_records` (BR-1); `05-VALIDATION.md:42`. Evidence: `.planning/milestones/v0.9.1-phases/05-verify/05-VERIFICATION.md` (status passed, re-run at a26fe2f) and `docs/runbooks/evidence/verify02-todo-linkage-a26fe2f-2026-10-21.log` (filtered run: total 1, succeeded 1, failed 0).
- [x] **REC-02**: The clean-clone bootstrap is re-recorded at the new HEAD: a fresh clone runs `./bootstrap.sh`, and date, host, SDK version, duration and result are recorded in `docs/runbooks/local-dev.md` with the log under `docs/runbooks/evidence/`. Source: audit HO-9 (`docs/runbooks/local-dev.md:106-118`; `05-VALIDATION.md:35`); handoff item 5. Evidence: `docs/runbooks/local-dev.md` (entry for HEAD a26fe2f) and `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-a26fe2f.log` (bootstrap OK; 1080 passed, 0 failed, 0 skipped).
- [x] **REC-03**: Nyquist validation runs for phases 1 to 5 and each phase gets a real verdict (audit: 0 of 5 compliant; phase 1 partial, phases 2 to 5 not validated). Source: audit front matter `nyquist`; audit section 9; audit section 12 item 5. Evidence: `.planning/milestones/v0.9.1-phases/01-foundation/01-VALIDATION.md` (status validated, nyquist_compliant false) and `02-core/02-VALIDATION.md`, `03-platform/03-VALIDATION.md`, `04-surfaces/04-VALIDATION.md`, `05-verify/05-VALIDATION.md` (each status partial, nyquist_compliant partial); none is compliant.
- [x] **REC-04**: The milestone re-audit reports a status other than `gaps_found`: no blocking code gaps, no blocking records, and every front-matter `tech_debt` entry closed or re-classified with evidence. Source: audit section 12 (routing); audit front matter `status: gaps_found`. Evidence: `.planning/v0.9.2-MILESTONE-AUDIT.md` front matter `status: tech_debt` (line 5).
- [x] **REC-05**: `docs/handoff.md` and `docs/open-questions.md` reflect every closure in this milestone (known deferrals, owner decisions, AC verdicts, open-question status), so no closed item still appears as deferred. Source: `docs/handoff.md` "Known deferrals" and "Owner decisions (2026-10-21)"; handoff AC_11 verdict. Evidence: docs/handoff.md and docs/open-questions.md reflect the closures (commit 3ce7250, then the handoff corrections in the commit that follows). No closed item is presented as deferred; the remaining open items are the external blockers listed in .planning/v0.9.2-MILESTONE-AUDIT.md section 4.

## Excluded From This Milestone

Human-only audit items not on this host and not in the requested scope. They are recorded here so they are not lost; they create no REQ-IDs.

- Audit HO-1: remote GitHub CI run and first push (FOUND-05). Needs a human push and a remote run; handoff decisions 6 and 10.
- Audit HO-5: owner confirmation that TCP-level readiness satisfies criterion 4c. Needs the owner's sign-off.
- Audit HO-8: screen-reader sign-off (UI-03 H-2). Needs a human with assistive technology.

## Future Requirements

None recorded for this milestone.

## Out of Scope

- Choosing the sign-in method itself. The owner decides it; this milestone implements the decision only after it is made (OD-03, HOST-02).
- Inventing vendor APIs. OD-08 to OD-11 implement only against verified interfaces; a missing verification is recorded as the blocker (PROJECT.md Constraints: no invented vendor facts).
- Multi-tenant admin, billing, model routing UI, plugin marketplace, app-store packaging (unchanged from PROJECT.md Out of Scope).

## Traceability

Filled by the roadmap.

| REQ-ID | Phase | Status |
|--------|-------|--------|

## Cross-platform UI and ADE harness (planned, ADR-0004)

Status: planned for v0.10.0 (XP) and v0.11.0 (ADE). Not counted in v0.9.2 completion. G-WEB failed 2026-10-21 on Avalonia 12.1.4; see ADR-0004.

Source: ADR-0004 (accepted 2026-10-21; reversible through gate G-WEB); `docs/plans/ade-harness-plan.md`. Platform routes for XP-02 to XP-06 are in the matrix in `docs/plans/ade-harness-plan.md`.

### Cross-platform UI (XP, v0.10.0)

- [ ] **XP-01**: One Avalonia UI codebase for Windows, macOS, Linux, iOS and Android; the web head stays Blazor WebAssembly on the shared presentation layer until gate G-WEB passes (ADR-0004).
  - Acceptance: the Windows, macOS, Linux, iOS and Android heads are built from one Avalonia UI solution. The web head stays Blazor WebAssembly on the shared presentation layer until G-WEB passes. `apps/OmpOS.App` (MAUI) is deleted only in the cutover commit, after every XP-10 parity test is green. `src/OmpOS.UI` (RCL) stays until the web head moves.
  - Verification: CI builds the five Avalonia heads from the one solution. An architecture test in `tests/OmpOS.ArchitectureTests` fails if any project references `Microsoft.Maui` after the MAUI cutover. The XP-10 parity run is green before the MAUI deletion commit.
- [ ] **XP-02**: Linux desktop head on X11 (default) and Wayland (native backend opt-in from Avalonia 12.1.0, otherwise XWayland); control tree exposed through AT-SPI2.
  - Acceptance: the head starts under an X11 session by default, under a Wayland session with the native backend opted in (Avalonia 12.1.0 or later), and under XWayland otherwise. The AT-SPI2 tree exposes the prompt input, send control and transcript with a role and a name each.
  - Verification: platform route per the matrix in `docs/plans/ade-harness-plan.md` (Linux, Tier 1 distributions). Run the head under X11 and Wayland sessions. Query the AT-SPI2 tree with a script or Accerciser and record role and name for the three controls.
- [ ] **XP-03**: Windows head builds for net10.0-windows on Windows 11 24H2 (Tier 1); UI Automation exposes names and roles.
  - Acceptance: the head builds for `net10.0-windows` with warnings as errors and starts on Windows 11 24H2. The UI Automation tree exposes the prompt input, send control and transcript with a name and a control type each.
  - Verification: platform route per the matrix in `docs/plans/ade-harness-plan.md` (Windows 11 24H2 runner). Inspect or a UIA client script records Name and ControlType for the three controls.
- [ ] **XP-04**: macOS head builds on macOS 26 (Tier 1); NSAccessibility exposes names and roles.
  - Acceptance: the macOS head builds on macOS 26 with warnings as errors. NSAccessibility exposes the prompt input, send control and transcript with a name and a role each. It replaces the superseded MAUI macOS build (v0.9.2 phase 15).
  - Verification: platform route per the matrix in `docs/plans/ade-harness-plan.md` (macOS 26 host, Phase 22). Accessibility Inspector records role and name for the three controls. Signing and notarization are Mac-channel items under XP-11.
- [ ] **XP-05**: iOS head builds for net10.0-ios on iOS/iPadOS 26 (Tier 1); UIAccessibility exposes names and roles; runtime and signing need the Mac channel.
  - Acceptance: the iOS head builds for `net10.0-ios` with warnings as errors on macOS with Xcode 27.1. UIAccessibility exposes labels and roles for the prompt input, send control and transcript. It replaces the superseded MAUI iOS build (v0.9.2 phase 16).
  - Verification: platform route per the matrix in `docs/plans/ade-harness-plan.md` (Phase 24). The compile runs in CI on macOS. Runtime on iOS/iPadOS 26 and signing are Mac-channel items under XP-11 and are human-gated.
- [ ] **XP-06**: Android head builds for net10.0-android on Android 16 (Tier 1); AccessibilityNodeInfo exposes names and roles.
  - Acceptance: the Android head builds for `net10.0-android` with warnings as errors. On an Android 16 device or emulator, AccessibilityNodeInfo exposes text or content description and a class name for the prompt input, send control and transcript.
  - Verification: platform route per the matrix in `docs/plans/ade-harness-plan.md`. The build runs in CI. On Android 16 a `uiautomator dump` records the text or content-description and class of the three controls.
- [ ] **XP-07**: Web head: Blazor WebAssembly (existing) renders the shared presentation layer; Avalonia.Browser adoption is gated by G-WEB.
  - Acceptance: the Blazor web head keeps its axe and CSP checks, and the shared presentation layer has parity tests. Avalonia.Browser is adopted only after G-WEB passes.
  - Verification: the axe run on the Blazor web head runs in CI, together with its CSP checks. G-WEB is re-measured on each Avalonia release, and each measurement is recorded under `docs/runbooks/evidence/avalonia-spike-2026-10-21/`.
- [ ] **XP-08**: Accessibility baseline on every head: keyboard operation of every control, visible focus, accessible names, text contrast of at least 4.5:1, reduced motion respected.
  - Acceptance: every control on every head is reachable and operable by keyboard alone. Every focusable control shows a visible focus indicator. Every control has an accessible name. Body text contrast is at least 4.5:1 against its background. With the reduced-motion setting on, non-essential animation is off.
  - Verification: automated keyboard traversal and contrast tests in the Avalonia test suite, run against the design tokens. Per-head keyboard run and screen-reader check recorded in the phase evidence for XP-02 to XP-07.
- [ ] **XP-09**: Plugin harness host contracts in the contracts layer: plugins contribute commands, panels, tools and agent modes; contract tests run in the gate.
  - Acceptance: the contracts layer defines plugin contribution types for commands, panels, tools and agent modes, with no UI framework reference. An in-process sample plugin registers one contribution of each kind and the host lists all four.
  - Verification: contract tests in `tests/OmpOS.Contracts.Tests` run in the CI gate. An architecture test asserts that the contracts layer does not reference Avalonia, MAUI or `OmpOS.UI`.
- [ ] **XP-10**: Parity before removal: each feature of the MAUI head has an Avalonia test before `apps/OmpOS.App` is removed. The Razor component library stays until the web head moves.
  - Acceptance: a parity list names every feature of `apps/OmpOS.App`, with one Avalonia test per feature. No feature row lacks a test. `apps/OmpOS.App` is removed only after the whole parity list is green. `src/OmpOS.UI` is not removed by this item.
  - Verification: the parity list is reviewed row by row against the page and component inventory of `apps/OmpOS.App`. The Avalonia parity run is green in CI before the removal commit, and that commit is the only one that deletes `apps/OmpOS.App`.
- [ ] **XP-11**: Mac-channel items are listed separately with their verification route (signing, notarization, device runs).
  - Acceptance: a separate list names each Mac-channel item (iOS and macOS signing, notarization, iOS and macOS device runs). Each entry gives its verification route, the macOS host required (Xcode 27.1 for iOS), and the human who runs it. No Mac-channel item is marked done from a non-Mac run.
  - Verification: review of the list against XP-04 and XP-05 and the superseded MAC-01 to MAC-04 entries. A human sign-off line is recorded when each Mac run completes.

### ADE harness (ADE, v0.11.0)

- [ ] **ADE-01**: Session chat with streamed generative UI blocks (parity with the current heads).
  - Acceptance: a prompt sent from the Avalonia head streams its events into the transcript as generative UI blocks. The block types match those the current RCL and MAUI heads render. After a reconnect the stream resumes from the last Seq without duplicate blocks.
  - Verification: Avalonia tests drive a fixture event stream through the head and assert the rendered block for each block type. Parity test compares the output with the recorded output of the current heads.
- [ ] **ADE-02**: Tool approvals in the run UI, routed through the ACP permission bridge.
  - Acceptance: a pending ACP `session/request_permission` appears in the run UI with the tool name and arguments. Approve and deny send the decision through the permission bridge (OD-01). With no decision, the request is denied by policy. An approval can only deny earlier, never allow.
  - Verification: integration test with a fake ACP transport emits `session/request_permission`. A UI test asserts the approval control. Tests assert the bridge receives the chosen decision and that a timeout produces deny.
- [ ] **ADE-03**: Steering and follow-up prompts while a run is active.
  - Acceptance: while a run is active the composer accepts a steering message, which is delivered to the active run, and a follow-up, which is queued and dispatched after the run completes. The run UI shows which kind is pending.
  - Verification: test with a fake active run. Assert the steering message reaches the run input channel and the follow-up is dispatched only after the completion event, in that order.
- [ ] **ADE-04**: Side chats and transcript search.
  - Acceptance: a side chat opens from a session without adding messages to the main transcript. Transcript search returns every case-insensitive match in the current session, and selecting a match jumps to it.
  - Verification: unit tests of the search function on a fixed transcript fixture, asserting match count and order. UI test asserting side-chat messages do not appear in the main transcript.
- [ ] **ADE-05**: Agents window: parallel runs with status, filter and open or close.
  - Acceptance: the Agents window lists runs with a status of running, completed, failed or cancelled. It filters by status and opens or closes a run view. Closing a view does not stop the run. Two runs execute concurrently without sharing transcript state.
  - Verification: tests with two concurrent fake runs. Assert status transitions, filter results, and that closing a view leaves the run state unchanged.
- [ ] **ADE-06**: Git worktree isolation per agent run.
  - Acceptance: each agent run executes in its own git worktree under a path the operator controls. Two runs on one repository never share a working directory. Changes stay inside the worktree, and the main checkout is unchanged by a run.
  - Verification: integration test on a temporary git repository. Start two runs, write a file in each, and assert separate worktrees (`git worktree list`) and an unchanged main checkout.
- [ ] **ADE-07**: Cloud agent runs on the hosted API, with status and reviewable output.
  - Acceptance: a run started in cloud mode executes through the hosted API (`services/OmpOS.Api`). Its status moves through queued, running and completed. After completion the diff and transcript are retrievable for review.
  - Verification: API integration test on the API test host starts a cloud run, polls status until completed, and fetches the output. The test asserts the diff is present in the output.
- [ ] **ADE-08**: Remote control and notifications from another device.
  - Acceptance: a second device lists active runs, sends a prompt or an approval, and receives a notification when a run needs approval or completes. Remote endpoints reject unauthenticated requests. Authentication uses the sign-in method chosen under OD-03.
  - Verification: API test asserts unauthenticated remote calls are rejected. End-to-end test asserts a second client receives the completion notification. This item is gated by the OD-03 decision.
- [ ] **ADE-09**: Code editor: syntax highlighting, tabs and find.
  - Acceptance: the editor opens workspace files in tabs. It highlights C#, TypeScript, JSON, YAML and Markdown. Find shows the match count and moves between matches.
  - Verification: Avalonia UI tests open two files in tabs, assert the find match count on a fixture, and assert highlight tokens for each listed language.
- [ ] **ADE-10**: Workspace file tree and open folder.
  - Acceptance: opening a folder sets the workspace root used for runs. The file tree loads children when a directory is expanded and is fully keyboard navigable.
  - Verification: test on a fixture directory asserts the children of an expanded node. Test asserts that run dispatch uses the opened folder as its workspace root.
- [ ] **ADE-11**: Diff review with accept and reject per hunk.
  - Acceptance: a run's changes are shown as hunks. Accepting a hunk keeps it, rejecting a hunk reverts only that hunk. After all decisions the file equals the accepted hunks applied to the original.
  - Verification: test on a fixture repository with a two-hunk change. Accept one hunk and reject the other, then assert the file content equals the expected result.
- [ ] **ADE-12**: Inline edit on a selection.
  - Acceptance: invoking inline edit on a selection sends the selected text and the instruction to the agent. The response replaces only the selected range, and only after the user accepts it. Undo restores the original text.
  - Verification: unit test of range replacement on a fixture buffer. UI test asserts a rejected response leaves the buffer unchanged.
- [ ] **ADE-13**: Inline completion backed by a configured model provider.
  - Acceptance: with a model provider configured, a suggestion appears at the caret and accepting it inserts the text. Without a configured provider, completion is off and no request is sent.
  - Verification: test with a stub provider endpoint asserts a request is sent only when a provider is configured. Accept and dismiss behaviour is asserted in a UI test.
- [ ] **ADE-14**: Integrated terminal; commands need approval; auto-run is opt-in.
  - Acceptance: the terminal starts in the workspace directory. Each command needs approval. Auto-run is off by default and is enabled only by explicit configuration. A declined command is not executed.
  - Verification: tests with a fake process runner assert that a command with default settings stays pending until approved, that auto-run executes without a prompt only when enabled, and that a declined command never spawns a process.
- [ ] **ADE-15**: Embedded browser pane and design mode (select an element and send it as context).
  - Acceptance: the browser pane loads a URL in an embedded web view. Design mode selects a DOM element and sends its selector, an HTML excerpt and its bounding box to the active session as context.
  - Verification: UI test on a local fixture page selects an element and asserts the context payload contains the selector and bounding box. The web view route is in the matrix in `docs/plans/ade-harness-plan.md`.
- [ ] **ADE-16**: MCP client: stdio and HTTP servers, a tool registry, enable and disable per session.
  - Acceptance: a stdio MCP server and an HTTP MCP server each register tools in one tool registry. Tools can be enabled or disabled per session. A disabled tool is not offered to the model.
  - Verification: integration tests with a fixture stdio server and a local HTTP fixture server. Assert the session tool list before and after a disable.
- [ ] **ADE-17**: Rules, skills and custom agent modes loaded from project files.
  - Acceptance: rule files, skill files and mode definitions in the project directory load into session context. A custom mode appears in the mode picker. A malformed file is reported as a diagnostic and skipped without failing the session.
  - Verification: loader tests on a fixture project directory that includes one malformed file. Assert the loaded set and the diagnostic.
- [ ] **ADE-18**: Codebase search: text and regex first, semantic index later.
  - Acceptance: text and regex search over the workspace returns file, line and match for each hit. An invalid regex is reported as an error. A semantic index is not part of this milestone.
  - Verification: unit tests on a fixture tree assert match count, file, and line. A test asserts an invalid regex yields an error result.
- [ ] **ADE-19**: Environment setup file for agent runs.
  - Acceptance: a setup file at the workspace root runs before an agent run starts. Its environment variables are visible to the run. A failing setup step marks the run failed before the agent starts.
  - Verification: integration test with a fixture setup file asserts the variable is visible inside the run. A second test with a failing step asserts the run is marked failed and no agent process starts.
- [ ] **ADE-20**: Export chats and runs to Markdown; usage history.
  - Acceptance: a chat or run exports to a Markdown file with its messages in order and a heading for each block. Usage history lists runs with date, model and token counts, and totals per period.
  - Verification: export of a fixture transcript is compared with a golden Markdown file. Usage history totals are asserted against fixture runs.
- [ ] **ADE-21**: VS Code extension compatibility: planned for a later milestone (v0.11.0 Phase 30), after XP-09 ships.
  - Acceptance: no extension compatibility code ships in Phases 26 to 29. In Phase 30 the extension host consumes the XP-09 contracts and ships only after the XP-09 contract tests are green.
  - Verification: Phase 30 plan review confirms the dependency on XP-09. The XP-09 contract tests are green before any extension work is merged.
- [ ] **ADE-22**: Admin, analytics and team controls: planned for a later milestone (v0.11.0 Phase 30).
  - Acceptance: team members have roles that gate admin actions, and a non-admin cannot perform one. Analytics show run counts and usage per team. Admin actions are recorded. Nothing ships before Phase 30.
  - Verification: Phase 30 authorization tests deny non-admin admin actions. Analytics totals are asserted against fixture data. The recording of admin actions is asserted in a test.

### Traceability

| REQ-ID | Phase | Milestone |
|--------|-------|-----------|
| XP-01 | 20, 25 | v0.10.0 Cross-platform UI |
| XP-02 | 22 | v0.10.0 Cross-platform UI |
| XP-03 | 22 | v0.10.0 Cross-platform UI |
| XP-04 | 22 | v0.10.0 Cross-platform UI |
| XP-05 | 24 | v0.10.0 Cross-platform UI |
| XP-06 | 24 | v0.10.0 Cross-platform UI |
| XP-07 | 23 | v0.10.0 Cross-platform UI |
| XP-08 | 20 | v0.10.0 Cross-platform UI |
| XP-09 | 20 | v0.10.0 Cross-platform UI |
| XP-10 | 21, 25 | v0.10.0 Cross-platform UI |
| XP-11 | 22, 24 | v0.10.0 Cross-platform UI |
| ADE-01 | 21 | v0.10.0 Cross-platform UI (parity surface) |
| ADE-02 | 21 | v0.10.0 Cross-platform UI (parity surface) |
| ADE-03 | 21 | v0.10.0 Cross-platform UI (parity surface) |
| ADE-04 | 26 | v0.11.0 ADE harness |
| ADE-05 | 26 | v0.11.0 ADE harness |
| ADE-06 | 26 | v0.11.0 ADE harness |
| ADE-07 | 26 | v0.11.0 ADE harness |
| ADE-08 | 26 | v0.11.0 ADE harness |
| ADE-09 | 27 | v0.11.0 ADE harness |
| ADE-10 | 27 | v0.11.0 ADE harness |
| ADE-11 | 27 | v0.11.0 ADE harness |
| ADE-12 | 27 | v0.11.0 ADE harness |
| ADE-13 | 29 | v0.11.0 ADE harness |
| ADE-14 | 28 | v0.11.0 ADE harness |
| ADE-15 | 29 | v0.11.0 ADE harness |
| ADE-16 | 28 | v0.11.0 ADE harness |
| ADE-17 | 28 | v0.11.0 ADE harness |
| ADE-18 | 28 | v0.11.0 ADE harness |
| ADE-19 | 28 | v0.11.0 ADE harness |
| ADE-20 | 29 | v0.11.0 ADE harness |
| ADE-21 | 30 | v0.11.0 ADE harness |
| ADE-22 | 30 | v0.11.0 ADE harness |
