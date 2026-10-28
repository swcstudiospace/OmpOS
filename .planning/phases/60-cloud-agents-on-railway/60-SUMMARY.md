# Phase 60 Summary: Cloud Agents on Railway

Status (recorded 2026-10-28): criterion 1 is met as recorded runs; criteria 2 and 3 are Partial; criterion 4 is Blocked: nothing of OmpOS is deployed on Railway. The worker bridge, the control plane in the API, the Railway client, the heads' pages and the desktop host are built and tested against fakes and recorded fixtures. No live Railway call, no live cloud agent run and no live GitHub call has happened, and the worker still cannot clone a repository, so no agent could do work on one. PX-17 is Partial. Nothing ran on Windows, macOS or Android; no live model turn was run (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded test results (recorded 2026-10-28, orchestrator): Api.IntegrationTests 1027/1027, Coordination 32 and Sessions.Postgres 58 passed on the final tree with throwaway podman services; AgentWorker 153, Sandbox 329, Acp 201, Presentation 2303, Native 2012 (2 skipped), Contracts 137, Architecture 46 passed on the final tree before the last wiring. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. Railway: nothing of OmpOS is deployed; the project "Ultrathink" has TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only (read-back by the orchestrator, recorded 2026-10-28).

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Worker bridges `omp acp` over an authenticated WebSocket; the API holds records durably with caps, idle sweeper and redaction; loopback tests with a fake omp, JWT tests, canary tests | Met (recorded) | `AcpBridgeProtocolTests`, `OmpProcessLifecycleTests` (fake omp script), `WorkerAuthenticationTests` (missing token 401, `TokenExpiresAsTheWorkerClockAdvances`, theory rows `wrong-audience`, `wrong-issuer`, subject mismatch 403), `WorkerJwtValidatorTests`, `AcpWebSocketTransportWorkerTests`, `WorkerSecretHandlingTests`, `WorkerSecretRedactorTests` (AgentWorker run); `CloudAgentLifecycleTests` (caps, `IdleSweep_IdlesAnInactiveAgentAndCountsComputeSeconds`, `IdleSweep_SkipsAnAgentWhoseRunIsInFlight`), `CloudSecretRedactorTests`, `CloudSecretTests`, `InMemoryCloudAgentStoreTests` (Api.IntegrationTests run); `PostgresCloudAgentStoreTests` (Sessions.Postgres run against a throwaway PostgreSQL). The cloud endpoints are off unless `Cloud:Enabled`; `Program.cs` calls `AddCloudAgentServices` and `MapCloudAgentEndpoints`. Caveats: the secret store is in memory (lost on restart, one instance); the worker has no checkout bootstrap (Partial below) |
| 2 | Railway client provisions, wakes and idles worker services through verified GraphQL operations; first live provisioning recorded after schema introspection | Partial | Over recorded fixtures: `RailwayCloudEnvironmentClientTests`, `RailwayWakeTests`, `RailwayErrorMappingTests`, `RailwayOptionsValidationTests`, `RailwayServiceLabelTests`, `StaticPoolEnvironmentClientTests`, `WorkerReadinessProbeTests`, `CloudSessionHostTests` inside the passing Sandbox run (`RailwayFixtureHandler` replays the responses). Not met: no live provisioning and no schema introspection record: `docs/runbooks/evidence/` holds no Railway introspection file (the only Railway record is `railway-ompos-api-2026-10-21.md`, the earlier API service), and ADR-0020 D8 lists unverified operations (below). The 13 operations marked VERIFIED in `RailwayOperations` are verified against Railway's documentation pages, not against the live schema |
| 3 | Both heads list cloud agents, create one with repositories, a branch and secrets, and show the pull request; view model and bUnit tests | Partial | Desktop: Settings "Cloud agents" page (`CloudSettingsPage`, `CloudAgentsPageView`, `NewCloudAgentView`), list with status, wake, stop, delete, send, per-repository Create or Open pull request, hand-off; `CloudAgentsViewModelTests`, `NewCloudAgentModelTests`, `CloudAgentDetailsModelTests`, `CloudHandoffTests` (Presentation run), `CloudAgentsPageViewTests`, `NewCloudAgentViewTests`, `CloudHandoffViewTests`, `ApiCloudAgentsHostTests` (Native run); the rail rows of cloud agents come from `CloudAgentRailMap`. Web head: the Cloud agents Settings page is information only (`WebInfoPages.Cloud`: "this page has nothing to switch"); it lists and creates nothing and no bUnit test covers a cloud agent page. The web agent list shows cloud sessions only when the service reports them |
| 4 | Staged Railway deployment of the worker and the API, health-checked and recorded with version and state, or the exact blocker | Blocked | Nothing deployed (Railway read-back, recorded 2026-10-28). The blockers as the files state them: `docs/runbooks/cloud-agents.md` "What is verified and what is blocked": "Live Railway (create, sleep, wake, delete): Blocked: deploys paused, GraphQL operations unverified (ADR-0020 D8), a Railway API token is needed"; "Public exposure: Blocked by OD-03 (identity provider)"; "Model credential in workers: Blocked by HOST-04 and OQ-71"; the worker has no checkout bootstrap, so a live agent has no repository. `docs/runbooks/hosting.md`: the `ompos-api` service does not exist ("Not yet performed"), no public domain until `Api__Auth__Authority` and `Api__Auth__Audience` are set (OD-03, HOST-02). OQ-100 (default taken 2026-10-27): staged deploys of the API and the worker on private networking only, no public domain for an unauthenticated API, no browser or desktop streaming; Railway's acceptable-use text on remote desktops is unresolved (OQ-73). No committed record of a worker image build exists: `podman build -f deploy/worker.Containerfile -t ompos-agent-worker:local .` is the command that would show one |

