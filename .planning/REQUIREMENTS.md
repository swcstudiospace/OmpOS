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
- Audit marks (2026-10-27): in the ADE, CX, VX and AL sections a box is checked only where `docs/quality/cursor-parity-audit-2026-10-26.md` rates the item MET or MET-LIVE against commit `d75fd88`. PARTIAL items stay unchecked and carry an audit note; DEFERRED and NOT-STARTED items stay unchecked. A checked box does not mean a live model turn ran: none was approved after Phase 11 (OD-07).

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
- [x] **TD-32**: The reconnect policy in the web head has a recorded closure: bounded reconnect with backoff implemented and tested, or the policy decision written down. Source: audit 04-surfaces WR-02 (`04-REVIEW.md:140`), line 102.
- [x] **TD-33**: Terminal SSE detection checks the event type, not only an event field. Source: audit WR-03 (`apps/OmpOS.Web/Services/SseEventStream.cs:157-158`), line 103.
- [x] **TD-34**: Seq dedup sets are bounded. Source: audit WR-04 (`src/OmpOS.EngineHost/RemoteEngineHost.cs:27,315-355`; `apps/OmpOS.Web/Services/SseEventStream.cs:62`), line 104.
- [x] **TD-35**: A2UI image URLs are restricted to an allowed scheme list. Source: audit WR-06 (`src/OmpOS.GenUi/A2uiSurfaceCatalog.cs:110-117`), line 105.
- [x] **TD-36**: `EngineEventMapper` maps unknown events to a fallback instead of throwing. Source: audit WR-07 (`apps/OmpOS.App/Components/EngineEventMapper.cs:56`), line 106.
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
  - Audit (2026-10-27): PARTIAL. Resume from the last Seq is not proven for the Avalonia head (it never reads the SSE stream); no model turn. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-01.
- [x] **ADE-02**: Tool approvals in the run UI, routed through the ACP permission bridge.
  - Acceptance: a pending ACP `session/request_permission` appears in the run UI with the tool name and arguments. Approve and deny send the decision through the permission bridge (OD-01). With no decision, the request is denied by policy. An approval can only deny earlier, never allow.
  - Verification: integration test with a fake ACP transport emits `session/request_permission`. A UI test asserts the approval control. Tests assert the bridge receives the chosen decision and that a timeout produces deny.
- [x] **ADE-03**: Steering and follow-up prompts while a run is active.
  - Acceptance: while a run is active the composer accepts a steering message, which is delivered to the active run, and a follow-up, which is queued and dispatched after the run completes. The run UI shows which kind is pending.
  - Verification: test with a fake active run. Assert the steering message reaches the run input channel and the follow-up is dispatched only after the completion event, in that order.
- [x] **ADE-04**: Side chats and transcript search.
  - Acceptance: a side chat opens from a session without adding messages to the main transcript. Transcript search returns every case-insensitive match in the current session, and selecting a match jumps to it.
  - Verification: unit tests of the search function on a fixed transcript fixture, asserting match count and order. UI test asserting side-chat messages do not appear in the main transcript.
- [ ] **ADE-05**: Agents window: parallel runs with status, filter and open or close.
  - Acceptance: the Agents window lists runs with a status of running, completed, failed or cancelled. It filters by status and opens or closes a run view. Closing a view does not stop the run. Two runs execute concurrently without sharing transcript state.
  - Verification: tests with two concurrent fake runs. Assert status transitions, filter results, and that closing a view leaves the run state unchanged.
  - Audit (2026-10-27): PARTIAL. The registry has no feeder; the rail and the Kanban tab list real sessions. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-05.
- [ ] **ADE-06**: Git worktree isolation per agent run.
  - Acceptance: each agent run executes in its own git worktree under a path the operator controls. Two runs on one repository never share a working directory. Changes stay inside the worktree, and the main checkout is unchanged by a run.
  - Verification: integration test on a temporary git repository. Start two runs, write a file in each, and assert separate worktrees (`git worktree list`) and an unchanged main checkout.
  - Audit (2026-10-27): PARTIAL. Wired into the hosted API only; the native head's runs use no worktree. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-06.
- [ ] **ADE-07**: Cloud agent runs on the hosted API, with status and reviewable output.
  - Acceptance: a run started in cloud mode executes through the hosted API (`services/OmpOS.Api`). Its status moves through queued, running and completed. After completion the diff and transcript are retrievable for review.
  - Verification: API integration test on the API test host starts a cloud run, polls status until completed, and fetches the output. The test asserts the diff is present in the output.
  - Audit (2026-10-27): PARTIAL. The contract and its tests exist; no live cloud run (OD-03, OQ-70). See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-07.
- [ ] **ADE-08**: Remote control and notifications from another device.
  - Acceptance: a second device lists active runs, sends a prompt or an approval, and receives a notification when a run needs approval or completes. Remote endpoints reject unauthenticated requests. Authentication uses the sign-in method chosen under OD-03.
  - Verification: API test asserts unauthenticated remote calls are rejected. End-to-end test asserts a second client receives the completion notification. This item is gated by the OD-03 decision.
  - Audit (2026-10-27): PARTIAL. Only the unauthenticated rejection is tested; no pairing and no notification (OD-03, OQ-68). See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-08.
- [x] **ADE-09**: Code editor: syntax highlighting, tabs and find.
  - Acceptance: the editor opens workspace files in tabs. It highlights C#, TypeScript, JSON, YAML and Markdown. Find shows the match count and moves between matches.
  - Verification: Avalonia UI tests open two files in tabs, assert the find match count on a fixture, and assert highlight tokens for each listed language.
- [ ] **ADE-10**: Workspace file tree and open folder.
  - Acceptance: opening a folder sets the workspace root used for runs. The file tree loads children when a directory is expanded and is fully keyboard navigable.
  - Verification: test on a fixture directory asserts the children of an expanded node. Test asserts that run dispatch uses the opened folder as its workspace root.
  - Audit (2026-10-27): PARTIAL. The tree is mounted; there is no open-folder action. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-10.
- [x] **ADE-11**: Diff review with accept and reject per hunk.
  - Acceptance: a run's changes are shown as hunks. Accepting a hunk keeps it, rejecting a hunk reverts only that hunk. After all decisions the file equals the accepted hunks applied to the original.
  - Verification: test on a fixture repository with a two-hunk change. Accept one hunk and reject the other, then assert the file content equals the expected result.
- [ ] **ADE-12**: Inline edit on a selection.
  - Acceptance: invoking inline edit on a selection sends the selected text and the instruction to the agent. The response replaces only the selected range, and only after the user accepts it. Undo restores the original text.
  - Verification: unit test of range replacement on a fixture buffer. UI test asserts a rejected response leaves the buffer unchanged.
  - Audit (2026-10-27): PARTIAL. The inline edit view is unbound and its client has no production caller. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-12.
- [ ] **ADE-13**: Inline completion backed by a configured model provider.
  - Acceptance: with a model provider configured, a suggestion appears at the caret and accepting it inserts the text. Without a configured provider, completion is off and no request is sent.
  - Verification: test with a stub provider endpoint asserts a request is sent only when a provider is configured. Accept and dismiss behaviour is asserted in a UI test.
  - Audit (2026-10-27): PARTIAL. The completion client is never built in the application (OQ-55). See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-13.
