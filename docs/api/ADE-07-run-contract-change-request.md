# ADE-07 run contract change request

- Status: Implemented 2026-10-26 as a contract change (version 1.2.0) under the program direction to complete Phase 26, with the deviations in section 9. The owner decisions of section 7 are not recorded as answered (OQ-70, OQ-85).
- Date: 2026-10-22
- Requirements: ADE-07 (cloud runs with status and reviewable output), ADE-05 (run status filter), ADE-08 (remote control of runs from another device)
- Risk: R-4 (hosted-API contract for cloud runs), `docs/plans/ade-harness-plan.md`
- Gate: Phase 26 success criteria 4 and 5 (`.planning/ROADMAP.md`)
- Blocked by: OD-03 (identity provider for the deployed API, `docs/handoff.md` decision 4), and ADE-06 worktree wiring (section 4)
- Scope: the hosted API contract in `services/Autonogrammer.Api` and `docs/api/openapi.json`. No endpoint is implemented by this document.

## 1. Request

R-4 requires that any endpoint missing for cloud runs becomes a contract change request before Phase 26 starts. This document is that request. It asks the owner to approve a run resource on the hosted API with five operations: create, status, diff, transcript and cancel. It also asks the owner to decide the open points in section 7.

## 2. Current contract

The committed contract `docs/api/openapi.json` lists five paths and no others:

| Method | Path | Current behaviour |
|---|---|---|
| POST | `/sessions` | Creates a session owned by the caller's token subject. Returns 201 with the session. A body `ownerId` is refused with 400. |
| GET | `/sessions` | Lists the caller's own sessions, oldest first. Another owner's sessions are not listed. |
| GET | `/sessions/{id}` | Returns one session, including its messages and events. Another owner's session returns 404. |
| POST | `/sessions/{id}/prompts` | Submits a prompt. Returns 202 with `PromptAcceptedResponse`. Idempotency-Key supported. |
| POST | `/sessions/{id}/tool-approvals/{requestId}` | Records `allow` or `deny`. 204 when recorded; 403 when an allow exceeds the ceiling; 409 when not pending. |
| GET | `/sessions/{id}/events` | Server-sent event stream, resumable by `Last-Event-ID` or `?since=`. |

Ownership, as implemented: the session owner is the subject claim of the validated bearer token. Every session route (`GET /sessions/{id}`, prompts, tool approvals and the event stream) resolves the session for the caller, and a session owned by another subject gets the same 404 as an unknown id (`services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs`, `FindOwnedSessionAsync`).

There is no run resource, no run endpoint, no diff endpoint, no transcript endpoint and no cancel endpoint. This was checked against `docs/api/openapi.json` (`paths` keys) and the route map in `services/Autonogrammer.Api/Endpoints`.

What exists that a run contract would build on:

- `SessionStatus` (`src/Autonogrammer.Contracts/Sessions/SessionStatus.cs`): `Idle`, `Running`, `AwaitingToolApproval`, `Completed`, `Failed`, `Cancelled`. There is no `Queued` value.
- `Cancelled` is set internally by `PromptRunner` and `SessionRunActivities` on cancellation. No HTTP operation can request it.
- Stream event kinds (`StreamEventPayloadConverter`): `text_delta`, `ui_block`, `error`, `diagnostic`, `completed`, `unknown`. No approval-request kind exists in the stream. An approval is visible as session status `AwaitingToolApproval`.
- The event stream ends on the terminal `completed` or `error` event, which gives a remote client a completion signal.

So a session can be read for its messages and events, which is a transcript of a kind, but nothing is scoped to a run and nothing is retrievable as a diff.

## 3. Needed contract

Paths are proposed as session-scoped, so that the existing owner and approval rules attach to a run through its session. Owner decides the path style in section 7.

| Method | Path | Behaviour | Success | Errors |
|---|---|---|---|---|
| POST | `/sessions/{id}/runs` | Creates a run for a prompt and starts it in a worktree (section 4). Status is `queued`. | 202 with the run, `Location` to the run | 400, 401, 404, 413, 415, 429 |
| GET | `/sessions/{id}/runs` | Lists the session's runs, filterable by `status`. Used by the Agents window (ADE-05). | 200 | 401, 404 |
| GET | `/sessions/{id}/runs/{runId}` | Returns the run with `status`: `queued`, `running`, `completed`, `failed` or `cancelled`. | 200 | 401, 404 |
| GET | `/sessions/{id}/runs/{runId}/diff` | Returns the unified diff of the run's worktree against its base. Only for a completed run. | 200 `text/plain` | 401, 404, 409 when not completed |
| GET | `/sessions/{id}/runs/{runId}/transcript` | Returns the run's messages and events as JSON. Only for a completed run. | 200 `application/json` | 401, 404, 409 when not completed |
| POST | `/sessions/{id}/runs/{runId}/cancel` | Requests cancellation of a queued or running run. Idempotent: a cancelled run returns the same response. | 202 | 401, 404, 409 when the run is already completed, failed or cancelled |

