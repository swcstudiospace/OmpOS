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
| TD-42 | Phase 1 | Planned |
| TD-43 | - | Closed |
| TD-44 | Phase 3 | Planned |
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