- [x] **ADE-14**: Integrated terminal; commands need approval; auto-run is opt-in.
  - Acceptance: the terminal starts in the workspace directory. Each command needs approval. Auto-run is off by default and is enabled only by explicit configuration. A declined command is not executed.
  - Verification: tests with a fake process runner assert that a command with default settings stays pending until approved, that auto-run executes without a prompt only when enabled, and that a declined command never spawns a process.
- [ ] **ADE-15**: Embedded browser pane and design mode (select an element and send it as context).
  - Acceptance: the browser pane loads a URL in an embedded web view. Design mode selects a DOM element and sends its selector, an HTML excerpt and its bounding box to the active session as context.
  - Verification: UI test on a local fixture page selects an element and asserts the context payload contains the selector and bounding box. The web view route is in the matrix in `docs/plans/ade-harness-plan.md`.
- [ ] **ADE-16**: MCP client: stdio and HTTP servers, a tool registry, enable and disable per session.
  - Acceptance: a stdio MCP server and an HTTP MCP server each register tools in one tool registry. Tools can be enabled or disabled per session. A disabled tool is not offered to the model.
  - Verification: integration tests with a fixture stdio server and a local HTTP fixture server. Assert the session tool list before and after a disable.
  - Audit (2026-10-27): PARTIAL. The registry is empty in the application and the gateway is not wired (ADR-0010, ADR-0014). See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-16.
- [ ] **ADE-17**: Rules, skills and custom agent modes loaded from project files.
  - Acceptance: rule files, skill files and mode definitions in the project directory load into session context. A custom mode appears in the mode picker. A malformed file is reported as a diagnostic and skipped without failing the session.
  - Verification: loader tests on a fixture project directory that includes one malformed file. Assert the loaded set and the diagnostic.
  - Audit (2026-10-27): PARTIAL. Rules, skills and modes are loaded and listed but not fed to a session. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-17.
- [x] **ADE-18**: Codebase search: text and regex first, semantic index later.
  - Acceptance: text and regex search over the workspace returns file, line and match for each hit. An invalid regex is reported as an error. A semantic index is not part of this milestone.
  - Verification: unit tests on a fixture tree assert match count, file, and line. A test asserts an invalid regex yields an error result.
- [ ] **ADE-19**: Environment setup file for agent runs.
  - Acceptance: a setup file at the workspace root runs before an agent run starts. Its environment variables are visible to the run. A failing setup step marks the run failed before the agent starts.
  - Verification: integration test with a fixture setup file asserts the variable is visible inside the run. A second test with a failing step asserts the run is marked failed and no agent process starts.
  - Audit (2026-10-27): PARTIAL. The setup runner has no caller (OQ-54). See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-19.
- [ ] **ADE-20**: Export chats and runs to Markdown; usage history.
  - Acceptance: a chat or run exports to a Markdown file with its messages in order and a heading for each block. Usage history lists runs with date, model and token counts, and totals per period.
  - Verification: export of a fixture transcript is compared with a golden Markdown file. Usage history totals are asserted against fixture runs.
  - Audit (2026-10-27): PARTIAL. Chat export only; the usage history is always empty. See `docs/quality/cursor-parity-audit-2026-10-26.md` row ADE-20.
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
| AL-01 | 44 | v0.14.0 Agent loop and Agent Panel |
| AL-02 | 44 | v0.14.0 Agent loop and Agent Panel |
| AL-03 | 44 | v0.14.0 Agent loop and Agent Panel |
| AL-04 | 44, 46 | v0.14.0 Agent loop and Agent Panel |
| AL-05 | 45 | v0.14.0 Agent loop and Agent Panel |
| AL-06 | 45 | v0.14.0 Agent loop and Agent Panel |
| AL-07 | 45, 46, 49 | v0.14.0 Agent loop and Agent Panel (reworded 2026-10-26: live delivery is the requirement, queue after the turn is the fallback) |
| AL-08 | 45 | v0.14.0 Agent loop and Agent Panel |
| AL-09 | 46, 47 | v0.14.0 Agent loop and Agent Panel (reworded 2026-10-26: default Cursor-style Agent Panel, no inspector; Phase 47 deferred) |
| AL-10 | 46 | v0.14.0 Agent loop and Agent Panel |
| AL-11 | 46 | v0.14.0 Agent loop and Agent Panel (reworded 2026-10-26: plugin connection changes in the header row) |
| AL-12 | 39, 46 | v0.14.0 Agent loop and Agent Panel (reworded 2026-10-26: screenshots without the inspector, plus the Kanban tab states) |
| AL-13 | 48 | v0.14.0 Agent loop and Agent Panel (added 2026-10-26) |
| AL-14 | 48 | v0.14.0 Agent loop and Agent Panel (added 2026-10-26) |
| AL-15 | 46, 49 | v0.14.0 Agent loop and Agent Panel (added 2026-10-26) |
| AL-16 | 49 | v0.14.0 Agent loop and Agent Panel (added 2026-10-26) |
| AL-17 | 39 | v0.13.0 Cursor 1:1 workspace and provider platform, Kanban tab (added 2026-10-26) |
| AL-18 | 39 | v0.13.0 Cursor 1:1 workspace and provider platform, Kanban tab (added 2026-10-26) |
| AL-19 | 39, 40, 46 | v0.13.0 and v0.14.0 (added 2026-10-26; tokens and licence stay VX-01, Phase 36) |
| AL-20 | 35 | v0.12.0 Cursor parity and quality bar (added 2026-10-26; tracked by the existing inventories) |
| AL-21 | 50 | v0.14.0 (added 2026-10-26; Dark default theme and theme choice) |
| AL-22 | 50 | v0.14.0 (added 2026-10-26; control library, density and layouts) |
| AL-23 | 50 | v0.14.0 (added 2026-10-26; static Greptile faces) |

## Cursor parity and quality bar (CX, v0.12.0, planned 2026-10-22)

Source: user request 2026-10-22 and the ultrathink plan graph ut-mvirx2k5-37cf010f. Cursor references are primary sources checked 2026-10-22. Not counted in v0.9.2 completion.

- [ ] **CX-01**: Plugin manifests are validated against a versioned schema before any code runs, with an API version gate and quarantine of failing plugins.
  - Acceptance: a malformed or hostile manifest is rejected with a specific error and runs no code; an incompatible API version is rejected naming the plugin and the supported range; a plugin that throws asynchronously is quarantined with a persisted reason, and the host keeps running.
  - Verification: gate tests with a sentinel-writing fixture, an incompatible-version fixture and an async-throwing fixture (Phase 31).
  - Audit (2026-10-27): PARTIAL. Library and tests only; no head loads a manifest (OQ-84). See `docs/quality/cursor-parity-audit-2026-10-26.md` row CX-01.