Notes on the contract:

- Status values: ADE-07 names `queued`, `running` and `completed`; ADE-05 also needs `failed` and `cancelled`. This request proposes all five so that one status set serves both. Owner decides (section 7).
- The run status is a projection of `SessionStatus` plus a run record. `Queued` is new and exists only on the run record, not on `SessionStatus`.
- Tool approvals for a run keep the existing `POST /sessions/{id}/tool-approvals/{requestId}` path. No second approval endpoint is added. A remote approval goes through the same ceiling check (403).
- Notifications (ADE-08): the events stream carries the approval-request state and the terminal event. A second device receives approval and completion signals by reading `GET /sessions/{id}/events`. Push delivery to a device is not part of this request and needs its own change request.
- Additive change: every operation in section 3 is new. Nothing in section 2 changes shape. Under the versioning policy in ADR-0008 this is a minor change, though ADR-0008 covers plugin contracts and does not yet cover the HTTP contract (section 7).

## 4. Dependency on worktree wiring

ADE-07 diff retrieval and Phase 26 criterion 3 depend on runs executing in per-run git worktrees. That wiring does not exist:

- `src/Autonogrammer.Harness.Worktrees` (`WorktreeManager`) is referenced only by `tests/Autonogrammer.Harness.Worktrees.Tests` and by the architecture test project (`tests/Autonogrammer.ArchitectureTests`).
- `services/Autonogrammer.Api` does not reference it. `DirectSessionRunner` and `PromptRunner` do not create a worktree.

Consequences for this contract:

- The diff endpoint has no defined base or source until a run owns a worktree. The diff is taken as `git diff` of the run's worktree against the base commit recorded when the run was created.
- `cancel` and run close must remove the worktree (Phase 26 criterion 3). Until the wiring exists, cancel can change status only.
- The hosted API runs on a server, so the worktree root is a server path. Its location, its owner and its cleanup policy must be set by the operator before any hosted run starts. This is an open point (section 7).

Phase 26 criterion 4 cannot pass until the wiring lands. The contract can be approved first, but the diff and run-execution acceptance tests (section 6) cannot pass before it.

## 5. Security rules

### 5.1 Authentication

- Every run operation requires an authenticated user. Runs are not `AllowAnonymous`. The fallback policy (`services/Autonogrammer.Api/Program.cs`) applies to them by default, and the new operations must not opt out.
- Outside Development the development principal is refused at startup (`ApiAuthOptionsValidator`). Inside Development it is off unless `Api:Auth:AllowDevelopmentPrincipal` is set. A run operation must answer 401 without a bearer token in both environments.
- OD-03 gate: the hosted run contract and remote control (ADE-08) MUST NOT be exposed publicly until an identity provider is configured for the deployed API (`docs/handoff.md` decision 4 and the deploy-blocker entry). Until OD-03 closes, the run endpoints are reachable only inside the private network.

### 5.2 Ownership (found while writing this request; resolved 2026-10-22, commit 9119f7e)

The session contract had three gaps before commit 9119f7e, and all three are now closed.

- `POST /sessions` took `ownerId` from the request body in preference to the authenticated subject. A body `ownerId` is now refused with 400, and the owner is always the caller's subject.
- `GET /sessions` listed every session in the registry. It now lists only the caller's sessions.
- `GET /sessions/{id}` and the other session operations did not compare the owner with the caller. They now return 404 for another owner's session, the same as for a missing one.

Run operations keep these rules: derive the owner from the validated token only, and return 404 (not 403) for a run or session the caller does not own, so that run identifiers are not revealed.

### 5.3 Other rules

- Diff and transcript contain user code and prompts. They are returned only to the owner and are not written to logs.
- Run creation and prompts use the rate-limiter policy pattern already used for prompts and session creation (`SessionEndpoints.PromptsPolicy`, `CreationPolicy`).
- A plugin or MCP tool call inside a run still goes through the ACP approval path (R-10). A run does not auto-approve anything.

## 6. Acceptance tests the contract must pass

Each test runs in the gate unless marked otherwise. Test names are proposed.

