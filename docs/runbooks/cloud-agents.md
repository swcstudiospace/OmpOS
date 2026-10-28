---
description: "Cloud agents on Railway: what runs where, the control plane API, secrets, limits, pull requests and what is verified versus blocked."
---

# Cloud agents: user and operator guide

Status: Partial (control plane, worker bridge, Railway client, desktop Cloud host and page, and worker hooks are built, wired and tested against fakes on Linux; the worker has no clone bootstrap; the control plane does not mint the hooks consent claim; the API is not deployed on Railway and no live cloud run has happened; a staged private worker was deployed and handshaken on Railway on 2026-10-28)

Last verified: 2026-10-28

Applies to: v0.15.0

Details (recorded 2026-10-28, tree 7535859). **No live cloud run on Railway has happened**: the API is not deployed (a staged private worker is, see "Staged worker on Railway" below), the identity provider is not chosen and no model credential is configured (see "What is verified and what is blocked"). Design record: [ADR-0020](../decisions/ADR-0020-cloud-agents-on-railway.md). Research: [09-remote-cloud-plan.md](../plans/v0.15.0-research/09-remote-cloud-plan.md).

**Status of this release.**

- **Built, wired, proven against fakes.** *API:* `AddCloudAgentServices` and `MapCloudAgentEndpoints` are called in `services/Autonogrammer.Api/Program.cs`, the routes are in `docs/api/openapi.json` (11 `/cloud` paths), and `Autonogrammer.Api.IntegrationTests` (`Cloud/`: `CloudAgentLifecycleTests`, `CloudAgentAccessTests`, `CloudSecretTests`, `CloudPullRequestTests`, `CloudOrphanTests`, `CloudDisabledTests` and more, over `FakeCloudWorker` and `FakeGitHubHandler`) passed 1027 of 1027 on the final tree. *Worker:* `services/Autonogrammer.AgentWorker` bridges `omp acp` to a WebSocket (`Autonogrammer.AgentWorker.Tests`: `AcpBridgeProtocolTests`, `WorkerAuthenticationTests`, `OmpProcessLifecycleTests`, `WorkerEntrypointScriptTests`, `WorkerSecretHandlingTests`, with a fake omp). *Desktop:* `HeadServices` creates `ApiCloudAgentsHost` only when `AUTONOGRAMMER_API_BASE_URL` is a valid http or https address (the token comes only from `AUTONOGRAMMER_API_TOKEN`); Settings > Cloud, the rail rows and the `/in-cloud` hand-off use it; tests: `ApiCloudAgentsHostTests` (a scripted HTTP handler, no real API), `CloudAgentsPageViewTests`, `NewCloudAgentViewTests`, `CloudHandoffViewTests`, `HeadServicesWiringTests` (the host is null without the address).
- **Built, not wired.** The worker has no clone bootstrap: it does not check out the repositories, run `AUTONOGRAMMER_ENVIRONMENT` or enforce `AUTONOGRAMMER_PROTECTED_BRANCHES`, so a live agent has no repository on disk. Hooks in the worker (`Worker:Hooks`, see [Hooks](../reference/hooks.md#hooks-in-cloud-workers)) read their consent from the `autonogrammer_hooks_consent` claim of the worker token, and the control plane does not mint that claim: a cloud session reports `untrusted` and runs no project hook (`CloudHooksWorkerTests` prove the worker side with a test-minted token). Cloud automations start these agents only when `Automations:Enabled` and `Cloud:Enabled` are both on (see [Automations](../reference/automations.md)).
- **Not live-verified.** No Railway create, sleep, wake or delete through the control plane, and no API on Railway; the worker image itself was built and run under podman and checked with `scripts/cloud/worker-live-check.mjs` (probes, the JWT gate, a real omp 18.12.5 answering `initialize` and `session/new` through the bridge, 14 of 14 checks; [evidence](evidence/cloud-worker-container-2026-10-28/README.md)), a staged private worker was later deployed to Railway and handshaken (`initialize` and `session/new` through the bridge, no model turn; see "Staged worker on Railway" below); no live GitHub; no model turn (open question OD-07). Nothing ran on Windows, macOS or Android.

## What runs where

| Piece | Where | What it does |
| --- | --- | --- |
| Control plane | `services/Autonogrammer.Api` (`Cloud/`) | Owns the agent records, secrets, git credentials, caps and the idle sweeper. Creates a worker through `ICloudEnvironmentClient`, signs a short-lived worker token per connection, runs prompts through the worker bridge and opens pull requests. |
| Worker | `services/Autonogrammer.AgentWorker` in a container (one Railway service and one volume per agent, or a pooled worker) | Verifies the token, runs `omp acp` and bridges it to a WebSocket (`GET /acp`). It has no Railway token, no database password and no signing key. |
| Engine | `omp acp` inside the worker | The same engine as locally. Its session files live on the worker volume, so a follow-up prompt resumes with `session/load`. |
| Session log | The API's session store | An agent's events are an ordinary session whose id starts with `cloud-`. `GET /sessions/{sessionId}` and `/cloud/agents/{id}/events` read it. |

A run of a `cloud-` session never executes in the API process: the harness factory refuses to start it locally and sends it to the worker.

## Enabling it (operator)

Cloud is off by default (`Cloud:Enabled=false`): no route is mapped, the sweeper idles. Setting `Cloud:Enabled=true` makes start-up validation check the whole section; a missing key stops the host and names itself, a value is never repeated.

| Key | Meaning | Default |
| --- | --- | --- |
| `Cloud:Environment:Provider` | `Railway`, `StaticPool` or `External` (a host that registers its own `ICloudEnvironmentClient`) | required |
| `Cloud:Environment:Railway:{ApiToken,ProjectId,EnvironmentId,Image,WorkerPublicKeysPem:N}` | Railway provider; the token is a secret | required for Railway |
| `Cloud:Environment:StaticPool:Workers:N:{WorkerId,BaseUri,VolumeMountPath}` | Always-warm workers. A pool worker cannot receive per-session variables, so secrets are refused | required for StaticPool |
| `Cloud:Worker:SigningKeyPem` | ES256 (EC P-256) PKCS#8 private key. The public half goes to the worker as `Worker__Auth__PublicKeysPem__N` | required |
| `Cloud:Worker:{Issuer,TokenLifetime,WorkspaceRoot,ApprovalKinds}` | `autonogrammer-api`; at most 15 minutes (default 5); `/data/work`; edit, delete, move, execute, read, search | defaults |
| `Cloud:Limits:{MaxAgentsPerOwner,MaxConcurrentRunningPerOwner,DefaultIdleMinutes,MaxIdleMinutes,MaxComputeSecondsPerAgent,MaxSecretsPerOwner}` | caps | 5, 2, 15, 1440, 0 (no limit), 64 |
| `Cloud:Sweeper:{Interval,OrphanScanInterval,ProvisioningTimeout,OrphanCleanup}` | sweeper cadence; cleanup of unclaimed workers is off | 1 min, 10 min, 20 min, false |
| `Cloud:Git:App:{AppId,PrivateKeyPem,InstallationId}` | GitHub App (preferred) | none |
| `Cloud:Git:Pat` | Fine-grained personal access token when there is no App: long-lived and not narrowable, so weaker | none |
| `Cloud:Git:{ApiBase,AllowedHosts}`, `Cloud:Git:PullRequests:NeverDraft` | GitHub API base; repository hosts a request may name (`github.com`); never open drafts | defaults, false |
| `Cloud:Secrets:Static:NAME` | Operator-defined secrets every owner may reference | none |
| `Cloud:Environments:<id>:{Install,Start,Terminals}` | Named environment definitions an agent selects with `environmentId` | none |

Keys, tokens and secrets come from environment variables or a secret store, never from a committed file. With `Sessions:Store=Postgres` the agent records live in the `cloud_agents` table (created at start-up, idempotent); otherwise they are in process memory.

**Secrets are write-only and not encrypted at rest by Autonogrammer.** The shipped store keeps values in process memory (lost on restart, not shared between instances). A multi-instance host must supply a persistent `ICloudSecretStore` + `ICloudSecretProvider` that encrypts values with a key the operator holds. Encryption at rest is the operator's concern.

## Using it

| Call | Result |
| --- | --- |
| `POST /cloud/agents` `{name?, repositories[{url, branch?, baseBranch?}], environmentId?, secretRefs[]?, idleMinutes?, model?, mode?}` + optional `Idempotency-Key` | `202` with the agent in `provisioning`. The same key and body replays (`200`, `Idempotency-Replayed: true`); the same key with another body is `409`. `429` at a cap. |
| `GET /cloud/agents`, `GET /cloud/agents/{id}` | The caller's agents. Another owner's agent is `404`. |
| `POST /cloud/agents/{id}/prompts` `{prompt}` | `202`; the run executes on the worker. Idle agents wake first; stopped, failed and orphaned ones answer `409` with a code. |
| `GET /cloud/agents/{id}/events` | SSE with the same event kinds, ids and resume rules (`Last-Event-ID`, `?since=`) as `GET /sessions/{id}/events`. |
| `POST .../cancel` | Cancels the run in flight on this server instance (`409 no_run_in_flight` otherwise). |
| `POST .../stop`, `POST .../wake` | Stop: cancels the run and lets the worker sleep. Wake: brings the worker up in the background (`202`); poll the agent. |
| `DELETE /cloud/agents/{id}` | Removes the worker and volume (only when the `io.autonogrammer.cloud-agent` label names this agent), then the record. The session log stays: the session store has no delete. |
| `POST .../pull-request` `{title, body?, repository?, baseBranch?, draft?}` | Opens a pull request from `autonogrammer/cloud/<id>`, ready for review unless `draft:true` and `NeverDraft` is off. |
| `PUT/GET/DELETE /cloud/secrets/{name}`, `GET /cloud/secrets` | Write-only secrets. A name is an environment variable name; reserved names (`AUTONOGRAMMER_*`, `WORKER_*`, `RAILWAY_*`, `LD_*`, `PATH`, `HOME`, ...) are refused. Values are 4 to 8192 characters. |
| `GET /cloud/limits` | Caps and use, including compute seconds. |

Status values: `provisioning`, `starting`, `running`, `idle`, `stopping`, `stopped`, `failed`, `deleting`, `orphaned`, `limit_reached`. A `failed` agent carries `error.code` (for example `provider_unreachable`, `wake_timeout`, `secret_missing`, `invalid_environment_config`); a wake retries.

## Behaviour to know

- **Idle.** An agent with no activity for its idle window (default 15 minutes) goes `idle`: the provider is asked to idle the worker (Railway sleeps it by itself 5 to 10 minutes later; there is no sleep command). A prompt or a wake request brings it back. A run in flight keeps an agent awake.
- **Caps.** Per owner: 5 agents and 2 running at once. A create or wake past a cap is `429` with `agent_limit_reached` or `running_limit_reached`. The check and the write are one atomic step, so two instances cannot both pass. Awake seconds are counted (`autonogrammer.cloud.compute.seconds`); `MaxComputeSecondsPerAgent` stops an agent that used its budget (`limit_reached`).
- **Secrets.** Referenced secrets become environment variables of that agent's own worker service at provisioning, never of a shared service and never written to the volume. They are redacted (`[REDACTED]`) from run output, the stored transcript, error text, pull request titles and bodies, and logs, including a secret split across stream chunks and JSON-escaped forms. A value the agent transforms (base64, hex) is not caught. A secret deleted after provisioning stays in the worker until the agent is deleted.
- **Git.** The worker gets a token limited to the agent's repositories (contents write, pull requests write); the control plane uses narrower ones (read; pull request write). Installation tokens last an hour and are replaced five minutes before they expire. The control plane never clones or pushes. It opens a pull request only when the branch exists on the host, and never from or into a default branch.
- **Environment file.** For a single-repository agent, `.autonogrammer/environment.json` (else `.cursor/environment.json`) is read through the git host, validated and handed to the worker as `AUTONOGRAMMER_ENVIRONMENT`; a saved definition selected with `environmentId` wins. The control plane never runs it. `build.dockerfile` and `snapshot` are reported as unsupported (no Docker daemon in a Railway service).
- **Orphans.** The sweep marks an agent whose recorded worker is gone as `orphaned`. A labelled worker no record claims is reported, and removed only when `Cloud:Sweeper:OrphanCleanup` is on. Workers without the label are never touched. Listing workers needs provider support that the shipped clients do not have (see the report).

## What is verified and what is blocked

| Item | State |
| --- | --- |
| Lifecycle, caps, idempotency, ownership, idle sweep, orphan handling, secret canaries, pull requests, token minting and expiry, environment file | Written and tested against `FakeCloudEnvironmentClient`, an in-process fake worker and a fake GitHub. Ran on the final tree: `Autonogrammer.Api.IntegrationTests` 1027 of 1027, recorded 2026-10-28. |
| Postgres store | `Autonogrammer.Sessions.Postgres.Tests` 58 passed against a throwaway podman PostgreSQL on the final tree, recorded 2026-10-28. |
| Worker-side checkout, install, start, git credential helper and push guard | **Not built.** The worker bridge only runs `omp acp`; it does not clone, run `AUTONOGRAMMER_ENVIRONMENT`, or enforce `AUTONOGRAMMER_PROTECTED_BRANCHES`. Until it does, a live agent has no repository on disk. |
| Live Railway (create, sleep, wake, delete) | Blocked: the API is not deployed on Railway, GraphQL operations unverified (ADR-0020 D8), a Railway API token is needed. A staged private worker was deployed and handshaken on 2026-10-28 (see "Staged worker on Railway" below); no create, sleep, wake or delete went through the control plane. |
| Listing Railway workers (orphan workers without a record) | Not possible: the client has no list operation. |
| Public exposure | Blocked by OD-03 (identity provider). |
| Model credential in workers | Blocked by HOST-04 and OQ-71. |
| GitLab, Bitbucket, Azure DevOps | Not supported (gap against Cursor). |
| Commit-message redaction of a secret the agent typed into a commit | Not possible from the control plane; needs a worker-side `commit-msg` hook. |

## Staged worker on Railway (2026-10-28)

After the v0.15.0 release a staged agent worker was deployed to Railway and exercised: the project `ompos-staging` (environment `staging`, service `ompos-worker`, volume `ompos-worker-data` at `/data`), private (no domain, no TCP proxy), built by Railway from the released tree with `deploy/worker.Containerfile`. The record, with object ids, the steps and the teardown steps, is [evidence/railway-autonogrammer-worker-2026-10-28.md](evidence/railway-autonogrammer-worker-2026-10-28.md). The refusal of 2026-10-21 (`Deploys have been paused temporarily`) did not recur that day; this does not say that Railway deploys are unrestricted. The service stays up and is billed at Railway usage rates; whether to keep it is OQ-171.

What it proves:

- The worker image builds on Railway's builder from the repository with the committed Containerfile and lockfiles; the entrypoint copes with a root-owned Railway volume and binds `[::]:$PORT`; Railway's health check accepts `/healthz`; the JWT gate and the omp bridge work on Railway's runtime as they did under podman ([evidence](evidence/cloud-worker-container-2026-10-28/README.md)).

What it does not prove:

- The control plane on Railway (the API is not deployed: production mode needs an identity provider, OD-03, and the substrate databases of the `Ultrathink` project were not touched).
- That the control plane mints tokens the worker accepts (a script played that role).
- A prompt turn (no model credential, OD-07).
- A git checkout, branch or pull request from the worker (the worker bootstrap is not built).
- Hooks in the worker.
- Sleep and wake of the service (serverless mode is off).
- The private network between two services (there is one service).
- Any remote desktop or browser streaming (OQ-73 is untouched: the worker has none).