- [ ] **CX-02**: Hooks run user commands on lifecycle events only after consent for the exact command.
  - Acceptance: a hook runs only after consent recorded against a hash of its exact command; editing the command forces new consent; each run records exit status, duration and truncated output; a failing hook does not block other hooks unless declared blocking.
  - Verification: gate test with zero spawned processes before consent; list, enable, disable and reorder tests (Phase 31). Cursor reference: https://cursor.com/docs/hooks (labelled beta in https://cursor.com/changelog/1-7; exit code 2 blocks an action, other failures let it proceed).
  - Audit (2026-10-27): PARTIAL. Library, hooks view and tests only; no head wires them (OQ-84). See `docs/quality/cursor-parity-audit-2026-10-26.md` row CX-02.
- [ ] **CX-03**: Automations run agent work on event, schedule or manual triggers, with guards that fail closed.
  - Acceptance: an automation that triggers itself is blocked at depth one and the block is recorded; chain depth is capped at 3; runs are rate limited per window and stop at a maximum runtime; an exception in the guard store refuses the run; consent is required before the first run.
  - Verification: gate tests with a fake clock and a fault-injected guard store (Phase 32). Cursor reference: https://cursor.com/docs/cloud-agent/automations.
  - Audit (2026-10-27): PARTIAL. The engine and guards pass their tests; no head composes the engine and no view exists. See `docs/quality/cursor-parity-audit-2026-10-26.md` row CX-03.
- [ ] **CX-04**: The embedded terminal opens an omp session in the project, and the app UI and the omp CLI read one plugin and MCP registry.
  - Acceptance: omp is launched with an argument array and the project directory; a missing omp shows its detected path, version and install guidance; one registry with a generation and checksum backs both views; omp's MCP connections go through the host-owned MCP gateway chosen 2026-10-22, which refuses ungranted tools.
  - Verification: gate tests with a fake executable; registry generation tests; a gateway refusal test (Phase 33).
  - Audit (2026-10-27): PARTIAL. The terminal part is met; the registry and the gateway are wired into no head. See `docs/quality/cursor-parity-audit-2026-10-26.md` row CX-04.
- [ ] **CX-05**: Remote development connects to a host over an authenticated, encrypted transport, with host-identity verification.
  - Acceptance: host identity is verified before the first connection; a changed key is refused with no auto-accept path; the disconnected state says local development remains available.
  - Verification: deferred, blocked on OD-06 (verified remote omp endpoint and WebSocket upgrade authentication) and the owner's protocol and host-key decision. No Cursor primary docs page for SSH remote development was found (https://cursor.com/docs/remote-ssh.md and https://cursor.com/docs/remote-development return 404) (Phase 34).
  - Audit (2026-10-28): PARTIAL. Host-identity verification and reconnect are built for ssh stdio (ADR-0015, PX-15) and a live loopback `sshd` run opened a session; no prompt turn (OD-07), no real network host, no live drop, and no Windows head.
- [ ] **CX-06**: The codebase meets a state-of-the-art, enterprise and production-ready bar, checked by a repository-wide review in which every finding is fixed or recorded.
  - Acceptance: every finding has a disposition (fixed, or recorded with an owner and an open-question link); no finding is silently suppressed.
  - Verification: findings table in the Phase 35 record.
  - Audit (2026-10-27): PARTIAL. The Phase 25.1 register and its fix phases exist; no per-file review of the final tree. See `docs/quality/cursor-parity-audit-2026-10-26.md` row CX-06.
- [x] **CX-07**: Quality gates are enforced in CI and in bootstrap: analyzers, warnings as errors, formatter, architecture tests and secret scanning.
  - Acceptance: each gate fails the build on violation; every suppression has a row in docs/open-questions.md.
  - Verification: gate run at Phase 35 HEAD.
- [x] **CX-08**: Decision records exist for the editor, terminal, MCP, browser pane and plugin isolation choices.
  - Acceptance: each record names the candidate chosen, its licence (SPDX, with its source URL), its verification route and its open risks.
  - Verification: docs/decisions listing (Phase 35).

## Vision and workspace platform (VX, v0.13.0, planned 2026-10-22)

Source: user request 2026-10-22 and ultrathink plan graph ut-mvjvg4gj-fb21d80f. Verdicts per research row are in `docs/plans/vision-research/`. Not counted in v0.9.2 completion.

- [ ] **VX-01**: One Avalonia token set provides White and Purple roles in light and dark variants, with OFL typography resources and one spacing and one radius scale; no view hard-codes a colour.
  - Acceptance: every foreground and background pair in both themes meets WCAG AA (4.5:1 for text, 3:1 for non-text); the Greptile families (DM Sans, Anybody, Space Mono) ship unmodified with their OFL notice; the owner signs off the rendered look.
  - Verification: contrast test in the gate; licence files in the bundle (Phase 36). Research: `docs/plans/vision-research/greptile-font.md`.
  - Audit (2026-10-27): PARTIAL. The owner's sign-off of the rendered look is missing. See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-01.
- [ ] **VX-02**: One slash command registry declares each command's name, arguments, description, platform availability and handler; commands appear only with a verified reference.
  - Acceptance: an empty query lists all commands; an unknown command shows a suggestion; a command that needs an unavailable capability is shown disabled with its reason; the catalogue records the Cursor reference version and date.
  - Verification: palette and registry tests (Phases 36, 38). Research: `docs/plans/vision-research/cursor-slash-commands.md` (86 tokens: 44 in the CLI reference table, 21 in Agent chat and the Agents Window, 21 in the changelog).
  - Audit (2026-10-27): PARTIAL. The palette lists omp's commands; the OmpOS catalogue holds one command of 86 and a chat session does not list it. See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-02.
- [x] **VX-03**: The chat window streams session output as distinct blocks with loading, empty, streaming, error and disconnected states; cancel stops a response without stopping the session.
  - Acceptance: text, tool call, tool result and file edit render as separate blocks; an error keeps the transcript and offers retry; reconnect after network loss does not duplicate blocks.
  - Verification: UI and replay tests (Phase 38).
- [x] **VX-04**: The Kanban board shows one task card per chat, with status taken from runtime events and never from client-side guesses.
  - Acceptance: runtime statuses are read-only; user-settable statuses accept drag; a stale heartbeat shows an indicator with a screen-reader label; updates arrive without a reload.
  - Verification: gate tests from fake session events (Phases 36, 39). Column names await owner confirmation (default recorded in CONTEXT).
- [x] **VX-05**: The settings window and model provider registry configure local, custom, cloud, API key and coding plan entries without code changes.
  - Acceptance: Runtime settings validate floors and ceilings; a custom provider needs no code change; a coding plan is a separate entry with its own auth, quota display and error mapping; remote plain http is refused unless explicitly allowed.
  - Verification: settings and catalogue tests (Phase 40). Research: `docs/plans/vision-research/providers-local-us.md` and `providers-eu-cn.md`.
- [ ] **VX-06**: Agent sessions run under herdr, which owns process lifetime; the client attaches, detaches and observes, and detach never terminates a session.
  - Acceptance: sessions persist locally and reattach after a client restart; heartbeat every 15 seconds, stale after three missed beats; a herdr server restart or machine reboot keeps no processes, so restored sessions resume from the stored native omp session reference (owner decision pending in STATE).
  - Verification: recorded-transcript fake tests (Phases 36, 37); the real endpoint is verified only after the owner decision. Research: `docs/plans/vision-research/herdr.md`.
  - Audit (2026-10-27): PARTIAL. herdr has a fake only and no product consumer (OQ-76). See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-06.
