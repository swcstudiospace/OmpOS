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

- [ ] **TD-01**: Temporal dev ports in `deploy/compose.yaml` bind to loopback or require auth; they are not reachable on all host interfaces. Source: audit tech_debt 01-foundation M-01 (`deploy/compose.yaml:9-11`), line 68.
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
- [ ] **TD-42**: `04-03-SUMMARY.md` frontmatter lists DESK-01 and MOB-01 as well as SHELL-01. Source: audit records-and-docs (`04-03-SUMMARY.md:2`), line 115.
- [ ] **TD-43**: The VERIFY-02 TODO-linkage gate has an in-repo positive case and negative case, not only a recorded negative control in commit a6fcadd. Source: audit test-evidence (`05-VALIDATION.md:42`; `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:121`), line 119.
- [ ] **TD-44**: The two `incomplete` axe items on the icon-only send control are resolved or documented with evidence, and the unexplained skip-link screenshot is explained or reproduced. Source: audit test-evidence (`docs/runbooks/local-dev.md:96-97`), line 120.

### Owner-Deferred Items (OD)

Source for each line: audit `owner_decided_deferrals` (OD-n) and the open-question ID.

- [ ] **OD-01**: Interactive tool approval: a UI decision reaches a pending ACP `session/request_permission`; `RequestToolApprovalAsync` and `ResolveToolApprovalAsync` (`src/OmpOS.Sessions/SessionService.cs:286`, currently internal) get a production caller; deny-by-policy default is preserved and approval may only deny earlier. Source: audit OD-1; `docs/open-questions.md:42` (Q-HARN-PERMISSION); handoff decision 2.
- [ ] **OD-02**: Temporal worker composition: real provider and activity implementations are composed into the worker, and the worker is wired into the API session-run path behind `Workflows:Enabled` (`services/OmpOS.Api/Program.cs:54-63` uses FakeSandboxProvider; `services/OmpOS.Workflows.Worker/WorkerHostConfiguration.cs:91-97` guards activities). Source: audit OD-2; `docs/open-questions.md:41` (Q-SB-WORKER-COMPOSE); handoff decision 1.
- [ ] **OD-03**: The single dev principal (`services/OmpOS.Api/Security/SingleDevPrincipal.cs:8,11`) is replaced by an authentication provider using the sign-in method the owner selects; unauthenticated cross-origin requests are denied. Gate: owner decision on sign-in method. Source: audit OD-3; `docs/open-questions.md:32` (OQ-26); handoff decision 4 and deploy blocker 1.
- [ ] **OD-04**: Per-sandbox credential issuance: each sandbox receives its own scoped credential, revoked at teardown, beyond duplicate env-target rejection. Source: audit OD-4; `docs/open-questions.md:40` (Q-SB-CREDENTIALS).
- [ ] **OD-05**: The direct-path retry policy is decided and implemented: bounded retries for idempotent activities as on the Temporal path, or an explicit fail-fast setting with tests; the decision is recorded in the open question. Source: audit OD-5; `docs/open-questions.md:39` (Q-WF-RETRY); handoff known deferrals.
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
- [ ] **HO-04**: The replay fixture `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` is re-validated against a live Temporal dev server on this host. Source: audit HO-4 (`03-VERIFICATION.md` human verification 1). The Temporal CLI was absent at scaffold time (PROJECT.md Context).

### Hosting (HOST)

Gate for every HOST requirement: owner decision on the sign-in method (OD-03).

