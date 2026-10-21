<!-- Tracking: ultrathink node n2 "Decompose into monorepo layout and dependency direction" — SPE-17752 (https://linear.app/swcstudio/issue/SPE-17752); node n8 "Synthesize ordered execution plan in parallel waves" — SPE-17756 (https://linear.app/swcstudio/issue/SPE-17756) -->

# Roadmap: OmpOS

## Milestones

- v0.9.1: foundation through verified handoff. Closed 2026-10-21; merge of PR #1 (swcstudiospace/OmpOS) pending. Archived in `.planning/milestones/` (v0.9.1-ROADMAP.md, v0.9.1-REQUIREMENTS.md, v0.9.1-MILESTONE-AUDIT.md, v0.9.1-phases/).
- v0.9.2 Hardening and hosted API: current. Requirements in `.planning/REQUIREMENTS.md` (75 REQ-IDs). Scope is the work still open; items already closed are recorded in "Closed Records" and are not re-planned.

## Planning Rules

- Phases run in wave order by the dependencies listed per phase. The orchestrator runs `./bootstrap.sh` once after each wave; a phase is done when its success criteria are observable and the gate passes at the phase HEAD.
- Every HOST requirement is gated on the owner's sign-in decision (OD-03). No hosted phase starts before it, and no public exposure happens before it (HOST-02).
- Vendor interfaces are implemented only against verified sources. Where verification fails, the phase records the verified blocker in `docs/open-questions.md` instead of inventing a mapping.
- Phases are sized to be verified by the gate on their own.

## Phases

- [ ] **Phase 1: Validation Evidence and Record Fixes** - Negative proofs for 01-VALIDATION rows and the 04-03 summary frontmatter
- [ ] **Phase 2: Acceptance-Criteria List Recovery** - Recover or record AC_7, AC_9, AC_10, AC_11
- [ ] **Phase 3: Web Head Reflection and Browser Verification** - Reflection switch off for the web head; axe and skip-link items resolved
- [ ] **Phase 4: AgentOS Remaining Operations** - Verified agentOS operations or verified fail-closed records
- [ ] **Phase 5: Railway Swarm Remaining Operations** - Verified Railway GraphQL operations or verified fail-closed records
- [ ] **Phase 6: Per-Sandbox Credential Issuance** - Scoped credentials for non-podman providers, revoked at teardown
- [ ] **Phase 7: PostgreSQL Session Log** - Shared session log behind `ISessionRepository` for API and worker
- [ ] **Phase 8: Worker Composition and Routed Settlement and Streaming** - ~~Real worker composition; routed runs write and stream through the log~~ Superseded 2026-10-21 (worker removed, docs/handoff.md)
- [ ] **Phase 9: Replay Re-Validation** - ~~`session-run.json` replayed against a live Temporal dev server~~ Superseded 2026-10-21 (Temporal removed, docs/handoff.md)
- [ ] **Phase 10: Routed Interactive Approvals** - UI decisions reach pending permission requests, direct and routed
- [ ] **Phase 11: RPC-Mode Update Recording** - Unseen `session/update` variants recorded from omp RPC turns (owner approval gate)
- [ ] **Phase 12: Hosted Deploy Config and Exposure Gate** - Railway and CORS lock (OD-03 gated); ~~Temporal Cloud~~ superseded 2026-10-21 (docs/handoff.md)
- [ ] **Phase 13: Hosted Routed omp Harness** - Hosted API runs the real harness (OD-03 gated)
- [ ] **Phase 14: Vercel Web Against Production Origin** - Deployed web head and CSP-clean smoke (OD-03 gated)
- [ ] **Phase 15: macOS iOS and MacCatalyst Builds** - Builds and macOS CI shells job (Mac channel gated)
- [ ] **Phase 16: macOS Runtime Spot-Check** - Desktop against remote backend; mobile remote-only (Mac channel gated)
- [ ] **Phase 17: Blocking Record and Nyquist Verdicts** - BR-1 closed; phases 1 to 5 validated
- [ ] **Phase 18: Handoff and Open-Question Closure** - Docs reflect every closure and blocker
- [ ] **Phase 19: Milestone Re-Audit** - Re-audit status is not `gaps_found`

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
| 1. Validation Evidence and Record Fixes | 0/TBD | Not started | - |
| 2. Acceptance-Criteria List Recovery | 0/TBD | Not started | - |
| 3. Web Head Reflection and Browser Verification | 0/TBD | Not started | - |
| 4. AgentOS Remaining Operations | 0/TBD | Not started | - |
| 5. Railway Swarm Remaining Operations | 0/TBD | Not started | - |
| 6. Per-Sandbox Credential Issuance | 0/TBD | Not started | - |
| 7. PostgreSQL Session Log | 0/TBD | Not started | - |
| 8. Worker Composition and Routed Settlement and Streaming | 0/TBD | Superseded 2026-10-21 (worker removed) | - |
| 9. Replay Re-Validation | 0/TBD | Superseded 2026-10-21 (Temporal removed) | - |
| 10. Routed Interactive Approvals | 0/TBD | Not started | - |
| 11. RPC-Mode Update Recording | 0/TBD | Gated: owner approval | - |
| 12. Hosted Deploy Config and Exposure Gate | 0/TBD | Gated: OD-03 | - |
| 13. Hosted Routed omp Harness | 0/TBD | Gated: OD-03 | - |
| 14. Vercel Web Against Production Origin | 0/TBD | Gated: OD-03 | - |
| 15. macOS iOS and MacCatalyst Builds | 0/TBD | Gated: Mac channel | - |
| 16. macOS Runtime Spot-Check | 0/TBD | Gated: Mac channel | - |
| 17. Blocking Record and Nyquist Verdicts | 0/TBD | Not started | - |
| 18. Handoff and Open-Question Closure | 0/TBD | Not started | - |
| 19. Milestone Re-Audit | 0/TBD | Not started | - |

## Blocked Records

Each item is recorded with its reason. Its REQ-ID has no phase until the unblock condition holds.

| Item | Blocker | Unblock condition | Owner |
|------|---------|-------------------|-------|
| OD-03 | Sign-in method not decided; no source for this milestone records a decision (Q OQ-26 covers only the single dev principal) | Owner selects the method. Phases 12 to 14 then start. Public exposure stays forbidden until it is in place (HOST-02) | Owner |
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

## v0.10.0 Cross-platform UI (planned)

Avalonia UI for Windows, macOS, Linux, iOS and Android. The web head stays Blazor WebAssembly; Avalonia.Browser is gated by G-WEB, which failed on Avalonia 12.1.4 (ADR-0004).

- [ ] **Phase 20: Avalonia Foundation** - Shared Avalonia native project, shared presentation layer, accessibility baseline, plugin host contracts
- [ ] **Phase 21: Parity Surface** - Session chat, tool approvals and steering, with parity tests on the presentation layer
- [ ] **Phase 22: Desktop Heads** - Linux, Windows and macOS heads with platform accessibility; macOS signing on the Mac channel
- [ ] **Phase 23: Web Head on the Shared Presentation Layer** - Blazor WebAssembly web head, G-WEB re-measurement, Vercel static hosting
- [ ] **Phase 24: iOS and Android Heads** - Mobile heads with platform accessibility; iOS on the Mac channel
- [ ] **Phase 25: Cutover** - Remove the MAUI head after parity; remove the Razor component library once the web head no longer references it

### Phase 20: Avalonia Foundation
**Goal**: One shared Avalonia native project, a framework-neutral presentation layer, an accessibility baseline and plugin host contracts form the base that every head builds on.
**Requirements**: XP-01, XP-08, XP-09
**Depends on**: Nothing (first phase of v0.10.0)
**Mac channel**: No. The shared project and its contract tests build and run on the Linux host and the CI runners; no signing or device run is involved.
**UI hint**: yes
**Success Criteria** (what must be TRUE):
  1. One Avalonia 12.1.4 native project is in the solution and builds with warnings as errors. Verify: gate at phase HEAD.
  2. A presentation layer project holds the view models and state extracted from the Razor components, with no UI-framework types, and references no Avalonia, MAUI or ASP.NET Core Components assembly. Verify: contract test in the gate that fails on any such reference or UI-framework type.
  3. The presentation layer is consumed by both the Avalonia head and the Blazor web head; both build against it and its tests pass in the gate. Verify: gate build of both heads and presentation-layer tests.
  4. Every control in the Avalonia head has an explicit AutomationProperties name, and the names of the prompt input, send control and transcript are exposed through AT-SPI2 on Linux. Verify: headless UI test in the gate that fails on an unnamed control; AT-SPI2 query output in the runbook.
  5. Shared controls are reachable by keyboard, have a visible focus style, and disable transitions when reduced motion is on. Verify: headless UI test in the gate over every shared control.
  6. Theme tokens for colour, type and spacing live in one resource dictionary, and a contract test asserts at least 4.5:1 text contrast for every foreground and background pair the dictionary defines. Verify: contract test in the gate.
  7. Plugin host contracts in the contracts layer define commands, panels, tools and agent modes with no UI-framework reference, and an in-process sample plugin registers one of each through the host. Verify: contract test in the gate that fails if the contracts assembly references a UI framework assembly, and asserts each registration.
**Plans**: 4 plans in 4 waves, each wave depends on the previous one
- [ ] 20-01-PLAN.md: Wave 1, plugin contracts, presentation skeleton, package pins and wiring
- [ ] 20-02-PLAN.md: Wave 2, presentation state extraction and caller migration
- [ ] 20-03-PLAN.md: Wave 3, Avalonia native head, accessibility and headless tests
- [ ] 20-04-PLAN.md: Wave 4, AT-SPI2 evidence, architecture and open-question records

### Phase 21: Parity Surface
**Goal**: Session chat, tool approvals and steering work on the shared presentation layer, and every RCL and MAUI feature in this surface has a test before its old head is removed.
**Requirements**: ADE-01, ADE-02, ADE-03, XP-10
**Depends on**: Phase 20
**Mac channel**: No. The parity tests run headless on the Linux host and the CI runners; no platform signing is involved.
**UI hint**: yes
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
**Success Criteria** (what must be TRUE):
  1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: gate run at phase HEAD.
  2. apps/OmpOS.App and its test projects are removed, and a repository search finds no reference to apps/OmpOS.App in the solution, project or CI files. Verify: search output in the phase record and the gate.
  3. src/OmpOS.UI and its test projects are removed only when no project in the solution references them. src/OmpOS.UI stays because the Blazor web head uses it; its removal is tracked under gate G-WEB, not under XP-01. Verify: repository search output and the gate.
  4. The gate is green at the phase HEAD. Verify: gate run at phase HEAD.
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