- [ ] **VX-07**: The omp engine is reached through a versioned adapter that emits typed events; no UI code depends on omp output formats.
  - Acceptance: replaying a recorded fixture twice yields the same typed event sequence; cancel leaves the session running; omp launch-time provider configuration is used only once verified.
  - Verification: fixture replay tests (Phases 36, 37).
  - Audit (2026-10-27): PARTIAL. `Harness.Agent` is referenced by no product project; the head uses ACP. See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-07.
- [ ] **VX-08**: Every head offers a terminal that matches its platform: a local PTY on desktop, a remote-attached terminal on mobile and web, and a Linux environment per cloud session on Railway.
  - Acceptance: desktop spawns a local shell per OS; mobile and web never spawn a local shell and show the local terminal as unavailable with a reason; the cloud environment is a persistent container with idle sleep (owner decision 2026-10-22) and restores its transcript on reattach.
  - Verification: per-target build checks and UI tests (Phases 37, 42, 43). Research: `docs/plans/vision-research/railway-cloud-desktop.md`; remote desktops need Railway's written confirmation before production.
  - Audit (2026-10-27): PARTIAL. The Linux PTY is met; no Windows or macOS backend, and mobile and web terminals are models only (OQ-69, OQ-74). See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-08.
- [ ] **VX-09**: A pets companion reflects session state at a glance, never blocks the chat or terminal, and respects reduced motion and a hide toggle.
  - Acceptance: hidden, idle, working, attention-needed and fallback states each have a text equivalent; reduced motion disables animation; 3D is built only after a per-target spike passes, and 2D is the default.
  - Verification: state and UI tests (Phase 41). Research: `docs/plans/vision-research/pets-feasibility.md`; the appearance needs owner sign-off.
  - Audit (2026-10-27): PARTIAL. State model only; no companion view (OQ-72). See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-09.
- [ ] **VX-10**: Provider keys live only in the platform secure store on desktop and mobile, or in session memory on web, and never reach logs, transcripts, crash output or telemetry.
  - Acceptance: a canary key injected into each sink is absent from every sink in the gate; web keys are cleared on reload; no key is written to browser storage.
  - Verification: canary harness tests (Phase 36); browser-run check (Phase 43). Web key storage (vault or per-session entry) is an owner decision; the default is session-only memory.
  - Audit (2026-10-27): PARTIAL. Keys sit in an owner-only file and in omp's store, not a platform secure store (OQ-92); no web key-entry page. See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-10.

## Agent loop and Agent Panel (AL, v0.14.0, planned 2026-10-26)

Source: user request 2026-10-26 and ultrathink plan graph ut-mvp0bh59-2dd6ef4c. Evidence: `docs/plans/agent-loop/`. Not counted in v0.9.2 completion. Revised 2026-10-26 from the user message of that date (ultrathink plan graph ut-mvp88pnw-6e8c4d24): AL-07, AL-09, AL-11 and AL-12 are reworded in place and AL-13 to AL-20 are added; the decisions DR-1 to DR-6 are recorded in `.planning/ROADMAP.md` (milestone v0.14.0).

- [x] **AL-01**: claude-ultrathink, agent-swarm and agent-substrate are git submodules of the repository, referenced by https remote URL and pinned to commits that exist on their remotes.
  - Acceptance: `.gitmodules` holds no local path; a fresh clone with `--recurse-submodules` restores them; the update procedure and each pin are documented; no sibling source is copied or edited.
  - Verification: gate test over `.gitmodules` and the gitlinks; fresh-clone run (Phase 44).
- [x] **AL-02**: GSD-Core is an omp plugin owned by this repository that reports its health and degrades without GSD-Core.
  - Acceptance: the probe prints one JSON object and exits 0, 2 or 3; the plugin copies no GSD file; its agents are linked by absolute symlink and never overwrite a user agent.
  - Verification: node tests and recorded probe output (Phase 44).
- [x] **AL-03**: OmpOS launches `omp acp` with the four plugins loaded and nothing from the user's global omp configuration.
  - Acceptance: the argument order, the environment and the MCP server list are produced by a pure planner and applied by the ACP transport; tokens appear only as environment variable names; per-session state directories isolate the orchestrator.
  - Verification: planner and transport tests against a fake omp; a recorded real handshake (Phase 44).
- [x] **AL-04**: A missing or uninitialised plugin is reported with an actionable message and never crashes the application.
  - Acceptance: `SubmoduleNotInitializedException` names the plugin, the path and the exact fix; the panel shows the plugin as unavailable and the session runs as plain omp.
  - Verification: unit tests (Phase 44), panel state tests (Phase 46).
- [x] **AL-05**: The stages of the loop (planner, orchestrator, substrate, workflow engine) are observable as stage events with degradation.
  - Acceptance: each observer maps recorded fixtures; unknown fields are ignored; a malformed or absent input is a Degraded or Unavailable event; cancellation leaves no child process.
  - Verification: fixture, degraded-path and cancellation tests (Phase 45).
- [x] **AL-06**: Several long-running sessions are kept in a catalog that survives an application restart.
  - Acceptance: atomic versioned persistence; a corrupt or unknown-version file is quarantined and treated as empty; a session running at shutdown is Lost on the next start and can be resumed from its stored reference.
  - Verification: persistence and restart tests (Phase 45).
- [ ] **AL-07**: The operator can steer a running session, and the steering is recorded.
  - Acceptance: ~~a message sent during a run queues as a steer or a follow-up and is delivered over ACP after the active turn in send order;~~ Superseded 2026-10-26 (DR-4: omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so prompting over ACP cannot deliver live; live delivery is the requirement, queue after the turn is the documented fallback): a steer sent during a run is delivered live into the running session (AL-15) and a follow-up is delivered after the active turn in send order; where live delivery is not available the steer queues after the active turn, is shown as queued and is never reported as delivered live; the substrate records it as a `note` event with `payload.action` `steer` when reachable; OmpOS never claims the substrate delivered it.
  - Verification: presenter tests and fake-gateway tests (Phases 45, 46); live delivery tests with a fake omp (Phase 49).
  - Audit (2026-10-27): PARTIAL. An injected delivery into a running turn is not verified live (OD-07, OQ-81). See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-07.
- [x] **AL-08**: The task context of a session (Kanban status, workflow phase, shared memory, lessons) is readable and scoped to the session.
  - Acceptance: parts that cannot be read are listed as warnings; text is plain and length capped; one session never reads another's state.
  - Verification: scope-isolation tests (Phase 45).
- [x] **AL-09**: ~~The application opens on the Agent Panel, a Cursor-class agent surface.~~ Superseded 2026-10-26 (DR-1): The application opens on the default Cursor-style Agent Panel (the session rail as the Agents sidebar, the thread and the composer); no second agent surface exists.
  - Acceptance: ~~session rail, thread, pipeline strip, composer with Send, Steer, Follow-up and Stop, and a collapsible inspector; every existing tool remains reachable.~~ Superseded 2026-10-26 (DR-1: no inspector and no separate pipeline strip surface): session rail, thread and composer with Send, Steer (send now), Follow-up (queue) and Stop, with the header chips and the one-line plugin connection row directly under the header; no region other than these exists and there is no inspector; every existing tool remains reachable.
  - Verification: headless UI test that no region other than rail, thread and composer (with its header) exists, and a recorded screenshot (Phase 46). The web head parity is Phase 47, deferred.
