# Staged agent worker on Railway, 2026-10-28

**Result: deployed, health-checked and exercised.** The worker image built from `deploy/worker.Containerfile` runs on Railway as a private service (no domain, no TCP proxy). Railway's health check on `/healthz` passed (deployment `SUCCESS`), and a real `omp acp` inside the container answered `initialize` and `session/new` through the JWT-gated WebSocket bridge. No model was called. This is the staged deploy of the default recorded in OQ-100; it is not the API and not a cloud agent a user can start.

The refusal of 2026-10-21 (`Deploys have been paused temporarily`, `docs/runbooks/evidence/railway-ompos-api-2026-10-21.md`) did not recur: creating the service, the volume, the variables and the deployment all succeeded on 2026-10-28.

## What exists on Railway

| Object | Value |
|---|---|
| Workspace | `Ming Chen's Projects` (personal), `24c26fa7-d90c-410b-8ab0-2fbe882fdbc0` |
| Project | `ompos-staging`, `71340b1d-e56a-4f57-ac8c-a3160ce5f36d`, private, no PR deploys; kept apart from the `Ultrathink` project that holds the substrate databases |
| Environment | `staging`, `5fdf46e0-cc10-4c53-bbc0-9749f30b9dc0` |
| Service | `ompos-worker`, `d7c267d3-ef92-4043-8d3b-7a5f8306321b`, state `live`, one replica in `sfo` |
| Volume | `ompos-worker-data`, `c08a33de-9a73-4418-9a14-b2afe56b85f7`, 1024 MB at `/data` |
| Deployment | `b6e5ac9d-831c-49ea-b51c-78e59271de05`, `SUCCESS`, created 2026-10-28T21:27:49Z, source: a clean `git archive` of the released tree (commit `3ace9781`, without the plugin submodules) |
| Domains and proxies | none (`list-domains` answers "No domains"; `describe-service` shows no TCP proxies) |
| Variables (names) | `PORT`, `RAILWAY_DOCKERFILE_PATH`, `Worker__Auth__Issuer`, `Worker__Auth__PublicKeysPem__0`, `Worker__WorkspaceRoot`. The only key material is the PEM of a throwaway ECDSA P-256 **public** key; its private half was kept on the host that ran the check and has been deleted, so no token for this worker can be minted any more. |

Service configuration read back with `describe-service`: builder `DOCKERFILE`, Dockerfile path `deploy/worker.Containerfile`, health check path `/healthz` with a 120 s timeout, restart policy on failure (3 retries), tracing off, no staged changes.

## How it was done

1. Railway MCP `create-project` (`ompos-staging`, environment `staging`), then `create-service` (empty, `ompos-worker`).
2. `set-variables` with `skipDeploys`, `update-service` (`dockerfilePath`, `healthcheckPath` `/healthz`, `healthcheckTimeout` 120, restart `ON_FAILURE` x 3) and `create-volume` at `/data`. The entrypoint needs the volume: Railway mounts it root-owned, the entrypoint repairs ownership and drops to uid 10001 (`ompos`).
3. `git archive HEAD` of the released tree into a temporary directory (60 MB), then `railway up -p <project> -e <environment> -s <service> --ci <directory>`. Railway's builder ran the stages of the Containerfile (`dotnet restore --locked-mode`, `dotnet publish`, Bun 1.4.3 and `@oh-my-pi/pi-coding-agent` 18.12.5 with their pinned digests) in about 70 seconds, pushed the image and finished with `Deploy complete`.
4. Read back with `list-deployments`, `get-logs`, `describe-service` and `list-domains`, and probed from inside the container with `railway ssh` (the account already had the host's key registered as `vps-substrate`; nothing was added).

## Observations

Deploy log (12 lines): `Mounting volume on: /var/lib/containers/railwayapp/bind-mounts/...`, `Starting Container`, `Now listening on: http://[::]:8080`, `Application started`, `Hosting environment: Production`. The one warning is ASP.NET Core's data-protection key note (no key ring encryptor), which the worker does not use for anything it exposes.

From inside the container (`railway ssh -- sh -c ...`):

| Probe | Result |
|---|---|
| `GET /healthz` | 200 `ok` |
| `GET /readyz` | 200 `{"status":"ready"}` |
| `GET /acp` without a token | 401 |
| `/data`, `/data/work` | owned by `ompos` (the entrypoint repaired the root-owned mount) |
| `omp --version` | `omp/18.12.5` |
| WebSocket upgrade on `/acp` with an ES256 token (`iss` `ompos-api`, `aud` `ompos-worker`, `sub` `railway-staging-1`, 10 minute lifetime, signed with the throwaway private key) | opened |
| `initialize` through the bridge | answered by `omp` 18.12.5, `loadSession` `true` |
| `session/new` with `cwd` `/data/work/railway-staging-1` | returned a session id |

The probe script ran under Bun inside the container, took the token as an argument and removed itself afterwards.

## What this proves and what it does not

- Proves: the worker image builds on Railway's builder from the repository with the committed Containerfile and lockfiles; the entrypoint copes with a root-owned Railway volume and binds `[::]:$PORT`; Railway's health check accepts `/healthz`; the JWT gate and the omp bridge work on Railway's runtime as they did under podman (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`).
- Does not prove: the control plane on Railway (the API is not deployed: production mode needs an identity provider, OD-03, and the substrate databases of the `Ultrathink` project were not touched); that the control plane mints tokens the worker accepts (a script played that role); a prompt turn (no model credential, OD-07); a git checkout, branch or pull request from the worker (the worker bootstrap is not built); hooks in the worker; sleep and wake of the service (serverless mode is off); the private network between two services (there is one service); any remote desktop or browser streaming (OQ-73 is untouched: the worker has none).

## State left behind and how to remove it

The service stays up so the owner can inspect it. It is billed at Railway's usage rates (a small always-on container and a 1 GB volume) and does nothing useful until a control plane exists. To remove it: delete the service `ompos-worker` and then the volume `ompos-worker-data` (Railway MCP `delete-service` and `delete-volume`, or the dashboard), then delete the project `ompos-staging` in the dashboard (project settings, danger zone). To stop only the spend, enable sleep when idle on the service (`update-service` with `sleepApplication` true).

To recreate it: repeat steps 1 to 3 above with a fresh key pair; set `Worker__Auth__PublicKeysPem__0` to the PEM of the control plane's public signing key, never to a private key.
