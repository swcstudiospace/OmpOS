# OmpOS v0.9.1 handoff — implemented / stubbed / deferred

**Milestone:** OmpOS v0.9.1 · **Date:** 2026-10-21 · **Author task:** Phase 5 verify pass

## Provenance of the acceptance list

The AC_1..AC_11 enumeration below is the list **reconstructed 2026-10-20** in
`.planning/milestones/v0.9.1-phases/05-verify/05-CONTEXT.md` from the ultrathink brief and the v1
requirement set. The original enumerated list could not be retrieved from the
ultrathink plan store, Notion task graph, or git history. This gap is tracked as
open question **Q-AC-LIST** in `docs/open-questions.md`. Phase 5 verifies against
the reconstructed list and states that here.

## Bootstrap gate evidence

Full gate `./bootstrap.sh` (SDK install, locked restore, warnings-as-errors
Release build, tests, format verification). Log: `docs/runbooks/evidence/bootstrap-gate-2026-10-21-180817d.log`:

- Line 81: `Test run summary: Passed!`
- Line 98: `total: 707` · Line 99: `failed: 0`
- Line 103: `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`
- Scope: everything in `OmpOS.Linux.slnf` (libraries, API, worker, web head, all
  test suites). The MAUI head is excluded from that filter by design. Its Android target is built by the macOS `shells` job in `ci.yml`; iOS and MacCatalyst are not built on hosted runners (they need Xcode 27.1, which is not a stable toolchain there).

**Gate defect, recorded honestly:** the API integration suite
(`tests/OmpOS.Api.IntegrationTests`, ~60 tests) was not registered in the
solution files until this pass, so earlier gate totals never ran it — a
pre-registration total of 552 excluded it. It is now registered in both files:
`OmpOS.slnx:39` and `OmpOS.Linux.slnf:35`. The 707 total above includes it.

**Caveat:** the bootstrap gate log is a working-tree run. The clean-clone run (VERIFY-01) at the final code head `180817d` is `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-180817d.log`: a fresh clone of the local repository, an empty SDK root, bootstrap OK, 707 tests, 0 failed. Earlier records are kept: `bc3d7b4` (706 tests), `fc4f069` (706 tests), `642c1e5` (704 tests) and `a6fcadd` (615 tests), each under `docs/runbooks/evidence/`.
`docs/runbooks/local-dev.md` documents the one-command clean-clone flow.

## AC_1..AC_11 verdicts