- [x] **AL-10**: Several sessions run concurrently, each with its own engine and status.
  - Acceptance: selecting a session rebinds the thread, composer and approvals; each row shows its own status and last activity; closing a session stops its engine.
  - Verification: headless test with two scripted sessions (Phase 46).
- [x] **AL-11**: The panel is operable by keyboard and by screen reader and honours reduced motion.
  - Acceptance: every tab stop has an accessible name; the documented keyboard map works; ~~stage changes use a polite live region~~ Superseded 2026-10-26 (DR-1: the stage strip is the one-line plugin connection row): plugin connection changes use a polite live region and errors an assertive one; new colours pass the contrast tests.
  - Verification: the accessibility walk over the opened panel and the contrast tests (Phase 46).
- [ ] **AL-12**: Every panel state has its own copy and screenshot evidence.
  - Acceptance: empty, initialising, streaming, error with retry, success, each degraded mode and a resumed session; ~~eight screenshots recorded.~~ Superseded 2026-10-26 (DR-1: the inspector is gone): eight screenshots of the default panel recorded without an inspector (empty, streaming, degraded, error, long-running list, narrow window, dense list, keyboard focus), plus one screenshot per Kanban tab state (loading, empty, error, populated, disconnected).
  - Verification: one test per state and the evidence files in `docs/runbooks/evidence/` (Phases 39, 46).
  - Audit (2026-10-27): PARTIAL. 2 of the 8 panel states and the populated and empty Kanban states have screenshots. See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-12.
- [x] **AL-13**: Ultrathink, Substrate, Swarm and GSD-Core are connected to every omp process the application starts, the engine session behind the Agent Panel and each omp terminal, each loaded exactly once from the pinned submodules.
  - Acceptance: the launch planner plans an interactive omp (terminal) launch as well as `omp acp`, with the same plugin set, per-session state directory and environment; the embedded terminal starts omp through the planner with an argument array and the project directory; with the user's own omp configuration present (global `config.yml` extensions and extension symlinks) each plugin is registered once, with unique command names; a missing omp, a missing plugin or an uninitialised submodule shows its fix text while the rest of the application works; if omp cannot be made to load each plugin exactly once, the fallback is recorded in an open-question row.
  - Verification: planner tests; gate test with a fake executable; a recorded no-model-turn run against a sandbox with a polluted global configuration (Phase 48).
- [x] **AL-14**: The connection of each plugin, and of all four together, is verified by a recorded run, and every connection failure is shown to the user.
  - Acceptance: for each of Ultrathink, Substrate, Swarm and GSD-Core: the plugin is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it (command output, tool calls, stage chips); a load or connect failure is shown with its fix text; starting a session from the application and opening an omp terminal for it shows all four plugins connected.
  - Verification: per plugin, recorded run output (commands registered, plugin-specific probe) and a degraded-path test; the end-to-end run recorded in the runbook under `docs/runbooks/evidence/` with the live part tagged Live and excluded from the gate (Phase 48).
- [ ] **AL-15**: A running session can be live-steered from the Agent Panel composer and from its Kanban card.
  - Acceptance: a steering message sent with send now from the composer or from the card is taken into account by the session before the turn ends; a follow-up queues for the end of the turn; the steering history shows delivered, queued or failed truthfully; the substrate records each steering action as a `note` event with `payload.action` `steer`, and delivery is never attributed to the substrate. The delivery mechanism is an open question with a default recorded in `docs/open-questions.md`.
  - Verification: tests with a fake omp that records the delivery, fake-gateway tests, and a recorded live run when a model turn is approved (Phases 46, 49).
  - Audit (2026-10-27): PARTIAL. No recorded live run into a running turn, and no view lists the steering history (OD-07). See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-15.
- [x] **AL-16**: Steering that cannot be delivered fails clearly and leaves the session unchanged.
  - Acceptance: steering a session that ended, disconnected or was never started shows a clear message, leaves the session state unchanged and keeps the message in the composer.
  - Verification: gate tests (Phase 49).
- [ ] **AL-17**: The Kanban tab shows each opened session as exactly one task card.
  - Acceptance: the tab is reachable from the application navigation; each opened session (Agent Panel chat or omp terminal) has one card; opening creates it and closing or ending moves it to its terminal column without removing it silently; card status mirrors the session lifecycle status; selecting a card opens or focuses its session; status updates live without a refresh; a card shows title, model and provider, last activity, status, stale indicator and, when Substrate is reachable, task, graph, lessons count and lease holder, and without Substrate these show as unavailable, never invented.
  - Verification: gate tests from fake session events, tests with a fake Substrate gateway reachable and unreachable, and a headless UI test (Phase 39).
  - Audit (2026-10-27): PARTIAL. The lease holder is always unavailable. See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-17.
- [ ] **AL-18**: The Kanban tab renders every state, is accessible and updates live without breaking reduced motion.
  - Acceptance: loading skeleton, empty with a way to start a session, error with retry, populated, and disconnected (terminal dropped) distinct from failed; keyboard navigation with visible focus; labels exposed to assistive technology; status never conveyed by colour alone; live updates honour reduced motion; runtime statuses are not draggable and only Backlog and Archived accept drag; a card can steer its session (AL-15).
  - Verification: one UI test per state, the accessibility walk over the tab and the evidence screenshots (Phase 39).
  - Audit (2026-10-27): PARTIAL. Screenshots exist for populated and empty only. See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-18.
- [x] **AL-19**: The Greptile font (DM Sans body, Anybody display, Space Mono code) is used consistently across the application.
  - Acceptance: every text control in the Agents layout, the Kanban tab, the Settings window and the terminal chrome resolves its family through the `OmpOS.Font.*` tokens, with no literal family in AXAML; the recorded screenshots show DM Sans.
  - Verification: a headless test walking those surfaces and the recorded screenshots (Phases 39, 40, 46); the token set and licence notice stay VX-01 (Phase 36).
- [ ] **AL-20**: The application is a full Cursor desktop clone in UI, capabilities and settings, tracked by the existing inventories, with no addition beyond the Kanban tab and the expanded provider settings.
  - Acceptance: parity is tracked by ADE-01 to ADE-22, CX-01 to CX-08, VX-01 to VX-10 and the 34-row table in `docs/plans/vision-research/cursor-settings-window.md`; no new inventory is created; the only additions beyond Cursor parity are the Kanban tab (AL-17, AL-18) and the expanded provider settings (VX-05); no plugin enable or disable setting exists until the user confirms.
  - Verification: the Phase 35 audit of the final tree against those inventories (Phase 35 criterion 4).
  - Audit (2026-10-27): PARTIAL. The audit exists; the clone is not complete (audit section 4). See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-20.
