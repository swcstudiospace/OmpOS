# Requirements: Autonogrammer

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
- [ ] **TD-08**: 01-REVIEW finding L-04 is closed; `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:37-41` is among the L items. Source: audit L-04 (`01-REVIEW.md`; `LayerMap.cs:37-41`), line 72.
- [ ] **TD-09**: The semver assertion in `tests/Autonogrammer.Contracts.Tests/ContractVersionTests.cs:16` checks a real semver value instead of passing vacuously. Source: audit IN-01, line 73.
- [ ] **TD-10**: The duplicate `.env` entry in `.gitignore` (lines 7 and 485) is removed. Source: audit IN-02, line 74.
- [ ] **TD-11**: `01-VALIDATION.md` rows 1-01-03 and 1-01-09 are exercised, or the `nyquist_compliant` flag matches the evidence. Source: audit 01-VALIDATION (`01-VALIDATION.md:55,61`), line 75.
- [ ] **TD-12**: Inbound `x-correlation-id` is validated (length and charset) before use and before persistence. Source: audit F-05 (`services/Autonogrammer.Api/Middleware/CorrelationIdMiddleware.cs:36`), line 79.
- [ ] **TD-13**: Resume positions `since` and `Last-Event-ID` are validated (malformed or negative rejected, bounded). Source: audit F-06 (`services/Autonogrammer.Api/Endpoints/EventStreamEndpoints.cs:46-69`), line 80.
- [ ] **TD-14**: `HarnessEventMapper` maps unknown subtypes to the unknown fallback instead of throwing. Source: audit F-07 (`services/Autonogrammer.Api/Harness/HarnessEventMapper.cs:32`), line 81.
- [ ] **TD-15**: `PromptRunner` keeps tracking newer runs; completion of an older run does not evict them. Source: audit F-08 (`services/Autonogrammer.Api/Services/PromptRunner.cs:109`), line 82.
- [ ] **TD-16**: `CreateSessionRequest.OwnerId` length and format are bounded in `SessionEndpoints.cs` (CreateSessionAsync). Source: audit F-09 (info), line 83.
- [ ] **TD-17**: `ps` output is validated before it is used as a container id. Source: audit WR-01 (`src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs:322`), line 87.
- [ ] **TD-18**: The caller `SessionId` is escaped or validated before it is used in the podman filter. Source: audit WR-02 (`LocalPodmanComposeProvider.cs:315-316`), line 88.
- [ ] **TD-19**: `AllowHostSecrets` and `PerSandboxCredentials` are enforced, not ignored. Source: audit WR-03 (`LocalPodmanComposeProvider.cs:340-356`), line 89.
- [ ] **TD-20**: Descriptor labels do not embed the host temp path. Source: audit WR-04 (`LocalPodmanComposeProvider.cs:279-280`), line 90.
- [ ] **TD-21**: Raw stderr is not placed in exception messages; it goes to a bounded, redacted diagnostic. Source: audit WR-05 (`LocalPodmanComposeProvider.cs:303`), line 91.
- [ ] **TD-22**: The TTL boundary in `SandboxLifecycle.cs` has no off-by-one error. Source: audit WR-06 (`src/Autonogrammer.Sandbox/SandboxLifecycle.cs:231`), line 92.
- [ ] **TD-23**: Jittered backoff has a positive floor. Source: audit WR-07 (`SandboxLifecycle.cs:277-278`), line 93.
- [ ] **TD-24**: OpenShell YAML list values render as YAML values, not CLR type names. Source: audit WR-08 (`src/Autonogrammer.Sandbox/OpenShellProvider.cs:221-228`), line 94.
- [ ] **TD-25**: A production `IProcessRunner` is registered by a host (no caller today), and `RealProcessRunner` is no longer confined to the tests project. Source: audit WR-09 (`tests/Autonogrammer.Sandbox.Tests/RealProcessRunner.cs:1-13`; `src/Autonogrammer.Sandbox/SandboxServiceCollectionExtensions.cs:15-18`), line 95.
- [ ] **TD-26**: `RealProcessRunner` starts processes from an argument list, not hand-rolled quoting. Source: audit WR-11 (`RealProcessRunner.cs:19,116-124`), line 96.
- [ ] **TD-27**: A session-store failure in `SandboxLifecycle` surfaces as a typed error, not an untyped exception. Source: audit WR-12 (`SandboxLifecycle.cs:80`), line 97.
- [ ] **TD-28**: 03-REVIEW finding IN-01 is closed. Source: audit IN-01 to IN-04 (`03-REVIEW.md`, including DeleteComposeFile exception filtering in `LocalPodmanComposeProvider.cs`), line 98.
- [ ] **TD-29**: 03-REVIEW finding IN-02 is closed. Source: audit IN-02 (`03-REVIEW.md`), line 98.
- [ ] **TD-30**: 03-REVIEW finding IN-03 is closed. Source: audit IN-03 (`03-REVIEW.md`), line 98.
- [ ] **TD-31**: 03-REVIEW finding IN-04 is closed. Source: audit IN-04 (`03-REVIEW.md`), line 98.
- [x] **TD-32**: The reconnect policy in the web head has a recorded closure: bounded reconnect with backoff implemented and tested, or the policy decision written down. Source: audit 04-surfaces WR-02 (`04-REVIEW.md:140`), line 102.
- [x] **TD-33**: Terminal SSE detection checks the event type, not only an event field. Source: audit WR-03 (`apps/Autonogrammer.Web/Services/SseEventStream.cs:157-158`), line 103.
- [x] **TD-34**: Seq dedup sets are bounded. Source: audit WR-04 (`src/Autonogrammer.EngineHost/RemoteEngineHost.cs:27,315-355`; `apps/Autonogrammer.Web/Services/SseEventStream.cs:62`), line 104.
- [x] **TD-35**: A2UI image URLs are restricted to an allowed scheme list. Source: audit WR-06 (`src/Autonogrammer.GenUi/A2uiSurfaceCatalog.cs:110-117`), line 105.
- [x] **TD-36**: `EngineEventMapper` maps unknown events to a fallback instead of throwing. Source: audit WR-07 (`apps/Autonogrammer.App/Components/EngineEventMapper.cs:56`), line 106.
- [ ] **TD-37**: The accepted `MarkupString` sink (handoff decision 3) has a regression test proving its only input is `MarkdownRenderer` output. Source: audit IN-01 (`src/Autonogrammer.UI/Components/Blocks/MarkdownBlockView.razor:4`), line 107.
- [ ] **TD-38**: Whitespace-only drafts are not sent. Source: audit IN-02 (`src/Autonogrammer.UI/Components/Composer.razor:36-45`), line 108.
- [ ] **TD-39**: History replay deduplicates by Seq in `SelectSessionAsync`. Source: audit IN-03 (`apps/Autonogrammer.Web/Pages/Index.razor`), line 109.
- [ ] **TD-40**: `03-VERIFICATION.md` frontmatter status and criteria row 4c agree with its Re-verification section (MET). Source: audit records-and-docs (`03-VERIFICATION.md:4`, `:61`, `:125-161`), line 113.
- [ ] **TD-41**: `04-VERIFICATION.md` score and UI-03 row agree with its Re-verification section (VERIFIED). Source: audit records-and-docs (`04-VERIFICATION.md:5`, `:28`), line 114.
- [x] **TD-42**: `04-03-SUMMARY.md` frontmatter lists DESK-01 and MOB-01 as well as SHELL-01. Source: audit records-and-docs (`04-03-SUMMARY.md:2`), line 115. Closed: commit 882444f; frontmatter at `.planning/milestones/v0.9.1-phases/04-surfaces/04-03-SUMMARY.md:3` lists `[DESK-01, MOB-01, SHELL-01]`.
- [ ] **TD-43**: The VERIFY-02 TODO-linkage gate has an in-repo positive case and negative case, not only a recorded negative control in commit a6fcadd. Source: audit test-evidence (`05-VALIDATION.md:42`; `tests/Autonogrammer.ArchitectureTests/RepoHygieneTests.cs:121`), line 119.
- [x] **TD-44**: The two `incomplete` axe items on the icon-only send control are resolved or documented with evidence, and the unexplained skip-link screenshot is explained or reproduced. Source: audit test-evidence (`docs/runbooks/local-dev.md:96-97`), line 120. Closed: commit 924ca78; evidence `docs/runbooks/evidence/web-*-924ca78-2026-10-21.*` and the dated entry 2026-10-21 (commit 924ca78) in `docs/runbooks/local-dev.md`.

### Owner-Deferred Items (OD)

Source for each line: audit `owner_decided_deferrals` (OD-n) and the open-question ID.