## What the phase delivered

- The worker service and its image: `/acp` WebSocket bridge, JWT checks, frame guard, health endpoints, secret redaction, entrypoint with volume ownership (`60-01-PLAN.md`, units 1 to 3).
- The Railway client over the GraphQL API with an explicit VERIFIED or UNVERIFIED status per operation, a static-pool client, and a readiness probe (unit 4).
- The control plane in the API and the Postgres store: create with repositories, branch, secrets by name and idle time; caps; idempotency keys; owner scoping; wake, stop, delete, prompt, pull request (GitHub App or PAT); orphan scanner; metrics (units 5 and 6). Contract version is now 1.5.0 (`src/OmpOS.Contracts/ContractVersion.cs`).
- Hooks inside cloud workers (unit 7): worker side only; see Partial.
- The desktop Cloud agents page, form, hand-off and host; rail rows for cloud agents (units 8 and 9).

## Deviations

- The route is `/acp` with the session as the token subject (no session in the URL); a duplicate subject is HTTP 409 before the upgrade.
- The worker uses `Worker__*` configuration keys (repo style) where the brief said `OMPOS_*`.
- Secrets travel as Railway service variables of the session's service (ADR-0020 D7), not the 0600 file of the ssh path.
- The hooks addendum is `docs/decisions/addendum-ADR-0020-hooks.md` (the `ADR-*` glob of `DecisionRecordTests` would otherwise demand a second number).
- The `agents` compose profile of the first commit is no longer in `deploy/compose.yaml`; the podman commands are in the header of `deploy/worker.Containerfile`.
- `docs/runbooks/hosting.md` still says "Worker. None" for the hosted API topology; it predates the cloud worker and was not updated by this phase.
- The form applies one branch and one base branch to every repository (the API takes them per repository).

## Partial

- Worker-side bootstrap is not built: no clone of the repositories, no branch, no run of `OMPOS_ENVIRONMENT`, no git credential helper, no push guard, no commit-message redaction (`docs/runbooks/cloud-agents.md`; no `OMPOS_REPOSITORIES` reader exists in `services/OmpOS.AgentWorker` or `deploy`). The control plane sets the working directory to a checkout that nothing creates, so `session/new` would fail until the worker owns that step.
- Cloud hooks are not wired end to end: no `ompos_hooks_consent` claim is minted by the API, `AcpAgentSession.HookNotificationReceived` has no consumer in the API, `Worker__Hooks__*` is not set by the control plane and the image does not carry `plugins/ompos-hooks-omp`; until then every cloud session reports `untrusted` and runs no project hook (the safe default).
- Git token for a worker awake longer than an hour is stale (installation token, delivered once at provisioning).
- GitHub only: GitLab, Bitbucket and Azure DevOps are not supported.
- The web head has no cloud agent list or form (criterion 3).
- Orphan workers cannot be listed through `ICloudEnvironmentClient` (the interface has no list operation); the scanner works through the `ICloudWorkerCatalog` port, whose `SupportsInventory` is false for the Railway and static clients (CloudControlPlane report), and orphan removal is off by default.
- Deleting a cloud agent keeps its transcript (`ISessionRepository` has no delete).

## Blocked (OQ/OD)

- Criterion 4 and the live half of criterion 2: a Railway API token, the paused deploys, OQ-100 (staged private deploys only), OQ-73 (acceptable use), OD-03 (no sign-in authority, so no public API) and HOST-04 (no model credential). Exact unverified Railway items (ADR-0020 D8 and `RailwayOperations`): `serviceCreate.input.environmentId` (not sent); return types of `serviceInstanceUpdate`, `serviceInstanceDeployV2`, `variableCollectionUpsert`, `volumeInstanceUpdate`, `volumeDelete`, `serviceInstanceRedeploy`; newest-first order of `deployments(first:1)` and the statuses `INITIALIZING`, `REMOVING`, `NEEDS_APPROVAL`; where `traceId` sits in an error; whether `serviceInstanceDeployV2` starts an image-sourced service; whether the private DNS name equals the service name (`http://{label}.railway.internal:8080/`); the per-environment volume lookup; and that no stop-service operation is used, so `RequestIdle` cannot command sleep.
- The Railway Sandbox tier (`sandboxCreate`) stays fail-closed (`RailwaySwarmProvider`; Q-SB-RAILWAY).

## Known limits

- Idle depends on Railway Serverless (`sleepApplication`) acting on its own; the client cannot command it. Whether `deploymentStop` keeps the volume, bills nothing and can be woken by `deploymentRestart` is UNVERIFIED and the operation is not used.
- The Railway `BootGeneration` counter is lost when the control plane restarts.
- Cancel and the run tracker are per API instance; a cancel on another instance answers `409 no_run_in_flight`.
- The environment file reader treats a bare `.` build path as the repository root; this is UNVERIFIED against Cursor's published schema (`curl https://www.cursor.com/schemas/environment.schema.json`).
- A 256-hash consent token is about 23 KiB against Kestrel's 32 KiB header limit: UNVERIFIED.
- The new-agent form has no model or environment lists (no API endpoint exists for them) and the details page shows "No history yet."
- `docs/runbooks/cloud-agents.md` still reads "Not run by the author" in its table; the recorded runs above supersede that wording for the test rows.