- [x] **AL-21**: The application starts in the Dark theme and Settings > General offers System, Light and Dark; the choice persists.
  - Acceptance: `RequestedThemeVariant` is Dark by default; the General page saves System, Light or Dark and the running window follows it; views that build colours in code re-read the tokens on a change; every text and non-text colour pair meets its contrast requirement in both variants.
  - Verification: theme preference tests, `ThemeContrastTests` in both variants, and the screenshots in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` and `after-light/` (Phase 50).
- [x] **AL-22**: Controls, density and layouts follow the design specification `docs/plans/ui-parity/cursor-agents-window-parity.md` and keep the accessibility contract.
  - Acceptance: buttons, inputs, toggles, tabs, list rows, chips, scrollbars, tooltips and menus are styled from OmpOS tokens; desktop controls use 32 px targets and none is smaller than 24 by 24 px; the skip link is hidden until it has focus; the rail, thread, composer, Kanban tab, omp terminal and Settings window follow section 4 of the specification; x:Name values, accessible names, tab order, the focus ring, reduced motion and the keyboard map are unchanged.
  - Verification: control style, density and skip link tests, `KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, and the screenshot set (Phase 50).
- [x] **AL-23**: The Greptile faces are bundled as static instances and used by role: DM Sans for interface text, Anybody for display headings of 20 px and above only, Space Mono for code and identifiers.
  - Acceptance: the font files and their licence texts are in the application assets; no Thin weight renders; every `OmpOS.Font.Display` use is 20 px or larger and semibold or heavier; no literal family exists in AXAML (AL-19).
  - Verification: `FontResourceTests`, a typography markup test and the recorded screenshots (Phase 50).

## Parity and publication (PX, v0.15.0, planned 2026-10-27; audited 2026-10-28)

Source: the user request of 2026-10-27 and the research in `docs/plans/v0.15.0-research/`. Every requirement names its phase; verification follows the gate rules of the earlier milestones (no live model turn, no external account action without a record).

- [x] **PX-01**: The default look is White and Purple.
  - Acceptance: a new installation starts in the Light theme (white surfaces, purple `#6d28d9`) in the desktop and the web head; System, Light and Dark remain choices and persist; every colour pair keeps its contrast in both variants. Supersedes the Dark default of AL-21 (OQ-94).
  - Verification: theme preference tests (default Light), `ThemeContrastTests`, screenshots in both themes (Phase 51).
  - Audit (2026-10-28): MET. Light default on desktop, Android and web; Dark and System persist; `DefaultThemeTests`, `ThemeSwitchTests`. The owner has not signed off the look (OQ-94 default). See `.planning/phases/51-brand-theme-and-licence/51-VERIFICATION.md`.
- [x] **PX-02**: OmpOS has a brand set built from the Greptile typefaces.
  - Acceptance: mark, wordmark lockup, banner (1280 by 320), social preview (1280 by 640, under 1 MB), favicons and desktop and Android icons exist under `assets/brand/` with the script that builds them; text is outlined from the bundled faces; the heads and the README use them.
  - Verification: a script test of sizes and dimensions and review by eye (Phase 51).
  - Audit (2026-10-28): MET. Brand set and `check-brand.sh` run 2026-10-28; the images were reviewed by the builder only; `.icns`, `.ico` and Android adaptive icon not seen in their native shells. See `.planning/phases/51-brand-theme-and-licence/51-VERIFICATION.md`.
- [x] **PX-03**: The repository is licensed AGPL-3.0-or-later.
  - Acceptance: `LICENSE` holds the canonical text; `NOTICE` and `THIRD-PARTY-NOTICES.md` account for every shipped package, font and bundled asset; package metadata carries the SPDX expression; the README states it; OQ-93 records the reading of "AGL 3.0" and closes OQ-62.
  - Verification: a licence check script run in the gate (Phase 51).
  - Audit (2026-10-28): MET with a caveat. Files, SPDX and README are in place; `check-licences.sh` is in the gate and passed on e24e6851 after the notices were regenerated; GitHub detects AGPL-3.0; `LICENSE` equals the constant the check compares (sha256 `0d96a4ff...`), but was not compared with gnu.org (UNVERIFIED). See `.planning/phases/51-brand-theme-and-licence/51-VERIFICATION.md`.
- [ ] **PX-04**: The network source offer of AGPL section 13 is met on every surface.
  - Acceptance: `GET /source` is anonymous and returns the licence, the repository URL and the commit; the web footer and About page and the desktop About show them.
  - Verification: API integration test, bUnit test, headless view test (Phase 51).
  - Audit (2026-10-28): PARTIAL. API `GET /source` and desktop About met; web footer offers the source repository root and version, no web About page and no revision link. See `.planning/phases/51-brand-theme-and-licence/51-VERIFICATION.md`.
