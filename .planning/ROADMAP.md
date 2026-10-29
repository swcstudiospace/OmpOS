<!-- Tracking: ultrathink node n2 "Decompose into monorepo layout and dependency direction" — SPE-17752 (https://linear.app/swcstudio/issue/SPE-17752); node n8 "Synthesize ordered execution plan in parallel waves" — SPE-17756 (https://linear.app/swcstudio/issue/SPE-17756) -->

# Roadmap: Autonogrammer

## Milestones

- v0.9.1: foundation through verified handoff. Closed 2026-10-21; merge of PR #1 (swcstudiospace/OmpOS) pending. Archived in `.planning/milestones/` (v0.9.1-ROADMAP.md, v0.9.1-REQUIREMENTS.md, v0.9.1-MILESTONE-AUDIT.md, v0.9.1-phases/).
- v0.9.2 Hardening and hosted API: audit status `tech_debt` (2026-10-21, `.planning/v0.9.2-MILESTONE-AUDIT.md`). Phases 1-5, 7, 10, 11, 17-19 complete; 8, 9, 15, 16 superseded; 6, 12, 13, 14 blocked on external criteria. Completion awaits the owner (irreversible). Requirements in `.planning/REQUIREMENTS.md` (75 REQ-IDs).
- v0.13.0 Cursor 1:1 workspace and provider platform: planned 2026-10-22 from the ultrathink plan (graph ut-mvjvg4gj-fb21d80f); phases 36 to 43; requirements VX-01 to VX-10 in `.planning/REQUIREMENTS.md`.
- v0.10.0 Cross-platform UI: active. Phases 20 and 21 complete (both verified). Phase 22 blocked on external hosts: criterion 1 met on the XWayland path (2026-10-24); criteria 3 (Windows runner) and 4 and 5 (Mac channel) open. Phases 23 to 25 partial or blocked (see STATE). Requirements XP-01 to XP-11.
- ~~v0.14.0 Agent loop and Agent Panel~~ Superseded 2026-10-26 (DR-1: the default Cursor-style Agent Panel is the agent panel, there is no second surface) v0.14.0 Agent loop on the Omp Engine and the default Agent Panel: planned 2026-10-26 from the user request to connect claude-ultrathink, agent-swarm and agent-substrate to the agent loop as pinned submodules and omp plugins, add GSD-Core as an omp plugin, build the default Agent Panel and push to main; revised 2026-10-26 from the user message of that date: the four plugins are connected to every omp process the application starts (the engine session and the omp terminals), a running session can be live-steered, and the Kanban tab (Phase 39) and the Cursor-shaped Settings window with provider configuration (Phase 40) are the only additions beyond Cursor parity; ~~phases 44 to 47; requirements AL-01 to AL-12~~ Superseded 2026-10-26 (Phases 48 and 49 and AL-13 to AL-20 added): phases 44 to 49 (with Phases 33, 35, 39 and 40 extended); requirements AL-01 to AL-20 in `.planning/REQUIREMENTS.md`.
- Position at commit `d75fd88` (audited 2026-10-27, `docs/quality/cursor-parity-audit-2026-10-26.md`): the Agent Panel, the Kanban tab, omp terminals with the four plugins, live steering, the Settings window with model providers and the Cursor-style restyle (Phase 50) are built and verified without a model turn. Not built or not wired: the browser pane (Phase 29), hooks, automations, the plugin host, herdr and the MCP gateway in any head, the Pets view, remote and mobile terminals, usage history. No live model turn was approved after Phase 11 (OD-07), and nothing was run on Windows, macOS, Android or a hosted deployment.
- v0.16.0 Autonogrammer: accounts, Cowork and Dispatch: planned 2026-10-28 from the user request and the ultrathink plan graph ut-mvs8ru79-82536fbd; phases 66, 66.1 (scaffold) and 67 to 77; requirements AG-66-* to AG-77-* and AG-SC-1 to AG-SC-6 in `.planning/REQUIREMENTS.md`. Developed on branch `feat/autonogrammer` in its own worktree; the v0.15.0 line of phases 51 to 65 belongs to a separate session on another branch.

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
- [ ] **Phase 12: Hosted Deploy Config and Exposure Gate** - Railway and CORS lock (OD-03 gated); ~~Temporal Cloud~~ superseded 2026-10-21 (docs/handoff.md). BLOCKED (external): OD-03; Railway refused deploys as paused on 2026-10-21 (did not recur on 2026-10-28)
- [ ] **Phase 13: Hosted Routed omp Harness** - Hosted API runs the real harness (OD-03 gated). BLOCKED (external): OD-03, criterion 2 (Railway refused deploys as paused on 2026-10-21; that did not recur on 2026-10-28)
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
  2. The MAUI override at `apps/Autonogrammer.App/Autonogrammer.App.csproj:27` is either removed with build evidence, or kept with its justification recorded. The MAUI SDK is not on this host, so no MAUI result is claimed without that evidence.
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
  2. The replay of `tests/Autonogrammer.Workflows.Tests/TestData/histories/session-run.json` succeeds against the Phase 8 workflow code. The command, date and result are recorded in `03-VERIFICATION.md` human verification 1.
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
**Status (2026-10-21):** Criterion 3 met (commit 3f18b99, `tests/Autonogrammer.Api.IntegrationTests/PostgresRoutedRunTests.cs`). Criterion 2 blocked by the Railway deploy pause (`docs/runbooks/hosting.md`, deploy checklist). Update 2026-10-28: the pause did not recur and a staged private worker was deployed, but the API is still not deployed (OD-03), so criterion 2 stays blocked.
**Goal**: The hosted API runs the real omp harness and persists routed runs through the PostgreSQL log.
**Depends on**: Phase 8, Phase 12
**Requirements**: HOST-04
**Gate**: OD-03. Routed use also depends on OD-02 (Phase 7 and Phase 8).
**Success Criteria** (what must be TRUE):
  1. `services/Autonogrammer.Api/Program.cs` wires the real omp harness through configuration, and the in-memory adapter is not used on the hosted path.
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
**Goal**: `apps/Autonogrammer.App` builds for iOS and MacCatalyst with warnings as errors, and the macOS CI shells job runs green.
**Depends on**: Nothing for the builds; a pushed branch for the CI run
**Requirements**: MAC-01, MAC-02, MAC-03
**Gate**: A macOS host with Xcode 27.1 (the Mac channel). The CI run needs a remote push; the first push is human-only (HO-1) and excluded from this milestone.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. `apps/Autonogrammer.App` builds for `net10.0-ios` with warnings as errors on the macOS host, and the log is recorded.
  2. `apps/Autonogrammer.App` builds for `net10.0-maccatalyst` with warnings as errors on the same host, and the log is recorded.
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
| 12. Hosted Deploy Config and Exposure Gate | - | Blocked (external: OD-03 sign-in authority; Railway refused deploys as paused on 2026-10-21, which did not recur on 2026-10-28; criteria 1 and 3 met) | - |
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
| OD-10 | Verifier decision in `Q-SB-ENCLAVE`: the attestation report format to model is not chosen | Verifier and owner choose the report format; then the verdict is implemented against it | autonogrammer-maintainers |
| OD-11 | Podman 5.x required for `OpenShellProvider` through the compute driver; the host does not have it | Podman 5.x installed on this host; then exercise the driver and record the result in `Q-SB-OPENSHELL` | autonogrammer-maintainers |
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
- [x] **Phase 25.9: Suppression and Skip Review** - Every suppression and skip marker decided; counts reconciled (fix F-H). Complete 2026-10-26 (`38086c8`); record written 2026-10-27

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
**Status (2026-10-25)**: partially complete. Criterion 1 is met. Criteria 4 and 5 are met. Criterion 2's gate part is met: the axe smoke runs in `bootstrap.sh` and in the CI `build-test` job (2026-10-25). Criterion 2's deploy part and criterion 3 are blocked on AUTONOGRAMMER_API_ORIGIN and on the Vercel and Railway deploys.
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
**Status (2026-10-25)**: partially complete. Criterion 4's local build is met: `apps/Autonogrammer.Android` builds for net10.0-android36.0 with warnings as errors. Its CI run needs a push, which is not authorized (G24-2). Criteria 1 to 3 need a Mac with Xcode, and criterion 5 needs an Android 16 device or emulator (no /dev/kvm on this host).
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
**Status (2026-10-25)**: complete. Criterion 1 is met: all 24 pending rows have a passing native Avalonia test, 10 by native behaviour tests, 1 by-design test accepted by the owner, and 13 rows implemented in the Avalonia head with tests. Criterion 2 is met: `apps/Autonogrammer.App` is removed from the solution and from the tree, and the search finds no reference in the solution, project or CI files. Criterion 3 is met: `src/Autonogrammer.UI` stays, referenced by `apps/Autonogrammer.Web` and `tests/Autonogrammer.UI.Tests`. Criterion 4 is met at commit 59e9dd1 (gate: 2276 passed, 0 failed, 0 skipped). gsd-verifier passed 4 of 4 (25-VERIFICATION.md). The owner's waiver covers the dependencies on Phases 22 and 24 (2026-10-25); Phase 23 stays an open dependency by the owner's decision (2026-10-25). The QUALITY_BAR fixes are committed (0be414e, 59e9dd1); one gap stays recorded, the dead-code check (25-SUMMARY.md).
**Success Criteria** (what must be TRUE):
  1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: gate run at phase HEAD.
  2. apps/Autonogrammer.App and its test projects are removed, and a repository search finds no reference to apps/Autonogrammer.App in the solution, project or CI files. Verify: search output in the phase record and the gate.
  3. src/Autonogrammer.UI and its test projects are removed only when no project in the solution references them. src/Autonogrammer.UI stays because the Blazor web head uses it; its removal is tracked under gate G-WEB, not under XP-01. Verify: repository search output and the gate.
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
  3. Repository setup steps (.autonogrammer/setup.json) run only after consent recorded per repository, keyed by repository path and steps hash; a changed file asks again. Verify: SetupRunner tests cover refusal without consent, run after consent, and re-prompt after a change.
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
**Status (2026-10-27)**: complete. Commit `38086c8` (2026-10-26). The decisions, the counts and the ledger test are in `docs/quality/quality-bar.md` section 2 and `docs/open-questions.md`; the record was written afterwards: `.planning/phases/25.9-suppression-and-skip-review/25.9-CONTEXT.md`, `25.9-01-PLAN.md` and `25.9-01-SUMMARY.md`. The gate is met as recorded whole-solution test runs, not as a `./bootstrap.sh` run for that commit. Eight kept suppression rows (SUP-157 to SUP-164) were added later by other work and are noted in `quality-bar.md`.
**Success Criteria** (what must be TRUE):
  1. Each SUP row in docs/quality/quality-bar.md section 2 has a recorded decision: keep with its justification, or fix the cause. Verify: the decision table has one row per SUP id. **Result: Met (156 rows: 136 kept, 20 fixed).**
  2. The pragma and skip counts reconcile with the stated scope. Verify: the counts table names the scope for each count. **Result: Met (a raw grep still gives 19 pragma and 77 `SuppressMessage(` lines against the table's 17 and 64; the scanner excludes comments and strings and was not re-run to trace the difference).**
  3. Each `.editorconfig` severity-none override is justified or removed. Verify: the .editorconfig diff and the justification text. **Result: Met (SUP-001 to SUP-004 justify all four; `.editorconfig` is unchanged).**
  4. The gate is green at the phase HEAD. Verify: ./bootstrap.sh. **Result: Met (recorded whole-solution test runs at the final tree; a `./bootstrap.sh` run for `38086c8` itself is not recorded).**
**Plans**: `25.9-01-PLAN.md` (units built), `25.9-01-SUMMARY.md` in `.planning/phases/25.9-suppression-and-skip-review/`

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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criterion 1 met (side chats and transcript search are in the application). Criteria 2 and 3 partial: the Agents window has no feeder (the Agent rail and the Kanban tab list real sessions) and the worktree manager is wired only into the hosted API. Criterion 4 partial: the run contract and its tests exist, with no live cloud run (OD-03, OQ-70). Criterion 5 not started (OD-03, OQ-68).
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 1 and 3 met (the editor is mounted; Diff review is fed by the Changes bar since `d75fd88` and tested against temporary files, with no real agent edit because no model turn ran). Criteria 2 and 4 partial: no open-folder action (the root is the launch directory), and the inline edit view is unbound with no production caller of its client.
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 3 and 4 met. Criteria 1, 2 and 5 partial: the terminal panel approves through its own buttons and not the ADE-02 bridge, and runs on Linux only (OQ-69); the MCP registry is empty in the application; the setup runner has no caller.
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criterion 1 not started (no browser pane code; OQ-51). Criteria 2 and 3 partial: the completion client is never built in the application (wire format OQ-55), and the usage history is always empty.
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
**Status (2026-10-27)**: deferred in writing, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 1 and 5 met; criteria 2 to 4 deferred (OD-03, OQ-60, OQ-61). No stub ships (`DeferredScopeTests`).
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
- [ ] **Phase 34: Remote Development** - Authenticated, encrypted remote transport; BLOCKED on OD-06 and owner decisions (CX-05) Superseded in part 2026-10-28: Phase 58 answers OD-06 by design (omp over ssh stdio, ADR-0015); its host-trust and reconnect criteria are implemented and unit tested, live only to the session open.
- [ ] **Phase 35: Code Quality Bar** - Repository-wide review, enforced gates and decision records (CX-06, CX-07, CX-08) Partial 2026-10-27: criteria 1 and 4 partly met (see its Status line).

### Phase 31: Plugin Manifests and Hooks
**Goal**: Plugins declare a validated manifest with API version, capabilities and permissions. The host loads, quarantines or rejects them without crashing, and hooks run only after consent for their exact command.
**Requirements**: CX-01, CX-02
**Depends on**: Phase 21 (approval path), Phase 28 (consent, permission broker, process supervisor)
**Mac channel**: No. The gate runs on the Linux host.
**UI hint**: yes
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. The manifest parser, the host, quarantine, the hook runner and the hooks view exist with tests; no head loads a manifest or runs a hook, and no code outside tests creates the hooks view (OQ-84; ADR-0013 Proposed).
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. The guards, the engine and the presentation model pass their tests (chain depth, fail-closed store, rate and runtime limits); no head or service composes the engine, nothing starts an agent run from an automation, and no automations view exists.
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 1 and 4 met live with no model turn (the terminal launches the located omp through the planner with the four plugins: `LiveOmpTerminalTests`, `LiveOmpHandshakeTests`). Criteria 2 and 3 partial: `Harness.Registry` and `Harness.McpGateway` are referenced by no head or service, and omp is not launched with the gateway (ADR-0010 Proposed).
**Success Criteria** (what must be TRUE):
  1. The terminal launches the located omp executable with an argument array and the project directory. A missing omp shows its detected path, version and install guidance while the rest of the app keeps working. Verify: gate test with a fake executable.
  2. The registry has one writer; its export carries a generation and checksum; the UI list and the omp view report the same generation. Verify: gate test.
  3. omp's MCP connections go through the host-owned MCP gateway (decision 2026-10-22), which refuses tools that are not granted. Verify: gate test that an ungranted tool call is refused at the gateway.
  4. The terminal starts omp with the four plugins loaded (Phase 48). Verify: Phase 48 criteria 1, 2 and 3 (planner terminal launch, gate test with a fake executable, recorded exactly-once run).
**Plans**: TBD

### Phase 34: Remote Development
**Goal**: Remote development connects to a host over an authenticated, encrypted transport and verifies host identity before the first connection.
**Requirements**: CX-05
**Status (2026-10-22)**: BLOCKED. The remote omp endpoint and WebSocket upgrade authentication are unverified (OD-06, Q-HARN-WS). The remote protocol and host-key storage are not decided. No Cursor primary docs page for SSH remote development was found (https://cursor.com/docs/remote-ssh.md and https://cursor.com/docs/remote-development return 404). This phase stays deferred in writing until OD-06 has a verified spec and the owner decides the protocol and host-key store. No stub ships.
**Update (2026-10-28)**: OD-06 and OQ-75 are answered by design: a remote omp runs over `ssh` stdio (ADR-0015, Phase 58) and a cloud agent over the Autonogrammer-owned WebSocket bridge (ADR-0020, Phase 60). The host-trust terminal (host identity before the first connection) and the reconnect with `session/load` are built and tested with a fake ssh; a real omp opened a session through a real `sshd` on loopback (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`). Not verified: a prompt turn (OD-07), a real network host, a live connection drop, Windows heads. The 2026-10-22 text above is the history.
**Plans**: TBD

### Phase 35: Code Quality Bar
**Goal**: The codebase meets an enterprise and production bar, measured by repo-enforced checks, with every audit finding fixed or recorded with an owner.
**Requirements**: CX-06, CX-07, CX-08
**Depends on**: Phases 20 to 33 (the audit covers the final tree)
**Status (2026-10-27)**: partial. Records: `.planning/phases/35-cursor-parity-and-quality-bar/`; audit: `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 2 and 3 met. Criterion 1 partly met (the Phase 25.1 register and fix phases 25.2 to 25.9; no new per-file review of the final tree). Criterion 4 partly met (every row has a status; 55 of the 83 open rows cite no owner record). `./bootstrap.sh` itself was not run for the record.
**Success Criteria** (what must be TRUE):
  1. A repository-wide review produces per-file findings; each finding is fixed or recorded with an owner and an open-question link. Verify: findings table in the phase record. **Result: Partly met (Phase 25.1 register of 130 rows and fix phases 25.2 to 25.9, baseline `59e9dd1`; no new per-file review of the final tree).**
  2. Analyzer, warnings-as-errors, formatter, architecture and secret-scan checks are enforced in CI and in bootstrap, and every suppression has a row in docs/open-questions.md. Verify: gate run at phase HEAD. **Result: Met (recorded whole-solution test runs; `./bootstrap.sh` itself not recorded for the final tree).**
  3. Decision records exist for the editor, terminal, MCP, browser pane and plugin isolation choices. Verify: docs/decisions listing. **Result: Met (ADR-0006, 0007, 0009, 0010, 0013 and 0014; ADR-0006, 0010, 0013 and 0014 are Proposed, ADR-0009 is accepted for Linux only).**
  4. The audit covers the Cursor-clone inventories ADE-01 to ADE-22, CX-01 to CX-08, VX-01 to VX-10 and the 34-row table in `docs/plans/vision-research/cursor-settings-window.md` (AL-20); no new inventory is created, and each row is verified, planned or recorded with an owner. Verify: findings table in the phase record, one row per inventory. **Result: Partly met (the audit `docs/quality/cursor-parity-audit-2026-10-26.md` has one row per inventory item with status, evidence and gap; 55 of the 83 open rows cite no owner record).**
**Plans**: TBD

## v0.13.0 Cursor 1:1 workspace and provider platform (planned, added 2026-10-22)

Source: user request 2026-10-22 and the ultrathink plan graph ut-mvjvg4gj-fb21d80f (spec `/root/.omp/agent/ultrathink/sessions/01a160e3-3eca-717c-b5de-fb65516aa688.xml`). Primary-source research with a verdict per row is in `docs/plans/vision-research/`. Requirements VX-01 to VX-10 are in `.planning/REQUIREMENTS.md`. Phases 36 to 43 build on v0.11.0 (sessions, approvals, terminal, MCP) and v0.12.0 (plugin host, registry, omp launcher).

Phases 36 to 38 do not depend on the blocked v0.10.0 items (Phases 22, 24, 25), so they run while those stay blocked. The blocked items stay open; this plan closes none of them.
- [x] **Phase 36: Contracts and Theme Foundation** - token set (White and Purple), Greptile font families under OFL, command record, Kanban status model, provider record and URL validator, secret type with canary harness, herdr and omp adapter contracts with recorded fakes
- [ ] **Phase 37: Session Runtime, Streaming and Local Terminal** - session lifecycle service, heartbeat and stale detection, sequence-numbered stream with replay, local PTY backend on Linux. Criteria 1 to 4 verified (37-VERIFICATION.md); criterion 5 open by design (owner durability decision, STATE)
- [ ] **Phase 38: Chat Window and Slash Commands** - chat states and composer context, slash registry and palette
- [ ] **Phase 39: Kanban Board** - one task card per chat with runtime status and a stale flag Partial 2026-10-27: criteria 7 and 8 are partly met (the head reports no lease holder; two of five state screenshots recorded), the rest met by tests with fakes (see its Status line).
- [ ] **Phase 40: Settings Window and Model Providers** - Runtime section, provider catalogue (local, custom, cloud, API key, coding plan) Partial 2026-10-26: criterion 5 is deferred (owner credentials, OQ-71) and criterion 6 is partly met (8 kept, 4 inside a page, 22 omitted; the Network and Keyboard pages exist; no test enumerates the rows; see its Status line).
- [ ] **Phase 41: Pets Companion** - pet state model, 2D fallback, hide and reduced-motion controls
- [ ] **Phase 42: Cloud Terminals on Railway** - persistent container per session with idle sleep; prototype until Railway confirms policy in writing
- [ ] **Phase 43: Mobile and Web Terminals** - remote-attached terminal on iOS, Android and WebAssembly; session-only web keys by default
### Phase 36: Contracts and Theme Foundation
**Status (2026-10-27)**: complete as recorded (6 of 6 criteria in 36-VERIFICATION.md); audited as 5 met and 1 partial in `docs/quality/cursor-parity-audit-2026-10-26.md`: criterion 6 (the herdr and omp adapter contracts) has recorded fakes and tests, but no product project references either. Pending, recorded and not failed: VX-01 owner look sign-off; VX-06 real herdr endpoint; VX-07 omp launch-time provider configuration. The static Greptile faces of Phase 50 replaced the variable font files this phase bundled; the verification paths and a removed test name in 36-VERIFICATION.md were corrected on 2026-10-27.
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
**Cross-reference (2026-10-26)**: Greptile font consistency (AL-19) is verified in Phases 39, 40 and 46; criterion 1 here keeps the VX-01 token and licence checks.
**Plans**: TBD

### Phase 37: Session Runtime, Streaming and Local Terminal
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 3 and 4 met (replay without duplicates; the Linux PTY backend). Criteria 1 and 2 partial: 37-VERIFICATION.md records them as verified against the herdr fake, but no product project references the herdr service or monitor, and the head keeps, re-attaches and ages sessions through `AgentSessionRegistry` (stale after 45 s) instead. Criterion 5 is open by design: the real herdr endpoint waits on the owner's restart-semantics decision (OQ-76). Windows and macOS local backends not run (no runner; macOS on the Mac channel).
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
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. Criteria 1 and 2 met. Criterion 3 partial: the palette is in the composer since `d75fd88` and lists the commands omp advertises (471 in the sandbox of the screenshots); three of its four behaviours have view tests, the disabled-command reason has a model test only. Criterion 4 partial: the catalogue holds one command that a chat session does not list (no handler), its Cursor reference and read date are recorded in code and not in a CONTEXT, and there is no Phase 38 record.
**Success Criteria** (what must be TRUE):
  1. The transcript renders text, tool call, tool result and file edit as distinct blocks; loading, empty first run, streaming with cancel, error with retry that keeps the transcript, and disconnected with reconnect status each have a UI test. Verify: gate UI tests.
  2. The composer shows the active model and provider; a model switch the provider does not support shows a notice. Verify: UI test.
  3. Typing a slash opens the palette; an empty query shows the full list; an unknown command shows a suggestion; a disabled command shows its reason. Verify: UI tests.
  4. The catalogue lists Cursor commands only with the reference version and date recorded in CONTEXT; unverified names are not added. Parity sign-off is an owner decision (default recorded: functional parity against the public docs). Verify: CONTEXT entry and catalogue test.
**Plans**: TBD

### Phase 39: Kanban Board
**Goal**: ~~The Kanban board shows one task card per chat with its live runtime status, and status changes arrive as events.~~ Superseded 2026-10-26 (user direction: the Kanban tab is where every opened session is shown): The Kanban board shows one task card per chat with its live runtime status, and status changes arrive as events, and the Kanban tab shows every opened session as a task card.
**Requirements**: VX-04, AL-17, AL-18
**Depends on**: Phases 36, 37; criteria 5 to 8 also need Phases 45 and 48 (Substrate fields, omp terminal sessions), criterion 9 needs Phase 49
**Mac channel**: No.
**Status (2026-10-27)**: partial. Criteria 1 to 6, 9 and 10 met by hermetic tests with fake sessions and fakes of the steering and Substrate services; criterion 4 is the recorded default (OQ-77 open); criteria 7 and 8 partly met: the head never reports a lease holder, and only two of the five state screenshots (populated, empty) are recorded. Closing a session records it Paused (a Completed session stays Completed) and its card shows in Archived; a session that ended by itself stays in Completed, Failed or Unreachable until it is closed (OQ-79). No live model turn, no run against a real Substrate, nothing on Windows, macOS, Android or the web head. Re-verified at `d75fd88` in `docs/quality/cursor-parity-audit-2026-10-26.md`: unchanged (`HeadKanbanDetailsSource` still returns no lease holder; the populated and empty screenshots exist in Dark and Light). Records: `.planning/phases/39-kanban-board/39-CONTEXT.md`, `39-01-PLAN.md`, `39-SUMMARY.md` and `39-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. One card per chat shows title, active model and provider, last activity time and status; statuses come from the runtime enumeration, with no client-side guesses. Verify: gate tests from fake session events. **Result: Met.**
  2. Runtime statuses are not user-draggable; only the user-settable statuses (default Backlog and Archived) accept drag. Verify: gate test. **Result: Met.**
  3. A card with a stale heartbeat shows the stale indicator with a screen-reader label; an unreachable session shows the card error state. Verify: UI test. **Result: Met.**
  4. Column names are the defaults recorded in CONTEXT until the owner confirms the final names. Verify: CONTEXT entry (owner decision). **Result: Met (recorded default; the owner has not confirmed, OQ-77 open).**
  5. The Kanban tab exists and is reachable from the application navigation. Verify: headless UI test that navigates from the main window to the tab. **Result: Met.**
  6. Each opened session (Agent Panel chat or omp terminal) appears as exactly one card: opening creates it, and closing or ending moves it to its terminal column (Completed, Failed or Archived) and never removes it silently (DR-5b). Verify: gate tests from fake session events (open, close, end) for a chat and for a terminal session. **Result: Met (fakes; closing records a session Paused, so its card goes to Archived, and a Completed session stays in Completed, see OQ-79; no test drives the head from a close click to the column).**
  7. Card status mirrors the session lifecycle status using the CONTEXT defaults (Backlog, Queued, Running, Awaiting input, Needs attention, Paused, Completed, Failed, Unreachable, Archived; OQ-77 keeps the final names open and OQ-78 records the mapping default); closing or ending a session moves its card to its terminal column (OQ-79); selecting a card opens or focuses its session; status updates live without a refresh. A card shows title, model and provider, last activity, status, stale indicator and, when Substrate is reachable, task, graph, lessons count and lease holder; without Substrate these show as unavailable, never invented. Verify: gate tests from fake session events, plus tests with a fake Substrate gateway reachable and unreachable. **Result: Partly met (everything except the lease holder: the head reports none, so `Lease` always reads unavailable; no real Substrate run).**
  8. The tab renders loading skeleton, empty with a way to start a session, error with retry, populated, and disconnected (terminal dropped) distinctly from failed; it is keyboard navigable with visible focus, exposes labels to assistive technology, never conveys status by colour alone and honours reduced motion for live updates. The context and the steering history moved off the old Inspector region (DR-1) show in the card detail. Verify: one UI test per state, the accessibility walk over the tab, and one screenshot per state in `docs/runbooks/evidence/`. **Result: Partly met (one test per state and the accessibility tests are met; screenshots exist for populated and empty only; the card shows the Substrate fields and the last steering result, not a list of steering history).**
  9. A running session can be steered from its card (Phase 49 delivers and reports it). Verify: UI test that the card steer action calls the steering service for that session, with delivery tests in Phase 49. **Result: Met (against fakes; delivery is Phase 49).**
  10. Every text control on the tab resolves its family through the `Autonogrammer.Font.*` tokens, with no literal family in AXAML (AL-19). Verify: headless test walking the Kanban tab, and a recorded screenshot showing DM Sans. **Result: Met (tests; the screenshots `04-kanban-cards.png` and `05-kanban-empty.png` show DM Sans body text, judged by eye).**
**Plans**: `39-01-PLAN.md` (units built), `39-SUMMARY.md`, `39-VERIFICATION.md` in `.planning/phases/39-kanban-board/`

### Phase 40: Settings Window and Model Providers
**Goal**: The settings window holds a Runtime section and a Model Providers section, and every provider is a registry entry configured without code changes.
**Requirements**: VX-05
**Depends on**: Phases 36, 37
**Mac channel**: No.
**Status (2026-10-27)**: partial. Criteria 1, 2, 4 and 7 met by gate tests and the recorded screenshots; criterion 3 met (live, no model turn) for base URL, auth kind, model list and capability flags, with no editable auth header name; criterion 5 deferred (external: owner credentials, OQ-71); criterion 6 partly met: `docs/settings-parity.md` decides all 34 rows (8 kept as a page, 4 shown inside a page, 22 omitted); the Network and Keyboard Shortcuts pages of owner option (a) exist since `a12871a` (`NetworkDiagnosticsSourceTests`, `ShortcutCatalogueTests`; eleven pages in all), and no test enumerates the 34 rows. Providers and keys live in omp (OQ-92): the Models page edits only Autonogrammer-owned blocks of omp's `models.yml`, imports a built-in provider's key with `omp auth import -`, and keeps a custom provider's key in an owner-only file passed to omp as an environment variable. Live, no model turn, real omp 18.12.6 in a sandbox: the four `LiveOmpConfigTests` passed. After the Settings and Models wave every test project passed (F6b), and whole-solution test runs at the final tree passed (F10). Not verified: provider credential validity and quotas, a live model turn (OD-07), Windows, macOS, Android, hosted deployments. Records: `.planning/phases/40-settings-window-and-model-providers/40-CONTEXT.md`, `40-01-PLAN.md`, `40-SUMMARY.md` and `40-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. The Runtime section sets idle stop (default 24 hours) and completed-session retention (default 7 days) with validated floors and ceilings; invalid values are rejected inline; reset-to-default works per section; closing with unsaved changes asks first. Verify: UI tests. **Result: Met (floors and ceilings of 1 to 720 hours and 1 to 90 days are the code's assumptions, not owner decisions).**
  2. Local hosting entries (Ollama, LM Studio, llama.cpp, vLLM, LocalAI, Jan, Docker Model Runner) and the generic OpenAI-compatible local transport are recorded with source and date; an unreachable local server shows a not-running error. Verify: probe test against a fake server and a catalogue test that every entry carries source and date. **Result: Met (fake server; no real local server was started).**
  3. A custom provider is added from a user entry (base URL, auth header, model list, capability flags) without a code change. Verify: gate test. **Result: Met (live, no model turn) with one difference: the form offers an auth kind (none, API key, bearer), not a free header name.**
  4. Cloud API-key and coding-plan entries are added only where the research verified the endpoint, auth and quota (default: verified providers only). Coding plans are separate entries with their own auth, quota display and error mapping; quota exhaustion and plan expiry produce actionable messages. Verify: catalogue tests per entry. **Result: Met (catalogue and error-mapper tests; the built-in API-key table is verified against omp, not the research files).**
  5. Live validation with real credentials is blocked until the owner supplies test credentials per provider. Verify: STATE entry. **Result: Deferred (external: owner credentials, OQ-71; no live provider call was made).**
  6. The Settings window follows the Cursor Settings structure of rows 1 to 34 of `docs/plans/vision-research/cursor-settings-window.md` (owner decision 2026-10-26, DR-6: owner decisions 1 and 2 of that research answered (a), a Cursor-shaped Settings window with per-provider fields in its Models section, expanded for providers), and its Models section surfaces provider configuration (per-provider fields, custom provider entry, local hosting probes) as criteria 2 to 4 specify; no provider is named that the research files do not verify, and no plugin enable or disable setting exists (DR-5c; the default is recorded as an open question until the user confirms). Verify: settings UI tests per section against the 34 research rows, the catalogue tests of criteria 2 to 4, and a test that no plugin switch is rendered. **Result: Partly met (eleven pages and the decision for every row in `docs/settings-parity.md`: 8 kept as a page, 4 shown inside a page, 22 omitted; no plugin switch, `PluginsPageTests.ThereIsNoSwitch`; the Network and Keyboard Shortcuts pages exist; no test enumerates the 34 rows).**
  7. Every text control in the Settings window resolves its family through the `Autonogrammer.Font.*` tokens, with no literal family in AXAML (AL-19). Verify: headless test walking the Settings window, and a recorded screenshot showing DM Sans. **Result: Met (tests; the Settings screenshots of all eleven pages in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` show DM Sans body text and the page headings in Anybody SemiBold at 20 px, judged by eye).**
**Plans**: `40-01-PLAN.md` (units built), `40-SUMMARY.md`, `40-VERIFICATION.md` in `.planning/phases/40-settings-window-and-model-providers/`

### Phase 41: Pets Companion
**Goal**: A companion character shows session state at a glance, never blocks the chat or terminal, and respects reduced motion and a hide toggle.
**Requirements**: VX-09
**Depends on**: Phases 36, 39
**Mac channel**: No.
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. The pet state model, the preferences and the text equivalents exist with tests (`PetStateModelTests`); no companion view, art, animation or layout test exists, and the 3D spikes were never run (criterion 3 holds only because nothing is built). The owner sign-off and the asset licence are open (OQ-72).
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
**Status (2026-10-27)**: prototype only, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. `CloudSessionHost` and its tests over `FakeCloudEnvironmentClient` meet criteria 1 and 3 as a library; no head or API uses it, and the live run is blocked (paused deploys, OD-03). Criterion 2 is deferred: no remote desktop is built until Railway confirms in writing (OQ-73). No production deploy until criterion 2 is met.
**Plans**: TBD

### Phase 43: Mobile and Web Terminals
**Goal**: iOS, Android and WebAssembly show a remote-attached terminal, never spawn a local shell, and keep web keys out of durable browser storage.
**Requirements**: VX-08, VX-10
**Depends on**: Phases 36, 37, 40
**Mac channel**: Yes for the iOS build and iOS accessibility dump (listed under XP-11). The Android build runs on the Linux runner.
**Status (2026-10-27)**: partial, audited in `docs/quality/cursor-parity-audit-2026-10-26.md`. The web head references no process-spawning assembly (`ProjectGraphTests`) and the terminal models pass their tests (`TerminalAvailabilityTests`, `RemoteTerminalReplayTests`), but the Android head still references `Native.Views` and so `Harness.Terminal`, there is no terminal view or real transport on mobile or web, and no web page uses the session key holder. Criterion 4 is recorded as blocked (OQ-74).
**Success Criteria** (what must be TRUE):
  1. Mobile and web builds reference no local process spawning; the local terminal shows as unavailable with its reason. Verify: per-target build check and UI test.
  2. A remote-attached terminal renders the same terminal model as desktop and resumes after network loss without duplicated blocks. Verify: gate replay test.
  3. WebAssembly keeps provider keys in session-only memory, cleared on reload, with a documented threat model; no key is written to browser storage. Verify: browser-run check and the canary test. Pause: server-side vault or per-session entry (default session-only).
  4. The Android accessibility dump needs an Android 16 device or emulator, which this host does not have; it is recorded as blocked.
**Plans**: TBD

## v0.14.0 Agent loop and Agent Panel (planned, added 2026-10-26; revised 2026-10-26)

Source: user request 2026-10-26 and the ultrathink plan graph ut-mvp0bh59-2dd6ef4c (spec `/root/.omp/agent/ultrathink/sessions/01a17752-7e43-7002-8ac7-6781e5b19475.xml`); revised the same day from the user message of that date and the ultrathink plan graph ut-mvp88pnw-6e8c4d24. Evidence is in the discovery dossiers under `docs/plans/agent-loop/`. Decisions are recorded in `docs/decisions/ADR-0012-agent-loop-plugins.md`. ~~Requirements AL-01 to AL-12 are in `.planning/REQUIREMENTS.md`.~~ Superseded 2026-10-26 (AL-13 to AL-20 added): Requirements AL-01 to AL-20 are in `.planning/REQUIREMENTS.md`.

Settled decisions: the three plugins run inside the omp engine and Autonogrammer launches omp with them and observes them (the hybrid of the ultrathink clarification q1, settled from repository evidence); the Agent Panel is built on the shared presentation layer in the Avalonia native head (q2, the MAUI head is removed); the runnable panel comes first and externally blocked phases are deferred with recorded reasons (q3). Live delivery of a steering message into a running session does not exist in agent-substrate on main (its Phase 53 is unmerged), ~~so Autonogrammer delivers steering over ACP and only records it in the substrate.~~ Superseded 2026-10-26 (DR-4: omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so prompting over ACP cannot deliver live; delivery is Phase 49 and queue-after-turn stays the documented fallback): Autonogrammer records steering in the substrate and never attributes delivery to it.

Revision 2026-10-26 (source: the user message of 2026-10-26; the shared decision labels below are also used in the ADR-0012 update section and in the Phase 46 amendments):
- DR-1 No additional Agent Panel. The default Cursor-style Agent Panel is the agent panel and stays a faithful Cursor clone. Superseded: the Inspector region (Pipeline, Context and Steering history tabs and the Inspector toggle) and any wording that calls the panel a new Cursor-class surface with its own regions. Kept: the session rail (Cursor's Agents sidebar), the thread, the composer with Cursor-style queue and send-now, the header chips and the one-line plugin connection row directly under the header. The context and steering history content moves to the Kanban card.
- DR-2 The four plugins (Ultrathink, Substrate, Swarm, GSD-Core) are connected to every omp process the application starts, the engine session behind the Agent Panel and the omp terminals, loaded exactly once from the pinned submodules, and verified by a recorded run per plugin plus one end-to-end run without a model turn (Phase 48).
- DR-3 The Kanban tab is the only product addition besides provider settings: one card per opened session (chat or omp terminal), live status, selecting a card opens or focuses the session, steering from the card (Phase 39).
- DR-4 Steering reuses the existing Steer intent. Live delivery over ACP prompting is not possible (a new `session/prompt` cancels the running turn) and Substrate on the pinned commit has no live delivery (OQ-66), so the delivery mechanism is an open question with a recommended default in `docs/open-questions.md`; the requirement is written at outcome level (AL-15, AL-16, Phase 49).
- DR-5 Defaults, non-blocking, changeable by the user: (a) Kanban columns mirror the session lifecycle status; (b) closing or ending a session moves its card to its terminal column and never removes it silently; (c) the four plugins are always on, with no enable or disable settings item (the launch planner keeps its per-plugin switches for operators and tests).
- DR-6 Settings: the user answered owner decisions 1 and 2 of `docs/plans/vision-research/cursor-settings-window.md` with (a), a Cursor-shaped Settings window with per-provider fields in its Models section; Phase 40 carries it.

- [ ] **Phase 44: Plugin Submodules and Launch Contract** - three pinned submodules, the GSD-Core omp plugin, the plugin catalog and its guard, ACP launch plumbing, repository hygiene and CI Partial 2026-10-27: criterion 2 (fresh-clone run) has no run recorded in the repository; criterion 3 is met since the `bin` scripts are tracked and the node tests are gated (see its Status line).
- [x] **Phase 45: Agent Loop Stage Sources** - stage contracts, observers for Ultrathink, Swarm, Substrate and GSD, task context, session registry with restart resume, degradation Complete 2026-10-26.
- [ ] **Phase 46: Default Agent Panel Parity** - ~~the default surface: session rail, thread, pipeline strip, composer with steering, context inspector, states and accessibility~~ Superseded 2026-10-26 (DR-1: no inspector and no separate pipeline strip surface): the default Cursor-style Agent Panel kept faithful to Cursor: session rail, thread, composer with queue and send-now, header plugin connection row, states, accessibility and the Greptile font Partial 2026-10-26: criterion 6 (eight screenshots) is not met; Phase 50 carries the visual parity follow-up.
- [ ] **Phase 47: Agent Panel on the Web Head** - deferred: the hosted API runs the InMemory harness and Railway deploys were refused as paused on 2026-10-21 (not on 2026-10-28) Records: `.planning/phases/47-agent-panel-on-the-web-head/47-DEFERRED.md`.
- [x] **Phase 48: Omp Terminals with the Four Plugins** - the launch planner plans interactive omp launches, the embedded terminal starts omp through it, the four plugins load exactly once in every app-started omp process, per-plugin and end-to-end connection evidence Complete 2026-10-27 (live, no model turn): criterion 8 is met by an application run in which a chat session and an omp terminal session each show all four plugins connected (the Substrate group reads Degraded in that run).
- [ ] **Phase 49: Live Steering** - a running session takes a steering message from the Agent Panel composer and from its Kanban card, delivery is reported truthfully, failures leave the session unchanged and Substrate records the action Partial 2026-10-26: criteria 2 to 5 met by tests with fakes; criterion 1 is partly met (fake-omp tests and the live wire contract; the recorded live run into a running turn is not verified, no model turn approved).
- [x] **Phase 50: Cursor Visual Parity** - dark default theme with a System, Light and Dark choice, the control library on Autonogrammer tokens, static Greptile faces, compact density, a skip link that shows on focus, and the Cursor Agents-window layout for the rail, thread, composer, Kanban tab, omp terminal and Settings window, with before and after screenshots Complete 2026-10-27: criteria 1 to 7 met (see its Status line); the owner has not signed off the look (OQ-87 to OQ-91 are defaults).

### Phase 44: Plugin Submodules and Launch Contract
**Goal**: The three sibling projects are pinned submodules, GSD-Core is an omp plugin, and Autonogrammer can launch omp with the four plugins loaded, hermetically and reproducibly.
**Requirements**: AL-01, AL-02, AL-03, AL-04
**Depends on**: Phase 21 (the ACP session and approval path)
**Mac channel**: No.
**Status (2026-10-27)**: partial. Criteria 1, 3, 4 and 5 met by gate tests (criterion 3 also by the plugin node tests: the GSD-Core `bin` scripts are tracked since `fee72a5` and `scripts/ci/plugin-node-tests.sh` runs the suite from `bootstrap.sh` and CI); criterion 6 met by the live test with no model turn (real omp 18.12.6); criterion 7 met as a gate-equivalent run (4237 passed, 0 failed; `./bootstrap.sh` itself not re-run for the record). Criterion 2 (fresh-clone run) is not recorded in the repository: the audit `docs/quality/cursor-parity-audit-2026-10-26.md` records one fresh clone of an earlier snapshot, in which the hygiene tests were not run. Owner action open: the `SUBMODULE_TOKEN` secret (OQ-63). Records: `.planning/phases/44-plugin-submodules-launch-contract/44-SUMMARY.md` and `44-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. `.gitmodules` lists `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` by https remote URL with no local path, each pinned to a commit that exists on its remote. Verify: a gate test parses `.gitmodules` and `git ls-tree HEAD plugins/` for three gitlinks, and `docs/plugins.md` records each pin and the update procedure. **Result: Met.**
  2. A fresh clone with `--recurse-submodules` restores the plugin directories, and the repository hygiene tests pass with them present. Verify: fresh-clone run recorded in the runbook. **Result: Not verified (no fresh-clone run is recorded).**
  3. `plugins/gsd-core-omp` reports its health as JSON (exit 0 healthy, 2 degraded, 3 not installed) and degrades without GSD-Core. Verify: node tests and the recorded probe output. **Result: Met (the `bin` scripts are tracked since `fee72a5`; `scripts/ci/plugin-node-tests.sh` runs the node suite from `bootstrap.sh` and CI; the audit of 2026-10-27 ran it: 69 pass, 0 fail, node v24.18.0; no probe run against a real GSD-Core install is recorded).**
  4. The plugin catalog names the four plugins in composition order (Ultrathink, Swarm, Substrate, GSD-Core). An uninitialised submodule raises `SubmoduleNotInitializedException` naming the plugin, the path and `git submodule update --init --recursive plugins/<name>`. Verify: unit tests. **Result: Met.**
  5. The launch planner produces the argv and environment for `omp acp`: exact argument order, a per-session swarm state directory, `SWARM_GSD_STATE_FILE` ~~empty~~ Superseded 2026-10-26 (the pseudo-terminal backend removes an empty variable): a per-session path `<session>/swarm/gsd-state.md`, no `SWARM_AGENT` or `SWARM_TASK_ID`, tokens only by environment variable name and never in argv. The ACP transport applies extra arguments and environment, and `session/new` carries the MCP server specs. Verify: transport and planner tests against a fake omp. **Result: Met (amended wording; the per-session path replaces the empty value).**
  6. A real `omp acp` handshake with the planned flags starts and lists the plugin commands, with no model turn. Verify: runbook entry with the probe output. **Result: Met (live, no model turn).**
  7. The gate is green, the repository hygiene scan excludes `plugins/`, and CI checks out submodules (the private agent-substrate submodule needs a token secret, recorded as an owner action). Verify: `./bootstrap.sh`. **Result: Met (gate-equivalent run; the token is an open owner action, OQ-63).**
**Plans**: `44-01-PLAN.md` (units built), `44-SUMMARY.md`, `44-VERIFICATION.md` in `.planning/phases/44-plugin-submodules-launch-contract/`

### Phase 45: Agent Loop Stage Sources
**Goal**: What the plugins do becomes observable stage events and task context in a session catalog that survives a restart, and every missing or failing plugin degrades visibly instead of crashing.
**Requirements**: AL-05, AL-06, AL-07, AL-08
**Depends on**: Phase 44
**Mac channel**: No.
**Status (2026-10-26)**: complete. Criteria 1 to 5 met by fixture and fake-gateway tests (a steering action is recorded in the Substrate ledger; delivery is Phase 49); criterion 6 met as a gate-equivalent run (4237 passed, 0 failed). No real Substrate server, swarm run or planner run was used. Records: `.planning/phases/45-agent-loop-stage-sources/45-CONTEXT.md`, `45-SUMMARY.md` and `45-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. The Ultrathink, Swarm, Substrate and GSD observers map recorded fixtures (the planner session file, the swarm `live.json` and `events.jsonl`, substrate HTTP replies, GSD query output) to stage events; unknown fields are ignored. Verify: fixture tests per observer. **Result: Met.**
  2. Each degraded path (planner off or failing, swarm missing, substrate unreachable, GSD tools missing, submodule uninitialised) yields a Degraded or Unavailable event with a plain-text reason and no exception; cancellation ends every observer and leaves no child process. Verify: degraded-path and cancellation tests. **Result: Met.**
  3. The session registry persists several sessions atomically, quarantines a corrupt or unknown-version file, marks a session that was running at shutdown as Lost, and returns the stored references needed to resume it. Verify: persistence and restart tests. **Result: Met.**
  4. Task context merges the GSD phase with the substrate brief and lessons, and a session never reads another session's state. Verify: scope-isolation tests. **Result: Met.**
  5. A steering message is recorded in the substrate as a `note` event with `payload.action` `steer` when it is reachable, and is skipped with a Degraded stage when it is not. Verify: tests with a fake gateway. **Result: Met (recording only; Substrate does not deliver, OQ-66).**
  6. The gate is green. Verify: `./bootstrap.sh`. **Result: Met (gate-equivalent run).**
**Plans**: `45-01-PLAN.md` (units built), `45-SUMMARY.md`, `45-VERIFICATION.md` in `.planning/phases/45-agent-loop-stage-sources/`

### Phase 46: Default Agent Panel Parity
**Retitled 2026-10-26 (DR-1)**: ~~Phase 46: Agent Panel~~ Superseded 2026-10-26 (the panel is the default Cursor-style Agent Panel, not a new Cursor-class surface with an inspector); amendments are tabulated in `.planning/phases/46-agent-panel/46-CONTEXT.md` and `46-UI-SPEC.md`.
**Goal**: ~~The application opens on a Cursor-class Agent Panel that runs several concurrent sessions through the composed loop, shows the pipeline stages, takes steering and keeps every state accessible.~~ Superseded 2026-10-26 (DR-1): The application opens on the default Cursor-style Agent Panel (session rail as the Agents sidebar, thread, composer) that runs several concurrent sessions through the composed loop on the Omp Engine, shows the plugin connection in the one-line row under its header, takes steering in the composer (delivery is Phase 49), keeps every state accessible and uses the Greptile font; there is no second agent surface.
**Requirements**: AL-09, AL-10, AL-11, AL-12, AL-19
**Depends on**: Phases 44, 45
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-27)**: partial. Criteria 1 to 5, 7 and 8 met by headless tests and the gate-equivalent run (criterion 8 also by the Phase 50 screenshots, which show DM Sans and Space Mono); criterion 6 not met: of the eight screenshots only the empty and the degraded states are recorded, and those were made for Phase 50. No live model turn was run, so streaming and errors with a real model are not verified. The Phase 50 visual parity work (static fonts, tokens, control library, Dark default) restyles the panel and has its own records. Records: `.planning/phases/46-agent-panel/46-01-PLAN.md`, `46-SUMMARY.md` and `46-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. ~~The default window shows the session rail, the thread, the pipeline strip, the composer and the inspector. Verify: headless UI test and a screenshot recorded under Xvfb.~~ Superseded 2026-10-26 (DR-1: no inspector, no separate pipeline strip surface): The application opens on the default Cursor-style Agent Panel: session rail (the Agents sidebar), thread and composer, with the header chips and the one-line plugin connection row directly under the header; no region other than these exists, and there is no inspector and no second agent surface. Verify: headless UI test that no region other than rail, thread and composer (with its header) exists, plus a screenshot recorded under Xvfb. **Result: Met.**
  2. Two sessions each own an engine; selecting a session rebinds the thread, composer and approvals, and each row shows its own status. Verify: headless test with two scripted sessions. **Result: Met (scripted engines).**
  3. ~~While a run is active, a message queues as a steer or a follow-up, appears in the steering history and is recorded in the substrate. Verify: UI and presenter tests.~~ Superseded 2026-10-26 (DR-1: the steering history moves to the Kanban card; DR-4: live delivery is Phase 49): While a run is active, the composer queues a message as a follow-up or sends it now as a steer in Cursor style, and the choice is recorded in the substrate when it is reachable. Verify: UI and presenter tests. **Result: Met (queueing and recording; live delivery into a running turn is Phase 49).**
  4. Empty, initialising, streaming, error with retry, success, each degraded mode and a resumed long-running session render their copy. Verify: one test per state. **Result: Met.**
  5. ~~Every tab stop has an accessible name, the keyboard map in the UI spec works, stage changes are announced through a polite live region, motion honours reduced motion, and new tokens pass the contrast tests. Verify: the accessibility walk over the opened panel and the contrast tests.~~ Superseded 2026-10-26 (DR-1: stage changes become plugin connection changes in the header row): Every tab stop has an accessible name, the keyboard map in the UI spec works, plugin connection changes are announced through a polite live region, motion honours reduced motion, and new tokens pass the contrast tests. Verify: the accessibility walk over the opened panel and the contrast tests. **Result: Met.**
  6. ~~Eight screenshots (empty, streaming with pipeline, degraded, error, long-running list, narrow window, dense list, keyboard focus) are recorded under Xvfb. Verify: evidence files in `docs/runbooks/evidence/`.~~ Superseded 2026-10-26 (DR-1: the inspector and the separate pipeline are gone): Eight screenshots of the default panel (empty, streaming, degraded, error, long-running list, narrow window, dense list, keyboard focus) are recorded under Xvfb; the Kanban tab states are recorded in Phase 39 criterion 8. Verify: evidence files in `docs/runbooks/evidence/`. **Result: Not met (2 of 8 states recorded, for another phase).**
  7. The gate is green. Verify: `./bootstrap.sh`. **Result: Met (gate-equivalent run).**
  8. Greptile font consistency (AL-19): every text control in the Agents layout and the terminal chrome resolves its family through the `Autonogrammer.Font.*` tokens (DM Sans body, Anybody display, Space Mono code), with no literal family in AXAML, and the recorded screenshots show DM Sans. Verify: headless test walking the Agents layout and the terminal chrome, and the screenshots of criterion 6. **Result: Met (tests; the Phase 50 screenshots in `after-dark/` and `after-light/` show DM Sans and Space Mono, judged by eye).**
**Plans**: `46-01-PLAN.md` (units built), `46-SUMMARY.md`, `46-VERIFICATION.md` in `.planning/phases/46-agent-panel/`

### Phase 47: Agent Panel on the Web Head
**Goal**: ~~The Blazor web head shows the same panel through the API.~~ Superseded 2026-10-26 (DR-1 wording): The Blazor web head shows the same default panel through the API.
**Requirements**: AL-09 (web head)
**Depends on**: Phase 46; v0.9.2 Phase 13 (hosted routed harness)
**Mac channel**: No.
**Status (2026-10-26)**: deferred. The hosted API runs the InMemory harness, Railway deploys are paused (update 2026-10-28: the refusal did not recur), and the web head cannot spawn processes or read the plugin state, so a faithful panel needs new API endpoints for stage events. Resume when the hosted routed harness (Phase 13) is unblocked. Nothing was built or verified. Record: `.planning/phases/47-agent-panel-on-the-web-head/47-DEFERRED.md`.
**Success Criteria** (what must be TRUE):
  1. The API exposes the stage events of a session and the web head renders them with the shared presentation models. Verify: API integration test and bunit test. **Result: Deferred (hosted routed harness, Phase 13, is blocked; no code written).**
**Plans**: none; `47-DEFERRED.md` in `.planning/phases/47-agent-panel-on-the-web-head/`

### Phase 48: Omp Terminals with the Four Plugins
**Goal**: Every omp process the application starts, the engine session behind the Agent Panel and each omp terminal, loads Ultrathink, Substrate, Swarm and GSD-Core exactly once from the pinned submodules, and each plugin's connection, or its failure and fix text, is visible to the user.
**Requirements**: AL-13, AL-14
**Depends on**: Phases 44, 45; Phase 33 (the terminal launcher)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-27)**: complete (live, no model turn). Criteria 1, 2 and 9 met; criteria 3 to 7 met without a model turn (probe evidence and live tests against the real omp 18.12.6 on Linux; Substrate loads only when a healthy endpoint is configured, D48-10 in the context); criterion 8 met: in the application under Xvfb with the real omp, a chat session and an omp terminal session each show all four plugins "Connected to omp" (`docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/README.md`, "Application evidence"); the chat and the terminal are two sessions of one sandbox, and the Substrate group reads Degraded there (brief refused with HTTP 403). Not verified: a live model turn (OD-07), an `injected` steering outcome into a running turn, Windows and macOS, Android, hosted deployments. Records: `.planning/phases/48-omp-terminals-with-the-four-plugins/48-CONTEXT.md`, `48-SUMMARY.md` and `48-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. The launch planner can plan an interactive omp (terminal) launch as well as `omp acp`, with the same plugin set, per-session state directory and environment. Verify: planner tests. **Result: Met.**
  2. The embedded terminal starts omp through the planner (argument array, project directory), and a missing omp, a missing plugin or an uninitialised submodule shows its fix text while the rest of the app keeps working. Verify: gate test with a fake executable. **Result: Met.**
  3. Exactly-once loading: with the user's own omp configuration present (global `config.yml` extensions and extension symlinks), each plugin is registered once in the omp process the application starts. Verify: recorded no-model-turn run against a sandbox that has a polluted global configuration, with unique command names; if omp cannot be made to load each plugin exactly once, the fallback is recorded in an open-question row (OQ-82 records the registration scope; OQ-80 records that the plugins are always on). **Result: Met (probe, no model turn; omp 18.12.5 probe sandbox, not an application run).**
  4. Ultrathink is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it (command output, tool calls, stage chips); a load or connect failure is shown with its fix text. Verify: recorded run output (commands registered, plugin-specific probe) and a degraded-path test. **Result: Met (live, no model turn).**
  5. Substrate is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it; a load or connect failure is shown with its fix text. Verify: recorded run output (commands registered, plugin-specific probe) and a degraded-path test. **Result: Met (live, no model turn; planned only with a healthy Substrate endpoint).**
  6. Swarm is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it; a load or connect failure is shown with its fix text. Verify: recorded run output (commands registered, plugin-specific probe) and a degraded-path test. **Result: Met (live, no model turn).**
  7. GSD-Core is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it; a load or connect failure is shown with its fix text. Verify: recorded run output (commands registered, plugin-specific probe) and a degraded-path test. **Result: Met (live, no model turn).**
  8. End to end: starting a session from the application and opening an omp terminal for it shows all four plugins connected. Verify: the run is recorded in the runbook (`docs/runbooks/evidence/`) with the live part tagged Live and excluded from the gate. **Result: Met (live, no model turn): a chat session and an omp terminal session each show all four plugins connected in the application; two sessions of one sandbox, not a terminal opened for the chat session; the Substrate group is Degraded in that run (HTTP 403 on the brief).**
  9. The gate is green. Verify: `./bootstrap.sh`. **Result: Met (gate-equivalent run).**
**Plans**: `48-01-PLAN.md` (units built), `48-SUMMARY.md`, `48-VERIFICATION.md` in `.planning/phases/48-omp-terminals-with-the-four-plugins/`

### Phase 49: Live Steering
**Goal**: A running session takes a steering message from the Agent Panel composer and from its Kanban card, the session visibly takes it into account before the turn ends, delivery is reported truthfully, and a steer that cannot be delivered fails without changing the session.
**Requirements**: AL-15, AL-16
**Depends on**: Phases 46, 48 (the Kanban card entry point also needs Phase 39)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-27)**: partial. OQ-81 is settled by the code (DR-4 as built): chat sessions use omp's own `_session/steering` request when `initialize` advertises `_meta.steering.supported`; omp terminals, and a chat session whose omp lacks the capability, use the omp extension inbox (`AUTONOGRAMMER_STEER_DIR`); queue-after-turn is the last fallback; Substrate only records the steer note. Criteria 2 to 5 met by tests with a fake omp, a fake steering plugin and a fake Substrate gateway, and by the gate-equivalent run (5409 passed, 0 failed, 10 skipped, F7). Criterion 1 partly met: the fake-omp tests are met and, live with no model turn against the real omp 18.12.6, `initialize` advertises the capability and `_session/steering` on an idle session answers `promptRequired`; the recorded live run is not verified because no model turn was approved (OD-07), so an `injected` outcome into a running turn and its visible effect were not observed. Not verified: Windows, macOS, Android, hosted deployments, a real Substrate server. Re-verified at `d75fd88` in `docs/quality/cursor-parity-audit-2026-10-26.md`: the mode and thinking chips are bound in `ComposerView` (since `977010c`); the slash palette and the Changes bar landed under the 49-03 commit but are not criteria of this phase, so they are recorded in Phase 50 and the audit. Whole-solution test runs at the final tree passed (F10). Records: `.planning/phases/49-live-steering/49-CONTEXT.md`, `49-01-PLAN.md`, `49-SUMMARY.md` and `49-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. A running session can be sent a steering message from the Agent Panel composer (send now) and from its Kanban card; the session visibly takes it into account before the turn ends, and the steering history shows delivered, queued or failed truthfully. Verify: tests with a fake omp that records the delivery, plus a recorded live run when a model turn is approved. **Result: Partly met (fake-omp tests met; the wire contract met live with no model turn; the recorded live run is Not verified, no model turn approved; the history entries are kept in the model and their results shown in the status line and on the card, no view lists them).**
  2. A follow-up queues for the end of the turn. Verify: presenter test with the fake omp that the follow-up is delivered only after the turn ends. **Result: Met (fakes; with native steering omp holds the follow-up, and its delivery after the turn is not verified without a model turn).**
  3. Steering a session that ended, disconnected or was never started fails with a clear message, leaves the session state unchanged and keeps the message in the composer. Verify: gate tests. **Result: Met.**
  4. Substrate records each steering action (the existing `events_emit` note with `payload.action` `steer`); delivery is never attributed to Substrate. Verify: fake-gateway test asserting the note payload and that the delivery outcome comes from Autonogrammer. **Result: Met (fake gateway; no real Substrate server).**
  5. The gate is green. Verify: `./bootstrap.sh`. **Result: Met (gate-equivalent run, 5409 passed at the earlier tree; whole-solution test runs at the final tree passed, F10; `./bootstrap.sh` itself not re-run for the record).**
**Plans**: `49-01-PLAN.md` (units built), `49-SUMMARY.md`, `49-VERIFICATION.md` in `.planning/phases/49-live-steering/`

### Phase 50: Cursor Visual Parity
**Goal**: The application looks and feels like Cursor's Agents window, set in the Greptile typefaces, and keeps its accessibility contract: Dark by default, a styled control library, compact density, and layouts for every view that match the design specification.
**Requirements**: AL-21, AL-22, AL-23
**Depends on**: Phases 39, 40, 46, 48 (the views it restyles)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-27)**: complete. Criteria 1 to 7 met: the static faces and `TypographyMarkupTests`, Dark default with System, Light and Dark, the control library, 32 px targets with the 24 px floor and the hidden skip link, the layouts and the 14-image screenshot set (27 images in each of `after-dark/` and `after-light/`), the unchanged accessibility contract; criterion 7 as recorded whole-solution test runs, not as a `./bootstrap.sh` run. Designed from `docs/plans/ui-parity/cursor-agents-window-parity.md` (15 landing steps); the owner questions O1 to O8 are answered by recorded defaults (OQ-87 to OQ-91), not by the owner: O2 keeps the native window frame on every platform. Also landed under the 49-03 commit and recorded here: the omp slash command palette, the Changes bar (Keep, Undo, Review, Keep all, Undo all, Stale refusal, CRLF handling), and the editor TextMate deadlock fix. Not verified: a real desktop (Wayland native, Windows, macOS, Android), a model turn. Records: `.planning/phases/50-cursor-visual-parity/50-CONTEXT.md`, `50-01-PLAN.md`, `50-SUMMARY.md` and `50-VERIFICATION.md`.
**Success Criteria** (what must be TRUE):
  1. The Greptile faces are bundled as static instances (DM Sans regular, medium and semibold; Anybody semibold; Space Mono regular and bold) with their licences; Anybody is used only at 20 px and above and never thin. Verify: `FontResourceTests` and a typography markup test. **Result: Met (`FontResourceTests`, `FontFaceResolutionTests`, `TypographyMarkupTests`; six static TTFs with OFL notices).**
  2. The application starts in the Dark theme, Settings > General offers System, Light and Dark, the choice persists, and views that build colours in code re-read the tokens when it changes. Verify: theme preference tests and `ThemeContrastTests` green in both variants with every colour role of the specification. **Result: Met (`ThemePreferenceTests`, `ThemeContrastTests` in both variants, `FileSettingsStoreTests`).**
  3. Buttons, inputs, toggles, tabs, list rows, chips, scrollbars, tooltips and menus are styled from Autonogrammer tokens through the Fluent resource aliases; no control shows unthemed default chrome. Verify: control style tests in both variants and the screenshot set. **Result: Met (`ControlStyleTests`, `ButtonLabelColourTests`, `ColourLiteralTests`; the screenshot set; popup corner wedges on Xvfb are not verified on a real desktop).**
  4. Desktop controls use 32 px targets (Android keeps its larger target value) and every interactive control is at least 24 by 24 px (WCAG 2.2 AA, 2.5.8); the skip link is hidden until it has focus and stays first in the tab order. Verify: density and skip link tests. **Result: Met (`DensityTests`, `SettingsAccessibilityTests`, `SkipLinkVisibilityTests`; Android keeps 44).**
  5. The rail, thread header and plugin connection row, transcript blocks, composer with the model chip, omp terminal, Kanban tab and Settings window follow section 4 of the specification. Verify: screenshots 01 to 05 at 1280 by 800 in Dark and Light and at 960 by 640, recorded in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` and `after-light/`. **Result: Met (27 images in each of `after-dark/` and `after-light/`, judged by eye; known defects are listed in the evidence README: the Kanban fifth column is cut at the scroll edge, omp output is pale in Light).**
  6. The accessibility contract is unchanged: x:Name values, accessible names, tab order, the 2 px focus ring, reduced motion and the keyboard map. Verify: `KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, `PanelMarkupTests` green. **Result: Met (`KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, `PanelMarkupTests` in the recorded runs).**
  7. The gate is green. Verify: `./bootstrap.sh`. **Result: Met (recorded whole-solution test runs, F10; `./bootstrap.sh`, `dotnet format` and the web smoke are not recorded for the final tree).**
**Plans**: `50-01-PLAN.md` (units built), `50-SUMMARY.md`, `50-VERIFICATION.md` in `.planning/phases/50-cursor-visual-parity/`

## v0.15.0 Cursor completeness, brand and public launch (released 2026-10-28 as v0.15.0)

Source: user request 2026-10-27 (show the finished Web Application; a GitHub presentation with a logo and banner; up-to-date documentation and a GitBook site; AGPL-3.0; the standard repository documents; every Cursor feature including hooks, cloud programming on Railway, remote environments, multi-repo programming, all slash commands picked up in the Remote and Desktop environments and Cursor-equivalent Settings; all EU, US and CN providers and coding plans; White and Purple in the Greptile typefaces) and the ultrathink plan graph ut-mvr8q2x6-c0fc2bc6 (spec `/root/.omp/agent/ultrathink/sessions/01a16494-919f-77b4-bd0c-d594d2a3137c.xml`, which restates the standing authority to deploy to Railway). The research behind every phase is in `docs/plans/v0.15.0-research/` (12 files, researched 2026-10-27 against Cursor 3.27 and omp 18.12.6).

Settled decisions (each has an open-question row with the default taken):
- SD-1 Brand. White and Purple is the default look: the default theme becomes Light (white surfaces, purple `#6d28d9`), Dark and System stay available. Supersedes the Dark default of Phase 50 (OQ-94). Type stays DM Sans, Anybody (20 px and above) and Space Mono.
- SD-2 Licence. "AGL 3.0" is read as the GNU Affero General Public License v3.0, recorded as `AGPL-3.0-or-later`, the licence of the ultrathink submodule (OQ-93, closes OQ-62). The owner may choose `-only`.
- SD-3 Web head. The Blazor WebAssembly head on the shared presentation layer is the web head; Avalonia.Browser stays failed on 12.1.4 (ADR-0004). It is shown two ways: a real local run (API and web, headless Chrome screenshots) and a labelled Demo build with a simulated engine on Vercel (OQ-99). A real hosted API stays blocked on the sign-in authority (OD-03).
- SD-4 Cursor scope. The inventory is Cursor's documentation as of 2026-10-27 (release 3.27, 92 slash tokens). Features that need Cursor's own accounts, billing or inference are OMITTED-BY-DECISION with the reason; features Cursor has retired (inline edit as a separate flow, Memories) are not built (OQ-95). Everything else is built, wired or listed as PARTIAL with the missing piece.
- SD-5 Hooks. Cursor-compatible `hooks.json` (21 events), executed by Autonogrammer; events that block are enforced by an Autonogrammer-owned omp extension because ACP alone cannot stop a tool call. ADR-0013 decision 4 (no shell, closed stdin) is amended (OQ-96).
- SD-6 Remote and cloud. A remote environment runs `omp acp` over `ssh` stdio; a cloud agent runs it in an Autonogrammer worker behind a WebSocket bridge. This answers OQ-75 and Q-HARN-WS by design (OQ-97, ADR-0015).
- SD-7 Providers. Every provider and plan in the research is a catalogue template with its base URL, authentication, API style, region and residency. Coding-plan presets are bring-your-own-key with the vendor's rule text beside the key field, and cloud agents default to pay-as-you-go keys (OQ-98).
- SD-8 Deploys. The request restates the authority to deploy to Railway. Deploys are staged, health-checked and private until the sign-in authority exists; every deploy records its version and state (OQ-100). Anything that Railway's acceptable-use text may forbid stays paused (OQ-73).
- SD-9 GitBook. The documentation lives in the repository in GitBook's Git Sync layout; connecting the space is an account action (OQ-101).

Build status (2026-10-28, updated after the release): the code of Phases 51 to 65 is released as v0.15.0. The full `./bootstrap.sh` gate exited 0 on a clean worktree of `e24e6851` (12215 tests, 12213 succeeded, 0 failed, 2 skipped, 25 minutes; format clean; web smoke with axe: 68 runs, 0 violations; `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`); `origin/main` was fast-forwarded from `04e80d1` to `9d4e0075`, the annotated tag `v0.15.0` points at it, and the `release` workflow published https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0; the GitHub settings were applied and read back (`docs/runbooks/evidence/github-setup-2026-10-28/`). Five phases are complete (52, 53, 58, 64, 65; 52, 58, 64 and 65 with caveats) and ten stay Partial (51, 54, 55, 56, 57, 59, 60, 61, 62, 63: built code that is not wired, a missing page, view or test, or a missing record, and in Phase 60 criterion 4 Partial: after the release a staged private worker was deployed to Railway and handshaken, the API is not deployed). The GitHub `ci` workflow failed for both release pushes at 'Start test services' (rootless podman in the runner container, OQ-168), so the local gate is the gate of record. No criterion needing a live model turn (OD-07), Windows, macOS, Android, a Railway deploy of the API or of a cloud agent, the sign-in authority (OD-03) or a GitBook publish is claimed; the staged private worker of 2026-10-28 (health check and `initialize`, `session/new` handshake, no model turn) is recorded in `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`. Records: `.planning/phases/51-*` to `65-*`; the final report with the verified, unverified and owner lists is `65-SUMMARY.md`.

- [ ] **Phase 51: Brand, Theme and Licence** - the Light default, the logo set, AGPL-3.0-or-later with notices and the section 13 source offer Partial 2026-10-28: criteria 1 to 3 met (brand and licence checks now in the gate and passed on e24e6851); the web head has no About page (criterion 4).
- [x] **Phase 52: Repository, Release and Community Health** - GitHub presentation, community files, CHANGELOG, release workflow, tag and release Complete 2026-10-28 with caveats: settings applied and read back (licence detected AGPL-3.0, community health 100), tag v0.15.0 and the Release exist; owner steps remain (social preview upload, discussion categories); `files.issue_template` is null in the profile JSON; older milestone tags do not exist.
- [x] **Phase 53: Web Head Foundation and API Surface** - styled shell and rail in White and Purple, self-hosted fonts, the client abstraction, and the API endpoints the screens need Complete 2026-10-28 (named tests and the web smoke inside the `./bootstrap.sh` gate that passed on e24e6851).
- [ ] **Phase 54: Web Agent Panel and Workspace Screens** - thread, composer, approvals, changes, Kanban, Settings, terminal, cloud and remote views, smoke v2 Partial 2026-10-28: no Cloud or Remote view in the web workbench (`ViewTabs` offers Agents and Kanban); the smoke walks one of 18 Settings pages; tests and smoke passed in the gate on e24e6851.
- [ ] **Phase 55: Showing the Web Application** - Demo build, hosted preview, local run, screenshots in both themes Partial 2026-10-28: the Demo is hosted at https://ompos-web.vercel.app (project `ompos-web`, deployment `ompos-px32dymvw-swcstudiospace.vercel.app`, live capture and evidence README committed); the source commit is not recorded and Dark 1024 and Dark 390 screenshots are not committed.
- [ ] **Phase 56: Slash Commands and Convention Discovery** - all 92 Cursor tokens plus the open-ended forms, automatic pickup of commands, skills, rules, subagents and MCP in desktop, remote and cloud sessions Partial 2026-10-28: 92 tokens resolve; no cloud-session discovery source exists; the web palette still passes `SlashDiscovery.Empty` and lists no discovered rows.
- [ ] **Phase 57: Hooks** - Cursor-compatible hooks run in every environment Partial 2026-10-28: hooks built and wired in the desktop head; no test makes a fake omp process load `plugins/autonogrammer-hooks-omp` and enforcement inside a real omp is UNVERIFIED (OD-07); remote sessions run no hook files; cloud hooks are off by default.
- [x] **Phase 58: Remote Environments over SSH** - profiles, transport, preflight, reconnect, remote files and terminal Complete 2026-10-28 with caveats: criteria 1 to 3 met by recorded test runs, the local gate of record and one live ssh run (session opened, no prompt turn); PX-15 stays Partial.
- [ ] **Phase 59: Multi-repo Workspaces** - several repositories in one session Partial 2026-10-28: library, picker and `--add-dir` launch met and in the passing gate of record (e24e6851, Worktrees tests included); `MultiRootWorkspaceTree` is bound by no head, `WorktreeRunSet.CreateAsync` is called by no session start, and the head-side convention merge is not wired.
- [ ] **Phase 60: Cloud Agents on Railway** - worker bridge, control plane, Railway client, secrets, pull requests, heads, staged deploy Partial 2026-10-28: worker bridge (now also live-checked as a podman image, 14 of 14), control plane, Railway client and desktop page built; no web list or form; criterion 4 Partial (staged private worker deployed and handshaken on Railway 2026-10-28; the API is not deployed: OD-03, OQ-71, OQ-73, OQ-100, OQ-171).
- [ ] **Phase 61: Settings Parity** - Cursor's configuration surface in both heads Partial 2026-10-28: 50 rows each have a status (6 Built, 2 Wired, 20 Partial, 22 Omitted; the parity check now runs in the gate); Labs page missing in both heads; Worktrees and Ignore have no web page.
- [ ] **Phase 62: Providers, EU US CN and Coding Plans** - the full catalogue in Settings Partial 2026-10-28: catalogue of 103 rows and Models page met and in the passing gate of record (e24e6851); omp validation of the C# writer is not a committed record (UNVERIFIED); no live key check (OQ-71).
- [ ] **Phase 63: Cursor Feature Completion** - the remaining agent, chat, context and safety features Partial 2026-10-28: criterion 2 met with caveats (every feature wired or listed Partial, tests in the passing gate of e24e6851); criterion 1 Partial: pull request, browser, terminal-output and side-chat mentions are not composed in any head (grep); nothing observed with a live omp (OD-07).
- [x] **Phase 64: Documentation and GitBook** - up-to-date guides, GitBook layout, link gate, parity audit refresh Complete 2026-10-28 with caveats: criteria 1 and 3 met (`check-docs.sh` 0 errors with and without `AUTONOGRAMMER_RELEASE=1`, 141 files, 73 in `SUMMARY.md`, in the gate through `repo-checks.sh`; audit of 494 rows re-graded at tree `7535859`); criterion 2 met with the owner's steps in the runbook, the GitBook space is not created (owner, OQ-101); PX-21 and PX-22 met.
- [x] **Phase 65: Integration and Release v0.15.0** - clean-tree gate, fast-forward push, tag, GitHub release, deploy records Complete 2026-10-28 with caveats: `./bootstrap.sh` exited 0 on a clean worktree of `e24e6851` (12215 tests, 0 failed, 2 skipped); `main` fast-forwarded to `9d4e0075`; tag `v0.15.0` and the GitHub Release exist; the `ci` workflow on GitHub failed at 'Start test services' for both pushes (OQ-168), so the local gate is the gate of record.

### Phase 51: Brand, Theme and Licence
**Goal**: Autonogrammer looks like one product in White and Purple with a real logo, and the repository carries a correct AGPL-3.0-or-later licence with notices and the network source offer.
**Requirements**: PX-01, PX-02, PX-03, PX-04
**Depends on**: Phase 50
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: criteria 1 to 3 Met (the `./bootstrap.sh` gate passed on e24e6851 and runs `check-brand.sh` and `check-licences.sh`; notices regenerated; LICENSE not compared with gnu.org, UNVERIFIED); criterion 4 Partial: the web head has no About page and its footer links the repository root.
**Success Criteria** (what must be TRUE):
  1. A new installation starts in the Light theme in the desktop head, and the web head starts Light; Dark and System remain choices and persist. Verify: theme preference tests (default Light), `ThemeContrastTests` in both variants, screenshots. **Result: Met (`DefaultThemeTests`, `ThemePreferenceTests`, `FileSettingsStoreTests`, `ThemeSwitchTests`, `ThemeContrastTests` in Light and Dark, each inside a passing project run and re-run by the `./bootstrap.sh` gate on e24e6851, recorded 2026-10-28; desktop and web Light screenshots under `docs/assets/screens/` and `docs/runbooks/evidence/web-app-2026-10-28/`).**
  2. The brand set exists under `assets/brand/` with its build script: mark, wordmark lockup, banner 1280 by 320, social preview 1280 by 640 below 1 MB, favicons, desktop and Android icons; text is outlined from the bundled Greptile faces. Verify: a script test of file sizes and dimensions; the images open and look right (reviewed by eye). **Result: Met (`assets/brand/` set built by `scripts/brand/build-brand.sh`; `bash scripts/ci/check-brand.sh` run 2026-10-28: ok, 28 PNGs, 9 SVGs, 18 other files, and run in the gate through `scripts/ci/repo-checks.sh` on e24e6851; banner 1280x320, social preview 1280x640 and 51609 bytes read from the files; a shell check, not a C# test; images reviewed by the builder, not an owner; `build-brand.sh` not re-run, UNVERIFIED).**
  3. `LICENSE` holds the canonical AGPL-3.0 text, `NOTICE` and `THIRD-PARTY-NOTICES.md` cover every shipped package, font and bundled asset, package metadata declares the SPDX expression, and the README states the licence. Verify: a licence check script run in the gate against the lock files. **Result: Met (`LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.md` (regenerated, 035dcf81), `REUSE.toml`, SPDX in `Directory.Build.props`, README licence section exist; `scripts/ci/check-licences.sh` is called by `bootstrap.sh` and `ci.yml` and passed in the gate on e24e6851, recorded 2026-10-28; `sha256sum LICENSE` equals the constant `0d96a4ff...0abcb0` that the script compares; the comparison with gnu.org could not be made, UNVERIFIED: `curl -fsSL https://www.gnu.org/licenses/agpl-3.0.txt | sha256sum`; GitHub detected the licence as AGPL-3.0, key `agpl-3.0`, in `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`).**
  4. The API answers `GET /source` anonymously with the licence, repository URL and commit; the web head footer and About page and the desktop About link to the source of the running revision. Verify: API integration test, bUnit test, headless view test. **Result: Partial (`GET /source` anonymous, `SourceEndpointTests`; desktop About links the revision, `AboutViewTests`; web footer shows the version and revision from `/source` and links the repository root, `ShellFooterTests`; all inside passing runs re-run by the gate on e24e6851; no web About page (no `/about` route in `apps/Autonogrammer.Web` or `Autonogrammer.UI`) and no revision link on the web).**
**Plans**: `51-01-PLAN.md` (units built), `51-SUMMARY.md`, `51-VERIFICATION.md` in `.planning/phases/51-brand-theme-and-licence/`

### Phase 52: Repository, Release and Community Health
**Goal**: The GitHub repository presents the product properly and follows the community standards.
**Requirements**: PX-05, PX-06, PX-07
**Depends on**: Phase 51 (brand files, licence)
**Mac channel**: No.
**UI hint**: no
**Status (2026-10-28)**: Complete with caveats: read-back saved (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`): licence `agpl-3.0`, community health 100, private reporting and security fixes enabled; tag v0.15.0 (9d4e0075) and the GitHub Release exist (`release` run 40758275112 succeeded). Caveats: `files.issue_template` is null while health is 100; social preview upload and discussion categories are owner steps; no test parses the `.github` YAML; older milestone tags v0.9.1 to v0.14.0 do not exist (compare links 404); GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at 'Start test services' (OQ-168), the local gate is the gate of record.
**Success Criteria** (what must be TRUE):
  1. The repository has a description, homepage, topics, a licence that GitHub detects, security reporting, secret scanning and Dependabot security updates, applied with `gh` and recorded; the social preview is prepared with the manual upload step. Verify: `gh api` read-back saved under `docs/runbooks/evidence/`. **Result: Met with a caveat (`scripts/github/apply-repo-settings.sh --apply` ran 2026-10-28: description, homepage = https://ompos-web.vercel.app, 15 topics, discussions, wiki off, delete-branch-on-merge, private reporting, secret scanning and push protection, Dependabot alerts and security updates, 22 labels; read-back saved in `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json` (19:53 UTC): licence key `agpl-3.0`, private vulnerability reporting enabled, automated security fixes enabled, 26 labels, no rulesets and no branch protection on main on purpose; social preview image `assets/brand/autonogrammer-social-preview.png` ready, the upload is an owner step, GitHub has no API for it).**
  2. CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, SUPPORT, CODEOWNERS, issue forms, the pull request template, `dependabot.yml`, release notes configuration and labels exist and pass the community profile check. Verify: `gh api repos/.../community/profile` health percentage and a YAML validity test. **Result: Met with caveats (`CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `.github/CODEOWNERS`, four issue form files, PR template, `dependabot.yml`, `release.yml`, `labels.yml` (22) exist; community profile `health_percentage` 100 in the read-back (readme, licence, contributing, code of conduct, PR template present) but `files.issue_template` is null in the same JSON; YAML validity: no test in the suite parses the `.github` YAML, the evidence is a one-off PyYAML parse of nine files (2026-10-28) and GitHub accepting them; discussion categories are an owner step).**
  3. `CHANGELOG.md` follows Keep a Changelog from v0.9.1 to v0.15.0, `Directory.Build.props` holds one version, and release v0.15.0 exists as a tag and GitHub Release with notes. Verify: the changelog check script and `gh release view`. **Result: Met with a caveat (`CHANGELOG.md` has 8 sections 0.15.0 to 0.9.1; `check-changelog.sh` valid and run in the gate through `repo-checks.sh`; `Directory.Build.props` holds `VersionPrefix` 0.15.0; annotated tag v0.15.0 at 9d4e0075 pushed 19:53 UTC; GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 published by `release` workflow run 40758275112 (31 s, success), notes = the CHANGELOG [0.15.0] section; the older milestone tags v0.9.1 to v0.14.0 do not exist so their compare links 404; the `ci` workflow runs 40758215402 and 40758275399 on GitHub failed at 'Start test services', OQ-168).**
**Plans**: `52-01-PLAN.md` (units built), `52-SUMMARY.md`, `52-VERIFICATION.md` in `.planning/phases/52-repository-release-and-community-health/`

### Phase 53: Web Head Foundation and API Surface
**Goal**: The web head is styled in White and Purple with the Greptile typefaces on a shell that mirrors the desktop, and the API exposes what the screens need.
**Requirements**: PX-08, PX-10
**Depends on**: Phase 51 (brand and tokens)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Complete: criteria 1 to 3 Met; the `./bootstrap.sh` gate passed on e24e6851 (12215 total, 12213 succeeded, 0 failed, 2 skipped; web smoke API half and Demo half, 68 axe runs, 0 violations). GitHub `ci` runs failed at 'Start test services' (OQ-168); the local gate is the gate of record.
**Success Criteria** (what must be TRUE):
  1. Bootstrap and the template CSS are gone; tokens, fonts (self-hosted woff2 with the OFL texts), reduced motion and the theme boot script exist; the loading ring, error bar and skip link keep their contract. Verify: CSS contract tests, `AccessibilityTests`, the existing web smoke. **Result: Met (`CssContractTests`, `AccessibilityTests`, `IndexHtmlTests` inside the passing UI and Web runs, re-run by the gate on e24e6851; `wwwroot/lib/bootstrap` deleted; `css/app.css` kept for `WebShellAccessibilityTests`; 68 smoke axe runs, 0 violations, in the gate's Demo half, recorded 2026-10-28).**
  2. The shell has a top bar, an agent rail (groups, status, New, Close), a thread region and a responsive drawer below 840 px. Verify: bUnit landmark and drawer-focus tests; screenshots at three widths. **Result: Met (`AppShellTests`, `AgentRailTests`, `ShellRegionTests`, `ShellLayoutTests`, re-run by the gate on e24e6851; the gate smoke asserts that the drawer traps focus and closes on Escape; screenshots `L1440-01-agents.png`, `L1024-01-agents.png`, `L390-01-agents.png`, `L390-02-drawer.png` in `docs/runbooks/evidence/web-app-2026-10-28/`).**
  3. The API serves the catalog, stages, context, steering, config options, slash commands, changes, board, lifecycle and workspace endpoints, owner-scoped, rate-limited and in OpenAPI 3.1. Verify: integration tests per endpoint (404 for other owners, 409, 413, 415) and a contract test against `docs/api/openapi.json`. **Result: Met (OpenAPI 3.1.1, 60 paths; endpoint tests incl. 404 other owner, 409, 413, 415 inside the passing `Api.IntegrationTests` run, 1027/1027, and the gate on e24e6851; `OpenApiCoverageTests`, `WorkspaceSettingsOpenApiTests`; limits: one engine session per prompt so config options, commands and live steering exist only during a run; closed flag, change decisions and stages are in memory).**
**Plans**: `53-01-PLAN.md` (units built), `53-SUMMARY.md`, `53-VERIFICATION.md` in `.planning/phases/53-web-head-foundation-and-api-surface/`

### Phase 54: Web Agent Panel and Workspace Screens
**Goal**: The web head offers the same working surfaces as the desktop Agent Panel.
**Requirements**: PX-09
**Depends on**: Phase 53
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: criterion 2 Met; criterion 1 lacks Cloud and Remote views in the web workbench (Settings information pages only; terminal Unavailable against the real API); criterion 3: the smoke ran in the `./bootstrap.sh` gate on e24e6851 (68 axe runs, 0 violations) but opens one of 18 Settings pages.
**Success Criteria** (what must be TRUE):
  1. The thread header with the plugin connection row, the twelve block views, the composer with model, mode and thinking chips and the slash palette, steering, tool approval decisions, the Changes bar with Keep, Undo and Review, the Kanban tab, the omp terminal view, and Cloud and Remote views render from the shared presentation models. Verify: bUnit tests per component and state. **Result: Partial (thread header, plugin connection row in `PipelineStrip`, blocks, composer chips and slash palette, steering, approvals, Changes bar, Kanban and terminal view have components and tests inside the passing UI run, re-run by the gate on e24e6851: `ThreadViewTests`, `ComposerPanelTests`, `ToolApprovalTests`, `ChangesBarTests`, `KanbanViewTests`, `TerminalViewTests`; missing: Cloud and Remote views in the web workbench, they exist only as Settings information pages; the terminal is Unavailable against the real API, scripted in the Demo).**
  2. Settings in the web head covers General, Runtime, Agents, Models and providers, Tab, Rules, Skills, Commands, Hooks, Plugins, Network and Keyboard, and no page ever renders a stored key. Verify: bUnit tests and a test that no key is rendered back or logged. **Result: Met (`WebSettingsOrder` registers 18 pages including all twelve named; `SettingsModelsPageTests`, `SettingsPagesRenderTests`, `DemoProvidersRouteTests` and `ProvidersCredentialEndpointTests` show the key is write-only, never echoed or logged, all inside passing runs re-run by the gate on e24e6851; Agents, Run mode, Workspaces, Remote, Cloud and Privacy are information pages, Tab is "not available in the web head").**
  3. Smoke v2 drives every screen in both themes at three widths with axe clean. Verify: `scripts/web-smoke/run-smoke.sh` in the gate. **Result: Partial (`scripts/web-smoke/smoke-demo.mjs` ran 68 axe runs in light and dark at 1440, 1024 and 390 with 0 violations; `run-smoke.sh` is called by `bootstrap.sh` line 81 and `.github/workflows/ci.yml` line 96 and passed in the gate on e24e6851 (API half and Demo half, recorded 2026-10-28); the smoke still opens one Settings page of 18, and there is no Cloud or Remote screen to walk).**
**Plans**: `54-01-PLAN.md` (units built), `54-SUMMARY.md`, `54-VERIFICATION.md` in `.planning/phases/54-web-agent-panel-and-workspace-screens/`

### Phase 55: Showing the Web Application
**Goal**: The owner can see the web application: running locally with real screens and on a hosted preview in a labelled Demo build.
**Requirements**: PX-11
**Depends on**: Phase 54
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: the Demo is hosted at https://ompos-web.vercel.app with project and deployment id recorded in `docs/runbooks/web-demo.md`, a live capture and an evidence README are committed; the source commit is not recorded and the evidence folder holds 16 of the matrix images (Dark 1024 and Dark 390 absent).
**Success Criteria** (what must be TRUE):
  1. A Demo build replaces the HTTP handler with an in-browser simulated API that answers the real routes, shows a permanent Demo banner, needs no network and keeps `connect-src 'self'`. Verify: contract test that every fixture validates against `docs/api/openapi.json`; `RenderCspScriptTests`. **Result: Met (`DemoApiHandlerTests`, `DemoFixturesContractTests`, `OpenApiCoverageTests`, `DemoExclusionTests`, `RenderCspScriptTests` inside the passing Web run and in the local gate of record (`./bootstrap.sh` on e24e6851, exit 0, 12215 tests, 0 failed, 2 skipped, recorded 2026-10-28); permanent `DemoBanner`, `connect-src 'self'`; fixtures are checked against the contract types and routes against the OpenAPI paths, not against the JSON schemas).**
  2. A hosted preview of the Demo build is reachable and recorded with its URL, commit and build id. Verify: `curl` of the URL and a headless Chrome screenshot of the hosted page. **Result: Partial (hosted at https://ompos-web.vercel.app, Vercel project `ompos-web` (`prj_5clbARYWIOrfgkMSK2Fvw2p0iXAc`), deployment `ompos-px32dymvw-swcstudiospace.vercel.app` recorded in `docs/runbooks/web-demo.md`; headless Chrome capture of the live URL committed as `live-vercel-L1440-01-agents.png` and `live-vercel-report.json` (`ready: true`, `errors: []`, `failedRequests: 0`); the commit the bundle was built from is not recorded and no curl output is committed, UNVERIFIED: `vercel inspect ompos-px32dymvw-swcstudiospace.vercel.app`).**
  3. A runbook shows how to run the web application locally against the real API, and screenshots of every screen in both themes at 1440, 1024 and 390 px are committed under `docs/runbooks/evidence/web-app-2026-10-27/`. Verify: the evidence README names the method and what is simulated. **Result: Partial (`docs/runbooks/web-demo.md` exists and its status line names the hosted Demo; `docs/runbooks/evidence/web-app-2026-10-28/README.md` exists and names the method and what is simulated; the folder holds 16 images: Light 1440 8, Dark 1440 3, Light 1024 1, Dark 1024 0, Light 390 2, Dark 390 0, plus two Models views, and the live capture; the matrix of every screen in both themes at three widths is not committed: the 68-run smoke images are not committed and Dark 1024 and Dark 390 are absent; the folder date is 2026-10-28, not 10-27).**
**Plans**: `55-01-PLAN.md` (units built), `55-SUMMARY.md`, `55-VERIFICATION.md` in `.planning/phases/55-showing-the-web-application/`

### Phase 56: Slash Commands and Convention Discovery
**Goal**: Every Cursor slash command resolves in the palette and the conventions of the workspace are picked up automatically in desktop, remote and cloud sessions.
**Requirements**: PX-12, PX-13
**Depends on**: Phase 53 (web palette), Phase 50
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: 92 tokens resolve; no cloud-session discovery source exists in the tree; `WebSessionControls` still passes `SlashDiscovery.Empty` to the web palette (re-checked by grep 2026-10-28).
**Success Criteria** (what must be TRUE):
  1. The catalogue holds all 92 documented tokens and aliases, each with a handler kind (native action, omp-delegated, skill, cloud hand-off, or unavailable with a stated reason), and the open-ended forms resolve: `/skill-name`, custom markdown commands, `/subagent-name` and the `&` prefix. Verify: a test that every token in `docs/reference/slash-commands.md` has a handler, resolver tests per kind. **Result: Met (`SlashCommandCatalogueTests`, `SlashCommandResolverTests`, `SlashCommandDispatcherTests`, `SlashCommandArgumentsTests`, `SlashPromptComposerTests`, inside the passing Presentation run of 2303 and in the local gate of record (`./bootstrap.sh` on e24e6851, exit 0, 12215 tests, 0 failed, 2 skipped, recorded 2026-10-28); the table of `docs/reference/slash-commands.md` has 93 rows = 92 Cursor tokens plus `/update-settings`, 47 NativeAction, 11 OmpDelegated, 10 Skill, 1 CloudHandoff, 24 Unavailable; statuses 36 Built, 24 Partial, 9 Delegated, 24 Unavailable).**
  2. Commands, skills, rules and subagents are discovered from `.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex` and `.agents` at project and user scope with precedence and diagnostics, in desktop sessions, remote sessions and cloud sessions. Verify: discovery tests over fixture trees for each source and each environment kind. **Result: Partial (desktop: `ConventionCatalogTests`, `ConventionCatalogSourceTests`, `LocalConventionFileSourceTests`, `ConventionMappingTests`; remote: `RemoteConventionFileSourceTests` and `RemoteSlashDiscoverySource` wired in `SessionEngineSet`, no test of the Native source and not exercised by the live ssh run; cloud: no discovery source exists in the tree (the implementers of `ISlashDiscoverySource` are `ConventionSlashDiscoverySource`, `ConventionsCommandsSource`, `RemoteSlashDiscoverySource` and the web `WorkspaceSlashDiscoverySource`); the heads read the primary root only).**
  3. The palette in both heads lists engine commands (from omp), Autonogrammer commands, discovered commands and skills with their source. Verify: palette model tests, bUnit and headless tests. **Result: Partial (desktop palette: `SlashPaletteModelTests`, `SlashPaletteDiscoveryTests`, `ComposerSlashPaletteViewTests`, `SlashPaletteBindingTests`; web palette lists the catalogue and the engine commands from `WebSessionControls` but still passes `SlashDiscovery.Empty` (`apps/Autonogrammer.Web/Services/WebSessionControls.cs`), so no discovered command, skill or subagent; the web `WorkspaceSlashDiscoverySource` feeds only the Settings Commands page).**
**Plans**: `56-01-PLAN.md` (units built), `56-SUMMARY.md`, `56-VERIFICATION.md` in `.planning/phases/56-slash-commands-and-convention-discovery/`

### Phase 57: Hooks
**Goal**: Cursor-compatible hooks run in desktop, remote and cloud sessions, with consent, a log and a settings page.
**Requirements**: PX-14
**Depends on**: Phase 53 (hosted event), ADR-0013
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: hooks built and wired in the desktop head; enforcement inside a real omp is UNVERIFIED (OD-07) and no test makes a fake omp process load the extension (grep re-check 2026-10-28); remote sessions run no hook files; cloud hooks are off by default.
**Success Criteria** (what must be TRUE):
  1. `hooks.json` files (managed, project, user, Claude settings, plugins) parse with the 21 Cursor events, command and prompt hook types, matchers, timeouts, `failClosed`, `loop_limit` and exit code 2 as deny; invalid files are diagnosed, not fatal. Verify: parser and runner tests over fixtures. **Result: Met (`HookEventsTests` 21 events, `HooksConfigParserTests`, `HooksConfigLoaderTests`, `HookSourceLocatorTests`, `HookOutputParserTests`, `HookDispatcherFailurePolicyTests`, `HookDispatcherLoopLimitTests`, `HookDispatcherPromptTests`, `SystemHookCommandRunnerTests`, inside the passing Plugins run of 429 and in the local gate of record (`./bootstrap.sh` on e24e6851, exit 0, 12215 tests, 0 failed, 2 skipped, recorded 2026-10-28); invalid files are diagnostics).**
  2. Blocking events (`preToolUse`, `beforeShellExecution`, `beforeMCPExecution`, `beforeReadFile`, `beforeSubmitPrompt`, `subagentStart`) are enforced inside omp by the Autonogrammer extension, and the observe-only events come from ACP traffic. Verify: node tests of the extension and an integration test with a fake omp that a denying hook stops a tool call. **Result: Partial (extension: `plugins/autonogrammer-hooks-omp/test/bridge.test.mjs` and `e2e.test.mjs`, 76 tests, also run by the gate's plugin node tests (69 + 76, exit 0, recorded 2026-10-28); ACP tap: `AcpHookTapTests`, `AcpHookGateTests`; `HookExtensionEndToEndTests` runs the real extension under node against the real bridge and shows a block until consent; `CloudHooksWorkerTests.ADenyingProjectHookStopsAToolCallInTheCloud` uses a fake omp behind the worker WebSocket and the test posts to the bridge itself. No test makes a fake omp process load the extension and have a denying hook stop a tool call (grep of `tests/` 2026-10-28: only launch-plan string tests, a temp-directory rig and the node-run end-to-end test name the plugin); real omp enforcement is UNVERIFIED (OD-07).)**
  3. First use of a project hook asks for consent against a hash that does not depend on the absolute path; changed hooks are quarantined until re-approved. Verify: consent tests (hash v2). **Result: Met (`HookConsentIdentityTests`: same hash under different checkout directories, edits void consent, v1 readable beside v2; `HooksServiceTests`: consent before a guarding hook runs, an edit invalidates consent and keeps the approved version, a runner fault quarantines the source; inside the passing Plugins and AgentLoop runs and the gate of record).**
  4. The Hooks settings page and the per-run log show every hook, its source, its last result and its timing; the head wiring replaces the test-only `PluginHost` construction (closes OQ-84). Verify: view model and headless tests. **Result: Partial (`HooksSettingsPageTests`, `HookRunLogModelTests`, `HookConsentPromptTests`, `HooksPageViewTests`, `HooksServiceSourceTests`, `HookLogAndNoticeViewTests`; the desktop head constructs `HooksService` (`HeadServices.Hooks`, `SessionEngineSet.StartHooksAsync`) and the Hooks page; no unit test of the head wiring itself; OQ-84 is only partly closed: `PluginHost` and the v1 `HookRunner` are still constructed only in tests and the product path is `HooksService`).**
**Plans**: `57-01-PLAN.md` (units built), `57-SUMMARY.md`, `57-VERIFICATION.md` in `.planning/phases/57-hooks/`

### Phase 58: Remote Environments over SSH
**Goal**: A session can run on another machine reached over SSH, with its files and terminal.
**Requirements**: PX-15
**Depends on**: Phase 53
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Complete with caveats: criteria 1 to 3 met by recorded test runs, the local gate of record (e24e6851, exit 0) and one live ssh run (session opened, no prompt turn); PX-15 stays Partial.
**Success Criteria** (what must be TRUE):
  1. Profiles come from `ssh -G` and the user's ssh config, preflight diagnoses a missing ssh, host key, omp or directory with fix text, and no secret appears in an argument list. Verify: tests with a fake ssh executable and canary secrets. **Result: Met (`SshConfigResolverTests`, `RemotePreflightTests`, `RemoteDiagnosisCatalogTests`, `CanarySecretTests`, `PosixQuotingTests` inside the passing Remote run and in the local gate of record (`./bootstrap.sh` on e24e6851, exit 0, 12215 tests, 0 failed, 2 skipped, recorded 2026-10-28); the preflight minimum omp version 18.12.0 is a guess).**
  2. `SshAcpTransport` runs `omp acp` on the remote host, survives a login banner, reconnects with `session/load` and marks the interrupted turn. Verify: fake-ssh tests and a recorded run against a loopback `sshd` on this host. **Result: Met (`SshAcpTransportTests`, `ReconnectingEngineHostTests` inside the passing Remote run; `RemoteSshLiveTests.ARealOmpOpensASessionThroughARealSshd` against a loopback sshd and omp 18.12.6 in `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`; no prompt turn (OD-07) and no dropped connection against a real host).**
  3. The remote file tree and the remote terminal work over ssh; New Session offers the environment. Verify: tests with fake ssh and a headless view test. **Result: Met (`RemoteWorkspaceFileSystemTests`, `RemoteTerminalLauncherTests`, `NewSessionTargetModelTests`, `RemoteViewTests` inside the passing Remote, Presentation and Native runs and the gate of record; wired in `SessionEngineSet`; not run against a real host).**
**Plans**: `58-01-PLAN.md` (units built), `58-SUMMARY.md`, `58-VERIFICATION.md` in `.planning/phases/58-remote-environments-over-ssh/`

### Phase 59: Multi-repo Workspaces
**Goal**: One session works across several repositories.
**Requirements**: PX-16
**Depends on**: Phase 58 (remote roots), Phase 56 (convention merge)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: library, picker and `--add-dir` launch met; the multi-root explorer, per-run worktree set and head-side convention merge are not wired (re-checked by grep on the release tree 9d4e0075; `Autonogrammer.Harness.Worktrees.Tests` is in `Autonogrammer.Linux.slnf` and passed in the gate of record, e24e6851, exit 0).
**Success Criteria** (what must be TRUE):
  1. A workspace set of several roots (local and remote) is validated, launched through omp's additional-directory support, and shown in the explorer with one tree per root. Verify: launch argument tests, a recorded omp probe, explorer tests. **Result: Partial (`WorkspaceSetValidatorTests`, `WorkspaceSetStoreTests`, `MultiRootLaunchTests`, `MultiRootWorkspaceTreeTests`, `WorkspaceSetPickerViewTests`, recorded probe `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` (omp 18.12.6, four transcripts), inside the passing runs and the gate of record (`./bootstrap.sh` on e24e6851, exit 0, recorded 2026-10-28); the desktop starts a session on the picked roots with one `--add-dir` pair each; missing: no head binds `MultiRootWorkspaceTree` (declared only in `src/Autonogrammer.Presentation/Editor/MultiRootWorkspaceTree.cs`, so the explorer is single-root), the validator refuses roots in different environments so a mixed local and remote set is not accepted, and a remote set with `--add-dir` on the remote host is untested).**
  2. Worktrees are created per git root for a run and closed with it, and `.code-workspace` files import and export. Verify: tests with real git in temporary directories and a file round trip. **Result: Partial (`WorktreeRunSetTests` and `CursorWorktreesConfigTests` with real git in temporary repositories, `CodeWorkspaceFileTests` file round trip, picker import and export in `WorkspaceSetPickerViewTests`; `Autonogrammer.Harness.Worktrees.Tests` is in `Autonogrammer.Linux.slnf` and passed in the gate log of e24e6851; `WorktreeRunSet.CreateAsync` has no caller outside `Autonogrammer.Harness.Worktrees` (grep), so per-run worktrees are a library only).**
  3. Rules, skills and commands merge across roots with per-root diagnostics. Verify: merge precedence tests. **Result: Partial (`ConventionCatalogTests.AnEarlierWorkspaceRootWinsASharedName` and duplicate diagnostics inside the passing Workspace run and the gate of record; `SessionEngineSet.SlashDiscoveryFor` and `ConventionSettingsLoader` pass the primary root only, so no head shows a merge across roots).**
**Plans**: `59-01-PLAN.md` (units built), `59-SUMMARY.md`, `59-VERIFICATION.md` in `.planning/phases/59-multi-repo-workspaces/`

### Phase 60: Cloud Agents on Railway
**Goal**: An agent can be started in the cloud against one or more repositories, run on Railway workers, and return a pull request.
**Requirements**: PX-17
**Depends on**: Phase 58 (transport and materializer), Phase 57 (hooks in cloud)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: worker bridge, control plane, Railway client and desktop page built; the worker image ran under podman, 14 of 14 checks (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`); criterion 4 Partial (a staged private worker was deployed to Railway, health-checked and handshaken after the release, 2026-10-28, `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`; the API is not deployed: OD-03; OQ-71, OQ-73, OQ-100, OQ-171); no worker clone bootstrap, no web list or form.
**Success Criteria** (what must be TRUE):
  1. The worker bridges `omp acp` over an authenticated WebSocket and the API holds the cloud agent records durably, with caps, an idle sweeper and secret redaction. Verify: loopback tests with a fake omp, JWT tests (expired, wrong audience), canary tests that no secret reaches a transcript, a log or an argument list. **Result: Met (`WorkerAuthenticationTests`, `AcpBridgeProtocolTests`, `WorkerSecretHandlingTests` inside the passing AgentWorker run and the gate of record (e24e6851, exit 0); `CloudAgentLifecycleTests` caps and idle sweep, `CloudSecretRedactorTests` inside the passing Api.IntegrationTests run (1027/1027); `PostgresCloudAgentStoreTests` inside the passing Sessions.Postgres run; live: the worker image from `deploy/worker.Containerfile` ran under podman with a real `omp acp` 18.12.5, 14 of 14 checks (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`, recorded 2026-10-28): health and readiness 200, no token, wrong audience, expired and over-15-minute tokens 401, subject `../etc` 403, no upgrade 400, WebSocket opened, `initialize` answered by omp with `loadSession` true, `session/new` returned a session id, a second connection for one session refused 409, no key or token material in the log; the live check does not show a prompt turn (OD-07), a Railway deploy (OQ-73, OQ-100), a control plane minting the tokens (the script minted them), a clone, branch or pull request from the worker, hooks in the worker, or sleep and wake; secret store is in memory and the worker has no clone bootstrap).**
  2. The Railway client provisions, wakes and idles worker services through the verified GraphQL operations; the first live provisioning is recorded after schema introspection. Verify: tests over recorded fixtures; the live record under `docs/runbooks/evidence/`. **Result: Partial (`RailwayCloudEnvironmentClientTests`, `RailwayWakeTests`, `RailwayErrorMappingTests` over recorded fixtures inside the passing Sandbox run and the gate of record; no live provisioning and no schema introspection record under `docs/runbooks/evidence/`; the rows ADR-0020 D8 marks UNVERIFIED stay unverified; blocked by a Railway API token, OQ-100, OD-03).**
  3. Both heads list cloud agents, create one with repositories, a branch and secrets, and show the pull request. Verify: view model and bUnit tests. **Result: Partial (desktop Cloud agents page, form, pull request and hand-off: `CloudAgentsViewModelTests`, `NewCloudAgentModelTests`, `CloudAgentsPageViewTests`, `ApiCloudAgentsHostTests` inside the passing Presentation and Native runs and the gate of record; the web head page is information only, no list, form or bUnit test: a missing deliverable).**
  4. A staged Railway deployment of the worker and the API is made within the granted authority, health-checked and recorded with its version and state, or the exact blocker is recorded. Verify: Railway status read-back. **Result: Partial. Worker half Met (update 2026-10-28, after the release, `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`: project `ompos-staging`, environment `staging`, service `ompos-worker`, volume `ompos-worker-data`, private with no domain and no TCP proxy; deployment `b6e5ac9d-831c-49ea-b51c-78e59271de05` SUCCESS, built by Railway from the released tree with `deploy/worker.Containerfile`; Railway's `/healthz` check passed; inside the container `/healthz` 200, `/readyz` 200, `/acp` without a token 401, `omp` 18.12.5, and a JWT-gated WebSocket bridge let a real `omp acp` answer `initialize` and `session/new`; no model turn, no clone, no sleep and wake test; the refusal of 2026-10-21 did not recur). API half Blocked: the API is not deployed because production mode needs an identity provider (OD-03), the model credential is blocked by HOST-04 and OQ-71, a Railway API token and the UNVERIFIED GraphQL operations (ADR-0020 D8) remain, `ompos-api` was not created (`docs/runbooks/hosting.md`), OQ-73 is untouched (no remote desktop or streaming exists); the project `ompos-staging` stays up and bills usage (OQ-171). Earlier read-back of the same day, before the staged deploy: the project "Ultrathink" holds TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only, and it was not touched.)**
**Plans**: `60-01-PLAN.md` (units built), `60-SUMMARY.md`, `60-VERIFICATION.md` in `.planning/phases/60-cloud-agents-on-railway/`

### Phase 61: Settings Parity
**Goal**: The Settings of both heads cover Cursor's configuration surface.
**Requirements**: PX-18
**Depends on**: Phases 56 to 60 (their pages), Phase 53
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: 50 rows each have a status (6 Built, 2 Wired, 20 Partial, 22 Omitted, recounted from the file); the parity test and `scripts/check-settings-parity.sh` passed in the gate of record (e24e6851); no Labs page in either head; Worktrees and Ignore have no web page.
**Success Criteria** (what must be TRUE):
  1. Each of the 50 settings rows of the 2026-10-27 inventory is built, wired to omp, or listed as omitted with the reason, in `docs/settings-parity.md`. Verify: a test that every row id has a status. **Result: Partial (`SettingsParityTableTests` inside the Native run that passed in the gate of record (e24e6851, exit 0) asserts S01 to S50 once each with a status and note; counted from the file: 6 Built, 2 Wired, 20 Partial, 22 Omitted; 30 rows are Built, Wired or Omitted with a reason, 20 are Partial with the missing piece stated; `scripts/check-settings-parity.sh` now runs in `scripts/ci/repo-checks.sh` (`repo-checks: ok` in the gate log)).**
  2. The new pages (Hooks, MCP, Cloud agents, Remote, Workspaces, Privacy, Worktrees, Ignore, Run mode, Labs) exist in the desktop and the web head. Verify: view model tests and headless tests per page. **Result: Partial (desktop: Hooks, MCP, Cloud agents, Remote, Workspaces, Privacy, Worktrees, Ignore and Run mode exist with `*SettingsPageTests`, `McpSettingsViewModel*Tests` and `*PageViewTests` inside the Presentation and Native runs that passed in the gate of record; web: Hooks, MCP list and information pages for Run mode, Workspaces, Remote, Cloud agents and Privacy; re-checked by grep 2026-10-28: no Worktrees or Ignore page in the web, no Labs page in either head).**
**Plans**: `61-01-PLAN.md` (units built), `61-SUMMARY.md`, `61-VERIFICATION.md` in `.planning/phases/61-settings-parity/`

### Phase 62: Providers, EU US CN and Coding Plans
**Goal**: Settings lists every EU, US and CN provider and coding plan of the research, ready to take a key.
**Requirements**: PX-19
**Depends on**: Phase 61 shell (Models page), Phase 53 (API)
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: catalogue of 103 rows and Models page met (`Autonogrammer.Harness.Providers.Tests` and `Autonogrammer.Harness.OmpConfig.Tests` passed in the gate of record e24e6851); omp validation of the C# writer is not a committed record (UNVERIFIED: C# writer output through `omp models --json` with empty stderr); no live key check (OQ-71).
**Success Criteria** (what must be TRUE):
  1. The catalogue holds at least the EU, US, CN, aggregator and local rows of the research with base URL, authentication, API style, region, residency, omp id or `models.yml` template, docs and key links, and plan terms; retired or unconfirmed vendors (GitHub Models, Lambda, Replicate) are not offered. Verify: catalogue tests (unique ids, https, no placeholders, every row has a source URL). **Result: Met (103 rows counted in the tree: 50 verified, 49 partial, 4 unverified, 23 coding plans, 46 omp ids, 7 sign-in rows, 150 routes (the report said 151); `CatalogueRowsTests`, `CatalogueRowValidatorTests`, `CatalogueUrlTemplateTests`, `ProviderIdMapTests` in `Autonogrammer.Harness.Providers.Tests`, which passed (9.7 s) in the gate log of e24e6851; `scripts/ci/check-providers-doc.sh` runs in `repo-checks.sh` ("docs/reference/providers.md is up to date"); no cell-by-cell audit against the research).**
  2. The writer produces valid `models.yml` blocks for all ten omp API styles, validated by omp itself. Verify: writer tests and an `omp models` probe in a sandbox. **Result: Partial (writer for all 13 omp api values: `OmpManagedBlockWriterApiTests`, `OmpProviderApiNamesTests`, `OmpCatalogueBlockTests` in `Autonogrammer.Harness.OmpConfig.Tests`, which passed (55 s) in the gate log of e24e6851; omp 18.12.6 accepted a Python replica of the layout in a sandbox on 2026-10-27, recorded only as text in ADR-0019, no evidence file (re-checked 2026-10-28); UNVERIFIED: C# writer output through `omp models --json` with empty stderr).**
  3. The Models page filters by region and kind, shows coding-plan cards with their rule text, and never renders a key. Verify: view model tests and screenshots. **Result: Met (`ProviderDirectoryTests`, `ProviderListingCardTests`, `ModelsListingViewTests`, `ApiModelsSettingsSourceTests`, `ProvidersCredentialEndpointTests` inside the passing Presentation, Native, Web and Api.IntegrationTests runs and the gate of record; screenshots `docs/runbooks/evidence/web-app-2026-10-28/M-11-models-cn-coding-plans.png` and `M-12-models-eu.png`; no desktop screenshot of the listing).**
**Plans**: `62-01-PLAN.md` (units built), `62-SUMMARY.md`, `62-VERIFICATION.md` in `.planning/phases/62-providers-eu-us-cn-and-coding-plans/`

### Phase 63: Cursor Feature Completion
**Goal**: The agent, chat, context and safety features of the inventory that are not covered by the phases above are built or recorded.
**Requirements**: PX-20
**Depends on**: Phases 56 to 62
**Mac channel**: No.
**UI hint**: yes
**Status (2026-10-28)**: Partial: criterion 2 met with caveats (eight features wired and four listed Partial with the missing piece, audit re-graded at tree `7535859`, tests inside the passing `./bootstrap.sh` gate on e24e6851, 12215 tests, 0 failed); criterion 1 Partial because `SessionEngineSet` sets no terminal-output reader, pull request source or side-chat list and `MentionSourceFactory` omits the browser source (grep 2026-10-28); nothing observed with a live omp (OD-07).
**Success Criteria** (what must be TRUE):
  1. `@` mentions cover files, folders, terminals, chats, git diffs, browser, rules, skills and pull requests, sent to omp as embedded resources. Verify: mention resolver tests. **Result: Partial (`MentionParserTests`, `MentionSourceTests`, `MentionComposerModelTests`, `MentionSecretFilesTests`, `MentionPickerModelTests`, `ComposerMentionsTests`, `ComposerMentionViewTests`, `MentionSourceFactoryTests`, `AcpPromptContentTests`, all inside the passing gate of e24e6851: files, folders, chats, git diffs, rules and skills are wired and sent as ACP `resource` blocks; grep of the tree 2026-10-28 confirms the other kinds are built but not composed: `SessionEngineSet` sets no `ReadTerminalOutput`, `PullRequests` or `SideChats`, so terminal output is "not available in this window", pull requests resolve to "needs a configured Git host" (`UnavailablePullRequestSource`) and side-chat mentions list nothing; `BrowserMentionSource` is constructed by no head and `MentionSourceFactory` does not list it (no browser pane, ADR-0009); not observed with a live omp).**
  2. Run mode (Auto-review, Allowlist, Run Everything), `permissions.json`, `sandbox.json`, `.cursorignore`, `.cursor/mcp.json` import into the session's MCP servers, `.cursor/worktrees.json` with the worktree commands, file checkpoints with rewind, queue versus send now, conversation search and the context ring exist or are listed as PARTIAL with the missing piece. Verify: tests per feature and the refreshed audit. **Result: Met with caveats (`RunModePolicyTests`, `PermissionsFileTests`, `SandboxBoundaryTests`, `McpServerCatalogTests`, `CheckpointStoreTests`, `QueuedMessagesModelTests`, `ConversationSearchModelTests`, `ContextRingModelTests`, `WorktreeApplierTests`, inside the passing gate of e24e6851; run modes (Allowlist, Run Everything), `permissions.json`, `.cursor/mcp.json` import, worktree commands, checkpoints with rewind, queue and conversation search are wired in the desktop head; Auto-review has no hosted classifier, `sandbox.json` is a read boundary only (no OS sandbox, omp has none), `.cursorignore` does not bind omp's own tools and the context ring has no per-category counts, each listed Partial with its missing piece in `docs/quality/cursor-parity-audit-2026-10-28.md` (re-graded at tree `7535859`); caveats: omp's permission field names UNVERIFIED (OQ-84), `HeadAutomationSession` and the slot consent bridge have no test of their own, nothing observed with a live omp (OD-07)).**
**Plans**: `63-01-PLAN.md` (units built), `63-SUMMARY.md`, `63-VERIFICATION.md` in `.planning/phases/63-cursor-feature-completion/`

### Phase 64: Documentation and GitBook
**Goal**: The documentation is up to date, honest about status, and published in GitBook's layout.
**Requirements**: PX-21, PX-22
**Depends on**: Phases 51 to 63
**Mac channel**: No.
**UI hint**: no
**Status (2026-10-28)**: Complete with caveats: layout, guides, link gate and refreshed audit exist; `bash scripts/ci/check-docs.sh` and `AUTONOGRAMMER_RELEASE=1 bash scripts/ci/check-docs.sh` both exit 0 (141 markdown files, 73 listed in `SUMMARY.md`, 0 errors, 0 warnings) and the check runs in `bootstrap.sh` and `ci.yml` through `scripts/ci/repo-checks.sh` (it ran inside the passing gate on e24e6851); the audit was re-graded row by row at tree `7535859` and its counts recompute; the GitBook space is not created, which is the owner's step (OQ-101, `docs/runbooks/gitbook-publish.md`); caveats: the audit ran no test and a few of its sentences (gate not finished) predate the passing gate, nothing ran on Windows, macOS or Android, no model turn (OD-07).
**Success Criteria** (what must be TRUE):
  1. `.gitbook.yaml`, `SUMMARY.md` and about 30 pages exist (get started, use, concepts, configure, agents everywhere, reference, project), every page states what is built, partial or blocked, and the README is the landing page. Verify: `scripts/docs/check_gitbook.py` in the gate (no orphan, duplicate, broken link or unbalanced block). **Result: Met (`.gitbook.yaml`, `docs/SUMMARY.md` with 73 pages listed per the checker, `docs/README.md` landing, root `README.md`, ten guides; `bash scripts/ci/check-docs.sh` and `AUTONOGRAMMER_RELEASE=1 bash scripts/ci/check-docs.sh` re-run 2026-10-28 on e120714b: exit 0, `141 markdown files, 73 listed in SUMMARY.md, 0 errors, 0 warnings`; the four former orphans (`docs/decisions/addendum-ADR-0015-containers.md`, `docs/reference/automations.md`, `docs/reference/builtin-skills.md`, `docs/runbooks/remote-containers.md`) are listed; the eight pinned screenshots exist; `bootstrap.sh` and `.github/workflows/ci.yml` call `scripts/ci/repo-checks.sh`, which calls `check-docs.sh` and `scripts/docs/check_gitbook.py`, pinned by `GateParityTests.BootstrapAndCiRunTheSameRepositoryChecks`, and the gate log of e24e6851 shows the 0-error line and `repo-checks: ok`; `docs/reference/hooks.md` no longer says the heads do not construct the hooks service).**
  2. The owner steps to publish the space are in `docs/runbooks/gitbook-publish.md`; if a signed-in session is available the space is created and connected, otherwise the exact steps remain. Verify: the runbook and, if done, the space URL. **Result: Met with caveats (`docs/runbooks/gitbook-publish.md` holds the 11 exact owner steps and a verification checklist, and its Status line is Blocked: no GitBook session or credential was available, so the space was not created and no URL exists; the criterion asks for the steps in that case; owner action, OQ-101; whether GitBook publishes files outside `SUMMARY.md` UNVERIFIED).**
  3. The Cursor parity audit is refreshed against the 2026-10-27 inventory with a status for every feature. Verify: the audit file counts and the settings and slash tables. **Result: Met (`docs/quality/cursor-parity-audit-2026-10-28.md`: 494 rows in 18 families, recomputed 2026-10-28 from its tables: MET 80, MET-LIVE 1, PARTIAL 170, DEFERRED 27, NOT-STARTED 111, OMITTED-BY-DECISION 105, equal to its summary table; settings 50 rows (MET 8, PARTIAL 20, OMITTED-BY-DECISION 22, equal to the S01 to S50 table of `docs/settings-parity.md`), slash 92 rows (MET 44, PARTIAL 24, DEFERRED 4, NOT-STARTED 6, OMITTED-BY-DECISION 14, equal to the 93-row table of `docs/reference/slash-commands.md` less `/update-settings`), shortcuts 55; every row re-graded at tree `7535859` by reading code and test names, with a re-verification log (section 23) and a drift list (section 24); caveats: no test run by the audit, its sentences that the gate had not finished predate the passing gate on e24e6851, it does not record the worker-image run, and 9 product files changed after `7535859`).**
**Plans**: `64-01-PLAN.md` (units built), `64-SUMMARY.md`, `64-VERIFICATION.md` in `.planning/phases/64-documentation-and-gitbook/`

### Phase 65: Integration and Release v0.15.0
**Goal**: The whole is green on a clean tree, pushed as a fast-forward, tagged and released.
**Requirements**: PX-23
**Depends on**: Phases 51 to 64
**Mac channel**: No.
**UI hint**: no
**Status (2026-10-28)**: Complete with caveats: the gate, the fast-forward push, the tag and the GitHub Release are done and recorded; the GitHub `ci` runs of both pushes failed at 'Start test services' (rootless podman in the runner container, OQ-168); the items needing a live model turn, Windows, macOS, Android, Railway, the sign-in authority or the owner are listed in `65-SUMMARY.md`.
**Success Criteria** (what must be TRUE):
  1. `./bootstrap.sh` passes on a clean worktree of the final commit; the log is saved under `docs/runbooks/evidence/`. Verify: the log. **Result: Met (`./bootstrap.sh` with `AUTONOGRAMMER_TEST_PARALLEL_MODULES=3 AUTONOGRAMMER_SMOKE_PUBLISH_TIMEOUT=2400 AUTONOGRAMMER_SMOKE_DEMO_TIMEOUT=3600` exited 0 on a clean git worktree of `e24e6851554ae6ea710a9841920030269f96d2be`, recorded 2026-10-28 in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`: pinned gitleaks scan OK, plugin node tests (69 and 76), `scripts/ci/repo-checks.sh` OK, locked restore, `scripts/ci/check-licences.sh` OK, warnings-as-errors Release build, `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped (two tests that need a non-root user) in 25 minutes with throwaway TimescaleDB and Dragonfly under podman, `dotnet format --verify-no-changes` clean, web smoke API half and Demo half with axe-core 4.14.0 (6 configurations, 68 axe runs, 0 violations); the log header records two earlier failed runs of the branch, `083ac50` (8 tests) and `75bc55b` (a cascade under load average 96), both fixed).**
  2. `origin/main` is an ancestor of the final commit and the push is a fast-forward; tag `v0.15.0` and its GitHub Release exist. Verify: `git merge-base --is-ancestor`, `gh release view`. **Result: Met (`origin/main` went from `04e80d1` to `9d4e00758589aa8deab7a68ad60074b664b88e9a` as a plain fast-forward, `git merge-base --is-ancestor 04e80d1 9d4e0075` holds and `git ls-remote origin refs/heads/main` returns `9d4e0075`; the annotated tag `v0.15.0` (object `8d4f22d1`) peels to `9d4e0075`; the `release` workflow run 40758275112 on the self-hosted `omega-ci` runner succeeded in 31 s and published the GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0, notes = the CHANGELOG `[0.15.0]` section, recorded 2026-10-28).**
  3. The final report separates verified items from items that need the owner (GitBook connection, social preview upload, Notion authentication, live model turn, provider credentials, Windows and macOS runs, the sign-in authority). **Result: Met (`65-SUMMARY.md` Final report: verified items with evidence, unverified items, and the owner list: GitBook connection, social preview upload and discussion categories, Notion and Linear tracker rows, older milestone tags, CI runner user namespaces, live model turn, provider credentials, Windows, macOS and Android runs, the sign-in authority, Railway deploy approval, AGPL -only or -or-later, owner sign-off of the look). CI caveat recorded 2026-10-28: the `ci` workflow failed for the main push (run 40758215402) and the tag push (run 40758275399) at 'Start test services' (`newuidmap: write to uid_map failed: Operation not permitted`; `cannot clone: Permission denied`), restore and build passed, the rest was skipped (OQ-168).**
**Plans**: `65-01-PLAN.md` (units built), `65-SUMMARY.md`, `65-VERIFICATION.md` in `.planning/phases/65-integration-and-release-v0150/`

### Progress (v0.15.0)

| Phase | Records | Status | Completed |
|-------|---------|--------|-----------|
| 51. Brand, Theme and Licence | `51-SUMMARY.md` | Partial (criteria 1 to 3 met; web head has no About page) | - |
| 52. Repository, Release and Community Health | `52-SUMMARY.md` | Complete with caveats | 2026-10-28 |
| 53. Web Head Foundation and API Surface | `53-SUMMARY.md` | Complete | 2026-10-28 |
| 54. Web Agent Panel and Workspace Screens | `54-SUMMARY.md` | Partial (no Cloud or Remote view; smoke walks one of 18 Settings pages) | - |
| 55. Showing the Web Application | `55-SUMMARY.md` | Partial (Demo hosted, evidence README and live capture committed; source commit and Dark 1024 and 390 screenshots missing) | - |
| 56. Slash Commands and Convention Discovery | `56-SUMMARY.md` | Partial (criterion 1 met; no cloud discovery source; web palette passes `SlashDiscovery.Empty`) | - |
| 57. Hooks | `57-SUMMARY.md` | Partial (no fake-omp load test; real-omp enforcement UNVERIFIED, OD-07) | - |
| 58. Remote Environments over SSH | `58-SUMMARY.md` | Complete with caveats (session open live; no prompt turn) | 2026-10-28 |
| 59. Multi-repo Workspaces | `59-SUMMARY.md` | Partial (explorer, per-run worktrees, convention merge unwired; gate passed) | - |
| 60. Cloud Agents on Railway | `60-SUMMARY.md` | Partial; criterion 4 Partial (staged private worker deployed and handshaken on Railway 2026-10-28; API Blocked by OD-03; worker image live-checked under podman) | - |
| 61. Settings Parity | `61-SUMMARY.md` | Partial (Labs page missing; web Worktrees and Ignore pages missing; gate passed) | - |
| 62. Providers, EU US CN and Coding Plans | `62-SUMMARY.md` | Partial (writer validation by omp not committed; gate passed) | - |
| 63. Cursor Feature Completion | `63-SUMMARY.md` | Partial (pull request, browser, terminal-output and side-chat mentions not composed; criterion 2 met with caveats) | - |
| 64. Documentation and GitBook | `64-SUMMARY.md` | Complete with caveats (GitBook space not created: owner, OQ-101) | 2026-10-28 |
| 65. Integration and Release v0.15.0 | `65-SUMMARY.md` | Complete with caveats (gate passed, pushed, tagged, released; GitHub `ci` fails at test services, OQ-168) | 2026-10-28 |

## v0.16.0 Autonogrammer: accounts, Cowork and Dispatch (planned, added 2026-10-28)

Source: user request 2026-10-28 and the ultrathink plan graph ut-mvs8ru79-82536fbd. The research is in `docs/plans/v0.16.0-research/` (reports 01 to 09, briefs 66 to 77 with `00-index.md` and `00-scaffold-manifest.md`); decisions are ADR-0030 to ADR-0037. Owner answers: q1 rename everything as a hard cutover while keeping the external omp engine names; q2 reuse the Spectrum Web Co Supabase project. Defaults in force: Code works signed out; offline Dispatch commands queue for 5 minutes; Computer Use always needs local approval. Requirements AG-66-* to AG-77-* and AG-SC-1 to AG-SC-6 are in `.planning/REQUIREMENTS.md`.

Settled decisions:
- SD-1 Rename scope. Every name the project owns changes to Autonogrammer as a hard cutover (owner answer q1): paths, contents, identifiers, containers, schema, headers, the Android id. The external omp engine names are kept: bare `omp`, `omp acp`, `~/.omp`, `OMP_*`, `models.yml` and first-party engine identifiers such as `OmpLauncher`. URLs and hostnames of GitHub, Vercel and Railway that still carry the former name are protected until the owner renames those services (owner actions, runbook `docs/runbooks/rebrand-autonogrammer.md` section 5). The tool is `scripts/rebrand/rebrand.py`; the mechanical pass is commit `4af7d83`.
- SD-2 Supabase. Sign-in reuses the Spectrum Web Co Supabase project (owner answer q2): passwordless email one-time code and magic link. We change nothing in the Supabase dashboard; the dashboard steps are owner actions (brief 67). No live key exists, so nothing is called live (ADR-0030).
- SD-3 One API. The existing `Autonogrammer.Api` is extended (accounts, Cowork, artifacts, Dispatch relay); no new service is created. The migration one-shot is the API image started with `migrate` (ADR-0031 D5).
- SD-4 Local agent and relay. The Cowork agent is the local desktop omp over ACP; Dispatch is a relay in the API through which desktops connect outbound only (ADR-0032, ADR-0036).
- SD-5 Platforms. Linux X11 is verified here (private Xvfb). Windows, macOS and Android code is compile-checked only and is marked unmeasured, never claimed run.
- SD-6 Scaffold first. Phase 66.1 performs ALL project creation and registry edits once, before parallel work; phase units add code and edit a registry file only for rows the manifest marks per phase (Reconciliation R17).
- SD-7 One contract bump. `ContractVersion.Current` moves from 1.5.0 to 1.6.0 once, in Phase 66.1; no phase edits it (R9).
- SD-8 Cowork never on the Kanban. `SessionSurface { Code, Cowork }` (column `surface text NOT NULL DEFAULT 'code'`) separates the surfaces; `ListByOwnerAsync` requires the surface; a Cowork session can never be a Kanban card in the registry, the models, the API repository or the routes (ADR-0032 D2).
- SD-9 Nothing deployed. Nothing is deployed on Railway and deploys stay paused (OQ-73). Hosted steps, live Supabase calls and live omp turns (OD-07) are owner actions; no criterion claims them.
- SD-10 Concurrent session isolation. This milestone lives on branch `feat/autonogrammer` in its own git worktree, separate from the checkout that another session uses for the v0.15.0 line. It is replayed onto the other line by re-running `scripts/rebrand`, not by merge (runbook section 3).

Execution order and parallel waves (from `briefs/00-index.md`): Phase 66, then Phase 66.1 (scaffold), then Phase 68 in parallel with Phase 67 units 1 to 3, then Phase 69, then Phase 70, then Phases 71, 72, 73 and 75 in parallel, then Phases 74 and 76, then Phase 77. Phase 67 units 4 to 6 follow Phase 68 unit 3 and the `SurfaceHost` chrome slot of Phases 69 and 70; the Dispatch-facing requirements of Phase 73 close after Phase 74. The orchestrator runs `./bootstrap.sh` once after each wave.

Nothing in this milestone is built yet; every phase below is planned.

- [ ] **Phase 66: Rebrand to Autonogrammer** - one mechanical pass renames every owned name, three C# migrations keep existing user data, regenerated files, one gate run
- [ ] **Phase 66.1: Scaffold: projects, registration and contract bump** - ONE serialised step creates and registers the 21 source and 19 test projects with AssemblyMarker.cs only, the LayerMap rows and six tuples, the package pins and the single ContractVersion bump 1.5.0 to 1.6.0
- [x] **Phase 66.2: Hosted real web app** - the web head builds as the real application with no Demo code, signs in with an owner token, and runs end to end against the API and the real omp (scripted model); hosting and engine hardening; review findings fixed. Nothing is deployed.
- [x] **Phase 66.3: Web accessibility (WCAG 2.2 AA)** - scripted checks of routes, keyboard, focus, forced colours, reflow, text spacing, target size and axe on every state, run against built bundles; ten defects fixed; statement, 55-row table and known issues. Screen readers and devices not run.
- [x] **Phase 66.4: Quality uplift: CI, operations and brand** - SBOM, pinned images, skip-marker and OpenAPI checks, operations documents, the hosted threat model, wordmark and assets; the gate ran in two passes (every step through the format check, 12670 tests, on one commit; the web smoke on the final code); a full single pass is running
- [ ] **Phase 67: Accounts: Supabase passwordless sign-in** - email code and magic link against the shared Supabase project, OS-store tokens on desktop, owner-scoped account and sessions API; Code works signed out
- [ ] **Phase 68: Data plane: names, schema and resilience** - physical names cut over to autonogrammer, additive reversible migrations M0001 to M0010, least-privilege roles, GreptimeDB telemetry, bounded resilience, default-off ledger sink
- [ ] **Phase 69: Surface switch and Cowork sessions** - Code or Cowork switch in the title bar and web top bar, the Cowork layout and the Cowork session model that can never reach the Kanban
- [ ] **Phase 70: Cowork chat core and tool host** - Cowork conversation on omp over ACP with attachments, a Cowork profile and the one governed tool host that Artifacts, Browser Use and Computer Use plug into
- [ ] **Phase 71: Cowork Artifacts** - immutable hash-chained artifact versions rendered only through inert renderers or an isolated surface; list, diff, copy, edit and export from any signed-in head
- [ ] **Phase 72: Cowork Browser Use** - host-launched isolated Chromium over a CDP pipe, `browser_*` tools with vision, egress guard, two-stage approvals, visible Stop and a hostile-page corpus
- [ ] **Phase 73: Cowork Computer Use** - desktop control behind one policy gate with per-application grants, local approval, kill switch, indicator and local audit; X11 verified under Xvfb, Windows and macOS compile-checked
- [ ] **Phase 74: Dispatch relay** - signed-in clients send tasks to the account's registered desktops through a relay in the existing API; desktops connect outbound only; Devices and Dispatch surfaces
- [ ] **Phase 75: Cowork parity A: projects, memory, schedules** - projects, global and per-project instructions, account memory the user controls, scheduled tasks the desktop runs while awake
- [ ] **Phase 76: Cowork parity B: plugins, Auto mode, isolation** - plugin bundles and skills, connector cards, Auto (rules) mode, an OS launch wrapper that confines writes, sub-agent progress, usage meter, notifications
- [ ] **Phase 77: Hardening, docs and integration** - one version bump, final docs, a parity audit with no empty row, a written security and accessibility review, the full gate, integration with the other line, a PR and the owner-action list

### Phase 66: Rebrand to Autonogrammer
**Goal**: Every name the project owns changes from the former name to Autonogrammer in one mechanical pass (paths, contents, identifiers, containers, schema, headers, Android id), existing users keep their data through three C# migrations, the generated files are regenerated, and the renamed tree is built and gated once. Not delivered: new artwork, the trademark decision, external renames (GitHub, Vercel, Railway, domain), `--urls`, a store identity for Android (runbook section 5 items are owner actions).
**Requirements**: AG-66-1 to AG-66-14
**Depends on**: None. The mechanical pass of `scripts/rebrand` is commit `4af7d83` on `feat/autonogrammer`; this phase keeps the requirements and checks of brief 66 (the runtime migrations, the regenerate commit, the gate) until they are verified and recorded. Brief 77 needs this phase applied.
**Mac channel**: No. Windows and macOS paths (data directory move, hook paths) are compile-checked only; the Android build and `ApplicationId` are not built here.
**UI hint**: no
**Success Criteria** (what must be TRUE):
  1. The rebrand tool is correct and its verification reports a clean tree. Verify: `python3 -m unittest scripts/rebrand/test_rebrand.py` passes; `python3 scripts/rebrand/rebrand.py verify --baseline /tmp/ag-recon/rebrand-report/rebrand-report.json` reports clean; the three probes of runbook s.2 give the stated results (the first lists protected URLs only).
  2. The renamed tree builds warning-free, is format-clean, restores in locked mode and the Linux test set is green. Verify: `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` clean; `dotnet restore Autonogrammer.Linux.slnf --locked-mode` clean; full Linux test run (`bootstrap.sh` gate) green, with `bash scripts/test-services.sh up` for the service-backed tests.
  3. The web smoke passes under the new names. Verify: Web smoke with non-colliding ports: `API_PORT=5287 WEB_PORT=5280 DEMO_PORT=5291 bash scripts/web-smoke/run-smoke.sh` (defaults are 5187, 5180, 5191; pick free ports, check with `ss -ltn` first).
  4. The desktop window title is Autonogrammer. Verify: Window title: build `apps/Autonogrammer.Native`, start `Xvfb :97 -screen 0 1280x800x24 &` (pick a free display; never `:99`, another session uses it; check `ls /tmp/.X11-unix`), run the head with `DISPLAY=:97` and a sandbox profile as in the desktop evidence page, read the title with `xdotool getwindowname` or `xprop -root _NET_ACTIVE_WINDOW`, expect `Autonogrammer`; stop the head and Xvfb. Alternative that needs no display: an Avalonia.Headless test asserting `MainWindow.Title == "Autonogrammer"`.
  5. The licence and brand checks pass and the leak-scan fingerprints of the relocated recorded frames are recorded. Verify: `bash scripts/ci/check-licences.sh`, `bash scripts/ci/check-brand.sh` pass; `.gitleaksignore` fingerprints for the relocated recorded frames added after the rename commit exists (runbook s.5 item 6).
**Plans**: `66-01-PLAN.md` in `.planning/phases/66-rebrand-to-autonogrammer/`

### Phase 66.1: Scaffold: projects, registration and contract bump
**Goal**: ONE serialised scaffold step creates and registers every new project of the milestone before any parallel feature work starts (Reconciliation R17 of `docs/plans/v0.16.0-research/briefs/00-index.md`): 21 source projects (3 at L1, 15 at L2, 3 at L3) and 19 test projects, each building with `AssemblyMarker.cs` only; the LayerMap rows and the six declared `SameLayerAllowed` tuples; `ProcessSpawningAssemblies`; the `Directory.Packages.props` pins; one `ContractVersion` bump from 1.5.0 to 1.6.0; one relock and one notices regeneration. The scaffold commit adds no behaviour, so every later unit is file-disjoint. No service is new.
**Requirements**: AG-SC-1 to AG-SC-6
**Depends on**: Phase 66 (the scaffold writes post-rename names). Source: `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md`.
**Mac channel**: No.
**UI hint**: no
**Success Criteria** (what must be TRUE):
  1. Create and register the 21 source and 19 test projects with `AssemblyMarker.cs` only. Verify: `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean (manifest section 4, step 8).
  2. The LayerMap rows, the six tuples and `ProcessSpawningAssemblies` hold. Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` (LayerMap, `ProjectGraphTests`, decision records, suppression ledger).
  3. Every project is registered in the solution files, the architecture project, `docs/architecture.md` and `CHANGELOG.md`. Verify: `bash scripts/ci/repo-checks.sh` (brand, changelog, docs checks) and the build above.
  4. The package pins are added and licences stay accepted. Verify: `bash scripts/ci/check-licences.sh` after the restore.
  5. `ContractVersion.Current` is 1.6.0 and was bumped once; no `Contracts/Plugins` type references a `Contracts/Ui` type. Verify: `dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release` and the scaffold check of manifest section 4 step 1.
  6. One relock and one notices regeneration are committed and the gate is green. Verify: `dotnet restore Autonogrammer.Linux.slnf --locked-mode`, `python3 scripts/legal/generate-third-party-notices.py --check`, then `./bootstrap.sh`.
**Plans**: `66.1-01-PLAN.md` in `.planning/phases/66.1-scaffold-projects-and-registration/`

### Phase 66.2: Hosted real web app
**Goal**: The web head built without Demo code is a working application: the owner signs in with a token, starts agents, watches real engine output stream, approves tool calls, reloads and resumes, against an API that narrows what a hosted run exposes. Not delivered: a deployment (needs the owner's approval), a live model turn (no credential), multi-user sign-in (ADR-0030), a sandbox provider (HOST-04).
**Requirements**: AG-HW-1 to AG-HW-12
**Depends on**: Phase 66 (the renamed tree, replayed onto `origin/main`); the committed state of the concurrent line, read through git only.
**Mac channel**: No.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The real bundle carries no Demo code. Verify: `bash scripts/vercel/build-web.sh <dir>` then `bash scripts/vercel/check-no-demo.sh <dir>`; `--expect-demo` on the Demo bundle finds Demo code.
  2. Owner-token sign-in, access control, hosting and engine hardening and the approval policy hold. Verify: `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release` (with `bash scripts/test-services.sh up`), `Harness.Acp.Tests`, `Web.Tests`, `UI.Tests`.
  3. The hosted path works end to end with the real omp. Verify: `bash scripts/hosted/run-local.sh --web-bundle <dir>` ends with `result: passed` for the API steps and the browser steps (`docs/runbooks/evidence/hosted-web-2026-10-29/`).
  4. Review findings are fixed or recorded. Verify: threat model rows T1.4 and T4.1 to T4.7, open questions OQ-184 to OQ-190.
  5. Nothing is deployed, pushed or provisioned without approval. Verify: `docs/runbooks/deploy-and-rollback.md` preconditions; `git ls-remote origin` shows no branch of this work.
**Plans**: `66.2-01-PLAN.md` in `.planning/phases/66.2-hosted-real-web-app/`

### Phase 66.3: Web accessibility (WCAG 2.2 AA)
**Goal**: The web head can be used with a keyboard, with text zoom to 400 percent, with forced colours, reduced motion and the dark scheme, and its state changes are announced; the scripted checks that show it run in the web smoke. Not delivered: a screen-reader pass, a real device, a Windows High Contrast check, the desktop head (OQ-178, OQ-179).
**Requirements**: AG-AX-1 to AG-AX-8
**Depends on**: Phase 66.2 (the routes, the connect screen and the real bundle the checks run against).
**Mac channel**: No.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The checks find no error on the Demo bundle and on the real bundle. Verify: `bash scripts/web-smoke/run-smoke.sh --a11y-report <dir>` (assertions `a11y<Group>`), and `docs/runbooks/evidence/web-accessibility-2026-10-29/`.
  2. The checks' own logic holds. Verify: `node --test scripts/web-smoke/a11y/*.test.mjs` (42 tests).
  3. The statement and the table say only what was shown. Verify: `docs/accessibility/statement.md`, `wcag-2.2-conformance.md`, `known-issues.md`.
**Plans**: `66.3-01-PLAN.md` in `.planning/phases/66.3-web-accessibility-wcag-22-aa/`

### Phase 66.4: Quality uplift: CI, operations and brand
**Goal**: The supply chain, the operations documents, the threat model and the brand assets reach the bar of a hosted product, and the whole tree passes the gate once. Not delivered: a run of CodeQL, Scorecard, trivy, an image build or an attestation (OQ-175), measured SLOs (OQ-180), the trademark decision.
**Requirements**: AG-QU-1 to AG-QU-8
**Depends on**: Phases 66.2 and 66.3.
**Mac channel**: No.
**UI hint**: no
**Success Criteria** (what must be TRUE):
  1. The repository checks pass. Verify: `bash scripts/ci/repo-checks.sh` (brand, changelog, docs, providers, skip markers).
  2. The SBOM is current. Verify: `bash scripts/ci/check-sbom.sh`.
  3. The full gate passes on the final commit. Verify: `./bootstrap.sh` on a clean worktree; the log is under `docs/runbooks/evidence/`. **Result: Partial**: `561a83b5` passed every step through the format check (12670 tests, 0 failed) and failed the smoke on host load (`bootstrap-gate-2026-10-29-561a83b5.log`); the smoke alone passed on `d0d0b879` (`web-accessibility-2026-10-29/final-smoke-d0d0b879/`); a full pass on the last commit is Run 5 of `66.4-VERIFICATION.md`.
**Plans**: `66.4-01-PLAN.md` in `.planning/phases/66.4-quality-uplift-ci-ops-brand/`

### Phase 67: Accounts: Supabase passwordless sign-in
**Goal**: A person can sign in on the desktop and web heads with an email code or a magic link against the shared Supabase project, stay signed in across restarts on desktop with the token in the operating system store, and read their own account and sessions through the owner-scoped API. Code mode keeps working signed out; Cowork, Dispatch and the account surface need an account.
**Requirements**: AG-67-1 to AG-67-24
**Depends on**: Phase 66.1; Phase 68 (units 4 and 5 need unit 3: the surface column and the repository methods; units 1 to 3 and 6 do not); Phases 69 and 70 (the `SurfaceHost` chrome slot for the account menu). Brief: `67-accounts.md`; ADR-0030.
**Mac channel**: No. Windows DPAPI and macOS Keychain stores are compile-checked only; no live Supabase call is possible here.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The Accounts library (GoTrue client, one-time code, PKCE link, session state) passes its offline tests. Verify: `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"` passes.
  2. The OS secret stores and the desktop sign-in adapters pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live"` and `dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"` pass.
  3. The Linux keyring round trip works on this host or skips with a stated reason. Verify: `AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live"` stores, reads, lists and removes a throwaway item in this host's keyring, or skips with a stated reason.
  4. The API account endpoints are owner-scoped and the OpenAPI document equals the served one. Verify: `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live"` passes, including `GuardTests` OpenAPI equality.
  5. The account surfaces of every head pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `... tests/Autonogrammer.Native.Tests ...`, `... tests/Autonogrammer.UI.Tests ...` and `... tests/Autonogrammer.Web.Tests ...` pass.
  6. The architecture rules hold (layers, project graph, decision records, suppression ledger, TODO scan). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, project graph, ADR structure, suppression ledger, TODO scan).
  7. The build is warning-free and the licence check accepts the new package. Verify: `dotnet build Autonogrammer.Linux.slnf -c Release -warnaserror` is clean; `bash scripts/ci/check-licences.sh` accepts the new package.
  8. The restore passes in locked mode after the relock. Verify: `CI=true dotnet restore Autonogrammer.Linux.slnf --locked-mode` passes after the relock.
**Plans**: `67-01-PLAN.md` in `.planning/phases/67-accounts-supabase-passwordless-sign-in/`

### Phase 68: Data plane: names, schema and resilience
**Goal**: Cut every Autonogrammer-owned physical data name over to `autonogrammer` now (nothing is deployed), add the additive PostgreSQL/TimescaleDB schema that accounts, Cowork and Dispatch need with a reversible migration runner and least-privilege roles, move telemetry to GreptimeDB database `autonogrammer`, wrap every store call in bounded resilience with specific degraded states, and define the optional default-off audit path to the Substrate ledger. Plugin and Substrate namespaces are never touched.
**Requirements**: AG-68-1 to AG-68-14
**Depends on**: Phase 66.1. Consumed by Phase 67 (M0003), Phase 74 (M0005, M0008 and its own M0010) and Phase 75 (M0009). Brief: `68-data-plane.md`; ADR-0031.
**Mac channel**: No. Head banners on Windows, macOS and Android are not run; the Railway private network, the Timescale edition and any hosted deploy are not reachable (OQ-73).
**UI hint**: no
**Success Criteria** (what must be TRUE):
  1. The build and locked restore pass and no new package appears in the licence check. Verify: `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet restore Autonogrammer.Linux.slnf --locked-mode` pass; `bash scripts/ci/check-licences.sh` passes with no new package.
  2. The whole Linux test set passes against the throwaway services. Verify: `source "$(bash scripts/test-services.sh up)"; dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` passes.
  3. No first-party source holds an old physical data name. Verify: `grep -rniE "autonogrammer:|schema autonogrammer|autonogrammer_test|autonogrammer\.(sessions|cloud)" src services tests scripts deploy .env.example` returns nothing outside the allowed external omp tokens.
  4. Telemetry reaches the GreptimeDB database `autonogrammer` (live, gated to a machine with podman). Verify: `AUTONOGRAMMER_LIVE_GREPTIME=1 dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --no-build --filter-trait "Category=Live"` after `bash scripts/test-services.sh up --with-greptime` passes on a machine with podman.
  5. ADR-0031 is indexed and the decision-record tests pass. Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` passes with ADR-0031 indexed.
**Plans**: `68-01-PLAN.md` in `.planning/phases/68-data-plane-names-schema-and-resilience/`

### Phase 69: Surface switch and Cowork sessions
**Goal**: A signed-in user switches between Code and Cowork from the title bar of the Native head and the top bar of the Web head, by keyboard or pointer, with accessible semantics and a remembered choice; both surfaces keep running when hidden; Cowork has its own layout, its own session store and its own list, and a Cowork session can never create, update or appear as a Kanban card, in the desktop registry, the models, the API repository or the API routes.
**Requirements**: AG-69-1 to AG-69-13
**Depends on**: Phase 67 (account state through the `ICoworkAccountGate` port); Phase 68 (`ListByOwnerAsync`, the `surface` column). Brief: `69-mode-shell-and-cowork-sessions.md`; ADR-0032.
**Mac channel**: No. Windows and macOS rendering and the macOS Meta shortcuts are compile-checked only.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. Cowork can never reach the Kanban and the project graph holds. Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*"` and `-- --filter-class "*ProjectGraphTests*"` pass.
  2. The Cowork session model and the presentation layer pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release` pass.
  3. The Native head switches surfaces, Cowork never appears on the Kanban, and the existing Kanban, motion and settings tests still pass unchanged apart from the harness helper. Verify: `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*"` and `-- --filter-class "*CoworkKanbanIsolationTests*"` pass, and the existing `KanbanHeadTests`, `HeadMotionTests`, `SettingsEntryTests` still pass unchanged apart from the harness helper.
  4. The UI and Web heads pass their tests. Verify: `dotnet test --project tests/Autonogrammer.UI.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Web.Tests -c Release` pass.
  5. The Cowork session endpoints and the board surface filter pass against the throwaway services. Verify: `scripts/test-services.sh up` then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release` passes (`CoworkSessionEndpointsTests`, `AccountBoardSurfaceTests`).
  6. The build is warning-free and the notices check is clean (no new package). Verify: `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` is clean (no new package).
**Plans**: `69-01-PLAN.md` in `.planning/phases/69-surface-switch-and-cowork-sessions/`

### Phase 70: Cowork chat core and tool host
**Goal**: A signed-in user holds a Cowork conversation with the local omp engine: streaming, stop, retry, steering, history after a restart, file and image attachments, a Cowork profile (system prompt, permissions, folder boundary) that differs from Code, and one governed seam, the tool host, through which Artifacts, Browser Use and Computer Use hand tools to omp, with approvals, caps, audit and images that reach both the model and the transcript.
**Requirements**: AG-70-1 to AG-70-14
**Depends on**: Phase 69 (units 1 to 3); Phase 67 (the account gate); Phase 68 (the audit writer; a null audit is used until it lands). Brief: `70-cowork-chat-core.md`; ADR-0032.
**Mac channel**: No. Windows and macOS path rules and the composer on those heads are compile-checked only; no live omp turn (OD-07).
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The tool host, gate, limiter, profile, permission and fake-provider contract tests pass. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` passes (tool host, gate, limiter, profile, permissions, fake provider contract tests).
  2. The MCP gateway passes, including the real MCP client image test. Verify: `dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release` passes, including the real MCP client image test.
  3. The Native head's Cowork chat passes its tests. Verify: `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Cowork*"` passes.
  4. Image fold and replay parity hold in the presentation, UI and session projects. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.UI.Tests` and `tests/Autonogrammer.Sessions.Tests` pass (image fold and replay parity).
  5. The project graph holds, the build is warning-free and the notices check is clean (no new package). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"` passes; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` is clean (no new package).
**Plans**: `70-01-PLAN.md` in `.planning/phases/70-cowork-chat-core-and-tool-host/`

### Phase 71: Cowork Artifacts
**Goal**: Cowork shows what the agent makes (markdown, code, HTML pages and small apps, SVG, Mermaid, CSV, images) in a panel beside the chat, as immutable hash-chained versions the user can diff, copy, edit, export and list from any signed-in head. Generated content is untrusted: it renders only in-process through renderers that cannot execute it, or in an isolated surface with a restrictive CSP, no network by default, no navigation and no bridge to the app.
**Requirements**: AG-71-1 to AG-71-24
**Depends on**: Phases 67, 68 and 70 (`Cowork.Abstractions`, `IDockPanel`, `ToolUpdateImage`); ADR-0009 and ADR-0010. Brief: `71-artifacts.md`; ADR-0033.
**Mac channel**: No. Windows WebView2, macOS WKWebView and Android WebView are compile-checked only; a private Xvfb run on WebKitGTK is the Linux evidence.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. Every hostile corpus entry is rendered inert and the artifact library passes. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` passes, including every hostile corpus entry.
  2. The Postgres artifact stores pass against the throwaway services. Verify: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live"` passes with `scripts/test-services.sh up` sourced.
  3. The artifact endpoints pass and the OpenAPI document is fresh. Verify: `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"` passes and `docs/api/openapi.json` is fresh.
  4. The heads' artifact panels pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests`, `Native.Tests`, `UI.Tests`, `Web.Tests` with `-- --filter-class "*Artifact*"` pass.
  5. The architecture tests pass (LayerMap, graph, decision records, suppression ledger). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, graph, decision records, suppression ledger).
  6. The artifact page of the web smoke makes no outbound request, no navigation and no parent access from the hostile fixtures. Verify: `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` passes with the artifact page: no outbound request, no navigation, no parent access from the hostile fixtures.
  7. The licence check and the notices check exit 0. Verify: `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` exit 0.
**Plans**: `71-01-PLAN.md` in `.planning/phases/71-cowork-artifacts/`

### Phase 72: Cowork Browser Use
**Goal**: The Cowork agent can browse: a host-launched, isolated Chromium driven over a CDP pipe by PuppeteerSharp, shown as a live pane on the desktop head (the Web and Android panes render the same models and wait for a relay feed, which ADR-0036 holds back), exposed to omp as `browser_*` tools with vision, under an egress guard, two-stage approvals, a visible Stop and a hostile-page test corpus.
**Requirements**: AG-72-1 to AG-72-16
**Depends on**: Phase 70 (and Phase 69); Phases 68 and 67; ADR-0036 D13 (no relay frames). Brief: `72-browser-use.md`; ADR-0034.
**Mac channel**: No. Windows and macOS launch, pipe handles, proxy and a downloaded browser are compile-checked only; the Linux criteria run on `google-chrome` with a private Xvfb.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The browser library passes its offline tests. Verify: `dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"` passes.
  2. The live browser tests pass with a headless Chromium on this host. Verify: `AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live"` passes (headless; this host is uid 0, hence the sandbox switch).
  3. The headful smoke passes under a private Xvfb. Verify: `AUTONOGRAMMER_LIVE_BROWSER=1 AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 xvfb-run -a dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live" -- --filter-class "*HeadfulSmokeLiveTests*"` passes.
  4. The heads, architecture graph, notices and decision records pass and the build is warning-free. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.Native.Tests`, `tests/Autonogrammer.UI.Tests` and `tests/Autonogrammer.ArchitectureTests -- --filter-class "*ProjectGraphTests*"` pass; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` passes; `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` passes.
  5. A live omp turn with the gateway, an image result and `browser.enabled: false` is recorded in the runbook as an owner-approved run, or stays stated as not run. Verify: A live omp turn with the gateway, an image result and `browser.enabled: false` is a separate owner-approved run (ADR-0010, OD-07), recorded in `docs/runbooks/browser-use.md`.
**Plans**: `72-01-PLAN.md` in `.planning/phases/72-cowork-browser-use/`

### Phase 73: Cowork Computer Use
**Goal**: Cowork can look at and operate the user's own desktop (X11 verified here under a private Xvfb; Windows and macOS compile-checked) through 15 computer tools (`computer_*` descriptors) behind one policy gate, with per-application grants, local approval, a kill switch, an always-visible indicator and a local audit log. Off by default, never in Code mode, never in the cloud, never remotely approvable.
**Requirements**: AG-73-1 to AG-73-24
**Depends on**: Phases 67 and 70; Phase 72 (optional); Phase 74 for the Dispatch-facing requirements only (relay refusals, the capability flag, `AllowDispatchSessions`), which close after both phases land. Brief: `73-computer-use.md`; ADR-0035.
**Mac channel**: No. Windows (SendInput, UIPI, GDI) and macOS (TCC, CGEvent, `screencapture`) are compile-checked only; Wayland portals and AT-SPI are not exercised.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The Computer Use core passes its offline tests. Verify: `nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release --filter-not-trait "Category=Live"` passes.
  2. The X11 backend passes its live tests under a private Xvfb, or each test skips with a stated reason. Verify: `AUTONOGRAMMER_LIVE_X11=1 nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release --filter-trait "Category=Live"` passes, or each test reports `Assert.Skip("Xvfb not found")`. The fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` on a free display, passes the display name to the backend options and never reads or sets the session `DISPLAY`.
  3. The live X11 cases (pointer, click, typing, capture, hotkey, second grab, window listing) behave as the brief states. Verify: Live cases: `xdotool` moves the pointer and the backend reads it; the backend clicks a fixture and `xev` logs the button event; `type` of `Hello, ünï` reaches `xev` as the same keysyms; `scrot` and the backend frames of a fixed `xterm` agree within a per-channel tolerance; the hotkey fires on `xdotool key ctrl+alt+shift+Escape`; a second grab reports `KillSwitchUnavailable`; a bare Xvfb lists windows through `XQueryTree`.
  4. The Windows and macOS backends compile warning-free on Linux. Verify: `nice -n 15 dotnet build src/Autonogrammer.Harness.ComputerUse.Windows/Autonogrammer.Harness.ComputerUse.Windows.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1` and the same for `.MacOS` pass.
  5. The project graph, the Computer Use boundary and the decision records hold (ADR-0035 indexed). Verify: `nice -n 15 dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"` and `-- --filter-class "*ComputerUseBoundaryTests*"` pass; `-- --filter-class "*DecisionRecordTests*"` passes with ADR-0035 indexed.
  6. The licence check and notices check pass unchanged (no package added). Verify: `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` pass unchanged (no package added).
**Plans**: `73-01-PLAN.md` in `.planning/phases/73-cowork-computer-use/`

### Phase 74: Dispatch relay
**Goal**: A signed-in client (phone app, web app, another desktop) sends tasks to one of the account's registered desktops through a relay in the existing `Autonogrammer.Api`; desktops connect outbound only. Delivered: contracts, API (registry, hub, feed), desktop connector, and the Devices and Dispatch surfaces for the Native head (shared with Android) and the Web head. Not delivered: a full mobile app, push, screen streaming, transcript mirroring.
**Requirements**: AG-74-1 to AG-74-18
**Depends on**: Phase 67 (`IAccountSession`, `Secrets.Os`, `IAccountFeature`); Phase 68 (M0005, M0008, keyspace, `ITenantHasher`, `IAuditLedger`, `accounts.feed_seq`); Phases 69 and 70 (the chrome slot, the Cowork host); Phases 72 and 73 (tool classes, `AllowDispatchSessions`). Brief: `74-dispatch.md`; ADR-0036.
**Mac channel**: No. The Android build, a real phone, Windows and macOS (credential store, desktop head) are compile-checked only; Android as a controller stays compile-only (OQ-429).
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The Dispatch contracts pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` passes.
  2. The relay API and the Postgres Dispatch store pass against the throwaway services. Verify: `bash scripts/test-services.sh up`, then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` and `dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` pass.
  3. The desktop connector and the client pass with no sleeps (manual `TimeProvider`). Verify: `dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` and `...Dispatch.Client.Tests` pass with no sleeps (manual `TimeProvider`).
  4. The Dispatch presentation and the heads' Devices and Dispatch surfaces pass their tests. Verify: `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Dispatch*"`, and the same filter for `UI.Tests`, `Native.Tests`, `Web.Tests`, pass.
  5. The architecture tests pass (LayerMap, ADR-0036 record, Web free of process-spawning references, no TODO without an OQ row). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, `DecisionRecordTests` for ADR-0036 and the README row, Web free of process-spawning references, no TODO marker without an OQ row).
  6. The build and format are clean and the OpenAPI document is regenerated and equal to the served one. Verify: `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` are clean; `docs/api/openapi.json` is regenerated and `GuardTests` equality holds.
**Plans**: `74-01-PLAN.md` in `.planning/phases/74-dispatch-relay/`

### Phase 75: Cowork parity A: projects, memory, schedules
**Goal**: A signed-in user keeps projects (name, instructions, context links, local folders), global and per-project instructions, and an account memory they control, and can schedule recurring tasks that the desktop runs while it is awake. Everything is listed, editable and deletable by the user, and nothing leaves the account store except by the user's request.
**Requirements**: AG-75-1 to AG-75-11
**Depends on**: Phase 67; Phase 68 (unit 3 writes M0009); Phases 69 and 70 (project store, host, profile, tool host); ADR-0010 and ADR-0014. Brief: `75-cowork-parity-a.md`; ADR-0037.
**Mac channel**: No. Windows and macOS are not run; a task waking from real sleep and omp honouring `memory.backend: off` are not observed (OD-07).
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The build is warning-free after a locked restore. Verify: `dotnet restore Autonogrammer.Linux.slnf --locked-mode`, then `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`.
  2. The Postgres memory store passes against the throwaway services. Verify: `TEST_SERVICES_ENV="$(bash scripts/test-services.sh up)"; source "$TEST_SERVICES_ENV"`, then `dotnet test --project tests/Autonogrammer.Cowork.Memory.Postgres.Tests -c Release --no-build`.
  3. The Cowork, memory, automations, agent loop, presentation, Native, UI, Web and API tests pass. Verify: `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Cowork.Tests`, `Autonogrammer.Cowork.Memory.Tests`, `Autonogrammer.Harness.Automations.Tests`, `Autonogrammer.Harness.AgentLoop.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.Native.Tests`, `Autonogrammer.UI.Tests`, `Autonogrammer.Web.Tests`, `Autonogrammer.Api.IntegrationTests`.
  4. The architecture tests pass (layers, same-layer tuple, process-ban naming, suppression ledger, unlinked-marker rule). Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release --no-build` (layers, same-layer tuple, process-ban naming, suppression ledger, unlinked-marker rule).
  5. The repository checks, licence check and format check pass. Verify: `bash scripts/ci/repo-checks.sh`, `bash scripts/ci/check-licences.sh`, `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`.
  6. The full gate passes and a parity row is MET only when a named passing test class covers it. Verify: `./bootstrap.sh` is the full gate. A parity row is MET only if a named test class above covers it.
**Plans**: `75-01-PLAN.md` in `.planning/phases/75-cowork-parity-projects-memory-schedules/`

### Phase 76: Cowork parity B: plugins, Auto mode, isolation
**Goal**: Cowork and Code sessions share one extension and safety layer: content plugin bundles and skills, connector cards with a three-state tool permission matrix, an Auto (rules) approval mode with deletion protection and folder access modes for Cowork, an OS-level launch wrapper that confines writes, a sub-agent progress view, a local usage meter and desktop notifications. Nothing here claims to match Anthropic's classifiers or VM; each limit is stated on the screen that shows the feature.
**Requirements**: AG-76-1 to AG-76-14
**Depends on**: Phase 75 (unit 75-06: `ILaunchContributor`, `LaunchContribution`, `docs/parity/cowork-parity.md`); Phases 69 and 70. Brief: `76-cowork-parity-b.md`; ADR-0037.
**Mac channel**: No. macOS `sandbox-exec` and `osascript`, Windows job objects and OS notification display are not run; a real `bwrap` run needs a host that allows user namespaces (OQ-168).
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. The build is warning-free after a locked restore. Verify: `dotnet restore Autonogrammer.Linux.slnf --locked-mode`, then `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`.
  2. The plugin bundle, isolation, notification, Cowork, agent loop, MCP, ACP, workspace, presentation, Native and architecture tests pass. Verify: `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Harness.PluginBundles.Tests`, `Autonogrammer.Harness.Isolation.Tests`, `Autonogrammer.Harness.Notifications.Tests`, `Autonogrammer.Cowork.Tests`, `Autonogrammer.Harness.AgentLoop.Tests`, `Autonogrammer.Harness.Mcp.Tests`, `Autonogrammer.Harness.Acp.Tests`, `Autonogrammer.Harness.Workspace.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.Native.Tests`, `Autonogrammer.ArchitectureTests`.
  3. A real `bwrap` run passes on a host that allows it; without the variable the tests skip and do not fail. Verify: On a host where `bwrap --version` works and user namespaces are allowed: `AUTONOGRAMMER_LIVE_ISOLATION=1 dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live"`. Without the variable the tests skip, they do not fail.
  4. The repository checks, licence check and format check pass and the full gate is run. Verify: `bash scripts/ci/repo-checks.sh`, `bash scripts/ci/check-licences.sh`, `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `./bootstrap.sh` is the full gate.
  5. A parity row is MET only with a named passing test class; rows that depend on omp consuming a flag stay PARTIAL until a live run is recorded. Verify: A row of `docs/parity/cowork-parity.md` is MET only with a named passing test class; rows that depend on omp consuming a flag stay PARTIAL until a live run is recorded.
**Plans**: `76-01-PLAN.md` in `.planning/phases/76-cowork-parity-plugins-auto-isolation/`

### Phase 77: Hardening, docs and integration
**Goal**: After phases 67 to 76 have landed and the rename (phase 66) is applied, bring milestone v0.16.0 to a shippable state: one version bump, final documentation, a parity audit that cannot contain an empty row, a written security review and accessibility pass over the new surfaces, the full gate, integration with the other line of work, a PR, and the list of actions only the owner can do. Not delivered: any live run (OD-07 is not approved; no Supabase, no Railway), Windows or macOS runs, a release tag, any rename outside the repository.
**Requirements**: AG-77-1 to AG-77-17
**Depends on**: Phases 67 to 76 landed and Phase 66 applied. Brief: `77-hardening-and-integration.md`; ADR-0037 D14; all of ADR-0030 to ADR-0037.
**Mac channel**: No. No live run (OD-07 not approved; no Supabase, no Railway) and no Windows or macOS runner; the `VersionPrefix` bump and relock of the lock files are done once, at the end.
**UI hint**: no
**Success Criteria** (what must be TRUE):
  1. The Cowork audit guard passes and its fixture failures are proven. Verify: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkAudit*"` passes, with fixture failures proven (AG-77-9).
  2. The parity counts, docs and changelog checks pass. Verify: `python3 scripts/docs/parity_counts.py --check` and `bash scripts/ci/check-docs.sh` and `bash scripts/ci/check-changelog.sh` pass.
  3. The version bump commit touches only the version files and locks and the locked restore passes. Verify: `dotnet restore Autonogrammer.Linux.slnf --locked-mode` passes after the bump commit; `git diff --stat` of that commit shows only the version files and locks.
  4. The security findings table has no open or accepted Critical or High row and the accessibility matrix has no empty cell. Verify: The security findings table has no Critical or High row `open` or `accepted`; the accessibility matrix has no empty cell.
  5. The full gate exits 0, the evidence page is committed and CI is green on the PR. Verify: `./bootstrap.sh` exits 0 and the evidence page is committed; CI is green on the PR.
**Plans**: `77-01-PLAN.md` in `.planning/phases/77-hardening-docs-and-integration/`

### Progress (v0.16.0)

| Phase | Records | Status | Completed |
|-------|---------|--------|-----------|
| 66. Rebrand to Autonogrammer | - | Planned | - |
| 66.1. Scaffold: projects, registration and contract bump | - | Planned | - |
| 66.2. Hosted real web app | 66.2-CONTEXT.md, 66.2-01-PLAN.md, 66.2-SUMMARY.md, 66.2-VERIFICATION.md | Complete (not deployed) | 2026-10-29 |
| 66.3. Web accessibility (WCAG 2.2 AA) | 66.3-CONTEXT.md, 66.3-01-PLAN.md, 66.3-SUMMARY.md, 66.3-VERIFICATION.md | Complete with caveats (no screen reader, no device) | 2026-10-29 |
| 66.4. Quality uplift: CI, operations and brand | 66.4-CONTEXT.md, 66.4-01-PLAN.md, 66.4-SUMMARY.md, 66.4-VERIFICATION.md | Complete with caveats (the gate ran in two passes; no image built, no workflow run on GitHub) | 2026-10-29 |
| 67. Accounts: Supabase passwordless sign-in | - | Planned | - |
| 68. Data plane: names, schema and resilience | - | Planned | - |
| 69. Surface switch and Cowork sessions | - | Planned | - |
| 70. Cowork chat core and tool host | - | Planned | - |
| 71. Cowork Artifacts | - | Planned | - |
| 72. Cowork Browser Use | - | Planned | - |
| 73. Cowork Computer Use | - | Planned | - |
| 74. Dispatch relay | - | Planned | - |
| 75. Cowork parity A: projects, memory, schedules | - | Planned | - |
| 76. Cowork parity B: plugins, Auto mode, isolation | - | Planned | - |
| 77. Hardening, docs and integration | - | Planned | - |