- [ ] **OD-01**: Interactive tool approval: a UI decision reaches a pending ACP `session/request_permission`; `RequestToolApprovalAsync` and `ResolveToolApprovalAsync` (`src/Autonogrammer.Sessions/SessionService.cs:286`, currently internal) get a production caller; deny-by-policy default is preserved and approval may only deny earlier. Source: audit OD-1; `docs/open-questions.md:42` (Q-HARN-PERMISSION); handoff decision 2.
- [ ] **OD-02**: ~~Temporal worker composition: real provider and activity implementations are composed into the worker, and the worker is wired into the API session-run path behind `Workflows:Enabled` (`services/Autonogrammer.Api/Program.cs:54-63` uses FakeSandboxProvider; `services/Autonogrammer.Workflows.Worker/WorkerHostConfiguration.cs:91-97` guards activities). Source: audit OD-2; `docs/open-questions.md:41` (Q-SB-WORKER-COMPOSE); handoff decision 1.~~ **Composition half superseded 2026-10-21:** the Temporal worker is removed and there is no worker service. Session runs execute in-process in the API through `DirectSessionRunner` (`docs/handoff.md`, "Temporal removal and Railway topology (2026-10-21)", supersession row OD-02). Q-SB-WORKER-COMPOSE is resolved by removal. The persistence half (Phase 7 in ROADMAP.md) stays open, and its session log is shared by the API only.
- [ ] **OD-03**: The single dev principal (`services/Autonogrammer.Api/Security/SingleDevPrincipal.cs:8,11`) is replaced by an authentication provider using the sign-in method the owner selects; unauthenticated cross-origin requests are denied. Gate: owner decision on sign-in method. Source: audit OD-3; `docs/open-questions.md:32` (OQ-26); handoff decision 4 and deploy blocker 1.
- [ ] **OD-04**: Per-sandbox credential issuance: each sandbox receives its own scoped credential, revoked at teardown, beyond duplicate env-target rejection. Source: audit OD-4; `docs/open-questions.md:40` (Q-SB-CREDENTIALS).
- [ ] **OD-05**: The direct-path retry policy is decided and implemented: bounded retries for idempotent activities as on the Temporal path, or an explicit fail-fast setting with tests; the decision is recorded in the open question. Source: audit OD-5; `docs/open-questions.md:39` (Q-WF-RETRY); handoff known deferrals. **Note 2026-10-21:** the "Temporal path" comparison is historical. Temporal is removed; retry lives in `DirectSessionRunner` and the run lease only (Q-WF-RETRY, `docs/handoff.md`). **Superseded 2026-10-21:** Temporal removed; see docs/handoff.md, Temporal removal and Railway topology (2026-10-21), item 1.
- [ ] **OD-06**: The remote WebSocket ACP transport stub is replaced by a transport that passes `AcpTransportContractTests<T>`, using framing and authentication verified against the ACP or omp specification; any unverified part stays in the open question. Source: audit OD-6; `docs/open-questions.md:34` (Q-HARN-WS).
- [ ] **OD-07**: Unseen `session/update` variants are recorded from live `omp acp` and omp RPC turns, frozen as fixtures, and mapped; the unknown fallback applies only to variants still unseen. Source: audit OD-7; `docs/open-questions.md:43` (Q-HARN-UPDATE).
- [ ] **OD-08**: `AgentOsProvider` implements the sandbox operations against a verified agentOS interface, or the open question records the verified blocker. No vendor API is invented. Source: audit OD-8; `docs/open-questions.md:35` (Q-SB-AGENTOS); `VendorStubTests`.
- [ ] **OD-09**: `RailwaySwarmProvider` implements the sandbox operations against verified Railway endpoints and authentication, or the open question records the verified blocker. Source: audit OD-9; `docs/open-questions.md:36` (Q-SB-RAILWAY).
- [ ] **OD-10**: The attested-enclave verifier produces a verdict from a verified report format (dstack attestation shape) instead of always `Unverified`, or the open question records the verified blocker. Source: audit OD-10; `docs/open-questions.md:37` (Q-SB-ENCLAVE).
- [ ] **OD-11**: `OpenShellProvider` (`src/Autonogrammer.Sandbox/OpenShellProvider.cs:11`) is exercised through the podman compute driver on Podman 5.x against the verified gateway and policy contract, or the open question records the verified blocker. Source: audit OD-11; `docs/open-questions.md:38` (Q-SB-OPENSHELL).
- [ ] **OD-12**: The Blazor reflection override for the web and MAUI heads is resolved: the head runs with `JsonSerializerIsReflectionEnabledByDefault=false` under tests, or a scoped override is recorded with its justification. Source: audit OD-12; `docs/open-questions.md:45` (Q-WEB-REFLECTION).
- [ ] **OD-13**: The authoritative AC_1..AC_11 enumeration is recovered from the ultrathink plan (Linear SPE-17749 to SPE-17798; Notion task 3ffbc1a0c7ae8112aa70d2e15b81929f), or recorded as unrecoverable; the AC verdicts in `docs/handoff.md` are re-checked against the recovered list. Source: audit OD-13; `docs/open-questions.md:44` (Q-AC-LIST); handoff provenance; PROJECT.md Context (Process).

### Human-Only Items on This Host (HO)

- [ ] **HO-02**: `podman` and `podman-compose` are installed on this host; `podman-compose config` validates `deploy/compose.yaml` and the compose stack boots (FOUND-08). Source: audit HO-2 (`01-VERIFICATION.md:50-55`; `deploy/compose.yaml`). Podman was absent at scaffold time (PROJECT.md Context).
- [ ] **HO-03**: `RealPodmanComposeConfig_SmokeTest` runs and passes on this host's podman, not skipped. Source: audit HO-3 (`tests/Autonogrammer.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs:136`; `03-VERIFICATION.md` human verification 2).
- [ ] **HO-04**: ~~The replay fixture `tests/Autonogrammer.Workflows.Tests/TestData/histories/session-run.json` is re-validated against a live Temporal dev server on this host. Source: audit HO-4 (`03-VERIFICATION.md` human verification 1). The Temporal CLI was absent at scaffold time (PROJECT.md Context).~~ **Superseded 2026-10-21:** the Temporal history fixture is retired, so there is no replay to re-validate. The evidence file `docs/runbooks/evidence/ho-04-replay-revalidation-2026-10-21.md` is kept and marked superseded (`docs/handoff.md`, supersession row HO-04).

### Hosting (HOST)

Gate for every HOST requirement: owner decision on the sign-in method (OD-03).

- [ ] **HOST-01**: **Wording updated 2026-10-21 (Temporal removal):** the API alone deploys to Railway; there is no worker service (`docs/handoff.md`, decision 2). The API deploys from committed config-as-code built with podman-compatible OCI build files, configured through the variables in `.env.example`; `/health/live` and `/health/ready` respond on the deployed URL. Source: PROJECT.md Constraints (Hosting split); PROJECT.md Out of Scope (Railway deployment, superseded by this milestone); handoff "Before any public deployment".
- [ ] **HOST-02**: The hosted API is not publicly exposed until the OD-03 sign-in method is in place; the deploy checklist verifies that unauthenticated cross-origin requests are denied by the locked CORS default. Source: handoff decision 4; handoff "Before any public deployment" item 1; audit OD-3.
- [ ] **HOST-03**: ~~The API and worker connect to Temporal Cloud with credentials supplied only through environment variables (none committed), and readiness reflects Temporal Cloud connectivity.~~ **Superseded 2026-10-21:** replaced by the Railway services in `docs/handoff.md` decision 2 (TimescaleDB session log, Dragonfly coordination, GreptimeDB telemetry over OTLP). No Temporal Cloud connection exists. Source: PROJECT.md Out of Scope (Temporal Cloud setup, superseded by this milestone); PROJECT.md Key Decisions (Temporal optional behind `Workflows:Enabled`, superseded 2026-10-21); audit OD-2.
- [ ] **HOST-04**: The hosted API path runs the real omp harness instead of only the in-memory adapter (`services/Autonogrammer.Api/Program.cs`). Source: handoff decision 9; audit integration INT-OMP-IN-API (`services/Autonogrammer.Api/Program.cs:51`).
- [ ] **HOST-05**: The web head's `connect-src` allows the production API origin. `vercel.ts` generates it at build time from the validated `AUTONOGRAMMER_API_ORIGIN` (`scripts/vercel/render-csp.sh` validates the origin), and the web head reads that origin from `Api:BaseUrl` or `apiBaseUrl`. Source: audit HO-7 (superseded reference `vercel.json:31`, which no longer exists); handoff "Before any public deployment" item 2.
- [ ] **HOST-06**: The static publish output is deployed to Vercel, and the runbook browser smoke on the deployed URL shows the same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar) with no CSP violations. Source: audit HO-7; handoff "Before any public deployment" item 4.

### Mac Builds (MAC)

- [ ] **MAC-01**: `apps/Autonogrammer.App` builds for `net10.0-ios` with warnings as errors on a macOS host with Xcode 27.1. Source: audit HO-6; handoff decision 5 and "Before any public deployment" item 3; `docs/open-questions.md:23` (OQ-17). **Superseded 2026-10-21 (ADR-0004):** replaced by XP-05 (iOS) and XP-04 (macOS) in v0.10.0.
- [ ] **MAC-02**: `apps/Autonogrammer.App` builds for `net10.0-maccatalyst` with warnings as errors on the same macOS host. Source: audit HO-6; handoff "Before any public deployment" item 3. **Superseded 2026-10-21 (ADR-0004):** replaced by XP-05 (iOS) and XP-04 (macOS) in v0.10.0.
- [ ] **MAC-03**: The macOS CI `shells` job (`.github/workflows/ci.yml:74-88`) runs green on a macOS runner and records DESK-01, MOB-01 and SHELL-01 evidence; they stay Partial until it does. Source: audit HO-6; handoff decision 5; `docs/open-questions.md:23` (OQ-17).
- [ ] **MAC-04**: Runtime spot-check on the macOS build: desktop streams against the remote backend; mobile resumes remote-only and never spawns a process. Source: handoff "Before any public deployment" item 3 (verify line).

### Records and Evidence (REC)

- [x] **REC-01**: Blocking record BR-1 is closed: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`tests/Autonogrammer.ArchitectureTests/RepoHygieneTests.cs:121`) runs and passes at HEAD; `05-VERIFICATION.md` is re-run to replace its gaps_found status; VERIFY-02 is added to `05-01-SUMMARY.md` requirements-completed only if the re-run passes. Source: audit section 2.2 and front matter `gaps.blocking_records` (BR-1); `05-VALIDATION.md:42`. Evidence: `.planning/milestones/v0.9.1-phases/05-verify/05-VERIFICATION.md` (status passed, re-run at a26fe2f) and `docs/runbooks/evidence/verify02-todo-linkage-a26fe2f-2026-10-21.log` (filtered run: total 1, succeeded 1, failed 0).
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
  - Acceptance: the Windows, macOS, Linux, iOS and Android heads are built from one Avalonia UI solution. The web head stays Blazor WebAssembly on the shared presentation layer until G-WEB passes. `apps/Autonogrammer.App` (MAUI) is deleted only in the cutover commit, after every XP-10 parity test is green. `src/Autonogrammer.UI` (RCL) stays until the web head moves.
  - Verification: CI builds the five Avalonia heads from the one solution. An architecture test in `tests/Autonogrammer.ArchitectureTests` fails if any project references `Microsoft.Maui` after the MAUI cutover. The XP-10 parity run is green before the MAUI deletion commit.
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
  - Verification: contract tests in `tests/Autonogrammer.Contracts.Tests` run in the CI gate. An architecture test asserts that the contracts layer does not reference Avalonia, MAUI or `Autonogrammer.UI`.
- [ ] **XP-10**: Parity before removal: each feature of the MAUI head has an Avalonia test before `apps/Autonogrammer.App` is removed. The Razor component library stays until the web head moves.
  - Acceptance: a parity list names every feature of `apps/Autonogrammer.App`, with one Avalonia test per feature. No feature row lacks a test. `apps/Autonogrammer.App` is removed only after the whole parity list is green. `src/Autonogrammer.UI` is not removed by this item.
  - Verification: the parity list is reviewed row by row against the page and component inventory of `apps/Autonogrammer.App`. The Avalonia parity run is green in CI before the removal commit, and that commit is the only one that deletes `apps/Autonogrammer.App`.
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
  - Acceptance: a run started in cloud mode executes through the hosted API (`services/Autonogrammer.Api`). Its status moves through queued, running and completed. After completion the diff and transcript are retrievable for review.
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
  - Audit (2026-10-27): PARTIAL. The palette lists omp's commands; the Autonogrammer catalogue holds one command of 86 and a chat session does not list it. See `docs/quality/cursor-parity-audit-2026-10-26.md` row VX-02.
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
- [x] **AL-03**: Autonogrammer launches `omp acp` with the four plugins loaded and nothing from the user's global omp configuration.
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
  - Acceptance: ~~a message sent during a run queues as a steer or a follow-up and is delivered over ACP after the active turn in send order;~~ Superseded 2026-10-26 (DR-4: omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so prompting over ACP cannot deliver live; live delivery is the requirement, queue after the turn is the documented fallback): a steer sent during a run is delivered live into the running session (AL-15) and a follow-up is delivered after the active turn in send order; where live delivery is not available the steer queues after the active turn, is shown as queued and is never reported as delivered live; the substrate records it as a `note` event with `payload.action` `steer` when reachable; Autonogrammer never claims the substrate delivered it.
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
  - Acceptance: every text control in the Agents layout, the Kanban tab, the Settings window and the terminal chrome resolves its family through the `Autonogrammer.Font.*` tokens, with no literal family in AXAML; the recorded screenshots show DM Sans.
  - Verification: a headless test walking those surfaces and the recorded screenshots (Phases 39, 40, 46); the token set and licence notice stay VX-01 (Phase 36).