- [x] **PX-05**: The GitHub repository is presented properly.
  - Acceptance: description, homepage, topics, detected licence, security reporting, secret scanning and Dependabot security updates are set and recorded; the social preview is prepared with its manual step.
  - Verification: `gh api` read-back saved as evidence (Phase 52).
  - Audit (2026-10-28): MET with caveats. Description, homepage, topics, detected licence (AGPL-3.0, key `agpl-3.0`), private vulnerability reporting, secret scanning with push protection and Dependabot security updates are applied and read back after the push (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`); the social preview image is ready and its upload is an owner step (OQ-170). See `.planning/phases/52-repository-release-and-community-health/52-VERIFICATION.md`.
- [x] **PX-06**: The standard community files exist.
  - Acceptance: CONTRIBUTING (with the DCO rule), CODE_OF_CONDUCT, SECURITY, SUPPORT, CODEOWNERS, issue forms, a pull request template, `dependabot.yml`, release notes configuration and labels.
  - Verification: community profile check and YAML validity tests (Phase 52).
  - Audit (2026-10-28): MET with caveats. All community files exist and parse; the community profile health is 100 (recorded 2026-10-28, `read-back.json`) while its `files.issue_template` is `null` for the three issue forms (UNVERIFIED why); no test in the suite parses the `.github` YAML (a one-off PyYAML parse of nine files passed). See `.planning/phases/52-repository-release-and-community-health/52-VERIFICATION.md`.
- [x] **PX-07**: The changelog and the release are in place.
  - Acceptance: `CHANGELOG.md` in Keep a Changelog form from v0.9.1 to v0.15.0; one version source; tag and GitHub Release v0.15.0.
  - Verification: changelog check script, `gh release view` (Phases 52 and 65).
  - Audit (2026-10-28): MET with caveats. `CHANGELOG.md` has 8 sections (check script valid), `Directory.Build.props` holds one version, and the annotated tag `v0.15.0` and the GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 exist (the `release` workflow run 40758275112 succeeded in 31 s, recorded 2026-10-28). The tags of v0.9.1 to v0.14.0 do not exist, so those compare links 404 (OQ-108). See `.planning/phases/52-repository-release-and-community-health/52-VERIFICATION.md`.
- [x] **PX-08**: The web head is styled in White and Purple with self-hosted Greptile faces.
  - Acceptance: no Bootstrap; tokens mirror the desktop; woff2 fonts with their OFL texts; reduced motion; theme boot script; the shell has a top bar, rail, thread region and a drawer below 840 px.
  - Verification: CSS contract tests, `AccessibilityTests`, bUnit tests, smoke (Phase 53).
  - Audit (2026-10-28): MET. Tokens, self-hosted woff2 with OFL texts, theme boot and shell exist in the tree; contract and shell tests inside the passing UI and Web runs and the gate on `e24e6851`; the gate's Demo smoke ran 68 axe runs with 0 violations. See `.planning/phases/53-web-head-foundation-and-api-surface/53-VERIFICATION.md`.
- [ ] **PX-09**: The web head offers the desktop's working surfaces.
  - Acceptance: thread header with the plugin connection row, the block views, composer chips and slash palette, steering, approvals, Changes bar, Kanban, Settings, omp terminal, Cloud and Remote views.
  - Verification: bUnit tests per component and state; smoke v2 with axe on every screen in both themes at three widths (Phase 54).
  - Audit (2026-10-28): PARTIAL. The working surfaces except Cloud and Remote views are built and tested (UI 595 passed); Kanban moves by buttons, not drag and drop; terminal needs a stream endpoint. The gate's Demo smoke passed (68 axe runs, 0 violations); still no Cloud or Remote view in the web workbench (recorded 2026-10-28). See `.planning/phases/54-web-agent-panel-and-workspace-screens/54-VERIFICATION.md`.
- [x] **PX-10**: The API serves the endpoints the screens need.
  - Acceptance: catalog, stages, context, steering, config options, commands, changes, board, lifecycle, workspace, settings, providers (write-only credentials), hooks, automations, files, terminal stream and cloud endpoints, owner-scoped and in OpenAPI 3.1; none echoes a credential.
  - Verification: integration tests per endpoint and a contract test against `docs/api/openapi.json` (Phases 53, 54, 60, 62).
  - Audit (2026-10-28): MET. Catalog, events, stages, context, steering, config, commands, changes, board, lifecycle, workspace, source and the settings routes are in `docs/api/openapi.json`; Api.IntegrationTests 1027/1027 passed and passed again inside the gate on `e24e6851` (recorded 2026-10-28). Behaviour limits in Known gaps. See `.planning/phases/53-web-head-foundation-and-api-surface/53-VERIFICATION.md`.
- [ ] **PX-11**: The web application can be seen.
  - Acceptance: a labelled Demo build with a simulated engine and a hosted preview; a local run against the real API; screenshots of every screen in both themes at 1440, 1024 and 390 px with the method stated.
  - Verification: fixture contract test, `curl` and a screenshot of the hosted page, the evidence folder (Phase 55).
  - Audit (2026-10-28): PARTIAL. The Demo is live at https://ompos-web.vercel.app (deployment id and live capture recorded, evidence README present); the commit of the hosted bundle is not recorded and the screenshot matrix of both themes at three widths is incomplete. No live model turn (OD-07). See `.planning/phases/55-showing-the-web-application/55-VERIFICATION.md`.
- [ ] **PX-12**: All Cursor slash commands resolve.
  - Acceptance: the 92 documented tokens and aliases each have a handler kind and a visible status; `/skill-name`, custom markdown commands, `/subagent-name` and the `&` hand-off resolve; the palette of both heads lists engine, OmpOS, discovered and skill commands with their source.
  - Verification: a test that every documented token has a handler; resolver and palette tests (Phase 56).
  - Audit (2026-10-28): PARTIAL. All 92 tokens plus `/update-settings` have a kind and status and resolve; 24 are Partial and 24 Unavailable with stated reasons; the web head lists no discovered rows; no command ran against a live omp (OD-07). Local gate of record passed on `e24e6851` (recorded 2026-10-28); a grep re-check of 2026-10-28 confirms the web palette still passes `SlashDiscovery.Empty`. See `.planning/phases/56-slash-commands-and-convention-discovery/56-VERIFICATION.md`.
- [ ] **PX-13**: The conventions of a workspace are picked up automatically.
  - Acceptance: commands, skills, rules and subagents are discovered from `.ompos`, `.omp`, `.cursor`, `.claude`, `.codex` and `.agents` at project and user scope with precedence and diagnostics, in desktop, remote and cloud sessions.
  - Verification: discovery tests over fixture trees per source and per environment kind (Phase 56).
  - Audit (2026-10-28): PARTIAL. Six directories at two scopes with precedence and diagnostics work in desktop sessions and are wired for remote sessions (untested at the Native seam); cloud sessions have no discovery; multi-root merge exists in the library but no head passes more than one root. Local gate of record passed on `e24e6851` (recorded 2026-10-28); a grep re-check of 2026-10-28 confirms no cloud discovery source exists. See `.planning/phases/56-slash-commands-and-convention-discovery/56-VERIFICATION.md`.
- [ ] **PX-14**: Cursor-compatible hooks run in every environment.
  - Acceptance: 21 events, command and prompt hooks, matchers, timeouts, `failClosed`, `loop_limit`, exit code 2 as deny; blocking events enforced inside omp by an OmpOS extension; consent on a path-independent hash; Hooks page and run log; head wiring (closes OQ-84).
  - Verification: parser, runner, consent and extension tests; a fake-omp test that a denying hook stops a tool call (Phase 57).
  - Audit (2026-10-28): PARTIAL. 21 events, consent v2, extension, tap, page and desktop wiring are built and tested (local gate of record passed on e24e6851, recorded 2026-10-28); no test makes a fake omp process load `plugins/ompos-hooks-omp`; real-omp enforcement is UNVERIFIED (OD-07); remote ssh sessions read no hook files and run no bridge; cloud hooks are off by default, not in the worker image, with no consent snapshot producer. See `.planning/phases/57-hooks/57-VERIFICATION.md`.
- [ ] **PX-15**: A session can run on a remote host over SSH.
  - Acceptance: profiles from `ssh -G`, preflight with fix text, `SshAcpTransport` for `omp acp`, banner tolerance, reconnect with `session/load`, remote file tree and terminal, no secret in an argument list.
  - Verification: fake-ssh tests with canary secrets and a recorded run against a loopback `sshd` (Phase 58).
  - Audit (2026-10-28): PARTIAL. Ssh stdio remote sessions, files, conventions, terminal, picker and reconnect are built, wired into the desktop head and unit tested; a real omp opened a session through a real sshd on loopback; no prompt turn, no real network host, no live drop, and a Windows head rejects a POSIX remote working directory (`AcpAgentSession.cs` line 982). Containers (podman) and WSL are built and wired as an addendum (live podman passed in the Wave 4 tree; WSL unrun on Windows). Local gate of record passed on `e24e6851` (recorded 2026-10-28). See `.planning/phases/58-remote-environments-over-ssh/58-VERIFICATION.md`.
- [ ] **PX-16**: A session can cover several repositories.
  - Acceptance: a workspace set of several roots is validated and launched through omp's additional-directory support; worktrees per git root; explorer with one tree per root; `.code-workspace` import and export; convention merge across roots.
  - Verification: launch, worktree and merge tests with real git, a recorded omp probe (Phase 59).
  - Audit (2026-10-28): Partial. Library, picker and `--add-dir` launch are tested and in the passing gate of record (e24e6851, recorded 2026-10-28; Worktrees tests included); no head binds `MultiRootWorkspaceTree`, no session start calls `WorktreeRunSet.CreateAsync`, heads discover conventions from the primary root only; no live model turn (OD-07).
- [ ] **PX-17**: An agent can run in the cloud on Railway and return a pull request.
  - Acceptance: authenticated WebSocket worker bridge, durable cloud agent records, caps, idle sweeper, secret redaction, Railway client, pull request flow, heads, and a staged deployment recorded with version and state or the exact blocker.
  - Verification: loopback and fake-worker tests, canary tests, recorded fixtures, Railway status read-back (Phase 60).
  - Audit (2026-10-28): Partial. Worker bridge, control plane, Railway client and desktop page are tested in the passing gate of record (e24e6851) and the worker image ran under podman, 14 of 14 checks (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`); nothing is deployed on Railway, the worker cannot clone a repository, the web head has no list or form, no live model turn (OD-07).
