# Phase 60 Verification: Cloud Agents on Railway

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `60-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The tests are hermetic: a fake omp script, a loopback WebSocket server, an in-process fake worker and fake GitHub, and recorded Railway responses. The AgentWorker and entrypoint tests need a POSIX shell with `head`, `tr`, `sed` and `sleep` and skip elsewhere. The Postgres tests use a throwaway PostgreSQL (podman, or `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`). No live Railway, GitHub or model call is made by these tests.

Recorded totals (recorded 2026-10-28): the `./bootstrap.sh` gate on commit e24e6851 exited 0 (`docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`): `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; the log lists `Autonogrammer.AgentWorker.Tests`, `Autonogrammer.Sandbox.Tests`, `Autonogrammer.Sessions.Postgres.Tests` and `Autonogrammer.Api.IntegrationTests` as passed. Separate runs on the final tree with throwaway podman services: Api.IntegrationTests 1027/1027, Coordination 32, Sessions.Postgres 58; earlier per-project runs: AgentWorker 153, Sandbox 329, Acp 201, Presentation 2303, Native 2012 (2 skipped), Contracts 137, Architecture 46. The commands below were not re-run by the author of this record. GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at "Start test services" (OQ-168), so they add no test result.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (worker) | `dotnet test tests/Autonogrammer.AgentWorker.Tests -c Release` | The `/acp` bridge pumps frames between a WebSocket and a fake `omp acp`; a missing, expired, wrong-audience, wrong-issuer or unsigned token is refused; a pinned worker serves only its session; a subject mismatch is 403; frames are one JSON object; canary secrets reach no log, no transcript frame and no argument list; the entrypoint script sets volume ownership | Inside the passing AgentWorker run (153) and the AgentWorker line of the e24e6851 gate log |
| 1 (live image) | `podman build -f deploy/worker.Containerfile -t autonogrammer-agent-worker:local .` then `node scripts/cloud/worker-live-check.mjs` | The built worker image refuses five bad-token cases (401 or 403) before any upgrade, answers health and readiness, accepts a valid WebSocket, carries `initialize` and `session/new` to a real `omp` 18.12.5, refuses a second connection for one session (409) and logs no key or token material. Does not prove a prompt turn (OD-07), a Railway deploy, a control plane minting tokens, a worker clone or hooks | 14 of 14 checks passed, recorded 2026-10-28: `docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md` |
| 1 (transport) | `dotnet test tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpWebSocketTransportTests*"` | Connect failure, drop, close code, oversized frame and bearer handling of `AcpWebSocketTransport` against a loopback listener | Inside the passing Acp run (201) |
| 1 (API) | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-namespace "*Cloud*"` | Create with caps and idempotency, ownership, wake, stop, delete, idle sweep (an agent with a run in flight is skipped), orphan handling, secret canaries across responses, events and logs, pull requests through a fake GitHub, token minting and expiry, environment file reading, cloud disabled by default | Inside the passing Api.IntegrationTests run (1027/1027) |
| 1 (store) | `dotnet test tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*PostgresCloudAgentStoreTests*"` | The durable store: atomic caps, unique idempotency key, update and activation against a real PostgreSQL | Inside the passing Sessions.Postgres run (58) |
| 2 | `dotnet test tests/Autonogrammer.Sandbox.Tests -c Release -- --filter-class "*RailwayCloudEnvironmentClientTests*" --filter-class "*RailwayWakeTests*" --filter-class "*RailwayErrorMappingTests*" --filter-class "*RailwayOptionsValidationTests*" --filter-class "*RailwayServiceLabelTests*" --filter-class "*StaticPoolEnvironmentClientTests*" --filter-class "*WorkerReadinessProbeTests*" --filter-class "*CloudSessionHostTests*"` | Provision, wake (60 s window at 2 s steps), idle request, delete, error mapping (401, 429, GraphQL errors, transport, oversized body) over replayed Railway responses; the static pool client; the session host over the v2 client | Inside the passing Sandbox run (329) |
| 2 (live) | None exists. To close it: capture the live schema with an owner-supplied Railway API token (`curl -H "Authorization: Bearer $RAILWAY_API_TOKEN" https://backboard.railway.com/graphql/v2` with an introspection query), then run one provisioning and record it under `docs/runbooks/evidence/` | The UNVERIFIED rows of ADR-0020 D8 | No recorded run. No introspection file exists in `docs/runbooks/evidence/` |
| 3 | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*CloudAgentsViewModelTests*" --filter-class "*NewCloudAgentModelTests*" --filter-class "*CloudAgentDetailsModelTests*" --filter-class "*CloudAgentStatusMapTests*" --filter-class "*CloudHandoffTests*" --filter-class "*CloudHandoffModelTests*" --filter-class "*CloudCopyTests*" --filter-class "*CloudInputValidatorTests*"` and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CloudAgentsPageViewTests*" --filter-class "*NewCloudAgentViewTests*" --filter-class "*CloudHandoffViewTests*" --filter-class "*ApiCloudAgentsHostTests*"` | The list and refresh, the form (Create disabled until every entry is valid and the cost sentence is confirmed; secret fields take names only and a pasted value is cleared; https repositories only), wake, stop, delete with confirmation, pull request creation and opening, the not-configured state, the mapping of the API routes and errors | Inside the passing Presentation and Native runs. No web head test: the web page is information only |
| 4 | Railway read-back: `railway status` or the Railway MCP `describe-environment` for the project | A staged worker and API with version and state | Partial. Worker half Met (update 2026-10-28, after the release): `list-deployments` shows deployment `b6e5ac9d-831c-49ea-b51c-78e59271de05` of the service `ompos-worker` (project `ompos-staging`, environment `staging`) `SUCCESS`; `describe-service` shows the Dockerfile path, the `/healthz` health check, no domain and no TCP proxy; `railway ssh` probes: `/healthz` 200, `/readyz` 200, `/acp` without a token 401, omp 18.12.5, a JWT-gated `initialize` and `session/new` through the bridge (`docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`). API half Blocked (OD-03). Read-back of the same day before the staged deploy: the project "Ultrathink" holds TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only |

## Beyond the criteria

| Item | Command | What it proves | Recorded result |
|------|---------|----------------|-----------------|
| Hooks in cloud workers | `dotnet test tests/Autonogrammer.AgentWorker.Tests -c Release -- --filter-class "*CloudHooksWorkerTests*" --filter-class "*CloudHookFilterTests*" --filter-class "*HooksConsentSnapshotReaderTests*"` | The worker runs consented project hooks through the shared bridge, refuses an invalid consent claim as a whole, reports `untrusted` without a claim, and sends `_autonogrammer/hook` frames; the omp extension is never run (the fake omp posts nothing, the tests post to the bridge themselves) | Inside the passing AgentWorker run |
| Contracts | `dotnet test tests/Autonogrammer.Contracts.Tests -c Release` | The cloud wire types round trip; contract version 1.5.0 | Inside the passing Contracts run (137) |
| Worker image | `podman build -f deploy/worker.Containerfile -t autonogrammer-agent-worker:local .` | The image builds with the pinned base images, Bun and omp digests | Built and run, recorded 2026-10-28 (see criterion 1, live image); the locked restore half is covered by the gate restore |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md` | The live check of the worker image under podman: 14 of 14 checks (method, table, what it proves and does not) |
| `docs/runbooks/cloud-agents.md` | Operator and user guide; the table "What is verified and what is blocked" |
| `docs/decisions/ADR-0020-cloud-agents-on-railway.md` | Decisions D1 to D8, including the operation table with VERIFIED and UNVERIFIED rows |
| `docs/decisions/addendum-ADR-0020-hooks.md` | Hooks in cloud workers |
| `docs/runbooks/hosting.md` | The hosted API definition; the deploy checklist is OD-03 gated and was not performed |
| `docs/runbooks/evidence/railway-autonogrammer-api-2026-10-21.md` | The earlier Railway record of the API service (the refusal of 2026-10-21); not a record of cloud workers |
| `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md` | The staged private worker on Railway: what exists, how it was made, the probes, what it proves and does not prove, the teardown steps |

## Known limits of this verification

- No deployment of the API, no schema introspection, no control-plane provisioning, no live cloud agent, no GitHub call, no model turn (OD-07). The worker image ran under podman, and after the release one staged private worker ran on Railway (health check and a handshake only; `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`).
- The worker has no checkout bootstrap, so no end-to-end run could exist even with a deployed control plane.
- The Windows, macOS and Android heads were not run.
- The `./bootstrap.sh` log of commit e24e6851 is committed; per-project logs of the earlier runs are not.