| ID | Test | Layer | Assertion |
|---|---|---|---|
| AT-01 | `RunContractOpenApiTests` | Contract (`docs/api/openapi.json`) | Every path and method in section 3 is present; the status enum lists exactly the five values; no run path has `security: []` or `AllowAnonymous`. R-4 requires this check. |
| AT-02 | `RunLifecycleTests` | Integration (`Autonogrammer.Api.IntegrationTests`) | POST creates a run in `queued`; the status moves to `running` and then `completed`, and each transition is observed through GET. |
| AT-03 | `RunDiffTests` | Integration, temporary git repository | The diff of a completed run contains that run's file changes and no other run's changes. GET diff of a non-completed run returns 409. |
| AT-04 | `RunTranscriptTests` | Integration | The transcript of a completed run lists its messages and events in order. A non-completed run returns 409. |
| AT-05 | `RunCancelTests` | Integration | Cancel moves a running run to `cancelled`; a second cancel returns the same response; cancel on a completed run returns 409. |
| AT-06 | `RunWorktreeIsolationTests` | Integration, temporary git repository (ADE-06, Phase 26 criterion 3) | Two runs in one repository get different worktrees (`git worktree list`); the worktree is removed when the run closes; the main checkout is unchanged. Requires section 4 wiring. |
| AT-07 | `RunAuthBoundaryTests` | Integration, production-shaped host and Development host with the development principal off | Every operation in section 3 answers 401 without a bearer token. Extends `RemoteControlAuthTests` for the run paths. |
| AT-08 | `RunOwnershipTests` | Integration | A token for user B gets 404 for user A's run, diff, transcript and cancel. Requires section 5.2 to be resolved for runs. |
| AT-09 | `RemoteControlNotificationTests` | Integration, two clients on the Linux host (Phase 26 criterion 5) | A second client sends a prompt to a local run and receives the approval-request state and the terminal `completed` event through the events stream. The run executes on the paired computer; the second client does not execute it. |
| AT-10 | `RunNotFoundTests` | Integration | Unknown run id returns 404 on each run operation. |
| AT-11 | Live cloud run (runbook, not gate) | Manual, recorded in the runbook (Phase 26 criterion 4) | One live cloud run on the hosted API reaches `completed` and its diff is retrievable. |
| AT-12 | Remote pairing run (runbook, not gate) | Manual, recorded in the runbook (Phase 26 criterion 5) | Pairing recorded; the run stays on the paired computer. |

AT-01 and the 401 cases of AT-07 can be written before any run code exists, because they check the contract and the auth boundary. The rest depend on the run implementation and on section 4.

## 7. Open decisions for the owner

1. Approve the run resource and the five operations in section 3, or amend them.
2. Path style: session-scoped (`/sessions/{id}/runs`, proposed) or top-level (`/runs`) with the owner check on each run.
3. Status set: five values (proposed, covering ADE-05 and ADE-07) or the three ADE-07 names alone.
4. Diff format: unified diff as `text/plain` (proposed) or a structured JSON file list with per-file hunks. ADE-11 (diff review, Phase 27) may need the structured form; deciding now avoids a second contract change.
5. Operator-controlled worktree root on the hosted server, its owner and its cleanup policy (section 4).
6. Ownership: resolved in commit 9119f7e (section 5.2). Owner to confirm that run endpoints return 404 for another owner's run.
7. Cross-session list of active runs for ADE-05 and ADE-08: a `GET /runs?status=` endpoint, or the session list filtered by status. Proposed: leave it out of this change and filter the session list.
8. Whether ADR-0008's version policy applies to the HTTP contract (`ContractVersion.Current` is `1.0.0`). ADR-0008 scopes plugin contracts only. Owner to decide whether the HTTP contract follows the same rule.
9. Timing: OD-03 closes before any public run endpoint. Owner to name the date or confirm that Phase 26 ships the run endpoints on the private network only.

## 8. Status

Proposed on 2026-10-22, with no endpoint, schema, test or code implemented by this document; R-4 asked for owner approval before Phase 26 starts. The endpoints were implemented on 2026-10-26 under the program direction to complete Phase 26, as described in section 9. The owner's answers to section 7 are still open.

## 9. Implementation note (2026-10-26)

The run resource exists in `services/Autonogrammer.Api` (`Endpoints/RunEndpoints.cs`) and in `docs/api/openapi.json`, with contract types in `src/Autonogrammer.Contracts/Runs`. The API is not deployed (Railway refused deploys as paused on 2026-10-21 and the refusal did not recur on 2026-10-28, but the API still waits for an identity provider, OD-03; OQ-70), so no public run endpoint exists and OD-03 is not yet needed. Tests: `CloudRunTests`, `CloudRunOwnerScopeTests`, `CloudRunOpenApiTests`, `RunContractJsonTests`.

Differences from this request:

- There is no list endpoint. The four operations are create, status, output and cancel.
- Diff and transcript are one `GET /sessions/{id}/runs/{runId}/output` response. A queued or running run returns its status only; a completed, failed or cancelled run returns its transcript and the harness-reported file changes.
- A file change is structured before and after text per file, not a unified patch. The worktree is removed when the session is disposed, so nothing is left to diff, and no diff algorithm was added to the API.
- Run records are held in memory per process (OQ-85).
- Cancelling a cancelled run repeats the same response; cancelling a completed or failed run returns 409.
