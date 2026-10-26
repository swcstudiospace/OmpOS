<!-- Tracking: ultrathink node n2 "Decompose into monorepo layout and dependency direction" — SPE-17752 (https://linear.app/swcstudio/issue/SPE-17752); node n8 "Synthesize ordered execution plan in parallel waves" — SPE-17756 (https://linear.app/swcstudio/issue/SPE-17756) -->

# Roadmap: OmpOS

## Milestones

- v0.9.1: foundation through verified handoff. Closed 2026-10-21; merge of PR #1 (swcstudiospace/OmpOS) pending. Archived in `.planning/milestones/` (v0.9.1-ROADMAP.md, v0.9.1-REQUIREMENTS.md, v0.9.1-MILESTONE-AUDIT.md, v0.9.1-phases/).
- v0.9.2 Hardening and hosted API: audit status `tech_debt` (2026-10-21, `.planning/v0.9.2-MILESTONE-AUDIT.md`). Phases 1-5, 7, 10, 11, 17-19 complete; 8, 9, 15, 16 superseded; 6, 12, 13, 14 blocked on external criteria. Completion awaits the owner (irreversible). Requirements in `.planning/REQUIREMENTS.md` (75 REQ-IDs).
- v0.13.0 Cursor 1:1 workspace and provider platform: planned 2026-10-22 from the ultrathink plan (graph ut-mvjvg4gj-fb21d80f); phases 36 to 43; requirements VX-01 to VX-10 in `.planning/REQUIREMENTS.md`.
- v0.10.0 Cross-platform UI: active. Phases 20 and 21 complete (both verified). Phase 22 blocked on external hosts: criterion 1 met on the XWayland path (2026-10-24); criteria 3 (Windows runner) and 4 and 5 (Mac channel) open. Phases 23 to 25 partial or blocked (see STATE). Requirements XP-01 to XP-11.
- v0.14.0 Agent loop and Agent Panel: planned 2026-10-26 from the user request to connect claude-ultrathink, agent-swarm and agent-substrate to the agent loop as pinned submodules and omp plugins, add GSD-Core as an omp plugin, build the default Agent Panel and push to main; phases 44 to 47; requirements AL-01 to AL-12 in `.planning/REQUIREMENTS.md`.

## Planning Rules

- Phases run in wave order by the dependencies listed per phase. The orchestrator runs `./bootstrap.sh` once after each wave; a phase is done when its success criteria are observable and the gate passes at the phase HEAD.
- Every HOST requirement is gated on the owner's sign-in decision (OD-03). No hosted phase starts before it, and no public exposure happens before it (HOST-02).
- Vendor interfaces are implemented only against verified sources. Where verification fails, the phase records the verified blocker in `docs/open-questions.md` instead of inventing a mapping.
- Phases are sized to be verified by the gate on their own.

## Phases

- [x] **Phase 1: Validation Evidence and Record Fixes** - Negative proofs for 01-VALIDATION rows and the 04-03 summary frontmatter
- [x] **Phase 2: Acceptance-Criteria List Recovery** - Recover or record AC_7, AC_9, AC_10, AC_11
- [x] **Phase 3: Web Head Reflection and Browser Verification** - Reflection switch off for the web head; axe and skip-link items resolved
- [x] **Phase 4: AgentOS Remaining Operations** - Verified agentOS operations or verified fail-closed records
- [x] **Phase 5: Railway Swarm Remaining Operations** - Verified Railway GraphQL operations or verified fail-closed records
- [ ] **Phase 6: Per-Sandbox Credential Issuance** - Scoped credentials for non-podman providers, revoked at teardown. BLOCKED (external): no verified vendor secret-store operation
- [x] **Phase 7: PostgreSQL Session Log** - Shared session log behind `ISessionRepository` for API and worker
- [x] **Phase 8: Worker Composition and Routed Settlement and Streaming** - ~~Real worker composition; routed runs write and stream through the log~~ Superseded 2026-10-21 (worker removed, docs/handoff.md)
- [x] **Phase 9: Replay Re-Validation** - ~~`session-run.json` replayed against a live Temporal dev server~~ Superseded 2026-10-21 (Temporal removed, docs/handoff.md)
- [x] **Phase 10: Routed Interactive Approvals** - UI decisions reach pending permission requests, direct and routed
- [x] **Phase 11: RPC-Mode Update Recording** - Unseen `session/update` variants recorded from omp RPC turns (owner approval gate)
- [ ] **Phase 12: Hosted Deploy Config and Exposure Gate** - Railway and CORS lock (OD-03 gated); ~~Temporal Cloud~~ superseded 2026-10-21 (docs/handoff.md). BLOCKED (external): Railway deploys paused
- [ ] **Phase 13: Hosted Routed omp Harness** - Hosted API runs the real harness (OD-03 gated). BLOCKED (external): Railway deploy pause, criterion 2
- [ ] **Phase 14: Vercel Web Against Production Origin** - Deployed web head and CSP-clean smoke (OD-03 gated). BLOCKED (external): production origin unset, Railway pause
- [x] **Phase 15: macOS iOS and MacCatalyst Builds** - Superseded by ADR-0004 (v0.10.0 Phases 22 and 24)
- [x] **Phase 16: macOS Runtime Spot-Check** - Superseded by ADR-0004 (v0.10.0 Phase 22)
- [x] **Phase 17: Blocking Record and Nyquist Verdicts** - BR-1 closed; phases 1 to 5 validated
- [x] **Phase 18: Handoff and Open-Question Closure** - Docs reflect every closure and blocker
- [x] **Phase 19: Milestone Re-Audit** - Re-audit status is not `gaps_found` (audit status `tech_debt`; completion awaits owner)

## Phase Details

### Phase 1: Validation Evidence and Record Fixes
**Goal**: The 01-VALIDATION rows and the 04-03 summary frontmatter match the evidence that exists.
**Depends on**: Nothing (first phase)
**Requirements**: TD-11, TD-42
**Success Criteria** (what must be TRUE):
  1. Rows 1-01-03 and 1-01-09 in `01-VALIDATION.md` each cite a negative proof that runs at HEAD and fails when the guarded behavior is broken, with the observed output recorded; otherwise `nyquist_compliant` is changed to match the evidence.
  2. `04-03-SUMMARY.md` frontmatter lists DESK-01, MOB-01 and SHELL-01 alongside SHELL-01's existing entry.
  3. The gate passes at the phase HEAD.
**Plans**: TBD

### Phase 2: Acceptance-Criteria List Recovery
**Goal**: The authoritative AC_1..AC_11 enumeration is recovered from its sources, or recorded as unrecoverable.
**Depends on**: Nothing
**Requirements**: OD-13
**Success Criteria** (what must be TRUE):
  1. AC_7, AC_9, AC_10 and AC_11 are searched in Notion task 3ffbc1a0c7ae8112aa70d2e15b81929f and in Linear SPE-17749 to SPE-17798. Each is either quoted verbatim with its source ID, or marked unrecoverable with the searches attempted.
  2. `Q-AC-LIST` in `docs/open-questions.md` states the recovered-or-unrecoverable outcome with its sources.
  3. Each AC verdict in `docs/handoff.md` is compared with the recovered list, and the agree-or-change result is written beside it.
**Plans**: TBD

### Phase 3: Web Head Reflection and Browser Verification
**Goal**: The web head runs with JSON reflection disabled, and the open accessibility items are resolved with evidence.
**Depends on**: Nothing
**Requirements**: OD-12, TD-44
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. With `JsonSerializerIsReflectionEnabledByDefault=false` in effect for the web head, the browser smoke completes first render without `JsonSerializerIsReflectionDisabled`. If an override is still needed, `Q-WEB-REFLECTION` records it as scoped with its evidence.
  2. The MAUI override at `apps/OmpOS.App/OmpOS.App.csproj:27` is either removed with build evidence, or kept with its justification recorded. The MAUI SDK is not on this host, so no MAUI result is claimed without that evidence.
  3. The axe run on the web head reports zero `incomplete` items for the icon-only send control, or each remaining item is documented with its evidence.
  4. The unexplained skip-link screenshot is reproduced with the command recorded, or explained in `docs/runbooks/local-dev.md`.
**Plans**: TBD

### Phase 4: AgentOS Remaining Operations
**Goal**: `AgentOsProvider` covers every remaining operation either against the verified agentOS contract or as a typed fail-closed result with the verified reason.
**Depends on**: Nothing
**Requirements**: OD-08
**Success Criteria** (what must be TRUE):
  1. Exec `cwd`, `env`, `timeout` and `tty`, `output.read` byte encoding, and provisioning `image`, `command`, `mounts`, `secrets`, `network` and `limits` are each either mapped to `agentos-sdk.dev/v1alpha1` with a contract test, or still return a typed `NotAvailable` that names the missing verified fact.
  2. `Q-SB-AGENTOS` in `docs/open-questions.md` is updated with the re-verified native-addon verdict and the source URL.
  3. `VendorStubTests` pass with the updated expectations, and no operation calls an endpoint outside the verified contract.
**Plans**: TBD

### Phase 5: Railway Swarm Remaining Operations
**Goal**: `RailwaySwarmProvider` covers provisioning, exec, logs and snapshot against verified Railway endpoints, or records the verified blocker for each.
**Depends on**: Nothing
**Requirements**: OD-09
**Success Criteria** (what must be TRUE):
  1. Each of provisioning, exec, logs and snapshot either calls a Railway GraphQL operation whose shape is verified against `https://backboard.railway.com/graphql/v2` with Bearer auth, or fails closed with a typed error and a verified blocker row in `Q-SB-RAILWAY`.
  2. The egress-control claim that blocks provisioning is re-checked against the official outbound-networking documentation; the result, verified or still unverified, is recorded.
  3. The `serviceDelete` success-shape assumption is checked against the schema, or stays marked as assumed with fail-closed behavior kept.
**Plans**: TBD

### Phase 6: Per-Sandbox Credential Issuance
**Goal**: Every non-podman sandbox provider issues a scoped credential per sandbox and revokes it at teardown.
**Depends on**: Phase 4, Phase 5
**Requirements**: OD-04
**Success Criteria** (what must be TRUE):
  1. Two sandboxes created by the same provider receive different credentials, and a test asserts they are not a shared env value.
  2. Teardown revokes the sandbox credential through the verified vendor call. Where no verified revoke call exists, teardown fails closed and the open question records it.
  3. Duplicate env-target rejection still holds, and no credential value appears in logs or exception messages (TD-21 redaction is preserved).
**Plans**: TBD

### Phase 7: PostgreSQL Session Log
**Goal**: Sessions persist to one PostgreSQL log shared by the API and the worker, behind `ISessionRepository`.
**Depends on**: Nothing (podman is present per HO-02)
**Requirements**: OD-02 (persistence half)
**Success Criteria** (what must be TRUE):
  1. A PostgreSQL implementation of `ISessionRepository` passes the abstract `SessionRepositoryContractTests<T>`, including gapless `Seq` from 1 and exactly 1..10 under concurrent append.
  2. `deploy/compose.yaml` runs the PostgreSQL service with its port bound to loopback (TD-01 rule), and `podman-compose config` validates the file.
  3. A test appends through one process and reads through another, both connected to the same database.
**Plans**: TBD

### Phase 8: Worker Composition and Routed Settlement and Streaming
**Superseded 2026-10-21 (Temporal removal, docs/handoff.md):** the worker is removed, so this phase is not planned. There is no worker to compose and no worker-routed run. Session runs execute in-process in the API through `DirectSessionRunner`, and the composition half of OD-02 is superseded (REQUIREMENTS.md). The criteria below are kept for history.
**Goal**: The worker runs real provider and activity implementations, writes events and settlement to the PostgreSQL log, and the API streams routed runs from that log.
**Depends on**: Phase 6, Phase 7
**Requirements**: OD-02 (composition half)
**Success Criteria** (what must be TRUE):
  1. The worker registers real provider and activity implementations; `FakeSandboxProvider` is not used by the worker, and `WorkerHostConfiguration.cs:91-97` no longer guards the activities off.
  2. With `Workflows:Enabled=true`, a session run submitted through the API executes in the worker, and its events and settlement are written to the PostgreSQL log.
  3. An SSE client connected to the API receives the routed run's events in `Seq` order, and resumes with `Last-Event-ID` from the log.
  4. With `Workflows:Enabled=false`, `DirectSessionRunner` behaves as before and its existing tests pass.
**Plans**: TBD

### Phase 9: Replay Re-Validation
**Superseded 2026-10-21 (Temporal removal, docs/handoff.md):** the Temporal history fixture is retired and no replay is run. The evidence file `docs/runbooks/evidence/ho-04-replay-revalidation-2026-10-21.md` is kept and marked superseded. The criteria below are kept for history.
**Goal**: ~~`session-run.json` replays cleanly against a live Temporal dev server on this host.~~ Superseded 2026-10-21.
**Depends on**: Phase 8
**Requirements**: HO-04
**Success Criteria** (what must be TRUE):
  1. ~~The Temporal CLI is installed on this host, and a Temporal dev server runs locally.~~ Superseded 2026-10-21.
  2. The replay of `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` succeeds against the Phase 8 workflow code. The command, date and result are recorded in `03-VERIFICATION.md` human verification 1.
  3. If the history no longer matches, the fixture is regenerated from a live run and the non-determinism cause is recorded. No determinism check is skipped.
**Plans**: TBD

### Phase 10: Routed Interactive Approvals
**Status (2026-10-21):** Criteria 1 and 3 met (commits 75bbca0 and 4196345). Criterion 2 superseded 2026-10-21 by the direct-run design (`docs/handoff.md`, "Temporal removal and Railway topology (2026-10-21)").
**Goal**: A UI decision reaches a pending ACP `session/request_permission` in direct and routed runs, and deny-by-policy remains the default.
**Depends on**: Phase 8
**Requirements**: OD-01
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. `RequestToolApprovalAsync` and `ResolveToolApprovalAsync` have a production caller. An approve or deny from the UI resolves the pending request in a direct run.
  2. In a routed run, the decision is delivered to the workflow through a signal path and resolves the same pending request. A test shows a decision sent through the API resolves the workflow's pending approval.
  3. With no configured grant, the request is denied. A UI approval can never grant what policy denies. Repeated decisions are rejected as already decided (409).
**Plans**: TBD

### Phase 11: RPC-Mode Update Recording
**Goal**: Unseen `session/update` variants from live omp RPC turns are recorded, frozen as fixtures, and mapped.
**Depends on**: Nothing
**Requirements**: OD-07
**Gate**: The owner must approve further live model turns before any RPC turn runs. The ten ACP turns already run under the earlier approval do not cover RPC.
**Success Criteria** (what must be TRUE):
  1. The owner's approval for the live RPC turns is recorded in `docs/open-questions.md` before any model turn runs.
  2. Each recorded RPC turn ends with a recorded stop reason, and the frames are frozen under `TestData/frames`; `RpcFrameReplayTests` pass.
  3. Each newly seen variant is either mapped to a `StreamEvent` or stays on the unknown fallback, and a contract test asserts which, in the same style as `OmpLiveVariantContractTests`.
**Plans**: TBD

### Phase 12: Hosted Deploy Config and Exposure Gate
**Superseded in part 2026-10-21 (Temporal removal, docs/handoff.md):** Temporal Cloud is replaced by the Railway services in docs/handoff.md decision 2, and there is no worker. Criteria 1 and 2 and the Temporal Cloud gate no longer apply: the API alone deploys, and `ready` does not reflect Temporal connectivity. Criterion 3 and the OD-03 gate still apply. HOST-03 is superseded.
**Goal**: ~~The API and worker have deployable config for Railway and Temporal Cloud~~ The API has deployable config for Railway (wording updated 2026-10-21), with no public exposure before the sign-in decision.
**Depends on**: Phase 7, Phase 8
**Requirements**: HOST-01, HOST-02, HOST-03
**Gate**: OD-03 (owner sign-in decision) and a Railway account; ~~a Temporal Cloud namespace with credentials supplied through the environment~~ superseded 2026-10-21.
**Success Criteria** (what must be TRUE):
  1. Railway config-as-code and podman-compatible OCI build files build the API and worker. Variables come from `.env.example`, and a repo scan finds no committed secret.
  2. On the deployed URL, `/health/live` and `/health/ready` respond; ~~`ready` reflects Temporal Cloud connectivity. A test covers the not-connected case~~ superseded 2026-10-21.
  3. The deploy checklist shows that unauthenticated cross-origin requests are denied by the locked CORS default. The API stays non-public until OD-03 is in place.
**Plans**: TBD

### Phase 13: Hosted Routed omp Harness
**Status (2026-10-21):** Criterion 3 met (commit 3f18b99, `tests/OmpOS.Api.IntegrationTests/PostgresRoutedRunTests.cs`). Criterion 2 blocked by the Railway deploy pause (`docs/runbooks/hosting.md`, deploy checklist).
**Goal**: The hosted API runs the real omp harness and persists routed runs through the PostgreSQL log.
**Depends on**: Phase 8, Phase 12
**Requirements**: HOST-04
**Gate**: OD-03. Routed use also depends on OD-02 (Phase 7 and Phase 8).
**Success Criteria** (what must be TRUE):
  1. `services/OmpOS.Api/Program.cs` wires the real omp harness through configuration, and the in-memory adapter is not used on the hosted path.
  2. The deployed image contains the verified omp binary version, and a prompt on the deployed API streams omp events to the client.
  3. Hosted routed runs read and write the PostgreSQL log from Phase 7, not in-memory state.
**Plans**: TBD

### Phase 14: Vercel Web Against Production Origin
**Goal**: The web head is deployed to Vercel and talks to the production API origin, with no CSP violations.
**Depends on**: Phase 12
**Requirements**: HOST-05, HOST-06
**Gate**: OD-03.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. `vercel.json` `connect-src` allows the production API origin with no wildcard. The web head reads that origin from `Api:BaseUrl` or `apiBaseUrl`.
  2. The static publish output is deployed to Vercel. The browser smoke on the deployed URL shows strong 1, img 0, no script and no error bar, matching `docs/runbooks/local-dev.md`.
  3. The browser console on the deployed URL shows zero CSP violations.
**Plans**: TBD

### Phase 15: macOS iOS and MacCatalyst Builds
**Superseded 2026-10-21 (ADR-0004):** replaced by v0.10.0 Phase 22 (macOS, XP-04) and Phase 24 (iOS, XP-05). Original scope kept for the record.
**Goal**: `apps/OmpOS.App` builds for iOS and MacCatalyst with warnings as errors, and the macOS CI shells job runs green.
**Depends on**: Nothing for the builds; a pushed branch for the CI run
**Requirements**: MAC-01, MAC-02, MAC-03
**Gate**: A macOS host with Xcode 27.1 (the Mac channel). The CI run needs a remote push; the first push is human-only (HO-1) and excluded from this milestone.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. `apps/OmpOS.App` builds for `net10.0-ios` with warnings as errors on the macOS host, and the log is recorded.
  2. `apps/OmpOS.App` builds for `net10.0-maccatalyst` with warnings as errors on the same host, and the log is recorded.
  3. The macOS `shells` job (`.github/workflows/ci.yml:74-88`) runs green on a macOS runner. DESK-01, MOB-01 and SHELL-01 move from Partial only on that run, with its evidence cited.
**Plans**: TBD

### Phase 16: macOS Runtime Spot-Check
**Superseded 2026-10-21 (ADR-0004):** replaced by v0.10.0 Phase 22 (macOS, XP-04) and Phase 24 (iOS, XP-05). Original scope kept for the record.
**Goal**: The macOS desktop and mobile builds behave as specified against the hosted backend.
**Depends on**: Phase 13, Phase 15
**Requirements**: MAC-04
**Gate**: The Mac channel.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The desktop build streams a prompt through the remote hosted backend and renders live blocks.
  2. The mobile build resumes remote-only sessions, and the process list shows it spawns no local process.
  3. Both results are recorded with build identifiers in the runbook.
**Plans**: TBD

### Phase 17: Blocking Record and Nyquist Verdicts
**Goal**: BR-1 is closed by a passing run, and phases 1 to 5 each get a real Nyquist verdict.
**Depends on**: Phases 1 to 11
**Requirements**: REC-01, REC-03
**Success Criteria** (what must be TRUE):
  1. `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` passes at the phase HEAD, and `05-VERIFICATION.md` is re-run to replace its `gaps_found` status.
  2. VERIFY-02 is added to `05-01-SUMMARY.md` requirements-completed only if that re-run passes.
  3. Nyquist validation runs for phases 1 to 5, and each `VALIDATION.md` records a verdict with `nyquist_compliant` matching it.
**Plans**: TBD

### Phase 18: Handoff and Open-Question Closure
**Goal**: `docs/handoff.md` and `docs/open-questions.md` state the outcome of every item in this milestone.
**Depends on**: Phases 1 to 17
**Requirements**: REC-05
**Success Criteria** (what must be TRUE):
  1. "Known deferrals" in `docs/handoff.md` lists only items still open or blocked, each with its blocker.
  2. "Owner decisions (2026-10-21)" records the supersession of owner decision 7 and the OD-03 status.
  3. Statuses in `docs/open-questions.md` match the outcomes of Phases 2 to 11 and the blocked records for OD-03, OD-06, OD-10 and OD-11.
  4. A search of these docs finds no closed item still described as deferred.
**Plans**: TBD

### Phase 19: Milestone Re-Audit
**Goal**: The milestone re-audit reports a status other than `gaps_found`.
**Depends on**: Phases 17 and 18
**Requirements**: REC-04
**Success Criteria** (what must be TRUE):
  1. The audit front matter status is not `gaps_found`.
  2. No blocking code gaps and no blocking records remain. Owner-blocked items are classified as blocked, with the recorded reason, and are not reported as code gaps.
  3. Every `tech_debt` entry is closed or re-classified with evidence.
**Plans**: TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Validation Evidence and Record Fixes | 882444f | Complete | 2026-10-21 |
| 2. Acceptance-Criteria List Recovery | 924ca78 | Complete (AC_7, AC_9, AC_10, AC_11 recorded unrecoverable) | 2026-10-21 |
| 3. Web Head Reflection and Browser Verification | d318d1b, 924ca78 | Complete | 2026-10-21 |
| 4. AgentOS Remaining Operations | ab71523 | Complete (fail-closed records) | 2026-10-21 |
| 5. Railway Swarm Remaining Operations | ab71523 | Complete (egress unverified, recorded) | 2026-10-21 |
| 6. Per-Sandbox Credential Issuance | - | Blocked (external: no verified vendor secret-store operation) | - |
| 7. PostgreSQL Session Log | b60d029 | Complete | 2026-10-21 |
| 8. Worker Composition and Routed Settlement and Streaming | - | Superseded 2026-10-21 (worker removed) | - |
| 9. Replay Re-Validation | - | Superseded 2026-10-21 (Temporal removed) | - |
| 10. Routed Interactive Approvals | 75bbca0, 4196345 | Complete (direct path; criterion 2 superseded) | 2026-10-21 |
| 11. RPC-Mode Update Recording | 05dd8ea | Complete | 2026-10-21 |
| 12. Hosted Deploy Config and Exposure Gate | - | Blocked (external: Railway deploys paused; criteria 1 and 3 met) | - |
| 13. Hosted Routed omp Harness | 3f18b99 | Blocked (external: criterion 2; criterion 3 met) | - |
| 14. Vercel Web Against Production Origin | - | Blocked (external: production origin unset; Railway pause) | - |
| 15. macOS iOS and MacCatalyst Builds | - | Superseded by ADR-0004 (v0.10.0 Phases 22 and 24) | - |
| 16. macOS Runtime Spot-Check | - | Superseded by ADR-0004 (v0.10.0 Phase 22) | - |
| 17. Blocking Record and Nyquist Verdicts | 8f422c9 | Complete (BR-1 closed; verdicts partial with named blockers) | 2026-10-21 |
| 18. Handoff and Open-Question Closure | 3ce7250, 0b67f4b | Complete | 2026-10-21 |
| 19. Milestone Re-Audit | audit file | Complete (status tech_debt; owner decides completion) | 2026-10-21 |

## Blocked Records

Each item is recorded with its reason. Its REQ-ID has no phase until the unblock condition holds.

| Item | Blocker | Unblock condition | Owner |
|------|---------|-------------------|-------|
| OD-03 | Decided 2026-10-21 (OIDC bearer, fail-closed; handoff owner decision 4). No identity provider authority is configured for the deployed API | Configure `Api:Auth:Authority` and `Api:Auth:Audience` for the deployed API. Phases 12 and 13 then continue. Public exposure stays forbidden until then (HOST-02) | Owner |
| OD-06 | External protocol gap in `Q-HARN-WS`: WebSocket upgrade authentication, reconnect, and an omp remote endpoint are unverified (omp ACP is stdio only) | A verified spec for upgrade auth and a real remote omp endpoint. The stub stays until then | Platform |
| OD-10 | Verifier decision in `Q-SB-ENCLAVE`: the attestation report format to model is not chosen | Verifier and owner choose the report format; then the verdict is implemented against it | ompos-maintainers |
| OD-11 | Podman 5.x required for `OpenShellProvider` through the compute driver; the host does not have it | Podman 5.x installed on this host; then exercise the driver and record the result in `Q-SB-OPENSHELL` | ompos-maintainers |
| HO-06 (audit; MAC-01 to MAC-04) | No macOS host with Xcode 27.1 is available to this milestone | macOS host reachable; Phases 15 and 16 then run | Owner |
| HO-07 (audit; HOST-05, HOST-06) | Vercel deploy requires the production API origin and the OD-03 sign-in gate | OD-03 decided and Phase 12 complete; then Phase 14 | Owner |
| HO-05 (audit, excluded; no REQ-ID) | Owner sign-off that TCP-level readiness satisfies criterion 4c | Owner sign-off | Owner |
| HO-08 (audit, excluded; no REQ-ID) | Screen-reader sign-off needs a person with assistive technology | Human sign-off | Human tester |

## Closed Records

These items are closed per the milestone assignment. They are not re-planned. REC-04 re-checks each against the final HEAD.

- Tech debt closed: TD-01 to TD-10, TD-12 to TD-41, TD-43.
- Owner-deferred closed: OD-05 (direct-path retry policy).
- Human-only closed on this host: HO-02 (podman and podman-compose installed), HO-03 (`RealPodmanComposeConfig_SmokeTest` runs).
- REC-02 (clean-clone bootstrap re-record) is recorded closed under the audit item HO-9, per the assignment. Its evidence reference is not in this roadmap; REC-04 confirms it at the final HEAD.

## Requirement Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| TD-01 | - | Closed |
| TD-02 | - | Closed |
| TD-03 | - | Closed |
| TD-04 | - | Closed |
| TD-05 | - | Closed |
| TD-06 | - | Closed |
| TD-07 | - | Closed |
| TD-08 | - | Closed |
| TD-09 | - | Closed |
| TD-10 | - | Closed |
| TD-11 | Phase 1 | Planned |
| TD-12 | - | Closed |
| TD-13 | - | Closed |
| TD-14 | - | Closed |
| TD-15 | - | Closed |
| TD-16 | - | Closed |
| TD-17 | - | Closed |
| TD-18 | - | Closed |
| TD-19 | - | Closed |
| TD-20 | - | Closed |
| TD-21 | - | Closed |
| TD-22 | - | Closed |
| TD-23 | - | Closed |
| TD-24 | - | Closed |
| TD-25 | - | Closed |
| TD-26 | - | Closed |
| TD-27 | - | Closed |
| TD-28 | - | Closed |
| TD-29 | - | Closed |
| TD-30 | - | Closed |
| TD-31 | - | Closed |
| TD-32 | - | Closed |
| TD-33 | - | Closed |
| TD-34 | - | Closed |
| TD-35 | - | Closed |
| TD-36 | - | Closed |
| TD-37 | - | Closed |
| TD-38 | - | Closed |
| TD-39 | - | Closed |
| TD-40 | - | Closed |
| TD-41 | - | Closed |
| TD-42 | Phase 1 | Complete (2026-10-21) |
| TD-43 | - | Closed |
| TD-44 | Phase 3 | Complete (2026-10-21) |
| OD-01 | Phase 10 | Planned |
| OD-02 | Phase 7 (persistence half); Phase 8 superseded 2026-10-21 | Planned (composition half superseded) |
| OD-03 | - | Blocked (owner) |
| OD-04 | Phase 6 | Planned |
| OD-05 | - | Closed |
| OD-06 | - | Blocked (external protocol gap) |
| OD-07 | Phase 11 | Planned (owner approval gate) |
| OD-08 | Phase 4 | Planned |
| OD-09 | Phase 5 | Planned |
| OD-10 | - | Blocked (verifier decision) |
| OD-11 | - | Blocked (Podman 5.x needed) |
| OD-12 | Phase 3 | Planned |
| OD-13 | Phase 2 | Planned |
| HO-02 | - | Closed |
| HO-03 | - | Closed |
| HO-04 | Phase 9 | Superseded 2026-10-21 |
| HOST-01 | Phase 12 | Gated: OD-03 |
| HOST-02 | Phase 12 | Gated: OD-03 |
| HOST-03 | Phase 12 | Superseded 2026-10-21 (Railway services replace Temporal Cloud) |
| HOST-04 | Phase 13 | Gated: OD-03 |
| HOST-05 | Phase 14 | Gated: OD-03 |
| HOST-06 | Phase 14 | Gated: OD-03 |
| MAC-01 | Phase 15 | Gated: Mac channel |
| MAC-02 | Phase 15 | Gated: Mac channel |
| MAC-03 | Phase 15 | Gated: Mac channel |
| MAC-04 | Phase 16 | Gated: Mac channel |
| REC-01 | Phase 17 | Planned |
| REC-02 | - | Closed (audit HO-9, per assignment) |
| REC-03 | Phase 17 | Planned |
| REC-04 | Phase 19 | Planned |
| REC-05 | Phase 18 | Planned |

## v0.10.0 Cross-platform UI (in progress)

Avalonia UI for Windows, macOS, Linux, iOS and Android. The web head stays Blazor WebAssembly; Avalonia.Browser is gated by G-WEB, which failed on Avalonia 12.1.4 (ADR-0004).

- [x] **Phase 20: Avalonia Foundation** - Shared Avalonia native project, shared presentation layer, accessibility baseline, plugin host contracts
- [x] **Phase 21: Parity Surface** - Session chat, tool approvals and steering, with parity tests on the presentation layer
- [ ] **Phase 22: Desktop Heads** - Linux, Windows and macOS heads with platform accessibility; macOS signing on the Mac channel
- [ ] **Phase 23: Web Head on the Shared Presentation Layer** - Blazor WebAssembly web head, G-WEB re-measurement, Vercel static hosting
- [ ] **Phase 24: iOS and Android Heads** - Mobile heads with platform accessibility; iOS on the Mac channel
- [x] **Phase 25: Cutover** - Remove the MAUI head after parity; remove the Razor component library once the web head no longer references it
- [x] **Phase 25.1: Codebase Quality Audit** - Report-only audit of the codebase against the QUALITY_BAR; fix phases follow the owner's acceptance of the findings
- [x] **Phase 25.2: Web Head Trust and Stream Bounds** - Prompts and SSE only to the configured origin; SSE reconnect budget and frame sizes bounded (fix F-A)
- [x] **Phase 25.3: Bounded Child and Vendor Reads** - Every external read path capped and tested (fix F-B)
- [ ] **Phase 25.4: Build, Deploy, and Image Integrity** - Verified installers, pinned actions, runners, images, and smoke tooling (fix F-C; partial: PodmanDriverSection :latest blocked on GHCR access)
- [x] **Phase 25.5: Failures That Stay Silent** - Swallowed faults logged or surfaced; logging split recorded (fix F-D; the M-4 detached-path test and the desktop logger wiring closed 2026-10-26)
- [x] **Phase 25.6: Secrets and Local Files** - Token scope, owner-only temp files, per-repository consent for setup steps (fix F-E)
- [x] **Phase 25.7: UI Head Accessibility** - Reduced motion, alt text, keyboard-operable error dismiss, focus contrast coverage (fix F-F)
- [x] **Phase 25.8: Test Quality** - Each named test asserts its claim; no fixed sleeps in settlement tests (fix F-G)
- [ ] **Phase 25.9: Suppression and Skip Review** - Every suppression and skip marker decided; counts reconciled (fix F-H)

### Phase 20: Avalonia Foundation
**Goal**: One shared Avalonia native project, a framework-neutral presentation layer, an accessibility baseline and plugin host contracts form the base that every head builds on.
**Requirements**: XP-01, XP-08, XP-09
**Depends on**: Nothing (first phase of v0.10.0)
**Mac channel**: No. The shared project and its contract tests build and run on the Linux host and the CI runners; no signing or device run is involved.
**UI hint**: yes
**Status (2026-10-23)**: complete. 7 of 7 criteria verified in 20-VERIFICATION.md (status passed). OQ-39 stays open; it is not a criterion failure.
**Success Criteria** (what must be TRUE):
  1. One Avalonia 12.1.4 native project is in the solution and builds with warnings as errors. Verify: gate at phase HEAD.
  2. A presentation layer project holds the view models and state extracted from the Razor components, with no UI-framework types, and references no Avalonia, MAUI or ASP.NET Core Components assembly. Verify: contract test in the gate that fails on any such reference or UI-framework type.
  3. The presentation layer is consumed by both the Avalonia head and the Blazor web head; both build against it and its tests pass in the gate. Verify: gate build of both heads and presentation-layer tests.
  4. Every control in the Avalonia head has an explicit AutomationProperties name, and the names of the prompt input, send control and transcript are exposed through AT-SPI2 on Linux. Verify: headless UI test in the gate that fails on an unnamed control; AT-SPI2 query output in the runbook.
  5. Shared controls are reachable by keyboard, have a visible focus style, and disable transitions when reduced motion is on. Verify: headless UI test in the gate over every shared control.
  6. Theme tokens for colour, type and spacing live in one resource dictionary, and a contract test asserts at least 4.5:1 text contrast for every foreground and background pair the dictionary defines. Verify: contract test in the gate.
  7. Plugin host contracts in the contracts layer define commands, panels, tools and agent modes with no UI-framework reference, and an in-process sample plugin registers one of each through the host. Verify: contract test in the gate that fails if the contracts assembly references a UI framework assembly, and asserts each registration.
**Plans**: 4 plans in 4 waves, each wave depends on the previous one
- [x] 20-01-PLAN.md: Wave 1, plugin contracts, presentation skeleton, package pins and wiring
- [x] 20-02-PLAN.md: Wave 2, presentation state extraction and caller migration
- [x] 20-03-PLAN.md: Wave 3, Avalonia native head, accessibility and headless tests
- [x] 20-04-PLAN.md: Wave 4, AT-SPI2 evidence, architecture and open-question records

### Phase 21: Parity Surface
**Goal**: Session chat, tool approvals and steering work on the shared presentation layer, and every RCL and MAUI feature in this surface has a test before its old head is removed.
**Requirements**: ADE-01, ADE-02, ADE-03, XP-10
**Depends on**: Phase 20
**Mac channel**: No. The parity tests run headless on the Linux host and the CI runners; no platform signing is involved.
**UI hint**: yes
**Status (2026-10-24)**: complete. 5 of 5 criteria verified in 21-VERIFICATION.md. Criterion 4 covers the approval kinds edit, delete, move, execute, read and search (owner decision F1); the other kinds are denied automatically. Criterion 5 queues steer and follow-up after the active turn (owner decision F3). No live omp turn was exercised.
**Success Criteria** (what must be TRUE):
  1. Session chat renders the streamed generative UI blocks that the Razor head renders, with one test per block type. Verify: gate; each test name appears in the parity inventory.
  2. Parity tests run against the presentation layer for the Razor head and the Avalonia head, with the same test cases for both. Verify: gate test that runs one case set against both heads and fails on any difference in asserted results.
  3. A parity inventory lists each RCL and MAUI feature in this surface with the test that covers it. Verify: gate test that fails when an inventory row has no test name or the named test does not exist.
  4. Tool approvals appear in the run UI, and an approve or deny decision reaches the ACP permission bridge and resolves the pending request. Verify: gate test that sends each decision and asserts the bridge resolves the matching request.
  5. Steering and follow-up prompts can be sent while a run is active and are delivered in send order. Verify: gate test with an active run that asserts queue order and delivery.
**Plans**: TBD

### Phase 22: Desktop Heads
**Goal**: The Linux, Windows and macOS desktop heads build from the shared project and expose names and roles through each platform's accessibility API.
**Requirements**: XP-02, XP-03, XP-04, XP-11
**Depends on**: Phases 20, 21
**Mac channel**: Yes. The macOS 26 build check, the NSAccessibility inspection, signing and notarization need a Mac host, so the owner runs them on the Mac channel and records them under XP-11. Their macOS scope replaces v0.9.2 Phases 15 and 16.
**UI hint**: yes
**Status (2026-10-25)**: deferred by owner decision (the phase was skipped on 2026-10-24). Met: criterion 2 on X11 (2026-10-22). Partial: criterion 1, with the X11 and XWayland starts recorded (2026-10-22 and 2026-10-24); the native Wayland opt-in run is not done. 22-SUMMARY.md corrects the earlier claim that criterion 1 was met. Deferred, not removed: criterion 1's opt-in run, and criteria 3 to 5, which need the Windows 11 24H2 runner and the Mac channel.
**Success Criteria** (what must be TRUE):
  1. The Linux head starts on X11 and on Wayland with the native backend opt-in, and each start is recorded with its backend named. Verify: Linux smoke logs in the runbook; a Wayland run with no Wayland session is recorded as not run, not as passed.
  2. The Linux head exposes the prompt input, the send control and the transcript through AT-SPI2 with their accessible names and roles. Verify: AT-SPI2 query output recorded in the runbook.
  3. The Windows head builds for net10.0-windows on Windows 11 24H2 in CI, and UI Automation returns the name and role of the same three controls. Verify: Windows runner log and UI Automation dump attached to the CI run.
  4. The macOS head builds for macOS 26 on the Mac channel, and NSAccessibility returns the name and role of the same three controls. Verify: Mac-channel build log and accessibility dump recorded in the runbook.
  5. The macOS head builds unsigned on the CI macOS runner; its signing and notarization run on the Mac channel and are listed under XP-11 with the ticket identifier. Verify: CI run link and the XP-11 entry in the runbook.
**Plans**: TBD

### Phase 23: Web Head on the Shared Presentation Layer
**Goal**: The Blazor WebAssembly web head runs on the shared presentation layer and keeps Vercel static hosting, with the G-WEB measurement recorded and repeated on each Avalonia release.
**Requirements**: XP-07
**Depends on**: Phases 20, 21
**Mac channel**: No. The accessibility-tree check and the browser smoke run in headless Chromium on the Linux host, and Vercel serves the static output.
**UI hint**: yes
**Status (2026-10-25)**: partially complete. Criterion 1 is met. Criteria 4 and 5 are met. Criterion 2's gate part is met: the axe smoke runs in `bootstrap.sh` and in the CI `build-test` job (2026-10-25). Criterion 2's deploy part and criterion 3 are blocked on OMPOS_API_ORIGIN and on the Vercel and Railway deploys.
**Success Criteria** (what must be TRUE):
  1. The Blazor WebAssembly web head consumes the shared presentation layer, and it builds and passes its tests in the gate. Verify: gate at phase HEAD.
  2. The axe run and the CSP check of the Blazor web head still pass on the deployed static build. Verify: axe run in the gate and browser console check on the Vercel URL showing zero CSP violations.
  3. Vercel serves the Blazor WebAssembly publish output as static files, unchanged from the current hosting. Verify: publish log and browser smoke on the deployed URL.
  4. The G-WEB re-measurement is recorded with its three measurements: stock Avalonia.Browser build, named-control build, and the Chromium accessibility tree roleNodes count. Verify: evidence file in docs with the measurement command, re-runnable from the repository.
  5. G-WEB is repeated on each Avalonia release, and Avalonia.Browser is considered for the web head only after a recorded pass. Verify: one dated G-WEB entry in docs per Avalonia release.
**Plans**: TBD

### Phase 24: iOS and Android Heads
**Goal**: The iOS and Android heads build from the shared project and expose names and roles through their platform accessibility APIs.
**Requirements**: XP-05, XP-06, XP-11
**Depends on**: Phases 20, 21
**Mac channel**: Yes. The iOS build, the UIAccessibility inspection, code signing and any device run need a Mac host and stay human-gated; they are listed under XP-11. The Android head needs no Mac step. The iOS and mobile scope replaces v0.9.2 Phases 15 and 16.
**UI hint**: yes
**Status (2026-10-25)**: partially complete. Criterion 4's local build is met: `apps/OmpOS.Android` builds for net10.0-android36.0 with warnings as errors. Its CI run needs a push, which is not authorized (G24-2). Criteria 1 to 3 need a Mac with Xcode, and criterion 5 needs an Android 16 device or emulator (no /dev/kvm on this host).
**Success Criteria** (what must be TRUE):
  1. The iOS head builds for net10.0-ios on iOS/iPadOS 26, and the build log is recorded. Verify: Mac-channel build log in the runbook.
  2. On an iOS 26 run, UIAccessibility returns the name and role of the prompt input, the send control and the transcript. Verify: Mac-channel accessibility dump recorded in the runbook.
  3. Code signing and iOS device runs are performed by the owner on the Mac channel and listed under XP-11 with their verification route. Verify: XP-11 entry in the runbook.
  4. The Android head builds for net10.0-android in CI on the Linux runner. Verify: CI run log.
  5. On an Android 16 run, AccessibilityNodeInfo returns the name and role of the same three controls. Verify: emulator or device accessibility dump recorded in the runbook.
**Plans**: TBD

### Phase 25: Cutover
**Goal**: The MAUI head is removed after parity, and the Razor component library is removed once the Blazor web head no longer references it.
**Requirements**: XP-01 (complete), XP-10 (complete)
**Depends on**: Phases 21, 22, 23, 24
**Mac channel**: No. Removal and the gate run on the Linux host; the Mac-channel evidence that replaces the MAUI iOS and macOS builds is recorded in Phases 22 and 24.
**Status (2026-10-25)**: complete. Criterion 1 is met: all 24 pending rows have a passing native Avalonia test, 10 by native behaviour tests, 1 by-design test accepted by the owner, and 13 rows implemented in the Avalonia head with tests. Criterion 2 is met: `apps/OmpOS.App` is removed from the solution and from the tree, and the search finds no reference in the solution, project or CI files. Criterion 3 is met: `src/OmpOS.UI` stays, referenced by `apps/OmpOS.Web` and `tests/OmpOS.UI.Tests`. Criterion 4 is met at commit 59e9dd1 (gate: 2276 passed, 0 failed, 0 skipped). gsd-verifier passed 4 of 4 (25-VERIFICATION.md). The owner's waiver covers the dependencies on Phases 22 and 24 (2026-10-25); Phase 23 stays an open dependency by the owner's decision (2026-10-25). The QUALITY_BAR fixes are committed (0be414e, 59e9dd1); one gap stays recorded, the dead-code check (25-SUMMARY.md).
**Success Criteria** (what must be TRUE):
  1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: gate run at phase HEAD.
  2. apps/OmpOS.App and its test projects are removed, and a repository search finds no reference to apps/OmpOS.App in the solution, project or CI files. Verify: search output in the phase record and the gate.
  3. src/OmpOS.UI and its test projects are removed only when no project in the solution references them. src/OmpOS.UI stays because the Blazor web head uses it; its removal is tracked under gate G-WEB, not under XP-01. Verify: repository search output and the gate.
  4. The gate is green at the phase HEAD. Verify: gate run at phase HEAD.
**Plans**: TBD

### Phase 25.1: Codebase Quality Audit
**Goal**: The codebase is measured against the QUALITY_BAR, and each finding is recorded with file and line evidence, so the owner can decide which fix phases to accept. This phase changes no product code.
**Requirements**: none; scope from the owner's uplift decision (2026-10-25)
**Depends on**: Phase 25 (the QUALITY_BAR baseline and its recorded fixes)
**Mac channel**: No.
**Status (2026-10-25)**: complete. Register: .planning/phases/25.1-codebase-quality-audit/25.1-FINDINGS.md (after reconciliation: 4 High, 35 Medium, 91 Low). Fix phases F-A to F-H proposed; awaiting owner acceptance.
**Success Criteria** (what must be TRUE):
  1. Each QUALITY_BAR item (pattern fidelity, correctness, boundaries, security, maintainability and observability, accessibility for UI, compatibility and reproducibility) has a repository scan, with its command and result recorded. Verify: the findings register lists each scan's command and output count.
  2. Each finding has a file and line, the QUALITY_BAR item it violates, a severity, and a proposed disposition (fix, accept, or defer). Verify: every register row has file:line evidence.
  3. A rule the repository cannot check is recorded as a gap, not claimed. Verify: the register's gap list names each gap with its reason.
  4. The phase changes no product code. Verify: the phase's diff touches only .planning paths.
  5. Fix phases are proposed from the register, grouped by area, with goals and criteria; each needs the owner's acceptance before it runs. Verify: the proposal lists each fix phase with its acceptance state.
**Plans**: 25.1-01 (complete)

### Phase 25.2: Web Head Trust and Stream Bounds
**Goal**: The web head sends prompts and streams only to the configured API origin, and its SSE client and parser stay bounded under progress and under abuse. Fixes F-A (25.1-FINDINGS.md section 8).
**Requirements**: none; scope from the owner's acceptance of F-A (2026-10-26)
**Depends on**: Phase 25.1 (findings register)
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commits 0ffe092 and b6e7664; gate 2287 passed, 0 failed, 0 skipped. Record: .planning/phases/25.2-web-head-trust-and-stream-bounds/25.2-01-SUMMARY.md.
**Success Criteria** (what must be TRUE):
  1. The web head sends prompts and SSE only to the configured API origin; a `?apiBaseUrl=` override is honoured only for loopback origins. Verify: ApiBaseUrlResolver tests cover a non-loopback override (refused) and a loopback override (accepted).
  2. The SSE reconnect budget resets when a connection delivers events. Verify: SseEventStream tests show a stream that drops more than MaxReconnects times with progress in between does not throw, and one with no progress throws at the budget.
  3. SSE line and frame sizes are capped; an oversized line fails with a bounded error. Verify: SseParser test with an oversized line.
  4. API response bodies in ApiSessionClient are size-capped. Verify: test with an oversized body.
  5. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: 25.2-01 (complete)

### Phase 25.3: Bounded Child and Vendor Reads
**Goal**: Every read of external bytes (child process output, remote frames, vendor responses) has a named cap, and an oversized input fails with a bounded error. Fixes F-B.
**Requirements**: none; scope from the owner's acceptance of F-B (2026-10-26)
**Depends on**: Phase 25.2
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commit 501c8db; gate 2302 passed, 0 failed, 0 skipped. Record: .planning/phases/25.3-bounded-child-and-vendor-reads/25.3-01-SUMMARY.md.
**Success Criteria** (what must be TRUE):
  1. ACP child stdout lines are capped as the RPC transport caps them; an oversized line fails the session with a bounded error. Verify: AcpStdioTransport test with an oversized line.
  2. RemoteEngineHost frame lines and frame data are capped before parse. Verify: test with an oversized frame.
  3. `omp --version` output is capped at the launcher probe's limit. Verify: OmpVersionProbe test.
  4. AgentOsProvider and RailwaySwarmProvider vendor response bodies are capped. Verify: provider tests with an oversized body.
  5. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: 25.3-01 (complete)

### Phase 25.4: Build, Deploy, and Image Integrity
**Goal**: Build and deploy inputs are verified or pinned: installers checked before execution, CI actions and runners pinned, images pinned by digest, and the smoke tool checks its download. Fixes F-C.
**Requirements**: none; scope from the owner's acceptance of F-C (2026-10-26)
**Depends on**: Phase 25.3
**Mac channel**: No.
**Status (2026-10-26)**: partial. Commit 523ac44; gate 2306 passed, 0 failed, 0 skipped. Blocked residual: PodmanDriverSection :latest (GHCR denies anonymous lookups). Accepted residual: Android workload version (M-30, rationale in 25.4-01-SUMMARY.md). Record: .planning/phases/25.4-build-deploy-and-image-integrity/25.4-01-SUMMARY.md.
**Success Criteria** (what must be TRUE):
  1. The Vercel installer is verified by SHA-256 before it runs (vercel.ts). Verify: the installCommand fails on a hash mismatch; the expected hash is committed.
  2. CI actions are pinned by full commit SHA, and CI runners are pinned to a named image. Verify: a repository test fails on any `uses:` line without a full SHA.
  3. global.json roll-forward and the bootstrap SDK checksum agree on the same SDK version. Verify: a check in the gate fails when they disagree.
  4. Compose and test-services images are pinned by digest. Verify: `podman-compose config` renders and the digests match the registry.
  5. run-smoke compares the axe-core tarball hash to a committed value and fails on mismatch. Verify: a smoke run with the committed hash passes, and a wrong expected hash fails.
  6. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: 25.4-01 (partial)

### Phase 25.5: Failures That Stay Silent
**Goal**: Each swallowed or dropped fault is logged or surfaced to its caller, and the logging split (services on ILogger, heads on Trace) is written down. Fixes F-D.
**Requirements**: none; scope from the owner's acceptance of F-D (2026-10-26)
**Depends on**: Phase 25.4
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commits bad4e67 and bf7e16b (the two open items: the M-4 detached-path test and the desktop ILoggerFactory wiring); gate 2390 passed, 0 failed, 0 skipped; web smoke exit 0. Record: .planning/phases/25.5-failures-that-stay-silent/25.5-01-SUMMARY.md.
**Success Criteria** (what must be TRUE):
  1. Each fault in M-2, M-3, M-4, M-12, M-17, M-18, and M-19 is logged or surfaced to its caller. Verify: a test per fault observes the log or the surfaced error.
  2. Close faults in the shipped desktop app reach a configured trace listener. Verify: a test or startup check shows the listener is attached in the app host.
  3. The logging split is recorded in a tracked document. Verify: the document names each layer and its mechanism.
  4. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: 25.5-01 (partial)

### Phase 25.6: Secrets and Local Files
**Goal**: Bearer tokens reach only https or loopback origins, compose files in shared temp directories are owner-only, and repository setup steps run only after per-repository consent. Fixes F-E.
**Requirements**: none; scope from the owner's acceptance of F-E and the consent answer (2026-10-26)
**Depends on**: Phase 25.5
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commit 1ef5051; gate 2390 passed, 0 failed, 0 skipped. Record: .planning/phases/25.6-secrets-and-local-files/25.6-01-SUMMARY.md. No head shows a setup-consent prompt yet (no head calls SetupRunner until Phase 28 criterion 5); the default prompt denies with a clear message.
**Success Criteria** (what must be TRUE):
  1. AgentOsProvider refuses at construction a non-loopback cleartext origin, and sends its bearer token only to the configured origin. Verify: provider tests.
  2. Compose files written to temp directories are owner-only. Verify: a test checks the file mode.
  3. Repository setup steps (.ompos/setup.json) run only after consent recorded per repository, keyed by repository path and steps hash; a changed file asks again. Verify: SetupRunner tests cover refusal without consent, run after consent, and re-prompt after a change.
  4. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: TBD

### Phase 25.7: UI Head Accessibility
**Goal**: The UI heads honour reduced motion, always label images, keep the error dismiss keyboard-operable, and cover the focus ring in the contrast test. Fixes F-F; OQ-34 and OQ-39 stay deferred.
**Requirements**: none; scope from the owner's acceptance of F-F (2026-10-26)
**Depends on**: Phase 25.6
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commit bf7e16b (with Phase 25.5); gate 2390 passed, 0 failed, 0 skipped; web smoke exit 0 with the new dismiss-control step. Record: .planning/phases/25.7-ui-head-accessibility/25.7-01-SUMMARY.md. OQ-34 and OQ-39 stay deferred.
**Success Criteria** (what must be TRUE):
  1. Android transitions honour reduced motion. Verify: the transition is removed or disabled under the reduced-motion setting; a test or static check covers it.
  2. A2UI images always have alt text, with a fallback when the agent omits it. Verify: A2UI test with a missing alt.
  3. The web error dismiss is a named, keyboard-operable control, and the error alert announces. Verify: web smoke axe run with zero violations and the keyboard check.
  4. The web focus-ring contrast is covered by a test. Verify: the contrast test includes the focus-ring pair.
  5. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: TBD

### Phase 25.8: Test Quality
**Goal**: Every named test asserts what its name claims, and settlement tests use deterministic waits, not fixed sleeps. Fixes F-G.
**Requirements**: none; scope from the owner's acceptance of F-G (2026-10-26)
**Depends on**: Phase 25.7
**Mac channel**: No.
**Status (2026-10-26)**: complete. Commit efc4e12; gate 2390 passed, 0 failed, 0 skipped. Record: .planning/phases/25.8-test-quality/25.8-01-SUMMARY.md (each strengthened test lists the mutation that fails it).
**Success Criteria** (what must be TRUE):
  1. Each test named in M-8, M-9, lens 1 section 2, InMemorySessionSignalsTests:71, and RailwaySwarmProviderTests:204 asserts the behaviour its name claims, or is removed. Verify: each test fails when its behaviour breaks.
  2. No fixed sleep remains in RunControls settlement tests. Verify: grep finds no Task.Delay or Thread.Sleep in those tests.
  3. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: TBD

### Phase 25.9: Suppression and Skip Review
**Goal**: Every suppression and skip marker in the 2026-10-22 inventory has a recorded decision, the counts reconcile with a stated scope, and each editorconfig severity override is justified or removed. Fixes F-H.
**Requirements**: none; scope from the owner's acceptance of F-H (2026-10-26)
**Depends on**: Phase 25.8
**Mac channel**: No.
**Status (2026-10-26)**: not started.
**Success Criteria** (what must be TRUE):
  1. Each SUP row in docs/quality/quality-bar.md section 2 has a recorded decision: keep with its justification, or fix the cause. Verify: the decision table has one row per SUP id.
  2. The pragma and skip counts reconcile with the stated scope. Verify: the counts table names the scope for each count.
  3. Each `.editorconfig` severity-none override is justified or removed. Verify: the .editorconfig diff and the justification text.
  4. The gate is green at the phase HEAD. Verify: ./bootstrap.sh.
**Plans**: TBD

## v0.11.0 ADE harness (planned)

Source: ADR-0004 decision 4 (plugin harness, in-process first, MCP out-of-process later). Phases 26 to 30 build on the v0.10.0 parity surface and desktop heads. Phase 21 covers ADE-01 to ADE-03 and Phase 20 provides the plugin host contracts used by Phase 30.

- [ ] **Phase 26: Agents, Worktrees, Cloud and Remote** - Side chats, transcript search, Agents window, worktree isolation, cloud runs, remote control
- [ ] **Phase 27: Editor and Review** - Code editor, file tree, diff review with per-hunk accept and reject, inline edit
- [ ] **Phase 28: Terminal, MCP, Rules and Search** - Approved terminal, MCP client, rules and skills and modes, codebase search, environment setup
- [ ] **Phase 29: Browser Pane, Completion and Export** - Embedded browser with design mode, inline completion, Markdown export and usage history
- [ ] **Phase 30: Later Compatibility and Admin** - VS Code extension compatibility and admin, analytics and team controls, with explicit deferral criteria

### Phase 26: Agents, Worktrees, Cloud and Remote
**Goal**: Side chats, transcript search, a parallel Agents window, per-run git worktrees, hosted cloud runs and remote control from another device work in the Avalonia UI.
**Requirements**: ADE-04, ADE-05, ADE-06, ADE-07, ADE-08
**Depends on**: Phases 21, 22; the cloud criterion also needs v0.9.2 Phase 13 (hosted routed harness) complete
**Mac channel**: No. Worktree, transcript and Agents window tests run on the Linux gate; the remote-control check uses a second client on the Linux or Windows host.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. A side chat opens from a session as its own chat, and transcript search returns matches across chats and opens the selected match. Verify: gate UI test with two chats and a known search term.
  2. The Agents window lists parallel runs with their status, filters the list by status, and opens or closes a run. Verify: gate UI test that starts three runs and asserts each status transition and filter result.
  3. Each agent run starts in its own git worktree, two concurrent runs in one repository never share a worktree, and the worktree is removed when the run closes. Verify: gate integration test on a temporary repository.
  4. A cloud run on the hosted API shows its status and its reviewable output when it completes. Verify: gate contract test against the hosted run contract, and one live cloud run recorded in the runbook.
  5. A second device can send a prompt to a local run and receive its status notifications, and the run stays on the paired computer. Verify: gate integration test with two clients on the Linux host, and a pairing run recorded in the runbook.
**Plans**: TBD

### Phase 27: Editor and Review
**Goal**: The Avalonia UI has a code editor, a workspace file tree, diff review with per-hunk accept and reject, and inline edit on a selection.
**Requirements**: ADE-09, ADE-10, ADE-11, ADE-12
**Depends on**: Phases 21, 22
**Mac channel**: No. All editor and review tests run headless on the Linux gate.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The editor highlights syntax for a fixture file in each language the repository uses, shows tabs, and finds text with a match count. Verify: gate UI test on the fixture files.
  2. Open folder sets the workspace root, and the file tree lists its files and expands directories. Verify: gate UI test on a fixture folder.
  3. Diff review shows each hunk of an agent change with its own accept and reject control. Accepting one hunk writes only that hunk, and rejecting one leaves its lines unchanged. Verify: gate test that asserts the file contents after each decision.
  4. Inline edit on a selection sends the selected range and the instruction to the model provider, and the returned edit opens as a reviewable diff instead of a direct write. Verify: gate test against a recorded provider reply.
**Plans**: TBD

### Phase 28: Terminal, MCP, Rules and Search
**Goal**: Agent runs get an integrated terminal with approvals, MCP servers, project rules, skills and modes, codebase search and an environment setup file.
**Requirements**: ADE-14, ADE-16, ADE-17, ADE-18, ADE-19
**Depends on**: Phases 21, 22
**Mac channel**: No. The terminal, MCP, search and setup tests run on the Linux gate against local stdio and HTTP fixture servers.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. A terminal command runs only after an approval through the ADE-02 bridge. Auto-run is off by default and turns on only through an explicit opt-in setting. Verify: gate test that a command without approval does not start, and a test of the default and opt-in settings.
  2. The MCP client connects to a stdio server and an HTTP server, lists their tools in one registry, and enables or disables each tool per session. Verify: gate integration test against local fixture servers.
  3. Rules, skills and custom agent modes load from project files and reload when a project file changes. Verify: gate test on a fixture project folder with an edit applied.
  4. Text and regex search over the workspace returns file, line and match. Semantic indexing is not part of this phase. Verify: gate test on a fixture repository.
  5. An environment setup file runs before an agent run; a failed setup stops the run and shows its error. Verify: gate test with a passing and a failing setup file.
**Plans**: TBD

### Phase 29: Browser Pane, Completion and Export
**Goal**: The UI has an embedded browser pane with design mode, inline completion from a configured model provider, and export of chats and runs with usage history.
**Requirements**: ADE-13, ADE-15, ADE-20
**Depends on**: Phases 26, 27
**Mac channel**: No. Verification runs on the Linux gate with headless Chromium and fixture pages; this phase makes no claim about the browser pane on the macOS and mobile heads.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The browser pane loads a URL, and design mode selects an element and sends it to the chat as context with its selector and bounding box. Verify: gate test against a fixture page in headless Chromium.
  2. Inline completion calls only the model provider set in configuration. With no provider configured, no provider call is made and completion stays off. Verify: gate test with a recorded provider reply and a no-configuration test.
  3. Chats and runs export to Markdown with their messages, tool calls and approvals, and usage history lists the recorded usage of each run. Verify: gate golden-file test on the Markdown export and a usage history test.
**Plans**: TBD

### Phase 30: Later Compatibility and Admin
**Goal**: VS Code extension compatibility and admin, analytics and team controls start only when their deferral criteria hold, and stay deferred in writing until then.
**Requirements**: ADE-21, ADE-22
**Depends on**: Phases 20, 26, 29
**Mac channel**: No. Compatibility and admin checks run on the Linux gate; no platform signing is involved.
**Success Criteria** (what must be TRUE):
  1. ADE-21 starts only when the XP-09 plugin host contract tests pass at HEAD. Until then the item is recorded as deferred with the unmet criterion. Verify: gate status recorded at phase start.
  2. ADE-22 starts only when the cloud runs of Phase 26 criterion 4 are live on the hosted API. Until then the item is recorded as deferred with the unmet criterion. Verify: gate status and the Phase 26 runbook entry recorded at phase start.
  3. When ADE-21 starts, a compatibility report lists each VS Code extension API the plugin harness supports, and each row is backed by a passing contract test. Verify: gate test that fails when a row has no test.
  4. When ADE-22 starts, role assignment, team controls and usage analytics are covered by gate contract tests on their API. Verify: gate contract tests.
  5. Deferred items ship no stub endpoint or placeholder control. Verify: repository search for their identifiers finds only the deferral record.
**Plans**: TBD

## v0.12.0 Cursor parity and quality bar (planned, added 2026-10-22)

Source: user request 2026-10-22. The ADE harness must match Cursor desktop capabilities (hooks, MCP, cloud, local and remote development, automations), and its embedded terminal opens the omp CLI so plugins and MCP servers can be reviewed on the command line. Code quality must meet a state-of-the-art, enterprise and production bar. Requirements CX-01 to CX-08 are in `.planning/REQUIREMENTS.md`. Phases 31 to 35 build on v0.11.0. Phases 26, 28 and 33 carry the MCP, terminal, cloud and local-run criteria (ADE-06, ADE-07, ADE-14, ADE-16, ADE-19).

- [ ] **Phase 31: Plugin Manifests and Hooks** - Manifest validation, API version gate and quarantine; lifecycle hooks with consent per exact command (CX-01, CX-02)
- [ ] **Phase 32: Automations** - Event, schedule and manual triggers with loop, chain, rate and runtime guards (CX-03)
- [ ] **Phase 33: Omp CLI Terminal and Shared Registry** - Terminal opens omp; one registry for the UI and omp; host-owned MCP gateway (CX-04)
- [ ] **Phase 34: Remote Development** - Authenticated, encrypted remote transport; BLOCKED on OD-06 and owner decisions (CX-05)
- [ ] **Phase 35: Code Quality Bar** - Repository-wide review, enforced gates and decision records (CX-06, CX-07, CX-08)

### Phase 31: Plugin Manifests and Hooks
**Goal**: Plugins declare a validated manifest with API version, capabilities and permissions. The host loads, quarantines or rejects them without crashing, and hooks run only after consent for their exact command.
**Requirements**: CX-01, CX-02
**Depends on**: Phase 21 (approval path), Phase 28 (consent, permission broker, process supervisor)
**Mac channel**: No. The gate runs on the Linux host.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. A hostile manifest is rejected by schema validation before any code runs, and an incompatible API version is rejected with a message naming the plugin and the supported range. Verify: gate test with a sentinel-writing fixture.
  2. A plugin that throws asynchronously is quarantined with a persisted reason, and the host and other plugins keep running. Verify: gate test.
  3. A hook runs only after consent for its exact command hash, re-prompts when the command changes, and records exit status, duration and truncated output. Verify: gate test with zero spawned processes before consent.
  4. The hooks list shows trigger, command and enabled state; enable, disable and reorder work; loading, empty, error, success and degraded states render. Verify: component tests on the presentation layer and the Avalonia head.
**Plans**: TBD

### Phase 32: Automations
**Goal**: Automations run agent work on event, schedule or manual triggers, with an explicit enable toggle, a maximum runtime and guards that fail closed.
**Requirements**: CX-03
**Depends on**: Phase 26 (local and cloud runs), Phase 31 (consent and execution log)
**Mac channel**: No.
**UI hint**: yes
**Cursor reference**: https://cursor.com/docs/cloud-agent/automations (cloud-agent triggers: schedule, source-control events, webhook and others; runs use a written prompt and enabled tools, not shell hooks).
**Success Criteria** (what must be TRUE):
  1. An automation that triggers itself is blocked at chain depth one and the block is recorded; chain depth is capped at 3. Verify: gate test.
  2. An exception injected into the guard store refuses the run (deny on error). Verify: gate test.
  3. Runs are capped per time window by a rate limit and stop at the maximum runtime. Verify: gate test with a fake clock.
  4. A paused automation names the guard that paused it and how to resume. Verify: component test.
**Plans**: TBD

### Phase 33: Omp CLI Terminal and Shared Registry
**Goal**: The embedded terminal opens an omp session in the project, and the app UI and the omp CLI read one plugin and MCP registry without drift.
**Requirements**: CX-04
**Depends on**: Phase 28 (terminal, MCP client)
**Mac channel**: No.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The terminal launches the located omp executable with an argument array and the project directory. A missing omp shows its detected path, version and install guidance while the rest of the app keeps working. Verify: gate test with a fake executable.
  2. The registry has one writer; its export carries a generation and checksum; the UI list and the omp view report the same generation. Verify: gate test.
  3. omp's MCP connections go through the host-owned MCP gateway (decision 2026-10-22), which refuses tools that are not granted. Verify: gate test that an ungranted tool call is refused at the gateway.
**Plans**: TBD

### Phase 34: Remote Development
**Goal**: Remote development connects to a host over an authenticated, encrypted transport and verifies host identity before the first connection.
**Requirements**: CX-05
**Status (2026-10-22)**: BLOCKED. The remote omp endpoint and WebSocket upgrade authentication are unverified (OD-06, Q-HARN-WS). The remote protocol and host-key storage are not decided. No Cursor primary docs page for SSH remote development was found (https://cursor.com/docs/remote-ssh.md and https://cursor.com/docs/remote-development return 404). This phase stays deferred in writing until OD-06 has a verified spec and the owner decides the protocol and host-key store. No stub ships.
**Plans**: TBD

### Phase 35: Code Quality Bar
**Goal**: The codebase meets an enterprise and production bar, measured by repo-enforced checks, with every audit finding fixed or recorded with an owner.
**Requirements**: CX-06, CX-07, CX-08
**Depends on**: Phases 20 to 33 (the audit covers the final tree)
**Success Criteria** (what must be TRUE):
  1. A repository-wide review produces per-file findings; each finding is fixed or recorded with an owner and an open-question link. Verify: findings table in the phase record.
  2. Analyzer, warnings-as-errors, formatter, architecture and secret-scan checks are enforced in CI and in bootstrap, and every suppression has a row in docs/open-questions.md. Verify: gate run at phase HEAD.
  3. Decision records exist for the editor, terminal, MCP, browser pane and plugin isolation choices. Verify: docs/decisions listing.
**Plans**: TBD

## v0.13.0 Cursor 1:1 workspace and provider platform (planned, added 2026-10-22)

Source: user request 2026-10-22 and the ultrathink plan graph ut-mvjvg4gj-fb21d80f (spec `/root/.omp/agent/ultrathink/sessions/01a160e3-3eca-717c-b5de-fb65516aa688.xml`). Primary-source research with a verdict per row is in `docs/plans/vision-research/`. Requirements VX-01 to VX-10 are in `.planning/REQUIREMENTS.md`. Phases 36 to 43 build on v0.11.0 (sessions, approvals, terminal, MCP) and v0.12.0 (plugin host, registry, omp launcher).

Phases 36 to 38 do not depend on the blocked v0.10.0 items (Phases 22, 24, 25), so they run while those stay blocked. The blocked items stay open; this plan closes none of them.
- [x] **Phase 36: Contracts and Theme Foundation** - token set (White and Purple), Greptile font families under OFL, command record, Kanban status model, provider record and URL validator, secret type with canary harness, herdr and omp adapter contracts with recorded fakes
- [ ] **Phase 37: Session Runtime, Streaming and Local Terminal** - session lifecycle service, heartbeat and stale detection, sequence-numbered stream with replay, local PTY backend on Linux. Criteria 1 to 4 verified (37-VERIFICATION.md); criterion 5 open by design (owner durability decision, STATE)
- [ ] **Phase 38: Chat Window and Slash Commands** - chat states and composer context, slash registry and palette
- [ ] **Phase 39: Kanban Board** - one task card per chat with runtime status and a stale flag
- [ ] **Phase 40: Settings Window and Model Providers** - Runtime section, provider catalogue (local, custom, cloud, API key, coding plan)
- [ ] **Phase 41: Pets Companion** - pet state model, 2D fallback, hide and reduced-motion controls
- [ ] **Phase 42: Cloud Terminals on Railway** - persistent container per session with idle sleep; prototype until Railway confirms policy in writing
- [ ] **Phase 43: Mobile and Web Terminals** - remote-attached terminal on iOS, Android and WebAssembly; session-only web keys by default
### Phase 36: Contracts and Theme Foundation
**Status (2026-10-24)**: complete. 6 of 6 criteria verified in 36-VERIFICATION.md. Pending, recorded and not failed: VX-01 owner look sign-off; VX-06 real herdr endpoint; VX-07 omp launch-time provider configuration.
**Goal**: The contracts every surface consumes exist and are tested: one token set, one command record, one status model, one provider record, one secret type, and versioned herdr and omp adapter contracts backed by recorded fakes.
**Requirements**: VX-01, VX-02, VX-04, VX-05, VX-06, VX-07, VX-10
**Depends on**: Phases 20, 21 (theme resource dictionary and presentation layer exist)
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. The theme dictionary defines White and Purple roles in light and dark variants, the Greptile families as OFL resources with their licence notice, one spacing scale and one radius scale; no view hard-codes a colour. Verify: contrast test in the gate asserting 4.5:1 for text and 3:1 for non-text in both themes.
  2. The command record and the registry exist with an empty catalogue; no command name is added without a verified reference. Verify: gate tests for empty query, unknown command and disabled reason.
  3. The Kanban runtime status enumeration and its independent stale flag exist, with the mapping table recorded in CONTEXT. Verify: gate test mapping each value and showing that the stale flag changes without changing status.
  4. The provider record and URL validator accept https, accept http only for localhost, and reject remote plain http unless explicitly allowed. Verify: gate tests.
  5. A SecretString type never renders its value in ToString or serialisation, and the canary harness fails when a registered secret reaches a stub log, transcript, crash or telemetry sink. Verify: gate canary test.
  6. The herdr session client contract and the omp adapter contract are versioned interfaces with recorded-transcript fakes; replaying a fixture twice yields the same typed event sequence; detach leaves the fake session alive. Verify: gate contract tests.
**Plans**: TBD

### Phase 37: Session Runtime, Streaming and Local Terminal
**Status (2026-10-24)**: partial. Criteria 1 to 4 verified in 37-VERIFICATION.md. Criterion 5 is open by design: the real herdr endpoint waits on the owner's restart-semantics decision (STATE). Windows and macOS local backends not run (no runner; macOS on the Mac channel).
**Goal**: Sessions reattach through the herdr contract after a client restart, streamed output renders without duplicates, and each desktop OS gets a real local shell.
**Requirements**: VX-06, VX-07, VX-08
**Depends on**: Phase 36
**Mac channel**: No for the Linux and Windows backends. The macOS local PTY backend runs on the Mac channel and is listed under XP-11.
**Success Criteria** (what must be TRUE):
  1. The session service creates, attaches, detaches, pauses, resumes and terminates sessions through the herdr contract; detach never terminates; the session id, provider selection and last-known status persist locally; a restart reattaches by stored id. Verify: gate lifecycle tests over the fake.
  2. Heartbeat every 15 seconds; a session is stale after three missed heartbeats (45 seconds of simulated time). Verify: gate test with a fake clock.
  3. Streamed output is sequence-numbered; replay from the last acknowledged sequence yields no duplicate blocks; cancelling a response leaves the session running. Verify: gate transcript replay test.
  4. A local shell spawns, echoes input and exits with its code on Linux in the gate. Windows and macOS are recorded as not run until their runners exist. Verify: Linux gate test and runbook entries for the other platforms.
  5. The real herdr endpoint stays blocked until the owner decides the restart semantics recorded in STATE. Verify: STATE entry.
**Plans**: TBD

### Phase 38: Chat Window and Slash Commands
**Goal**: The chat window streams session output in every state the plan names, and slash commands are discoverable through a registry whose catalogue matches a verified Cursor reference.
**Requirements**: VX-02, VX-03
**Depends on**: Phases 36, 37
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. The transcript renders text, tool call, tool result and file edit as distinct blocks; loading, empty first run, streaming with cancel, error with retry that keeps the transcript, and disconnected with reconnect status each have a UI test. Verify: gate UI tests.
  2. The composer shows the active model and provider; a model switch the provider does not support shows a notice. Verify: UI test.
  3. Typing a slash opens the palette; an empty query shows the full list; an unknown command shows a suggestion; a disabled command shows its reason. Verify: UI tests.
  4. The catalogue lists Cursor commands only with the reference version and date recorded in CONTEXT; unverified names are not added. Parity sign-off is an owner decision (default recorded: functional parity against the public docs). Verify: CONTEXT entry and catalogue test.
**Plans**: TBD

### Phase 39: Kanban Board
**Goal**: The Kanban board shows one task card per chat with its live runtime status, and status changes arrive as events.
**Requirements**: VX-04
**Depends on**: Phases 36, 37
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. One card per chat shows title, active model and provider, last activity time and status; statuses come from the runtime enumeration, with no client-side guesses. Verify: gate tests from fake session events.
  2. Runtime statuses are not user-draggable; only the user-settable statuses (default Backlog and Archived) accept drag. Verify: gate test.
  3. A card with a stale heartbeat shows the stale indicator with a screen-reader label; an unreachable session shows the card error state. Verify: UI test.
  4. Column names are the defaults recorded in CONTEXT until the owner confirms the final names. Verify: CONTEXT entry (owner decision).
**Plans**: TBD

### Phase 40: Settings Window and Model Providers
**Goal**: The settings window holds a Runtime section and a Model Providers section, and every provider is a registry entry configured without code changes.
**Requirements**: VX-05
**Depends on**: Phases 36, 37
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. The Runtime section sets idle stop (default 24 hours) and completed-session retention (default 7 days) with validated floors and ceilings; invalid values are rejected inline; reset-to-default works per section; closing with unsaved changes asks first. Verify: UI tests.
  2. Local hosting entries (Ollama, LM Studio, llama.cpp, vLLM, LocalAI, Jan, Docker Model Runner) and the generic OpenAI-compatible local transport are recorded with source and date; an unreachable local server shows a not-running error. Verify: probe test against a fake server and a catalogue test that every entry carries source and date.
  3. A custom provider is added from a user entry (base URL, auth header, model list, capability flags) without a code change. Verify: gate test.
  4. Cloud API-key and coding-plan entries are added only where the research verified the endpoint, auth and quota (default: verified providers only). Coding plans are separate entries with their own auth, quota display and error mapping; quota exhaustion and plan expiry produce actionable messages. Verify: catalogue tests per entry.
  5. Live validation with real credentials is blocked until the owner supplies test credentials per provider. Verify: STATE entry.
**Plans**: TBD

### Phase 41: Pets Companion
**Goal**: A companion character shows session state at a glance, never blocks the chat or terminal, and respects reduced motion and a hide toggle.
**Requirements**: VX-09
**Depends on**: Phases 36, 39
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. The pet state model (hidden, idle, working, attention-needed, fallback) drives a 2D anime-style companion; each state has a text equivalent for screen readers. Verify: gate state tests and a UI test.
  2. Reduced motion disables animation; the hide toggle removes the companion; the companion never overlaps the transcript or terminal. Verify: UI and layout tests.
  3. The 3D renderer is not built until a per-target spike passes; the research verdict is recorded in `docs/plans/vision-research/pets-feasibility.md`. Verify: spike record per target.
  4. The owner signs off the appearance before character art ships (validation sign-off). Character assets use only a verified licence. Verify: asset licence record.
**Plans**: TBD

### Phase 42: Cloud Terminals on Railway
**Goal**: Each cloud session runs in a Linux environment that sleeps when idle and restores its transcript on reattach.
**Requirements**: VX-06, VX-08
**Depends on**: Phases 37, 40; the cloud runtime criterion also needs v0.9.2 Phase 13 (hosted routed harness)
**Mac channel**: No.
**Success Criteria** (what must be TRUE):
  1. The cloud model is a persistent container per session with idle sleep (owner decision, 2026-10-22). Each session has one container and a volume for its transcript; reattach restores the transcript after the environment sleeps. Verify: integration test against a fake Railway client; the live run is recorded as blocked.
  2. Remote desktop or browser-streaming features are not built until Railway confirms in writing that the use is permitted under its acceptable-use and trust terms; the research found no primary statement either way. Verify: written confirmation in docs/runbooks. Pause: external confirmation.
  3. Process state across sleep is not claimed: a restored session resumes from its stored native omp session reference, and the transcript comes from the volume. Verify: test that a restored session shows the stored transcript and a resume marker.
**Status (2026-10-22)**: Prototype only, on a Railway project the owner controls. No production deploy until criterion 2 is met.
**Plans**: TBD

### Phase 43: Mobile and Web Terminals
**Goal**: iOS, Android and WebAssembly show a remote-attached terminal, never spawn a local shell, and keep web keys out of durable browser storage.
**Requirements**: VX-08, VX-10
**Depends on**: Phases 36, 37, 40
**Mac channel**: Yes for the iOS build and iOS accessibility dump (listed under XP-11). The Android build runs on the Linux runner.
**Success Criteria** (what must be TRUE):
  1. Mobile and web builds reference no local process spawning; the local terminal shows as unavailable with its reason. Verify: per-target build check and UI test.
  2. A remote-attached terminal renders the same terminal model as desktop and resumes after network loss without duplicated blocks. Verify: gate replay test.
  3. WebAssembly keeps provider keys in session-only memory, cleared on reload, with a documented threat model; no key is written to browser storage. Verify: browser-run check and the canary test. Pause: server-side vault or per-session entry (default session-only).
  4. The Android accessibility dump needs an Android 16 device or emulator, which this host does not have; it is recorded as blocked.
**Plans**: TBD

## v0.14.0 Agent loop and Agent Panel (planned, added 2026-10-26)

Source: user request 2026-10-26 and the ultrathink plan graph ut-mvp0bh59-2dd6ef4c (spec `/root/.omp/agent/ultrathink/sessions/01a17752-7e43-7002-8ac7-6781e5b19475.xml`). Evidence is in the discovery dossiers under `docs/plans/agent-loop/`. Decisions are recorded in `docs/decisions/ADR-0012-agent-loop-plugins.md`. Requirements AL-01 to AL-12 are in `.planning/REQUIREMENTS.md`.

Settled decisions: the three plugins run inside the omp engine and OmpOS launches omp with them and observes them (the hybrid of the ultrathink clarification q1, settled from repository evidence); the Agent Panel is built on the shared presentation layer in the Avalonia native head (q2, the MAUI head is removed); the runnable panel comes first and externally blocked phases are deferred with recorded reasons (q3). Live delivery of a steering message into a running session does not exist in agent-substrate on main (its Phase 53 is unmerged), so OmpOS delivers steering over ACP and only records it in the substrate.

- [ ] **Phase 44: Plugin Submodules and Launch Contract** - three pinned submodules, the GSD-Core omp plugin, the plugin catalog and its guard, ACP launch plumbing, repository hygiene and CI
- [ ] **Phase 45: Agent Loop Stage Sources** - stage contracts, observers for Ultrathink, Swarm, Substrate and GSD, task context, session registry with restart resume, degradation
- [ ] **Phase 46: Agent Panel** - the default surface: session rail, thread, pipeline strip, composer with steering, context inspector, states and accessibility
- [ ] **Phase 47: Agent Panel on the Web Head** - deferred: the hosted API runs the InMemory harness and Railway deploys are paused

### Phase 44: Plugin Submodules and Launch Contract
**Goal**: The three sibling projects are pinned submodules, GSD-Core is an omp plugin, and OmpOS can launch omp with the four plugins loaded, hermetically and reproducibly.
**Requirements**: AL-01, AL-02, AL-03, AL-04
**Depends on**: Phase 21 (the ACP session and approval path)
**Mac channel**: No.
**Status (2026-10-26)**: not started.
**Success Criteria** (what must be TRUE):
  1. `.gitmodules` lists `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` by https remote URL with no local path, each pinned to a commit that exists on its remote. Verify: a gate test parses `.gitmodules` and `git ls-tree HEAD plugins/` for three gitlinks, and `docs/plugins.md` records each pin and the update procedure.
  2. A fresh clone with `--recurse-submodules` restores the plugin directories, and the repository hygiene tests pass with them present. Verify: fresh-clone run recorded in the runbook.
  3. `plugins/gsd-core-omp` reports its health as JSON (exit 0 healthy, 2 degraded, 3 not installed) and degrades without GSD-Core. Verify: node tests and the recorded probe output.
  4. The plugin catalog names the four plugins in composition order (Ultrathink, Swarm, Substrate, GSD-Core). An uninitialised submodule raises `SubmoduleNotInitializedException` naming the plugin, the path and `git submodule update --init --recursive plugins/<name>`. Verify: unit tests.
  5. The launch planner produces the argv and environment for `omp acp`: exact argument order, a per-session swarm state directory, `SWARM_GSD_STATE_FILE` empty, no `SWARM_AGENT` or `SWARM_TASK_ID`, tokens only by environment variable name and never in argv. The ACP transport applies extra arguments and environment, and `session/new` carries the MCP server specs. Verify: transport and planner tests against a fake omp.
  6. A real `omp acp` handshake with the planned flags starts and lists the plugin commands, with no model turn. Verify: runbook entry with the probe output.
  7. The gate is green, the repository hygiene scan excludes `plugins/`, and CI checks out submodules (the private agent-substrate submodule needs a token secret, recorded as an owner action). Verify: `./bootstrap.sh`.
**Plans**: TBD

### Phase 45: Agent Loop Stage Sources
**Goal**: What the plugins do becomes observable stage events and task context in a session catalog that survives a restart, and every missing or failing plugin degrades visibly instead of crashing.
**Requirements**: AL-05, AL-06, AL-07, AL-08
**Depends on**: Phase 44
**Mac channel**: No.
**Status (2026-10-26)**: not started.
**Success Criteria** (what must be TRUE):
  1. The Ultrathink, Swarm, Substrate and GSD observers map recorded fixtures (the planner session file, the swarm `live.json` and `events.jsonl`, substrate HTTP replies, GSD query output) to stage events; unknown fields are ignored. Verify: fixture tests per observer.
  2. Each degraded path (planner off or failing, swarm missing, substrate unreachable, GSD tools missing, submodule uninitialised) yields a Degraded or Unavailable event with a plain-text reason and no exception; cancellation ends every observer and leaves no child process. Verify: degraded-path and cancellation tests.
  3. The session registry persists several sessions atomically, quarantines a corrupt or unknown-version file, marks a session that was running at shutdown as Lost, and returns the stored references needed to resume it. Verify: persistence and restart tests.
  4. Task context merges the GSD phase with the substrate brief and lessons, and a session never reads another session's state. Verify: scope-isolation tests.
  5. A steering message is recorded in the substrate as a `note` event with `payload.action` `steer` when it is reachable, and is skipped with a Degraded stage when it is not. Verify: tests with a fake gateway.
  6. The gate is green. Verify: `./bootstrap.sh`.
**Plans**: TBD

### Phase 46: Agent Panel
**Goal**: The application opens on a Cursor-class Agent Panel that runs several concurrent sessions through the composed loop, shows the pipeline stages, takes steering and keeps every state accessible.
**Requirements**: AL-09, AL-10, AL-11, AL-12
**Depends on**: Phases 44, 45
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-26)**: not started.
**Success Criteria** (what must be TRUE):
  1. The default window shows the session rail, the thread, the pipeline strip, the composer and the inspector. Verify: headless UI test and a screenshot recorded under Xvfb.
  2. Two sessions each own an engine; selecting a session rebinds the thread, composer and approvals, and each row shows its own status. Verify: headless test with two scripted sessions.
  3. While a run is active, a message queues as a steer or a follow-up, appears in the steering history and is recorded in the substrate. Verify: UI and presenter tests.
  4. Empty, initialising, streaming, error with retry, success, each degraded mode and a resumed long-running session render their copy. Verify: one test per state.
  5. Every tab stop has an accessible name, the keyboard map in the UI spec works, stage changes are announced through a polite live region, motion honours reduced motion, and new tokens pass the contrast tests. Verify: the accessibility walk over the opened panel and the contrast tests.
  6. Eight screenshots (empty, streaming with pipeline, degraded, error, long-running list, narrow window, dense list, keyboard focus) are recorded under Xvfb. Verify: evidence files in `docs/runbooks/evidence/`.
  7. The gate is green. Verify: `./bootstrap.sh`.
**Plans**: TBD

### Phase 47: Agent Panel on the Web Head
**Goal**: The Blazor web head shows the same panel through the API.
**Requirements**: AL-09 (web head)
**Depends on**: Phase 46; v0.9.2 Phase 13 (hosted routed harness)
**Mac channel**: No.
**Status (2026-10-26)**: deferred. The hosted API runs the InMemory harness, Railway deploys are paused, and the web head cannot spawn processes or read the plugin state, so a faithful panel needs new API endpoints for stage events. Resume when the hosted routed harness (Phase 13) is unblocked.
**Success Criteria** (what must be TRUE):
  1. The API exposes the stage events of a session and the web head renders them with the shared presentation models. Verify: API integration test and bunit test.
**Plans**: TBD