- [ ] **HOST-01**: The API and worker deploy to Railway from committed config-as-code built with podman-compatible OCI build files, configured through the variables in `.env.example`; `/health/live` and `/health/ready` respond on the deployed URL. Source: PROJECT.md Constraints (Hosting split); PROJECT.md Out of Scope (Railway deployment, superseded by this milestone); handoff "Before any public deployment".
- [ ] **HOST-02**: The hosted API is not publicly exposed until the OD-03 sign-in method is in place; the deploy checklist verifies that unauthenticated cross-origin requests are denied by the locked CORS default. Source: handoff decision 4; handoff "Before any public deployment" item 1; audit OD-3.
- [ ] **HOST-03**: The API and worker connect to Temporal Cloud with credentials supplied only through environment variables (none committed), and readiness reflects Temporal Cloud connectivity. Source: PROJECT.md Out of Scope (Temporal Cloud setup, superseded by this milestone); PROJECT.md Key Decisions (Temporal optional behind `Workflows:Enabled`); audit OD-2.
- [ ] **HOST-04**: The hosted API path runs the real omp harness instead of only the in-memory adapter (`services/OmpOS.Api/Program.cs`). Source: handoff decision 9; audit integration INT-OMP-IN-API (`services/OmpOS.Api/Program.cs:51`).
- [ ] **HOST-05**: `vercel.json` `connect-src` allows the production API origin (currently `'self'` and loopback only, `vercel.json:31`), and the web head reads that origin from `Api:BaseUrl` or `apiBaseUrl`. Source: audit HO-7 (`vercel.json:31`); handoff "Before any public deployment" item 2.
- [ ] **HOST-06**: The static publish output is deployed to Vercel, and the runbook browser smoke on the deployed URL shows the same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar) with no CSP violations. Source: audit HO-7; handoff "Before any public deployment" item 4.

### Mac Builds (MAC)

- [ ] **MAC-01**: `apps/OmpOS.App` builds for `net10.0-ios` with warnings as errors on a macOS host with Xcode 27.1. Source: audit HO-6; handoff decision 5 and "Before any public deployment" item 3; `docs/open-questions.md:23` (OQ-17).
- [ ] **MAC-02**: `apps/OmpOS.App` builds for `net10.0-maccatalyst` with warnings as errors on the same macOS host. Source: audit HO-6; handoff "Before any public deployment" item 3.
- [ ] **MAC-03**: The macOS CI `shells` job (`.github/workflows/ci.yml:74-88`) runs green on a macOS runner and records DESK-01, MOB-01 and SHELL-01 evidence; they stay Partial until it does. Source: audit HO-6; handoff decision 5; `docs/open-questions.md:23` (OQ-17).
- [ ] **MAC-04**: Runtime spot-check on the macOS build: desktop streams against the remote backend; mobile resumes remote-only and never spawns a process. Source: handoff "Before any public deployment" item 3 (verify line).

### Records and Evidence (REC)

- [ ] **REC-01**: Blocking record BR-1 is closed: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:121`) runs and passes at HEAD; `05-VERIFICATION.md` is re-run to replace its gaps_found status; VERIFY-02 is added to `05-01-SUMMARY.md` requirements-completed only if the re-run passes. Source: audit section 2.2 and front matter `gaps.blocking_records` (BR-1); `05-VALIDATION.md:42`.
- [ ] **REC-02**: The clean-clone bootstrap is re-recorded at the new HEAD: a fresh clone runs `./bootstrap.sh`, and date, host, SDK version, duration and result are recorded in `docs/runbooks/local-dev.md` with the log under `docs/runbooks/evidence/`. Source: audit HO-9 (`docs/runbooks/local-dev.md:106-118`; `05-VALIDATION.md:35`); handoff item 5.
- [ ] **REC-03**: Nyquist validation runs for phases 1 to 5 and each phase gets a real verdict (audit: 0 of 5 compliant; phase 1 partial, phases 2 to 5 not validated). Source: audit front matter `nyquist`; audit section 9; audit section 12 item 5.
- [ ] **REC-04**: The milestone re-audit reports a status other than `gaps_found`: no blocking code gaps, no blocking records, and every front-matter `tech_debt` entry closed or re-classified with evidence. Source: audit section 12 (routing); audit front matter `status: gaps_found`.
- [ ] **REC-05**: `docs/handoff.md` and `docs/open-questions.md` reflect every closure in this milestone (known deferrals, owner decisions, AC verdicts, open-question status), so no closed item still appears as deferred. Source: `docs/handoff.md` "Known deferrals" and "Owner decisions (2026-10-21)"; handoff AC_11 verdict.

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