- [ ] **AL-20**: The application is a full Cursor desktop clone in UI, capabilities and settings, tracked by the existing inventories, with no addition beyond the Kanban tab and the expanded provider settings.
  - Acceptance: parity is tracked by ADE-01 to ADE-22, CX-01 to CX-08, VX-01 to VX-10 and the 34-row table in `docs/plans/vision-research/cursor-settings-window.md`; no new inventory is created; the only additions beyond Cursor parity are the Kanban tab (AL-17, AL-18) and the expanded provider settings (VX-05); no plugin enable or disable setting exists until the user confirms.
  - Verification: the Phase 35 audit of the final tree against those inventories (Phase 35 criterion 4).
  - Audit (2026-10-27): PARTIAL. The audit exists; the clone is not complete (audit section 4). See `docs/quality/cursor-parity-audit-2026-10-26.md` row AL-20.
- [x] **AL-21**: The application starts in the Dark theme and Settings > General offers System, Light and Dark; the choice persists.
  - Acceptance: `RequestedThemeVariant` is Dark by default; the General page saves System, Light or Dark and the running window follows it; views that build colours in code re-read the tokens on a change; every text and non-text colour pair meets its contrast requirement in both variants.
  - Verification: theme preference tests, `ThemeContrastTests` in both variants, and the screenshots in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` and `after-light/` (Phase 50).
- [x] **AL-22**: Controls, density and layouts follow the design specification `docs/plans/ui-parity/cursor-agents-window-parity.md` and keep the accessibility contract.
  - Acceptance: buttons, inputs, toggles, tabs, list rows, chips, scrollbars, tooltips and menus are styled from Autonogrammer tokens; desktop controls use 32 px targets and none is smaller than 24 by 24 px; the skip link is hidden until it has focus; the rail, thread, composer, Kanban tab, omp terminal and Settings window follow section 4 of the specification; x:Name values, accessible names, tab order, the focus ring, reduced motion and the keyboard map are unchanged.
  - Verification: control style, density and skip link tests, `KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, and the screenshot set (Phase 50).
- [x] **AL-23**: The Greptile faces are bundled as static instances and used by role: DM Sans for interface text, Anybody for display headings of 20 px and above only, Space Mono for code and identifiers.
  - Acceptance: the font files and their licence texts are in the application assets; no Thin weight renders; every `Autonogrammer.Font.Display` use is 20 px or larger and semibold or heavier; no literal family exists in AXAML (AL-19).
  - Verification: `FontResourceTests`, a typography markup test and the recorded screenshots (Phase 50).

## Parity and publication (PX, v0.15.0, planned 2026-10-27; audited 2026-10-28)

Source: the user request of 2026-10-27 and the research in `docs/plans/v0.15.0-research/`. Every requirement names its phase; verification follows the gate rules of the earlier milestones (no live model turn, no external account action without a record).

- [x] **PX-01**: The default look is White and Purple.
  - Acceptance: a new installation starts in the Light theme (white surfaces, purple `#6d28d9`) in the desktop and the web head; System, Light and Dark remain choices and persist; every colour pair keeps its contrast in both variants. Supersedes the Dark default of AL-21 (OQ-94).
  - Verification: theme preference tests (default Light), `ThemeContrastTests`, screenshots in both themes (Phase 51).
  - Audit (2026-10-28): MET. Light default on desktop, Android and web; Dark and System persist; `DefaultThemeTests`, `ThemeSwitchTests`. The owner has not signed off the look (OQ-94 default). See `.planning/phases/51-brand-theme-and-licence/51-VERIFICATION.md`.
- [x] **PX-02**: Autonogrammer has a brand set built from the Greptile typefaces.
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
  - Acceptance: the 92 documented tokens and aliases each have a handler kind and a visible status; `/skill-name`, custom markdown commands, `/subagent-name` and the `&` hand-off resolve; the palette of both heads lists engine, Autonogrammer, discovered and skill commands with their source.
  - Verification: a test that every documented token has a handler; resolver and palette tests (Phase 56).
  - Audit (2026-10-28): PARTIAL. All 92 tokens plus `/update-settings` have a kind and status and resolve; 24 are Partial and 24 Unavailable with stated reasons; the web head lists no discovered rows; no command ran against a live omp (OD-07). Local gate of record passed on `e24e6851` (recorded 2026-10-28); a grep re-check of 2026-10-28 confirms the web palette still passes `SlashDiscovery.Empty`. See `.planning/phases/56-slash-commands-and-convention-discovery/56-VERIFICATION.md`.
- [ ] **PX-13**: The conventions of a workspace are picked up automatically.
  - Acceptance: commands, skills, rules and subagents are discovered from `.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex` and `.agents` at project and user scope with precedence and diagnostics, in desktop, remote and cloud sessions.
  - Verification: discovery tests over fixture trees per source and per environment kind (Phase 56).
  - Audit (2026-10-28): PARTIAL. Six directories at two scopes with precedence and diagnostics work in desktop sessions and are wired for remote sessions (untested at the Native seam); cloud sessions have no discovery; multi-root merge exists in the library but no head passes more than one root. Local gate of record passed on `e24e6851` (recorded 2026-10-28); a grep re-check of 2026-10-28 confirms no cloud discovery source exists. See `.planning/phases/56-slash-commands-and-convention-discovery/56-VERIFICATION.md`.