- [ ] **PX-18**: Settings cover Cursor's configuration surface.
  - Acceptance: each of the 50 inventory rows is built, wired to omp or listed as omitted with the reason; the new pages exist in both heads.
  - Verification: a status test over the row ids and page tests (Phase 61).
  - Audit (2026-10-28): Partial. 50 rows have a status (6 Built, 2 Wired, 20 Partial, 22 Omitted) with a test and a shell check that run in the passing gate of record (e24e6851); no Labs page, no web Worktrees or Ignore page, no live model turn (OD-07).
- [ ] **PX-19**: Settings lists every EU, US and CN provider and coding plan of the research.
  - Acceptance: catalogue rows with base URL, authentication, API style, region, residency, omp id or `models.yml` template, links and plan terms; retired or unconfirmed vendors are not offered; writer support for all ten omp API styles; region filter; coding-plan cards with the vendor rule text; no key is ever rendered.
  - Verification: catalogue and writer tests, an omp validation probe in a sandbox, view model tests (Phase 62).
  - Audit (2026-10-28): Partial. 103-row catalogue, writer and Models page are tested in the passing gate of record (e24e6851, Providers and OmpConfig tests included); the omp check of the C# writer output is UNVERIFIED (no evidence file), no key was checked against a vendor (OQ-71, OD-07).
- [ ] **PX-20**: The remaining Cursor agent, chat, context and safety features are built or recorded.
  - Acceptance: `@` mentions, run modes, `permissions.json`, `sandbox.json`, `.cursorignore`, `.cursor/mcp.json` import, worktree commands, file checkpoints, queue versus send now, conversation search and context ring each exist or are listed as PARTIAL with the missing piece.
  - Verification: tests per feature and the refreshed audit (Phase 63).
  - Audit (2026-10-28): PARTIAL. Files, folders, chats, git diffs, rules and skills mentions, run modes, `permissions.json`, MCP import, worktree commands, checkpoints, queue and conversation search are wired and tested (gate of e24e6851 passed); pull request, browser, terminal-output and side-chat mentions are built but not composed in any head (grep of the tree), Auto-review has no classifier, `sandbox.json` is a read boundary only; nothing observed with a live omp (OD-07). See `.planning/phases/63-cursor-feature-completion/63-VERIFICATION.md`.
- [x] **PX-21**: The documentation is current and published in GitBook's layout.
  - Acceptance: `.gitbook.yaml`, `SUMMARY.md`, about 30 pages stating status honestly, README as the landing page, the owner's publish steps; the offline checker passes.
  - Verification: `scripts/docs/check_gitbook.py` in the gate (Phase 64).
  - Audit (2026-10-28): MET with caveats. `.gitbook.yaml`, `docs/SUMMARY.md` (73 pages), the landing pages, ten guides and `docs/runbooks/gitbook-publish.md` exist; `check-docs.sh` (also with `OMPOS_RELEASE=1`) reports 0 errors and runs in `bootstrap.sh` and `ci.yml` through `repo-checks.sh`, inside the passing gate on e24e6851; the GitBook space is not created (the owner's step, OQ-101). See `.planning/phases/64-documentation-and-gitbook/64-VERIFICATION.md`.
- [x] **PX-22**: The Cursor parity audit is refreshed against the 2026-10-27 inventory.
  - Acceptance: every feature of the inventory has a status with evidence; counts are updated.
  - Verification: the audit file (Phase 64).
  - Audit (2026-10-28): MET with caveats. Every inventory row has a status; the 494 total, the settings (50), slash (92) and shortcuts (55) counts recompute from the tables and match `docs/settings-parity.md` and `docs/reference/slash-commands.md`; rows were re-graded at `7535859` by reading code and test names, no test was run by the audit, and its gate-not-finished sentences predate the passing gate on e24e6851. See `.planning/phases/64-documentation-and-gitbook/64-VERIFICATION.md`.
- [x] **PX-23**: The release is verified and published.
  - Acceptance: `./bootstrap.sh` green on a clean worktree, fast-forward push, tag v0.15.0, GitHub Release, deploy records, a final report separating verified from owner-dependent items.
  - Verification: the saved log, git ancestry check, `gh release view` (Phase 65).
  - Audit (2026-10-28): MET (recorded after the release). `./bootstrap.sh` exited 0 on a clean worktree of `e24e6851` (12215 tests, 0 failed, 2 skipped); `origin/main` fast-forwarded to `9d4e0075`; tag `v0.15.0` and the GitHub Release exist (https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0); the final report separates verified, unverified and owner items. Caveat: the GitHub `ci` runs of both pushes failed at 'Start test services' (OQ-168); the local gate is the gate of record. See `.planning/phases/65-integration-and-release-v0150/65-VERIFICATION.md`.

### Traceability (PX)

| REQ-ID | Phase | Status (2026-10-28) |
|--------|-------|---------------------|
| PX-01 | 51 | Met |
| PX-02 | 51 | Met |
| PX-03 | 51 | Met |
| PX-04 | 51 | Partial (no web About page) |
| PX-05 | 52 | Met (owner: social preview upload) |
| PX-06 | 52 | Met (no suite test of the `.github` YAML) |
| PX-07 | 52 | Met (older milestone tags missing, OQ-108) |
| PX-08 | 53 | Met |
| PX-09 | 54 | Partial (no Cloud or Remote view; terminal stream missing) |
| PX-10 | 53 | Met |
| PX-11 | 55 | Partial (source commit of the hosted bundle; Dark 1024 and Dark 390 screenshots) |
| PX-12 | 56 | Partial (web palette without discovered rows; no live omp) |
| PX-13 | 56 | Partial (no cloud discovery; primary root only) |
| PX-14 | 57 | Partial (real-omp enforcement UNVERIFIED, OD-07) |
| PX-15 | 58 | Partial (no prompt turn; no Windows head) |
| PX-16 | 59 | Partial (explorer, per-run worktrees, convention merge unwired) |
| PX-17 | 60 | Partial; staged Railway deploy Blocked (OD-03, OQ-71, OQ-73, OQ-100) |
| PX-18 | 61 | Partial (Labs page; web pages) |
| PX-19 | 62 | Partial (writer validation by omp not committed) |
| PX-20 | 63 | Partial (three mention sources not composed; no live omp) |
| PX-21 | 64 | Met (GitBook space not created, owner, OQ-101) |
| PX-22 | 64 | Met |
| PX-23 | 65 | Met (GitHub `ci` fails at test services, OQ-168; the local gate passed) |
