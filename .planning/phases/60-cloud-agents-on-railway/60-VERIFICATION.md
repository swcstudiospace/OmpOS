# Phase 60 Verification: Cloud Agents on Railway

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `60-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The tests are hermetic: a fake omp script, a loopback WebSocket server, an in-process fake worker and fake GitHub, and recorded Railway responses. The AgentWorker and entrypoint tests need a POSIX shell with `head`, `tr`, `sed` and `sleep` and skip elsewhere. The Postgres tests use a throwaway PostgreSQL (podman, or `OMPOS_TEST_POSTGRES_CONNECTION`). No live Railway, GitHub or model call is made.

Recorded totals (recorded 2026-10-28, orchestrator): Api.IntegrationTests 1027/1027, Coordination 32 and Sessions.Postgres 58 passed on the final tree with throwaway podman services; AgentWorker 153, Sandbox 329, Acp 201, Presentation 2303, Native 2012 (2 skipped), Contracts 137, Architecture 46 passed on the final tree before the last wiring. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The commands below were not re-run by the author of this record.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (worker) | `dotnet test tests/OmpOS.AgentWorker.Tests -c Release` | The `/acp` bridge pumps frames between a WebSocket and a fake `omp acp`; a missing, expired, wrong-audience, wrong-issuer or unsigned token is refused; a pinned worker serves only its session; a subject mismatch is 403; frames are one JSON object; canary secrets reach no log, no transcript frame and no argument list; the entrypoint script sets volume ownership | Inside the passing AgentWorker run (153) |
| 1 (transport) | `dotnet test tests/OmpOS.Harness.Acp.Tests -c Release -- --filter-class "*AcpWebSocketTransportTests*"` | Connect failure, drop, close code, oversized frame and bearer handling of `AcpWebSocketTransport` against a loopback listener | Inside the passing Acp run (201) |
| 1 (API) | `dotnet test tests/OmpOS.Api.IntegrationTests -c Release -- --filter-namespace "*Cloud*"` | Create with caps and idempotency, ownership, wake, stop, delete, idle sweep (an agent with a run in flight is skipped), orphan handling, secret canaries across responses, events and logs, pull requests through a fake GitHub, token minting and expiry, environment file reading, cloud disabled by default | Inside the passing Api.IntegrationTests run (1027/1027) |
| 1 (store) | `dotnet test tests/OmpOS.Sessions.Postgres.Tests -c Release -- --filter-class "*PostgresCloudAgentStoreTests*"` | The durable store: atomic caps, unique idempotency key, update and activation against a real PostgreSQL | Inside the passing Sessions.Postgres run (58) |
| 2 | `dotnet test tests/OmpOS.Sandbox.Tests -c Release -- --filter-class "*RailwayCloudEnvironmentClientTests*" --filter-class "*RailwayWakeTests*" --filter-class "*RailwayErrorMappingTests*" --filter-class "*RailwayOptionsValidationTests*" --filter-class "*RailwayServiceLabelTests*" --filter-class "*StaticPoolEnvironmentClientTests*" --filter-class "*WorkerReadinessProbeTests*" --filter-class "*CloudSessionHostTests*"` | Provision, wake (60 s window at 2 s steps), idle request, delete, error mapping (401, 429, GraphQL errors, transport, oversized body) over replayed Railway responses; the static pool client; the session host over the v2 client | Inside the passing Sandbox run (329) |
| 2 (live) | None exists. To close it: capture the live schema with an owner-supplied Railway API token (`curl -H "Authorization: Bearer $RAILWAY_API_TOKEN" https://backboard.railway.com/graphql/v2` with an introspection query), then run one provisioning and record it under `docs/runbooks/evidence/` | The UNVERIFIED rows of ADR-0020 D8 | No recorded run. No introspection file exists in `docs/runbooks/evidence/` |
| 3 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*CloudAgentsViewModelTests*" --filter-class "*NewCloudAgentModelTests*" --filter-class "*CloudAgentDetailsModelTests*" --filter-class "*CloudAgentStatusMapTests*" --filter-class "*CloudHandoffTests*" --filter-class "*CloudHandoffModelTests*" --filter-class "*CloudCopyTests*" --filter-class "*CloudInputValidatorTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*CloudAgentsPageViewTests*" --filter-class "*NewCloudAgentViewTests*" --filter-class "*CloudHandoffViewTests*" --filter-class "*ApiCloudAgentsHostTests*"` | The list and refresh, the form (Create disabled until every entry is valid and the cost sentence is confirmed; secret fields take names only and a pasted value is cleared; https repositories only), wake, stop, delete with confirmation, pull request creation and opening, the not-configured state, the mapping of the API routes and errors | Inside the passing Presentation and Native runs. No web head test: the web page is information only |
| 4 | Railway read-back: `railway status` or the Railway MCP `describe-environment` for the project | A staged worker and API with version and state | Blocked. Read-back recorded 2026-10-28: nothing of OmpOS is deployed; the project "Ultrathink" holds TimescaleDB, Dragonfly, GreptimeDB and two tailscale services |

## Beyond the criteria

| Item | Command | What it proves | Recorded result |
|------|---------|----------------|-----------------|
| Hooks in cloud workers | `dotnet test tests/OmpOS.AgentWorker.Tests -c Release -- --filter-class "*CloudHooksWorkerTests*" --filter-class "*CloudHookFilterTests*" --filter-class "*HooksConsentSnapshotReaderTests*"` | The worker runs consented project hooks through the shared bridge, refuses an invalid consent claim as a whole, reports `untrusted` without a claim, and sends `_ompos/hook` frames; the omp extension is never run (the fake omp posts nothing, the tests post to the bridge themselves) | Inside the passing AgentWorker run |
| Contracts | `dotnet test tests/OmpOS.Contracts.Tests -c Release` | The cloud wire types round trip; contract version 1.5.0 | Inside the passing Contracts run (137) |
| Worker image | `podman build -f deploy/worker.Containerfile -t ompos-agent-worker:local .` | The image builds with the locked restore | No recorded run |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/cloud-agents.md` | Operator and user guide; the table "What is verified and what is blocked" |
| `docs/decisions/ADR-0020-cloud-agents-on-railway.md` | Decisions D1 to D8, including the operation table with VERIFIED and UNVERIFIED rows |
| `docs/decisions/addendum-ADR-0020-hooks.md` | Hooks in cloud workers |
| `docs/runbooks/hosting.md` | The hosted API definition; the deploy checklist is OD-03 gated and was not performed |
| `docs/runbooks/evidence/railway-ompos-api-2026-10-21.md` | The earlier Railway record of the API service; not a record of cloud workers |

## Known limits of this verification

- No Railway deployment, no schema introspection, no live provisioning, no live cloud agent, no GitHub call, no model turn (OD-07).
- The worker has no checkout bootstrap, so no end-to-end run could exist even with a deploy.
- The Windows, macOS and Android heads were not run.
- No log of the per-project runs is committed; the `./bootstrap.sh` log is the orchestrator's.