| AC | Criterion (reconstructed) | Status | Evidence |
| --- | --- | --- | --- |
| AC_1 | Monorepo scaffold boots green from one command (`bootstrap.sh`) with pinned SDK, locked restore, warnings-as-errors | Implemented | `docs/runbooks/evidence/bootstrap-gate-2026-10-21-180817d.log` (exit path OK, 707/0); `OmpOS.Linux.slnf`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` |
| AC_2 | Every prompt string yields exactly one `IntentEnvelope`; catch-all fallback is unremovable; omp keyword rules honoured (PARSE-01..04) | Implemented | `PromptParserTests`, `MagicKeywordBoundaryTests`, `RecognizerOrderingTests` (`tests/OmpOS.Parser.Tests/`) |
| AC_3 | Typed harness with explicit state machine; scripted in-memory adapter drives UI work without omp; six failure modes covered (HARN-01..03, HARN-06) | Implemented (remote transport stubbed) | `HarnessStateMachineTests`, `InMemoryAdapterTests`, `FailureModeTests` (`tests/OmpOS.Harness.InMemory.Tests/`); remote WebSocket transport is a bounded stub — `WebSocketOmpTransportStubTests`, open question **Q-HARN-WS** |
| AC_4 | Real ACP v1 and omp RPC JSONL clients pass replay + live smoke against installed omp, no model spend (HARN-04, HARN-05) | Implemented (interactive approval deferred) | `AcpFrameReplayTests`, `AcpStdioTransportTests`, `AcpAgentSessionTests` (`tests/OmpOS.Harness.Acp.Tests/`, incl. authenticate + deny-by-default `fs/read_text_file` handlers); `RpcFrameReplayTests`, `RpcChunkReassemblyTests`, `RpcCommandCorrelationTests`, `RpcPromptCompletionTests`, `RpcLiveSmokeTests`; approval answers deny-by-default — interactive resolution deferred, **Q-HARN-PERMISSION** |
| AC_5 | Append-only sequence-numbered session log; one-active-prompt enforcement; replay-equal projection (SESS-01..03) | Implemented | `SessionServiceTests`, `ProjectionReplayTests`, `SessionRepositoryContractTests` (`tests/OmpOS.Sessions.Tests/`) |
| AC_6 | Secured observable API: validated endpoints, per-session rate limit, locked CORS, committed OpenAPI 3.1, SSE resume without gaps/duplicates, health, fail-fast config, correlation, redaction, auth boundary (API-01..03, OPS-01..04) | Implemented (auth is dev-only — deploy blocker) | `SessionEndpointTests`, `SseResumeTests`, `GuardTests`, `ParserPromptTests`, `SecurityMitigationsTests`, `OperationsTelemetryTests`, `AuthBoundaryTests` (`tests/OmpOS.Api.IntegrationTests/`); `docs/api/openapi.json` (committed contract). Auth is a no-op single-dev-principal boundary (OQ-26) — see deploy blockers |
| AC_7 | Sandbox provider abstraction: fake + local podman-compose providers under contract tests; explicit isolation defaults (SBOX-01..03, SBOX-05) | Implemented (per-sandbox credential issuance deferred) | `FakeSandboxProviderContractTests`, `LocalPodmanComposeProviderContractTests`, `SandboxLifecycleTests`, `IsolationDefaultsTests` (`tests/OmpOS.Sandbox.Tests/`); `deploy/compose.yaml`; per-sandbox credential issuance deferred — **Q-SB-CREDENTIALS** |
| AC_8 | Vendor sandbox stubs (OpenShell, agentOS, Railway, dstack-shaped enclave) return typed NotAvailable/Unverified with owner-tagged open questions; no invented vendor APIs (SBOX-04) | Stubbed by design | `VendorStubTests` (`tests/OmpOS.Sandbox.Tests/`): all four stubs throw `NotAvailable` for every operation; the enclave attestation verdict is `Unverified`; **Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE** |
| AC_9 | Durable session-run workflow behind `Workflows:Enabled` with deterministic replay test; direct runner is the default path (WFLW-01..03) | Implemented (worker composition + direct-path retry deferred) | With `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner` provisioned via `ISandboxProvider` (`FakeSandboxProvider` by default) with teardown in `finally` (`src/OmpOS.Workflows/DirectSessionRunner.cs:65`); DI registrations in `services/OmpOS.Api/Program.cs:67-69` (`services/OmpOS.Api/Workflows/ApiSessionRunActivities.cs`); `DirectRunnerTests`, `SessionRunWorkflowTests`, `TemporalReplayTests`, `WorkerHostTests` (`tests/OmpOS.Workflows.Tests/`). The Temporal worker is **not** wired into the API — **Q-SB-WORKER-COMPOSE** stands; no retry on the direct path — **Q-WF-RETRY** |
| AC_10 | Generative UI: all UiBlock variants + unknown-fallback render as a pure fold over the event log; Blazor WASM web head deploys statically to Vercel and completes the vertical slice in a browser; MAUI head shares the RCL — desktop in-process, mobile remote-only (UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01) | Implemented for RCL + web head; Deferred for MAUI binaries | `UiBlockRendererTests`, `ReplayTreeTests`, `StreamingStateTests`, `AccessibilityTests`, `SkipLinkIsFirstFocusableAndTargetsConversation`, `NewConversationButtonTests` (`tests/OmpOS.UI.Tests/`); `SseParserTests`, `SseEventStreamTests`, `ApiBaseUrlResolverTests` (apiBaseUrl rejection), `VercelConfigTests` (CSP `connect-src` property), `PublishSmokeTests` (`tests/OmpOS.Web.Tests/`); `EngineHostTests` (`tests/OmpOS.EngineHost.Tests/`). Web head browser-verified: `docs/runbooks/local-dev.md` (Vertical slice section) + `docs/runbooks/evidence/web-vertical-slice-smoke.webp`; axe-core reports zero violations (same runbook section). MAUI binaries deferred — macOS human build outstanding |
| AC_11 | Milestone governance: architecture/security/format/vulnerability gates green, docs (architecture, ADRs, runbooks, handoff) reflect the final tree with implemented/stubbed/deferred fidelity (VERIFY-01..03) | Implemented (TODO-linkage test: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion`) | Bootstrap gate in `docs/runbooks/evidence/bootstrap-gate-2026-10-21-180817d.log` (707/0); clean-clone run in `docs/runbooks/local-dev.md` (section Clean-clone bootstrap) and `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-180817d.log`; `ArchUnitLayerTests`, `ProjectGraphTests`, `RepoHygieneTests` (`tests/OmpOS.ArchitectureTests/`); `docs/architecture.md`, `docs/decisions/` (ADR-0001..0003 + index), `docs/runbooks/`, this file; VERIFY-02 sweep recorded in `.planning/milestones/v0.9.1-phases/05-verify/05-01-SUMMARY.md` |