- [ ] **PX-14**: Cursor-compatible hooks run in every environment.
  - Acceptance: 21 events, command and prompt hooks, matchers, timeouts, `failClosed`, `loop_limit`, exit code 2 as deny; blocking events enforced inside omp by an Autonogrammer extension; consent on a path-independent hash; Hooks page and run log; head wiring (closes OQ-84).
  - Verification: parser, runner, consent and extension tests; a fake-omp test that a denying hook stops a tool call (Phase 57).
  - Audit (2026-10-28): PARTIAL. 21 events, consent v2, extension, tap, page and desktop wiring are built and tested (local gate of record passed on e24e6851, recorded 2026-10-28); no test makes a fake omp process load `plugins/autonogrammer-hooks-omp`; real-omp enforcement is UNVERIFIED (OD-07); remote ssh sessions read no hook files and run no bridge; cloud hooks are off by default, not in the worker image, with no consent snapshot producer. See `.planning/phases/57-hooks/57-VERIFICATION.md`.
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
  - Audit (2026-10-28): Partial. Worker bridge, control plane, Railway client and desktop page are tested in the passing gate of record (e24e6851) and the worker image ran under podman, 14 of 14 checks (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`); the API is not deployed on Railway (OD-03); after the release, on 2026-10-28, a staged private worker was deployed to Railway, health-checked and handshaken without a model turn (`docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`); the worker cannot clone a repository, the web head has no list or form, no live model turn (OD-07).
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
  - Audit (2026-10-28): MET with caveats. `.gitbook.yaml`, `docs/SUMMARY.md` (73 pages), the landing pages, ten guides and `docs/runbooks/gitbook-publish.md` exist; `check-docs.sh` (also with `AUTONOGRAMMER_RELEASE=1`) reports 0 errors and runs in `bootstrap.sh` and `ci.yml` through `repo-checks.sh`, inside the passing gate on e24e6851; the GitBook space is not created (the owner's step, OQ-101). See `.planning/phases/64-documentation-and-gitbook/64-VERIFICATION.md`.
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
| PX-17 | 60 | Partial; staged worker deployed and handshaken on Railway 2026-10-28, API deploy Blocked (OD-03, OQ-71, OQ-73, OQ-100, OQ-171) |
| PX-18 | 61 | Partial (Labs page; web pages) |
| PX-19 | 62 | Partial (writer validation by omp not committed) |
| PX-20 | 63 | Partial (three mention sources not composed; no live omp) |
| PX-21 | 64 | Met (GitBook space not created, owner, OQ-101) |
| PX-22 | 64 | Met |
| PX-23 | 65 | Met (GitHub `ci` fails at test services, OQ-168; the local gate passed) |

## v0.16.0 Autonogrammer: accounts, Cowork and Dispatch (planned, added 2026-10-28)

Source: the user request of 2026-10-28, the ultrathink plan graph ut-mvs8ru79-82536fbd, ADR-0030 to ADR-0037 and the phase briefs in `docs/plans/v0.16.0-research/briefs/` (index `00-index.md`, scaffold manifest `00-scaffold-manifest.md`). Requirement ids are `AG-<phase>-<n>`, written verbatim from each brief; `AG-SC-1` to `AG-SC-6` are derived from the scaffold manifest (Phase 66.1). Verification follows the gate rules of the earlier milestones: no live model turn (OD-07), no live Supabase, no Railway deploy, no Windows, macOS or Android run is claimed.

### Scaffold (AG-SC, Phase 66.1)

- [ ] **AG-SC-1**: Create the 21 source projects (3 at L1, 15 at L2, 3 at L3) and the 19 test projects of `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md`, each with a csproj of only `ItemGroup`s, `InternalsVisibleTo` for its test project and an `AssemblyMarker.cs` only, so that every project builds with no behaviour.
- [ ] **AG-SC-2**: Register every new project in `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` (21 `Layers` rows) and declare exactly the six `SameLayerAllowed` tuples of the manifest (`Accounts` to `Harness.Secrets`; `Harness.Secrets.Os` to `Harness.Secrets`; `Cowork` to `Harness.AgentLoop`; `Cowork.Artifacts.Stores` to `Cowork.Artifacts`; `UI` to `Cowork.Artifacts`; `Cowork.Memory.Postgres` to `Cowork.Memory`), each with an `// intentional:` comment and no other tuple.
- [ ] **AG-SC-3**: Register every new project in `Autonogrammer.slnx` and `Autonogrammer.Linux.slnf` (40 lines each), add the 21 `ProjectReference` lines to the architecture test project, one row per project in `docs/architecture.md` and a `CHANGELOG.md` entry under Unreleased, and add the `ProjectReference` and `PackageReference` edits to the existing projects listed in section 3 of the manifest.
- [ ] **AG-SC-4**: `ProcessSpawningAssemblies` in `ProjectGraphTests.cs` gains `Harness.BrowserUse`, `Harness.ComputerUse.MacOS`, `Harness.Isolation` and `Harness.Notifications`; `Directory.Packages.props` pins `System.Security.Cryptography.ProtectedData` 10.0.13, `PuppeteerSharp` 25.15.0 and `Avalonia.Controls.DataGrid` 12.1.2 with their `Label` groups, and relabels the `Avalonia.Controls.WebView` group from spike to production.
- [ ] **AG-SC-5**: `ContractVersion.Current` is bumped ONCE from 1.5.0 to 1.6.0 in the scaffold step (no phase edits it), the Contracts folders are added additively, and the step checks that no type in `Contracts/Plugins` references a `Contracts/Ui` type (if the check fails the single bump becomes 2.0.0).
- [ ] **AG-SC-6**: ONE relock (`--force-evaluate`) commits the new and changed `packages.lock.json` files, ONE notices regeneration updates `THIRD-PARTY-NOTICES.md`, the locked restore passes and the gate of report 09 section 5 is green on a scaffold commit that adds no behaviour.

### Rebrand (66) (AG-66)

- [ ] **AG-66-1**: `rebrand.py dry-run` runs on a clean tree with `obj/` and `bin/` deleted, writes its report outside the repository, and exits 0 (exit 2 means collisions and blocks apply).
- [ ] **AG-66-2**: `apply` runs with the default `--engine-adapters keep` and without `--urls`, moves paths with `git mv`, ends with `post-apply verification: clean`, and commits nothing itself.
- [ ] **AG-66-3**: `verify --baseline <dry-run json>` passes: nothing left to rename, rejected near-match counts and every `keepPatternCounts` entry (including bare `omp`) equal to the baseline; `git grep -c compose` is equal before and after.
- [ ] **AG-66-4**: The three runbook acceptance probes (s.2) hold: the `autonogrammer` grep lists only protected URLs, `git ls-files | grep -i autonogrammer` lists only evidence, milestone and legal paths, `git grep -c 'omp acp'` is unchanged outside excluded zones.
- [ ] **AG-66-5**: The rename is one commit `rename: Autonogrammer to Autonogrammer (mechanical, tools: scripts/rebrand)`; the regenerate output (s.4) is a second commit; neither mixes hand-written code.
- [ ] **AG-66-6**: After the regenerate commit `dotnet restore --locked-mode`, `dotnet build ... -warnaserror`, `dotnet format ... --verify-no-changes --no-restore` and the Linux test set are green, and `docs/api/openapi.json` equals the served document.
- [ ] **AG-66-7**: User data directory: one shared resolver returns the `Autonogrammer` directory; a legacy `Autonogrammer` directory is moved once, atomically, leaving a marker file; a new file is never overwritten; the legacy directory is never read afterwards (runbook s.7 item 1).
- [ ] **AG-66-8**: `.autonogrammer` discovery: order `.autonogrammer`, legacy `.autonogrammer` (read-only, deprecated), `.omp` (unchanged); a legacy-only directory is read with a one-time notice and a documented move command; nothing is ever written into a legacy directory; legacy-only `permissions.json` and `sandbox.json` are never silently ignored (s.7 item 2).
- [ ] **AG-66-9**: `models.yml` markers: read, update and removal recognise `# autonogrammer:begin/end <id>` and `# autonogrammer:begin/end <id>`; a legacy block is rewritten under the new markers in the same write, never both, no duplicated provider entries; unmanaged bytes and comments are preserved (s.7 item 3).
- [ ] **AG-66-10**: Old-name literals exist only in files named `LegacyBrandNames.cs`, one per project that needs them; the architecture or a unit test fails on any other `autonogrammer` literal under `src/`, `services/`, `apps/` (guards the freeze in runbook s.6).
- [ ] **AG-66-11**: `AGENTS.md` and `README.md` use the new name and match the tree; `CLAUDE.md` exists, is minimal, and imports `AGENTS.md`; `CHANGELOG.md` has the rename entry under `## [Unreleased]` and no released section changed.
- [ ] **AG-66-12**: `scripts/test-services.sh` creates only `autonogrammer-test-*` containers, so it cannot touch another session's `autonogrammer-test-*` containers; `down` removes only its own two.
- [ ] **AG-66-13**: The desktop head window title is `Autonogrammer` (`apps/Autonogrammer.Native/MainWindow.axaml`), checked by a launch under a private Xvfb display.
- [ ] **AG-66-14**: The owner-action list of runbook s.5 is delivered with the result, each item marked open; nothing in the tree claims an external rename happened.

### Accounts (67) (AG-67)

- [ ] **AG-67-1**: The desktop head signs in with an email one-time code (6 to 10 digits) through POST /otp and POST /verify with type email.
- [ ] **AG-67-2**: The desktop head also signs in through a magic link with PKCE and a one-shot loopback listener on 127.0.0.1 with a path nonce, falling back cleanly to the code.
- [ ] **AG-67-3**: First-time email users (F1, signup email without a code) see a specific hint and a working link-then-new-code path whichever owner actions are done.
- [ ] **AG-67-4**: The web head signs in by code or by link, holds tokens and the PKCE verifier in memory only, and hands the returned code to the asking tab over a BroadcastChannel.
- [ ] **AG-67-5**: A hand-written GoTrue client in `Autonogrammer.Accounts` has typed models, source-generated JSON and results (no exceptions) for expected failures, and uses no Supabase SDK.
- [ ] **AG-67-6**: Refresh is single flight, writes the rotated refresh token to the store before publishing it, and adopts a token rotated by another process under a cross-process lock.
- [ ] **AG-67-7**: Startup never waits on the network; a stored session starts SignedIn and refreshes in the background; Offline is a notice, not a sign-out.
- [ ] **AG-67-8**: Sign-out clears local state first, then revokes with scope=local; sign out everywhere (global) and others are separate, confirmed actions.
- [ ] **AG-67-9**: Every failure maps to one of the AuthErrorKind values with its own fixed message and never shows raw exception or server text.
- [ ] **AG-67-10**: `Autonogrammer.Harness.Secrets.Os` implements ISecretStore with libsecret on Linux, Keychain Services on macOS and DPAPI on Windows, reporting typed Unavailable, Locked, Denied.
- [ ] **AG-67-11**: When the OS store fails the session is memory-only with a visible notice; the owner-only file store is used for accounts only after an explicit consent setting, and never for a device private key.
- [ ] **AG-67-12**: A first launch of a Code-only user makes no keyring call (setting `account.hasStoredSession`).
- [ ] **AG-67-13**: The API validates Supabase tokens with ES256 pinned, issuer and audience authenticated, rejects is_anonymous and a role other than authenticated, and takes the owner from sub.
- [ ] **AG-67-14**: GET /auth/config is anonymous and serves project URL, publishable key, enabled methods and timings; the validator refuses a secret key and a non-https URL.
- [ ] **AG-67-15**: Every rate-limit policy partitions by sub; unauthenticated callers share one partition.
- [ ] **AG-67-16**: With open sign-up, WorkspaceAccess denies every caller when Api:Workspace:Owners is empty unless the development principal is on.
- [ ] **AG-67-17**: OpenAPI declares the bearer scheme, security [] on the anonymous operations (`/auth/config`, `/source`, the webhook receiver and `/cowork/artifact-render/{ticket}` with its own limiter partition), scheme `DispatchDevice` on `/dispatch/devices/{id}/connect`, and 401 and 403 responses.
- [ ] **AG-67-18**: The `accounts` row (ADR-0031 M0004, no email stored; `AccountResponse.email` comes from the token's `email` claim) is created on the first authenticated request and `AccountEnrollmentMiddleware` upserts `last_seen_at` only, at most one write per sub per 10 minutes, and never blocks a request.
- [ ] **AG-67-19**: GET /account, /account/sessions, /account/sessions/{id} and /account/board are owner-scoped; an item of another owner is the same 404 as an absent one.
- [ ] **AG-67-20**: GET /board is replaced by GET /account/board and every caller moves in the same change.
- [ ] **AG-67-21**: SessionRegistry and SessionLifecycleStore are deleted and their callers use the repository (brief 68), so a restart keeps the list.
- [ ] **AG-67-22**: A Cowork session appears in /account/sessions and nowhere on the board, /sessions or the catalog event stream.
- [ ] **AG-67-23**: Both heads draw exactly one of SignedOut, AwaitingLink (resend cooldown, expiry countdown, change email), SigningIn (cancelable), SignedIn (identity, devices, sign out) and Error, accessibly.
- [ ] **AG-67-24**: The hosted web CSP gains a second validated connect-src origin for the auth host and the web head refuses a config whose project origin differs.

### Data plane (68) (AG-68)

- [ ] **AG-68-1**: Every Dragonfly key and channel is built by `DragonflyKeyspace` under the prefix `autonogrammer:v1:`, with a mandatory TTL of at most 24 h in the same command.
- [ ] **AG-68-2**: Autonogrammer uses its own pinned Dragonfly service, with no ACL user as the isolation boundary and no eviction.
- [ ] **AG-68-3**: The schema, database and test names are `autonogrammer`, `autonogrammer` and `autonogrammer_test`; no first-party source contains the old physical names.
- [ ] **AG-68-4**: Migrations up to the compiled head (M0001 to M0010; phase 68 U3 authors M0001 to M0009, M0006 carries the artifact tables and M0009 the ADR-0037 projects, instructions and memory tables, and M0010 `dispatch_device_state` is authored and registered in the runner list by phase 74 U2) are numbered, checksummed, additive, each with a down step, run by `Autonogrammer.Api migrate` under one advisory lock with the migrator role only.
- [ ] **AG-68-5**: The runtime role `autonogrammer_app` has DML only (INSERT and SELECT only on `dispatch_audit`); a role test proves it cannot run DDL or read another schema.
- [ ] **AG-68-6**: `ISessionRepository.ListByOwnerAsync` and `SetClosedAsync`, `Session.Surface` and `Session.ClosedAt` exist in the in-memory and Postgres stores.
- [ ] **AG-68-7**: Telemetry goes to GreptimeDB database `autonogrammer` with source `Autonogrammer.Api` and `autonogrammer.*` instruments, using only the pinned OpenTelemetry 1.20.0 packages.
- [ ] **AG-68-8**: `Autonogrammer.Resilience` gives every Postgres, Dragonfly and ledger call a deadline, bounded jittered retry (idempotent calls only), circuit breaker and bulkhead, on an injected `TimeProvider`.
- [ ] **AG-68-9**: Dragonfly outages fail closed (503 `data_plane_unavailable`, never a separate coordination problem type) for leases, idempotency and device nonces, and open for signals and presence; rate limits degrade to the in-process limiter.
- [ ] **AG-68-10**: `GET /status/data-plane` and the 503 problem `data_plane_unavailable` expose only component, state and a closed reason enum.
- [ ] **AG-68-11**: The ledger sink is off by default, outbox-fed, writes only through `SubstrateGateway.EmitEventAsync`, and sends only the closed field set (no personal data).
- [ ] **AG-68-12**: No client build, head or log holds a data-plane credential, connection string or pepper; configuration errors name the key, never the value.
- [ ] **AG-68-13**: Throwaway containers and variables are named `autonogrammer-test-*` and `AUTONOGRAMMER_TEST_*`; live tests are trait- and variable-gated; `GateParityTests` covers the test-services calls.
- [ ] **AG-68-14**: Account erasure deletes owner rows from every user-content table (including `cowork_memory_entries`, `cowork_memory_settings`, `cowork_instructions`, `cowork_projects`) and the tenant's Dragonfly keys, and leaves only keyed-hash audit rows.

### Surface switch and Cowork sessions (69) (AG-69)

- [ ] **AG-69-1**: A window-level `SurfaceHost` is the content of `MainWindow` and holds `MainView` (Code) and `CoworkView`; a window built without Cowork services shows no switch.
- [ ] **AG-69-2**: The switch is a two-item segmented control with the semantics of ADR-0032 D1 (Web tablist, Native toggle group, polite activity chip) and the shortcuts Ctrl+Shift+1 (Code) and Ctrl+Shift+2 (Cowork), each in the shortcut catalogue with a test.
- [ ] **AG-69-3**: The last surface persists as `shell.surface` in `config/settings.json` (Native) and `autonogrammer.surface` in `localStorage` (Web) and is restored only when Cowork is available.
- [ ] **AG-69-4**: Switching changes visibility only; no session of either surface is stopped, and `SurfaceActivityModel` shows running, needs-approval and failed counts of the hidden surface.
- [ ] **AG-69-5**: The Web head gets `SurfaceTabs`, a `Surface` fragment in `TopBar`, the route `/cowork` and a Cowork workbench shell with rail and empty, signed-out and error states.
- [ ] **AG-69-6**: `CoworkSessionRegistry` keeps Cowork sessions in `<stateRoot>/cowork/<account>/sessions.json`, separate from `<stateRoot>/sessions.json`, with its own 200-session cap and `RecoverAfterRestart`.
- [ ] **AG-69-7**: Cowork has its own contract types (`CoworkSessionSummary`, `ICoworkSessionCatalog`) in `Autonogrammer.Contracts.Cowork`; none is an `ISessionCatalog` or `SessionSummary`.
- [ ] **AG-69-8**: The Cowork layout (rail, conversation host, dock host with a tab strip) has framework-free models in `Autonogrammer.Presentation.Cowork` and two renderers, with no Kanban tab.
- [ ] **AG-69-9**: `CodeBoardSource` is the only caller of the repository list for the board and passes `SessionSurface.Code`; `PUT` and `DELETE /cowork/sessions/{id}` store summary rows with `surface = cowork`.
- [ ] **AG-69-10**: Architecture tests and behavioural tests make the Kanban rule fail the build when broken (ADR-0032 D2).
- [ ] **AG-69-11**: `CoworkSessionSync` pushes Cowork summaries to the API and keeps the latest per session while offline.
- [ ] **AG-69-12**: Each surface's top bar ends in a `HeaderEndSlot` bound to one `SurfaceChromeModel` (Presentation, `Cowork` folder; Native `SurfaceHost` and the Web `TopBar` place it) that ADR-0030 and ADR-0036 fill with the account menu and the Devices entry; the slot has a test that an entry added once appears in both surfaces in the same place and is keyboard reachable.
- [ ] **AG-69-13**: `IDockPanel` is a content contract; `CoworkDockHost` (unit 3) hosts a registered list of panels with a tab strip and one visible panel, and each provider (Artifacts, Browser Use) registers its panel; there is no single fixed dock slot (ADR-0032 D3).

### Cowork chat core and tool host (70) (AG-70)

- [ ] **AG-70-1**: `CoworkSessionProfile` produces the omp launch of a Cowork session: `--no-extensions`, an approval mode from the Cowork permission mode, `--add-dir` per trusted folder, an absolute `--append-system-prompt` file path, and a `--config` overlay with `browser.enabled: false` and `computer.enabled: false`.
- [ ] **AG-70-2**: `CoworkPermissionResolver` denies paths outside the trusted and Storage folders, always asks before a delete, allows `mcp__cowork_*` tools at the omp level, and follows Manual, Auto or Skip (`CoworkPermissionMode`; Auto is shown as "Auto (rules)") for the rest.
- [ ] **AG-70-3**: `CoworkSessionHost` implements `IAgentPanelHost` over the Cowork registry and composes engines with `EngineComposition.Compose`, never through the Code planner.
- [ ] **AG-70-4**: A Cowork conversation supports send, stop, retry, steer, resume from history (`session/load`), file attachments and image attachments, and refuses images with a specific message when the model reads none.
- [ ] **AG-70-5**: Tool-call updates carry images: `ToolUpdateContent.Images`, an image block in the transcript fold, the views, the export and the web projection, with alt text.
- [ ] **AG-70-6**: `Autonogrammer.Cowork.Abstractions` holds exactly the contract of ADR-0032 D5 and nothing else.
- [ ] **AG-70-7**: `CoworkToolHost` runs the eight-step call sequence of D5 for every call and enforces the descriptor rules and result caps.
- [ ] **AG-70-8**: `McpGatewayServer.Start(registry, invoker, coworkTools)` serves Cowork tools beside registry tools with disjoint names and maps results, images included, to `CallToolResult`.
- [ ] **AG-70-9**: `CoworkPolicyGate`, `CoworkApprovalBroker`, the grant store and the audit hook implement D6; `EveryCallOnExecutingDevice` is never satisfied by a grant, a mode or a remote decision.
- [ ] **AG-70-10**: `CoworkToolServer` starts one loopback gateway for Cowork, health-checks it before `session/new` and degrades with a visible notice, never by failing the session.
- [ ] **AG-70-11**: Cowork conversation models and views (Native and Web) show the transcript, composer, approval cards, provider status chips and the dock host.
- [ ] **AG-70-12**: `FakeCoworkToolProvider` and a contract test base let ADR-0033, 0034 and 0035 prove their providers against the same rules.
- [ ] **AG-70-13**: The host taints a session when a result carries untrusted content, raises `CoworkToolEvent`s (started, approval requested, completed, failed, denied), and in a tainted session Auto and Skip no longer apply to actuating calls or to `edit`, `delete`, `move` and `execute` (tests in units 1 and 3).
- [ ] **AG-70-14**: `ConfigOverlayWriter.WriteYaml(sessionStateDirectory, yaml)` (new, `Autonogrammer.Harness.AgentLoop/Launch`, owner-only mode, deleted on dispose) writes the Cowork overlay; the Cowork environment holds provider keys only and `CoworkPathBoundary` refuses the secrets, `~/.omp`, `~/.ssh` and account folders as trusted folders (tests in units 3 and 4).

### Cowork Artifacts (71) (AG-71)

- [ ] **AG-71-1**: An artifact has a host-generated id, a fixed kind (Markdown, Code, Html, Svg, Mermaid, Csv, Image), a validated title and media type, and an optional session link.
- [ ] **AG-71-2**: Versions are write-once, numbered from 1, each with a content SHA-256, a parent number and parent hash, an author set by the host, and a change note.
- [ ] **AG-71-3**: Update requires the base version, rejects a stale base with the latest number, and returns the existing version when the new bytes hash the same.
- [ ] **AG-71-4**: Every read re-verifies the hash and the parent link and refuses to render a version that fails.
- [ ] **AG-71-5**: Copy creates a new artifact with `DerivedFrom`; a user edit of a text kind saves a new version with author User.
- [ ] **AG-71-6**: Export writes one version as a file or an artifact as a deterministic zip, with safe defaults (sanitised SVG, neutralised CSV formulas, restricted HTML wrapper) and sanitised entry names.
- [ ] **AG-71-7**: Diff of text kinds uses DiffPlex under line and size caps and is cancellable; binary kinds diff as metadata.
- [ ] **AG-71-8**: The `artifact` tool family (`create`, `update` with `edits`, `read`, `list`) is provided through the host-owned MCP gateway with typed error codes, a write rate limit and `from_file` under the folder grants.
- [ ] **AG-71-9**: Limits (size per kind, versions, artifacts and bytes per account, title) are validated options and fail with typed results, never truncation.
- [ ] **AG-71-10**: Markdown renders through the existing Markdig no-HTML pipeline; Code through read-only AvaloniaEdit or escaped `pre`; neither runs script.
- [ ] **AG-71-11**: SVG passes an in-house XML allowlist sanitiser (DTD prohibited, no external reference, no script, no event attribute) and shows as an inert image.
- [ ] **AG-71-12**: CSV renders from a capped RFC 4180 preview as text cells with formula-like cells marked.
- [ ] **AG-71-13**: Images are validated by magic number, size and pixel caps before display and are never SVG.
- [ ] **AG-71-14**: Html and Mermaid run only in an isolated surface whose CSP is produced by one tested `ArtifactDocumentPolicy`.
- [ ] **AG-71-15**: The desktop surface uses NativeWebView with the post-message bridge off, a private non-persistent store, cancelled navigation and new windows, and no message subscription.
- [ ] **AG-71-16**: The web head uses a sandboxed iframe: `srcdoc` for static Html and Mermaid, and an API-served ticketed render document with its own response CSP for interactive Html.
- [ ] **AG-71-17**: Network access is requested per version, shown to the user, approved per session or per version, default off; storage access is not offered.
- [ ] **AG-71-18**: The surface is destroyed when hidden, stopped or signed out, and every blocked navigation is reported.
- [ ] **AG-71-19**: A local file store is authoritative on desktop; the API keeps the account listing; sync is one setting, off by default (metadata only until the user enables it; the web head shows a "sync is off" state for the artifact list), idempotent by hash, and a divergent number forks to a copy.
- [ ] **AG-71-20**: API routes are owner-scoped (another owner's id is 404), content is always served as an attachment octet-stream, and render tickets are 60 s, version-bound HMACs.
- [ ] **AG-71-21**: The panel is a labelled region with a named toolbar, a polite live region, non-colour diff markers, focus escape controls and no new colour role.
- [ ] **AG-71-22**: `ArtifactBlock` is an additive transcript block that opens the panel and degrades to `UnknownBlock` in old readers.
- [ ] **AG-71-23**: A hostile payload corpus is test data for every kind, every renderer and every tool argument, and every entry has an asserted outcome.
- [ ] **AG-71-24**: Create, update, grant and revoke emit a counter and an audit event (`artifact.created`, `artifact.updated`, `artifact.grant`, `artifact.revoke`, in the closed `cowork_events.kind` list fixed by phase 68 U3) carrying hashes and origins, never content, titles or account ids.

### Cowork Browser Use (72) (AG-72)

- [ ] **AG-72-1**: The agent browser is a host-owned Chromium process connected over a pipe (no debugging port) and used through PuppeteerSharp 25.15.0 as CDP client only.
- [ ] **AG-72-2**: Chromium starts with an allow-listed environment, argument-list arguments, the sandbox on, and a fresh 0700 profile per Cowork session wiped at its end.
- [ ] **AG-72-3**: Chromium is found (configured, managed, system) or acquired only through a user-confirmed, checksum-verified step; nothing downloads silently and nothing is bundled.
- [ ] **AG-72-4**: Every browser request passes a per-session loopback egress guard that resolves names itself, denies private, loopback, link-local and metadata addresses, and pins the connection to the validated address.
- [ ] **AG-72-5**: Top-level navigations, including redirects, pass a navigation gate; the requested origin is covered by the host gate (stage 1) and any other origin by a provider approval card (stage 2); Strict mode allows only listed hosts.
- [ ] **AG-72-6**: Frames from `Page.startScreencast` reach a pane through `IBrowserPaneFeed` with bounded rate and size; the desktop feed is built, the Web and Android feed is not (ADR-0036 D13) and those heads show `RemoteViewUnavailable`.
- [ ] **AG-72-7**: The user can take over the pane with pointer and keyboard on the desktop that runs the browser, and leaves keyboard capture with F6.
- [ ] **AG-72-8**: `BrowserToolProvider : ICoworkToolProvider` offers `browser_navigate`, `browser_click`, `browser_type`, `browser_scroll`, `browser_read`, `browser_screenshot`, `browser_wait`, `browser_back` and `browser_close` with hybrid element-ref and coordinate grounding and the Effect classes of ADR-0034 D7.
- [ ] **AG-72-9**: `browser_screenshot` returns one bounded image (default 1280x800, at most 3 MiB, at most 40 per session) the model receives and the pane shows in an Agent view strip.
- [ ] **AG-72-10**: A model that cannot read images gets a specific degraded state and a vision-model chooser fed by omp's model listing (`ModelReadsImages`).
- [ ] **AG-72-11**: Every page-derived block sets `IsUntrusted`, carries a per-result random-id marker, passes a visibility and invisible-Unicode filter, and is capped at 20,000 characters.
- [ ] **AG-72-12**: Sensitive fields, form submits, downloads, popups, dialogs, permissions and file choosers follow the ADR-0034 matrix; downloads are denied by default and uploads do not exist.
- [ ] **AG-72-13**: An indicator outside the page frame states that the agent controls the browser, with a Stop control that cancels in-flight calls and revokes control.
- [ ] **AG-72-14**: Dispatch-origin turns use the browser only when `AllowDispatchSessions` is on, with `Open` mode ignored and sensitive actions `MustBeLocal`.
- [ ] **AG-72-15**: Every limit (actions per minute, calls, screenshots, tabs, memory, idle, wall time) is a validated option, and logs and audit targets carry no page text, typed text or query string.
- [ ] **AG-72-16**: A local test site and a 19-page hostile corpus prove the host invariants on this Linux host, headless and under a private Xvfb.

### Cowork Computer Use (73) (AG-73)

- [ ] **AG-73-1**: `IComputerUseBackend` exposes availability, permissions, displays, windows (z-order), pointer, capture, input primitives, release-all and clipboard text, all async with `CancellationToken` last.
- [ ] **AG-73-2**: `IComputerActionGate` is the only route to injection; the executor needs a single-use ticket and an architecture test forbids any other reference to the backend.
- [ ] **AG-73-3**: The gate evaluates in the ADR-0035 D3 order and every refusal carries a stable `DenialCode` and a message id.
- [ ] **AG-73-4**: Grants are (session, `AppIdentity`, tier, origin, expiry); tiers are `ViewOnly`, `ClickOnly`, `Full`; the model cannot create a grant.
- [ ] **AG-73-5**: A data catalogue sets category defaults and a built-in denylist (terminals, password managers, system settings, trading and wallets, own process) that users may extend and never reduce.
- [ ] **AG-73-6**: `computer.list_windows` returns titles only for granted applications; hard-denied chords (launchers, run dialogs, kill chord) are refused.
- [ ] **AG-73-7**: Sensitivity classification (credential titles, deletion, purchase, mail and messaging raised above `ViewOnly`, clipboard, first action per application, Dispatch origin) forces local confirmation.
- [ ] **AG-73-8**: `IComputerApprovalPrompt` shows host-observed facts only, defaults focus to Deny, never auto-allows, and times out to deny after 120 s.
- [ ] **AG-73-9**: No primitive is injected while a prompt is open; own-process windows are denied as targets and masked in model frames; an optional security phrase shows in prompt and indicator.
- [ ] **AG-73-10**: `ComputerUseOptions` (`Enabled=false`) validate on start, name keys never values, and cover rate (60 per minute, burst 10), step limit (300, +100), failure pause (5), text cap (2000) and takeover threshold (24 px).
- [ ] **AG-73-11**: A global-hotkey kill switch on its own thread and OS connection is verified before arming; a trip revokes, releases all input, cancels the turn, updates the indicator and writes an audit record.
- [ ] **AG-73-12**: A top-most indicator is visible while armed or acting and the gate denies when it is not in the window list; pointer displacement over the threshold pauses with `UserTookOver`.
- [ ] **AG-73-13**: `ComputerAuditLog` writes owner-only JSON Lines with a `prev` SHA-256 chain, write-ahead before injection, denies on write failure, records typed text as length and hash only, keeps 30 days.
- [ ] **AG-73-14**: The frame pipeline maps coordinates (`CoordinateMap`), masks non-granted and own windows (`VisibleRegionMasker`), downscales to the model profile, encodes PNG under the byte cap and tracks the per-session image budget.
- [ ] **AG-73-15**: `ComputerToolProvider` (`ProviderId` `computer`) implements `ICoworkToolProvider` of ADR-0032 for the 15 `computer_*` tools with descriptor mapping, host-observed previews, untrusted blocks, and the Cowork `--config` overlay (owned by the Cowork profile) sets `computer.enabled=false` and `browser.enabled=false`.
- [ ] **AG-73-16**: The X11 backend captures with `XGetImage`, injects with XTest, lists displays (XRandR) and windows (EWMH, `XQueryTree` fallback) and types text including unmapped characters.
- [ ] **AG-73-17**: The X11 hotkey grabs the chord on a second connection and reports `KillSwitchUnavailable` on `BadAccess`.
- [ ] **AG-73-18**: Wayland sessions and platforms without a backend report typed unavailable states with remediation message ids and register no tools.
- [ ] **AG-73-19**: The Windows backend uses `SendInput`, GDI capture, UIPI and secure-desktop detection and `RegisterHotKey`, and builds warning-free on Linux.
- [ ] **AG-73-20**: The macOS backend uses CGEvent, the system `screencapture`, CoreFoundation window listing and Carbon hotkey, and builds warning-free on Linux.
- [ ] **AG-73-21**: `IComputerPermissionAdvisor` turns permission and availability states into guidance with Settings deep links and a bounded status poll.
- [ ] **AG-73-22**: No relay operation reaches a backend primitive; there is no `computer.*` relay operation (ADR-0036); a `cowork.task` whose `toolClasses` contains `computer_use`, on a desktop with `Cowork:ComputerUse:Enabled` and `Cowork:ComputerUse:AllowDispatchSessions`, registers `computer_*`, every other session registers none and `code.prompt` never does; approvals are local-only, `Cowork:ComputerUse:AllowDispatchSessions=false` by default, Dispatch grants end with the command and within 30 minutes.
- [ ] **AG-73-23**: A Computer Use settings page and opt-in dialog (provider disclosure, grants, denylist additions, kill chord, security phrase, audit) are accessible and tested headless.
- [ ] **AG-73-24**: The serialised scaffold step registers every project (slnx, slnf, `LayerMap`, architecture reference, locks, test projects); phase 73 adds code only and edits a registry file only for rows the scaffold manifest marks "per phase" (`docs/architecture.md` prose, parity audit rows); docs, changelog and `THIRD-PARTY-NOTICES.md` unchanged (no new package).

### Dispatch relay (74) (AG-74)

- [ ] **AG-74-1**: A desktop pairs under the signed-in account and receives a device id plus an API-signed ES256 credential (own key variable) bound to a device-held P-256 key stored only in the OS secret store.
- [ ] **AG-74-2**: Pairing requires a full sign-in no older than 10 minutes (newest non-refresh `amr` entry of the authenticated principal); Dispatch never re-validates user tokens.
- [ ] **AG-74-3**: Devices can be listed, renamed and revoked (one or all); each carries a capability list with versions, presence and last seen.
- [ ] **AG-74-4**: The hub is a WebSocket endpoint in `Autonogrammer.Api`; the newest connection of a device wins (close 4009); the hello challenge is single use (Dragonfly `auth` nonce).
- [ ] **AG-74-5**: Every command is a signed envelope (commandId, per-device seq, userId, deviceId, issuedAt, expiresAt, nonce, capability, payload at most 64 KiB, ES256 signature over canonical bytes), signed at delivery.
- [ ] **AG-74-6**: Per-device `seq` is strictly increasing and gap-free at allocation; delivery is at-least-once; the device applies each command once.
- [ ] **AG-74-7**: Acknowledgement states received, accepted, running, completed and failed carry a typed reason from a closed set.
- [ ] **AG-74-8**: A command for an offline device queues for at most 5 minutes and is then expired and never delivered.
- [ ] **AG-74-9**: No `computer.*` operation exists; Computer Use is a tool class named in the `toolClasses` of a `cowork.task` payload (`computer_use` is a capability flag and never a command capability; ADR-0035 D12 registers `computer_*` only for such a task on a desktop with `Cowork:ComputerUse:Enabled` and `Cowork:ComputerUse:AllowDispatchSessions`, and never for `code.prompt`), needs a local approval at task start and per action (ADR-0035), and cannot be approved remotely.
- [ ] **AG-74-10**: Revocation closes the socket within 2 seconds, fails queued and running commands, and makes the desktop end dispatched sessions and withdraw grants; "Sign out everywhere" revokes all devices.
- [ ] **AG-74-11**: Reconnect uses full-jitter backoff, unbounded, ceiling 30 seconds, with close-code specific behaviour.
- [ ] **AG-74-12**: Per-account rate limits (partitioned by `sub`), a per-device queue cap and a per-account device cap hold.
- [ ] **AG-74-13**: Every state change writes a `dispatch_audit` row in the same transaction; the ledger copy is ADR-0031's outbox, default off.
- [ ] **AG-74-14**: Presence is a Dragonfly TTL key under `autonogrammer:v1:presence`; first launch is one replica; the validator refuses unsafe multi-replica and in-memory settings.
- [ ] **AG-74-15**: The Devices and Dispatch views show no_devices, device_offline, queued with expiry, command_pending_approval, running with cancel, failed with reason, completed with summary, and every degraded state, accessibly.
- [ ] **AG-74-16**: Controllers use HTTPS POST plus SSE with `Last-Event-ID` resume only; no browser WebSocket.
- [ ] **AG-74-17**: The feature is off by default (404 `dispatch_disabled`); the desktop shows a persistent "Remote task running" strip.
- [ ] **AG-74-18**: Dispatch-started sessions use `SessionSurface.Code` or `Cowork` of ADR-0030 and reach the Cowork session host of ADR-0032; Cowork rows never enter the Code catalog.

### Cowork parity A (75) (AG-75)

- [ ] **AG-75-1**: A user can create, list, rename, archive and hard-delete projects (at most 200, name at most 100 characters) and bind local folders to them without uploading a file.
- [ ] **AG-75-2**: The API stores projects, global instructions and memory per account (`sub`), answers 404 for another account's row, and is rate limited per user.
- [ ] **AG-75-3**: Global instructions (at most 8 KiB) and project instructions (at most 16 KiB) are built with the memory brief (at most 4 KiB) into one standing-context block delivered as an embedded resource at the start of the first prompt, with a fixed paragraph of at most 2 KiB in the system-prompt file, inside the 8 KiB total apportioned in `CoworkSystemPromptOptions` (base 4 KiB, standing context 2 KiB, browser 1 KiB, computer 1 KiB).
- [ ] **AG-75-4**: Memory entries are scoped to an account or a project, full-text searchable, capped (1,000 entries, 1,000 characters, 20 agent saves per session per hour) and hard-deleted on request.
- [ ] **AG-75-5**: `MemoryContentPolicy` refuses never-saved classes always and sensitive categories unless the user opted in, before every write, and says why to the agent and the user.
- [ ] **AG-75-6**: `MemoryMode` is the session's fixed memory-off flag; Off removes the provider and the brief; pause, reset, per-entry delete and JSON export exist.
- [ ] **AG-75-7**: The agent reaches memory through the `memory` tool provider (`memory_recall`, `memory_save`, `memory_forget`, all `ByPolicy`), so Manual and Auto ask for save and forget.
- [ ] **AG-75-8**: A scheduled task can target a Cowork or Code session with a preset schedule, an approval mode and optional project and folders; a missed run folds into one late run within 24 hours.
- [ ] **AG-75-9**: An unattended run that needs approval waits, notifies and is cancelled after `UnattendedApprovalTimeout` (30 minutes), and the run writes `schedule.run`, `schedule.late` and `schedule.waiting_for_approval` events (the closed `cowork_events.kind` list of phase 68 U3); Skip is refused without a FilesystemIsolated session.
- [ ] **AG-75-10**: Projects, instructions, memory and scheduled tasks have view models and desktop views (projects and memory also on the web) with accessible names, keyboard paths and specific degraded states.
- [ ] **AG-75-11**: `docs/parity/cowork-parity.md` exists with rows C1 to C31 and K01 to K20 and this phase's rows graded with evidence.

### Cowork parity B (76) (AG-76)

- [ ] **AG-76-1**: A user can install a plugin bundle (Anthropic layout) from a local folder or `.zip` into user or project scope, enable or disable it, and see which of its components load; a project-scope bundle is off until enabled.
- [ ] **AG-76-2**: Install refuses path traversal, symbolic links, files over 2 MiB, more than 2,000 files or 50 MiB, and a malformed manifest, each with a named error.
- [ ] **AG-76-3**: Bundle skills reach omp through `skills.customDirectories`; commands join the palette as `/<plugin>:<command>`; agents are listed; `.mcp.json` servers join the MCP catalog off until confirmed one by one; hooks are listed and never run.
- [ ] **AG-76-4**: A per-skill enable switch excludes a disabled skill at launch; a built-in `/create-plugin` skill ships.
- [ ] **AG-76-5**: A connector card shows state; Check connection (user action only) lists tools; each tool and server is Always allow, Needs approval or Blocked, stored as permission rules.
- [ ] **AG-76-6**: `CoworkPermissionMode` gains `Auto`; Auto allows what the rules and the read-only list allow, asks otherwise, behaves like Manual for tool-provider and MCP calls and in a tainted session; the UI says "Auto (rules)"; the default stays Manual. "Mode" means only `CoworkPermissionMode` (Manual, Auto, Skip); other timeouts and policies use their own names (`CoworkToolHostOptions.ApprovalTimeout`, `Dispatch:CommandApprovalTimeout`, `UnattendedApprovalTimeout`, `BrowserUse:PolicyMode`).
- [ ] **AG-76-7**: Skip needs a FilesystemIsolated session or a per-session acknowledgement (ADR-0037 D10; an agreed amendment of ADR-0032 D4, written into ADR-0032 when it is accepted).
- [ ] **AG-76-8**: In every Cowork mode the ACP kind `delete` and shell commands recognised as deleting ask; the Cowork resolver also applies `Mcp(...)` allow and deny rules from the permission files.
- [ ] **AG-76-9**: Trusted folders gain an access mode (read-only or read-write) and a scope (session, project, global); `CoworkPathBoundary` also refuses `/`, the home directory itself and system directories.
- [ ] **AG-76-10**: The omp child starts through a process wrapper from a launch contribution: bubblewrap on Linux, `sandbox-exec` on macOS, a job object on Windows, with a probe, a reported level and named degraded states.
- [ ] **AG-76-11**: The sub-agent panel lists `task` tool calls from the ACP stream with status, elapsed time and the number running, accessibly.
- [ ] **AG-76-12**: A usage meter sums `usage_update` and per-turn usage into per-session and per-day views and a local ledger; the export history is no longer empty.
- [ ] **AG-76-13**: Notifications fan out to an in-app centre and OS sinks (Linux, macOS) with focus suppression, coalescing, Do-not-disturb and title-only privacy by default.
- [ ] **AG-76-14**: The phase's rows of `docs/parity/cowork-parity.md` are graded with evidence.

### Hardening and integration (77) (AG-77)

- [ ] **AG-77-1**: `VersionPrefix` in `Directory.Build.props` is `0.16.0`, and the API and worker Containerfile version `ARG` defaults (`AUTONOGRAMMER_VERSION` before the rename, `AUTONOGRAMMER_VERSION` after) equal it. The change is ONE commit, made after every other unit; `git log -S'<VersionPrefix>0.16.0'` finds exactly that commit.
- [ ] **AG-77-2**: After that commit `dotnet restore Autonogrammer.Linux.slnf --locked-mode` succeeds with the committed locks and the Android project restores on its own. The relock is run once; no later commit touches a `packages.lock.json`.
- [ ] **AG-77-3**: `CHANGELOG.md` has a `## [0.16.0]` section built from the Unreleased lines of the phases and a compare link; `Unreleased` is empty; `bash scripts/ci/check-changelog.sh` passes.
- [ ] **AG-77-4**: `ContractVersion.Current` is `1.6.0` and no commit after the scaffold commit touches `ContractVersion.cs` (R9: the bump is the scaffold step's, never this phase's).
- [ ] **AG-77-5**: Six runbooks exist, are listed in `docs/SUMMARY.md` and pass `scripts/docs/check_gitbook.py`: `accounts.md`, `data-plane.md`, `cowork.md`, `dispatch.md`, `browser-use.md`, `computer-use.md` (under `docs/runbooks/`), plus `v0.16.0-owner-actions.md`. Every page has front matter, one H1 and a Status line; ADR-0030 to ADR-0037, the three audit documents (AG-77-8, 10, 13) and every page a phase promised (`reference/cowork-projects-memory.md`, `reference/cowork-extensions.md`, `reference/cowork-safety.md`, `runbooks/cowork-isolation.md`) are listed too.
- [ ] **AG-77-6**: Each runbook has: what it does, how to enable it, a table of every option variable, how to verify, the degraded states with their user text, owner actions, what cannot be verified. A test fails when a `Dispatch__`, `Accounts__`, `DataPlane__` or `Cowork__` variable of `.env.example` is missing from a runbook table.
- [ ] **AG-77-7**: `README.md`, `docs/architecture.md`, `docs/guides/{status-and-roadmap,desktop-app-guide,web-app-guide,settings-guide,troubleshooting-and-faq,glossary,concepts}.md`, `docs/reference/api.md`, `docs/settings-parity.md` describe the Cowork surface, accounts, the data plane and Dispatch with their real status (off by default, nothing run live). `docs/architecture.md` has one row per `src/*` and `services/*` project; a test fails on a project directory without a row.
- [ ] **AG-77-8**: `docs/parity/cowork-parity.md` has rows C1 to C31 and K01 to K20 exactly once. A test fails on: an empty cell, a Status outside MET, MET-LIVE, PARTIAL, DEFERRED, NOT-STARTED, OMITTED-BY-DECISION; an empty Evidence cell; an empty Gap cell on any row that is not MET; a MET row whose Evidence names no test class that `grep` finds under `tests/`; a MET-LIVE row without a path to a recorded run; a counts block that differs from the counts regenerated by `scripts/docs/parity_counts.py --check`.
- [ ] **AG-77-9**: The same test has fixture cases for each failure above (one bad file each) and passes on the real file; a row cannot be graded by hand against the script.
- [ ] **AG-77-10**: `docs/quality/v0.16.0-security-review.md` has a scope table (one row per area of the Threat model section below, result and evidence) and a findings table: id, phase, area, severity (Critical, High, Medium, Low, Info), finding, evidence (file and test), status (open, fixed, accepted), owner action.
- [ ] **AG-77-11**: No finding of severity Critical or High is `open` or `accepted`: it is `fixed` with a named regression test. A Medium or Low `accepted` needs an OQ id. The test of AG-77-8 reads this table too. No phase PR merges while this is false.
- [ ] **AG-77-12**: With the shipped default configuration the API boots, `Dispatch`, Cowork and Accounts routes answer their disabled problem type (`dispatch_disabled`, `cowork_disabled`, accounts disabled) or 404, and each options validator refuses the unsafe values its brief names (in-memory presence with two replicas, a missing signing key with the feature on, a file key store). One integration test per module.
- [ ] **AG-77-13**: `docs/quality/v0.16.0-accessibility-pass.md` has a matrix of surface by check; every cell holds a result with the test or file that shows it, or "cannot be checked here" with the reason. Automated checks (names, roles, keyboard order, focus return, aria-live, reduced motion, contrast tokens) exist as tests for each surface of U5.
- [ ] **AG-77-14**: `./bootstrap.sh` exits 0 on the final integrated tree; the command, the date, the commit and the counts are recorded in `docs/runbooks/evidence/v0.16.0-gate-<date>/README.md`. A Live-category test is skipped with its stated reason, never run and never counted as passed.
- [ ] **AG-77-15**: After the replay of U7: `python3 scripts/rebrand/rebrand.py verify --baseline <dry-run json>` reports clean; the acceptance probes of the runbook (section 2) list only protected paths and URLs; ADR numbers, OQ ids, migration numbers (M0001 and up) and `ContractVersion` have no duplicate or fork; generated files (locks, notices, OpenAPI) are regenerated, never merged by hand.
- [ ] **AG-77-16**: One PR (or the repo's stacked equivalent) with the template completed (gate, tests, docs, CHANGELOG, OQ rows, DCO sign-off, light and dark screenshots for UI), CI green on every required check, every review thread (including Greptile, if it reviewed) answered; the PR description states what was not verified.
- [ ] **AG-77-17**: `docs/runbooks/v0.16.0-owner-actions.md` lists every owner action in the ADR-0030 table form (action and place, needed for, what the product does when it is not done) and the PR performs none of them.

### Traceability (AG)

| REQ-ID | Phase | Status (2026-10-28) |
|--------|-------|---------------------|
| AG-SC-1 | 66.1 | Planned |
| AG-SC-2 | 66.1 | Planned |
| AG-SC-3 | 66.1 | Planned |
| AG-SC-4 | 66.1 | Planned |
| AG-SC-5 | 66.1 | Planned |
| AG-SC-6 | 66.1 | Planned |
| AG-66-1 | 66 | Planned |
| AG-66-2 | 66 | Planned |
| AG-66-3 | 66 | Planned |
| AG-66-4 | 66 | Planned |
| AG-66-5 | 66 | Planned |
| AG-66-6 | 66 | Planned |
| AG-66-7 | 66 | Planned |
| AG-66-8 | 66 | Planned |
| AG-66-9 | 66 | Planned |
| AG-66-10 | 66 | Planned |
| AG-66-11 | 66 | Planned |
| AG-66-12 | 66 | Planned |
| AG-66-13 | 66 | Planned |
| AG-66-14 | 66 | Planned |
| AG-67-1 | 67 | Planned |
| AG-67-2 | 67 | Planned |
| AG-67-3 | 67 | Planned |
| AG-67-4 | 67 | Planned |
| AG-67-5 | 67 | Planned |
| AG-67-6 | 67 | Planned |
| AG-67-7 | 67 | Planned |
| AG-67-8 | 67 | Planned |
| AG-67-9 | 67 | Planned |
| AG-67-10 | 67 | Planned |
| AG-67-11 | 67 | Planned |
| AG-67-12 | 67 | Planned |
| AG-67-13 | 67 | Planned |
| AG-67-14 | 67 | Planned |
| AG-67-15 | 67 | Planned |
| AG-67-16 | 67 | Planned |
| AG-67-17 | 67 | Planned |
| AG-67-18 | 67 | Planned |
| AG-67-19 | 67 | Planned |
| AG-67-20 | 67 | Planned |
| AG-67-21 | 67 | Planned |
| AG-67-22 | 67 | Planned |
| AG-67-23 | 67 | Planned |
| AG-67-24 | 67 | Planned |
| AG-68-1 | 68 | Planned |
| AG-68-2 | 68 | Planned |
| AG-68-3 | 68 | Planned |
| AG-68-4 | 68 | Planned |
| AG-68-5 | 68 | Planned |
| AG-68-6 | 68 | Planned |
| AG-68-7 | 68 | Planned |
| AG-68-8 | 68 | Planned |
| AG-68-9 | 68 | Planned |
| AG-68-10 | 68 | Planned |
| AG-68-11 | 68 | Planned |
| AG-68-12 | 68 | Planned |
| AG-68-13 | 68 | Planned |
| AG-68-14 | 68 | Planned |
| AG-69-1 | 69 | Planned |
| AG-69-2 | 69 | Planned |
| AG-69-3 | 69 | Planned |
| AG-69-4 | 69 | Planned |
| AG-69-5 | 69 | Planned |
| AG-69-6 | 69 | Planned |
| AG-69-7 | 69 | Planned |
| AG-69-8 | 69 | Planned |
| AG-69-9 | 69 | Planned |
| AG-69-10 | 69 | Planned |
| AG-69-11 | 69 | Planned |
| AG-69-12 | 69 | Planned |
| AG-69-13 | 69 | Planned |
| AG-70-1 | 70 | Planned |
| AG-70-2 | 70 | Planned |
| AG-70-3 | 70 | Planned |
| AG-70-4 | 70 | Planned |
| AG-70-5 | 70 | Planned |
| AG-70-6 | 70 | Planned |
| AG-70-7 | 70 | Planned |
| AG-70-8 | 70 | Planned |
| AG-70-9 | 70 | Planned |
| AG-70-10 | 70 | Planned |
| AG-70-11 | 70 | Planned |
| AG-70-12 | 70 | Planned |
| AG-70-13 | 70 | Planned |
| AG-70-14 | 70 | Planned |
| AG-71-1 | 71 | Planned |
| AG-71-2 | 71 | Planned |
| AG-71-3 | 71 | Planned |
| AG-71-4 | 71 | Planned |
| AG-71-5 | 71 | Planned |
| AG-71-6 | 71 | Planned |
| AG-71-7 | 71 | Planned |
| AG-71-8 | 71 | Planned |
| AG-71-9 | 71 | Planned |
| AG-71-10 | 71 | Planned |
| AG-71-11 | 71 | Planned |
| AG-71-12 | 71 | Planned |
| AG-71-13 | 71 | Planned |
| AG-71-14 | 71 | Planned |
| AG-71-15 | 71 | Planned |
| AG-71-16 | 71 | Planned |
| AG-71-17 | 71 | Planned |
| AG-71-18 | 71 | Planned |
| AG-71-19 | 71 | Planned |
| AG-71-20 | 71 | Planned |
| AG-71-21 | 71 | Planned |
| AG-71-22 | 71 | Planned |
| AG-71-23 | 71 | Planned |
| AG-71-24 | 71 | Planned |
| AG-72-1 | 72 | Planned |
| AG-72-2 | 72 | Planned |
| AG-72-3 | 72 | Planned |
| AG-72-4 | 72 | Planned |
| AG-72-5 | 72 | Planned |
| AG-72-6 | 72 | Planned |
| AG-72-7 | 72 | Planned |
| AG-72-8 | 72 | Planned |
| AG-72-9 | 72 | Planned |
| AG-72-10 | 72 | Planned |
| AG-72-11 | 72 | Planned |
| AG-72-12 | 72 | Planned |
| AG-72-13 | 72 | Planned |
| AG-72-14 | 72 | Planned |
| AG-72-15 | 72 | Planned |
| AG-72-16 | 72 | Planned |
| AG-73-1 | 73 | Planned |
| AG-73-2 | 73 | Planned |
| AG-73-3 | 73 | Planned |
| AG-73-4 | 73 | Planned |
| AG-73-5 | 73 | Planned |
| AG-73-6 | 73 | Planned |
| AG-73-7 | 73 | Planned |
| AG-73-8 | 73 | Planned |
| AG-73-9 | 73 | Planned |
| AG-73-10 | 73 | Planned |
| AG-73-11 | 73 | Planned |
| AG-73-12 | 73 | Planned |
| AG-73-13 | 73 | Planned |
| AG-73-14 | 73 | Planned |
| AG-73-15 | 73 | Planned |
| AG-73-16 | 73 | Planned |
| AG-73-17 | 73 | Planned |
| AG-73-18 | 73 | Planned |
| AG-73-19 | 73 | Planned |
| AG-73-20 | 73 | Planned |
| AG-73-21 | 73 | Planned |
| AG-73-22 | 73 | Planned |
| AG-73-23 | 73 | Planned |
| AG-73-24 | 73 | Planned |
| AG-74-1 | 74 | Planned |
| AG-74-2 | 74 | Planned |
| AG-74-3 | 74 | Planned |
| AG-74-4 | 74 | Planned |
| AG-74-5 | 74 | Planned |
| AG-74-6 | 74 | Planned |
| AG-74-7 | 74 | Planned |
| AG-74-8 | 74 | Planned |
| AG-74-9 | 74 | Planned |
| AG-74-10 | 74 | Planned |
| AG-74-11 | 74 | Planned |
| AG-74-12 | 74 | Planned |
| AG-74-13 | 74 | Planned |
| AG-74-14 | 74 | Planned |
| AG-74-15 | 74 | Planned |
| AG-74-16 | 74 | Planned |
| AG-74-17 | 74 | Planned |
| AG-74-18 | 74 | Planned |
| AG-75-1 | 75 | Planned |
| AG-75-2 | 75 | Planned |
| AG-75-3 | 75 | Planned |
| AG-75-4 | 75 | Planned |
| AG-75-5 | 75 | Planned |
| AG-75-6 | 75 | Planned |
| AG-75-7 | 75 | Planned |
| AG-75-8 | 75 | Planned |
| AG-75-9 | 75 | Planned |
| AG-75-10 | 75 | Planned |
| AG-75-11 | 75 | Planned |
| AG-76-1 | 76 | Planned |
| AG-76-2 | 76 | Planned |
| AG-76-3 | 76 | Planned |
| AG-76-4 | 76 | Planned |
| AG-76-5 | 76 | Planned |
| AG-76-6 | 76 | Planned |
| AG-76-7 | 76 | Planned |
| AG-76-8 | 76 | Planned |
| AG-76-9 | 76 | Planned |
| AG-76-10 | 76 | Planned |
| AG-76-11 | 76 | Planned |
| AG-76-12 | 76 | Planned |
| AG-76-13 | 76 | Planned |
| AG-76-14 | 76 | Planned |
| AG-77-1 | 77 | Planned |
| AG-77-2 | 77 | Planned |
| AG-77-3 | 77 | Planned |
| AG-77-4 | 77 | Planned |
| AG-77-5 | 77 | Planned |
| AG-77-6 | 77 | Planned |
| AG-77-7 | 77 | Planned |
| AG-77-8 | 77 | Planned |
| AG-77-9 | 77 | Planned |
| AG-77-10 | 77 | Planned |
| AG-77-11 | 77 | Planned |
| AG-77-12 | 77 | Planned |
| AG-77-13 | 77 | Planned |
| AG-77-14 | 77 | Planned |
| AG-77-15 | 77 | Planned |
| AG-77-16 | 77 | Planned |
| AG-77-17 | 77 | Planned |