## Owner decisions (2026-10-21)

| # | Decision | Effect |
| --- | --- | --- |
| 1 | WFLW-01 is wired: with `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner`, provisioning via `ISandboxProvider` (`FakeSandboxProvider` by default) and tearing down in `finally`; the Temporal worker is not wired into the API | **Q-SB-WORKER-COMPOSE** stands; AC_9 reads Implemented with that deferral |
| 2 | `SessionService.RequestToolApprovalAsync` and `ResolveToolApprovalAsync` are `internal` (`InternalsVisibleTo` `Sessions.Tests` only); interactive approval stays deferred | **Q-HARN-PERMISSION** stands; AC_4 reads Implemented with that deferral |
| 3 | The markdown `MarkupString` sink is accepted: its only input is `MarkdownRenderer` output, produced with raw HTML disabled and a link/image scheme allowlist, under a CSP without inline script | No finding; recorded here as an accepted sink |
| 4 | The API has no authentication — a single local dev principal (OQ-26); it must not be exposed publicly until an auth provider exists | Deploy blocker (see below) |
| 5 | The macOS MAUI build is partly verified: the CI `shells` job builds the Android target on a hosted macOS runner; iOS and MacCatalyst need Xcode 27.1, which hosted runners do not provide as a stable toolchain | DESK-01 / MOB-01 / SHELL-01 stay Partial; AC_10 stays split |
| 6 | Remote repository: `https://github.com/swcstudiospace/OmpOS` (currently empty and public; local `git remote -v` is unconfigured) | origin is `https://github.com/swcstudiospace/OmpOS.git`; `main` and `feat/ompos-scaffold` were pushed on 2026-10-21; PR #1 is open |
| 7 | Tech debt from the milestone audit (43 items) is accepted and tracked in the backlog, not fixed in v0.9.1 | Recorded in the archived audit (`.planning/milestones/v0.9.1-MILESTONE-AUDIT.md`) |
| 8 | The human-only items are carried as tracked follow-ups: podman-compose validation, podman smoke test, live Temporal replay check, TCP readiness sign-off (4c), iOS and MacCatalyst builds (Xcode 27.1), live Vercel deploy, screen-reader pass, clean-clone re-record at a later HEAD | Each has its file reference in the audit; none blocks the archive |
| 9 | The API host runs only the in-memory harness for v0.9.1 (`services/OmpOS.Api/Program.cs`); wiring the real ACP harness is the first item of the next milestone | Acknowledged scope limit for the vertical slice |
| 10 | Ship: push `main` and `feat/ompos-scaffold` to `https://github.com/swcstudiospace/OmpOS` and open a PR into `main`. The PR is not merged by the orchestrator, but the user's later ship instruction supersedes this: the PR merges through ultrathink-ship only after a completed Greptile review scores 5 with no open threads and CI passes | Superseded by the user's ship instruction on 2026-10-21; this row is kept as history |

## Known deferrals

All open-question IDs below were confirmed present in `docs/open-questions.md` on 2026-10-21:

- **Interactive approval** — no UI-to-harness resolution path (**Q-HARN-PERMISSION**).
- **Remote WebSocket transport** — faulting stub; SSE remains the client transport (**Q-HARN-WS**).
- **Unseen `session/update` variants** — fixtures frozen from recorded/scripted turns; unknown-fallback path until verified against a live omp (**Q-HARN-UPDATE**).
- **Sandbox vendor stubs** — OpenShell, agentOS, Railway Swarm, attested enclave (**Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE**).
- **Per-sandbox credential issuance** — duplicate-target rejection only (**Q-SB-CREDENTIALS**).
- **Real provider/activity composition into the Temporal worker** — opt-in fake activities only (**Q-SB-WORKER-COMPOSE**).
- **Direct-path retry policy** — fail fast on the direct path; retries live on the Temporal path (**Q-WF-RETRY**).
- **Blazor reflection switch override** for the web/MAUI heads (**Q-WEB-REFLECTION**).
- **Authoritative AC enumeration** — verifying against the reconstructed list until the original is retrieved (**Q-AC-LIST**).
- **iOS and MacCatalyst builds (human)** — need Xcode 27.1; the macOS `shells` job builds only the Android target, and the other targets are unverified.
- **Live Vercel deploy** — needs a production API origin, which must also be added to `connect-src` (`vercel.json:31` currently allows only `'self'` + loopback).
- **Auth provider** — deploy blocker (decision 4 above).
- **Accepted MarkupString sink** — decision 3 above; not a deferral, recorded for audit continuity.
- **Remote repository** — decision 6 above; first push and remote CI run outstanding.

## Accessibility evidence (UI-03)

axe-core 4.14.0, WCAG 2.0 and 2.1 levels A and AA, run 2026-10-21 against the
published Release head in an owned headless Chrome (`docs/runbooks/local-dev.md`,
Accessibility run section):

- Empty state and an active conversation with a streamed reply: **0 violations** in both states.
- First-run findings, fixed: missing skip link and the New-button accessible-name mismatch (`AccessibilityTests.SkipLinkIsFirstFocusableAndTargetsConversation`, `NewConversationButtonTests`).
- Remaining `incomplete` items, both on the icon-only send control: axe cannot evaluate a pictogram's label or colour automatically. Measured live: foreground `rgb(0, 0, 0)` on `rgb(239, 239, 239)`, contrast **18.26:1** (WCAG 1.4.11 needs 3:1 for UI components; text needs 4.5:1).
- One unexplained screenshot (skip link visible immediately after the full axe run; hidden in every probe before, during stream/send, and in a later same-state screenshot) — cause not isolated, recorded in the runbook.
- Not verified here: the MAUI head's runtime behaviour (its Android target compiles in CI; nothing runs on a device) and assistive-technology testing with a screen reader.

## Before any public deployment (human verification)

1. **Auth provider (blocker).** The API accepts unauthenticated localhost callers as the single dev principal (`AuthBoundaryTests`, `OmpOS.Api.IntegrationTests`). Do not expose it publicly until a real auth provider fronts it. Verify: unauthenticated cross-origin requests are denied by the locked CORS default, and every session carries the expected owner once auth lands.
2. **Production API origin in `connect-src`.** `vercel.json:31` allows only `'self'` + loopback. Before deploying the static head, add the production API origin to `connect-src` (and point the head at it via `Api:BaseUrl` / `?apiBaseUrl=`); otherwise the browser blocks every API call. Verify: load the deployed head, send a prompt, observe the streamed reply with no CSP violations in the console.
3. **iOS and MacCatalyst builds (human).** With Xcode 27.1 on a macOS host, build `apps/OmpOS.App` for `net10.0-ios` and `net10.0-maccatalyst` with warnings as errors (the `shells` job builds only `net10.0-android`), then spot-check desktop against the remote backend and mobile remote-only resume. Verify: warning-free build; desktop streams; mobile never spawns a process.
4. **Live Vercel deploy.** Deploy the static output once the origin above exists and re-run the runbook browser smoke against it. Verify: same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar).
5. **Clean-clone bootstrap record (VERIFY-01).** Fresh `git clone` into a temp dir → `./bootstrap.sh` → record date, host, SDK version, duration, result in `docs/runbooks/local-dev.md`.
