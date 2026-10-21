# Hosting runbook (HOST-01)

## Status

Nothing has been deployed. No Railway service exists for OmpOS. The images below have not been built, and the Railway config files have not been applied. This runbook and the files under `deploy/` are the hosting definition only.

## Files

| File | Purpose |
|------|---------|
| `deploy/api.Containerfile` | Multi-stage image for `services/OmpOS.Api` |
| `deploy/worker.Containerfile` | Multi-stage image for `services/OmpOS.Workflows.Worker` |
| `.containerignore` | Build-context exclusions for both images: `bin/`, `obj/`, `.git`, `.dotnet`, `.nuget`, `.env*`, `.planning/` never enter a build |

The build files are named `*.Containerfile` because `RepoHygieneTests` forbids files named `Dockerfile*` or `docker-compose*`. Builds use podman only.

## Railway settings: no config file

Railway Config as Code is deprecated, and new services cannot opt into it (https://docs.railway.com/config-as-code). This repository therefore commits no `railway.json` or `railway.toml`. Every setting below is a dashboard setting or a service variable.

- The former `deploy/railway/api.json` and `deploy/railway/worker.json` were removed. They applied only to services that already used a config file, so they never applied to a new service.
- Owner decision 6 stands: dashboard settings and variables (the path recorded here), or IaC `.railway/railway.ts`. Do not manage one service with both systems (IaC docs).

### Settings verified against Railway docs

| Setting | Where | Value | Source |
|---------|-------|-------|--------|
| Builder | Service settings, build | Dockerfile | https://docs.railway.com/builds/dockerfiles |
| Dockerfile path | Variable `RAILWAY_DOCKERFILE_PATH` | `deploy/api.Containerfile` / `deploy/worker.Containerfile` | https://docs.railway.com/builds/dockerfiles#custom-dockerfile-path; https://docs.railway.com/variables/reference#user-provided-configuration-variables |
| Build context | Service Root Directory | Repository root (default) | https://docs.railway.com/guides/docker-compose#services-with-a-dockerfile-or-build-context |
| Health check path | Service settings, health check | `/health/ready` (API only) | https://docs.railway.com/deployments/healthchecks |
| Health check timeout | Service settings, health check | 120 s (API only; chosen value, not a platform default) | https://docs.railway.com/deployments/healthchecks |
| Restart policy | Service settings, deploy | On failure | Dashboard setting; the exact label was not re-verified |

Railway injects `PORT`. The API binds to it and falls back to 8080. The health check runs at deploy time only, from host `healthcheck.railway.app` (https://docs.railway.com/deployments/healthchecks).

The build context is the service Root Directory, not the Dockerfile's directory (https://docs.railway.com/guides/docker-compose#services-with-a-dockerfile-or-build-context). Keep the Root Directory at the repository root, because the Containerfiles copy repo-relative paths.

The API does not restrict hosts (no `AllowedHosts` setting in `services/OmpOS.Api`). That is acceptable only while the API stays private (owner decision 1).

## Base images

| Stage | Image | Why |
|-------|-------|-----|
| API build | `mcr.microsoft.com/dotnet/sdk:10.0.402-noble` | `global.json` pins SDK `10.0.402` with `rollForward: latestPatch`. The tag matches exactly. |
| API runtime | `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble` | The runtime bundled with SDK 10.0.402 is 10.0.13 (https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Noble (Ubuntu 24.04) is used because the entrypoint needs `/bin/sh` to map `PORT`. Chiseled images have no shell. |
| Worker build | `mcr.microsoft.com/dotnet/sdk:10.0.402-noble` | Same as the API. |
| Worker runtime | `mcr.microsoft.com/dotnet/runtime:10.0.13-noble` | The worker is a generic host with no HTTP listener, so the ASP.NET image is not needed. |

Sources for tags:

- SDK `10.0.402-noble`: https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md (tag table) and https://mcr.microsoft.com/v2/dotnet/sdk/tags/list.
- ASP.NET and runtime `10.0.13-noble`: https://raw.githubusercontent.com/dotnet/dotnet-docker/main/README.aspnet.md and https://raw.githubusercontent.com/dotnet/dotnet-docker/main/README.runtime.md; confirmed in the MCR tag lists `https://mcr.microsoft.com/v2/dotnet/aspnet/tags/list` and `https://mcr.microsoft.com/v2/dotnet/runtime/tags/list`.
- SDK 10.0.402 bundles runtime 10.0.13: https://dotnet.microsoft.com/en-us/download/dotnet/10.0 and https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.13/10.0.13.md.

Both stages pass `--locked-mode` restore, then `publish --no-restore`. Every project in both graphs has a committed `packages.lock.json`. Both final stages run as UID/GID `10001`.

Pin by digest before production (for example `sdk:10.0.402-noble@sha256:...`). Digests were not recorded here because no registry pull was performed.

## Build commands (podman only)

Run from the repository root:

```sh
podman build -f deploy/api.Containerfile -t ompos-api:local .
podman build -f deploy/worker.Containerfile -t ompos-worker:local .
```

Local smoke test (after building):

```sh
podman run --rm -p 8080:8080 -e Harness__Adapter=InMemory ompos-api:local
curl -fsS http://127.0.0.1:8080/health/ready
```

Build context: the whole repository. `.containerignore` excludes `bin/`, `obj/`, `.git`, `.dotnet`, `.nuget`, `.env*` and `.planning/`, so host build output and local secrets stay out of images.

## Environment variables

Config keys use `:` in code and `__` in the environment. Names below come from the option classes in `services/OmpOS.Api/Options/` and `services/OmpOS.Workflows.Worker/` (`WorkerHostConfiguration.cs`, `WorkerOptions.cs`).

### API (`services/OmpOS.Api`)

| Variable | Default | Required | Notes | Source |
|----------|---------|----------|-------|--------|
| `PORT` | unset (uses 8080) | No (platform injects it) | Listen port. Railway sets it. | `deploy/api.Containerfile` entrypoint |
| `ASPNETCORE_ENVIRONMENT` | `Production` (set in image) | No | Development enables Scalar UI and the developer exception page. Do not set Development in hosting. | `deploy/api.Containerfile` |
| `Harness__Adapter` | `InMemory` (appsettings) | Yes | Only `InMemory` is accepted. Startup fails naming this key if absent. | `HarnessOptions.cs` |
| `Api__Cors__Origins__0`, `__1`, ... | empty (no cross-origin) | Owner decision | Public web origin(s). Empty denies cross-origin traffic. | `ApiCorsOptions.cs` |
| `Api__MaxPromptLength` | `64000` | No | 1 to 10,000,000. | `ApiOptions.cs` |
| `Api__MaxRequestBodyBytes` | `1048576` | No | 1 to 1,073,741,824. | `ApiOptions.cs` |
| `Api__RateLimit__PromptsPerMinute` | `60` | No | 1 to 100,000. | `ApiRateLimitOptions.cs` |
| `Api__RateLimit__SessionsPerMinute` | `60` | No | 1 to 100,000. | `ApiRateLimitOptions.cs` |
| `Workflows__Enabled` | `false` | Owner decision | `false` runs prompts in-process with the fake sandbox. `true` dispatches to Temporal. | `WorkflowsOptions.cs`, `Program.cs` |
| `Workflows__Address` | none | Yes when `Workflows__Enabled=true` | Temporal host:port. Startup fails without it. | `WorkflowsOptions.cs` |
| `Workflows__Namespace` | `default` | Owner decision | Temporal namespace. | `WorkflowsOptions.cs` |
| `Workflows__TaskQueue` | `ompos-session-runs` | No | Must match the worker's queue. | `WorkflowsOptions.cs` |
| `Workflows__ApiKey` | none | Secret, when Temporal Cloud needs it | Bearer token. Set it in Railway as a secret. Never commit it. | `WorkflowsOptions.cs` |
| `Workflows__Tls` | `false` | Owner decision | Enables TLS on the Temporal client. | `WorkflowsOptions.cs` |
| `Otel__Exporter` | `none` | No | `none`, `console`, or `otlp`. | `OtelOptions.cs` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | SDK default | Required for `otlp` | Read by the OpenTelemetry .NET SDK, since the code calls `AddOtlpExporter()` with no arguments. [INFERENCE: standard SDK behaviour, not set in repo.] | `services/OmpOS.Api/Program.cs` |
| `OmpOS__Debug__LogPrompts` | `false` | No | Must stay `false` when hosted: `true` writes prompt bodies to logs. | `DebugOptions.cs` |

### Worker (`services/OmpOS.Workflows.Worker`)

| Variable | Default | Required | Notes | Source |
|----------|---------|----------|-------|--------|
| `Workflows__Enabled` | `false` | Yes | `false` prints a message and exits 0. The worker is inert until this is `true`. | `WorkerHostConfiguration.cs`, `Program.cs` |
| `Workflows__Address` | `localhost:7233` | Yes when enabled | Temporal host:port. Must be reachable from Railway. | `WorkerOptions.cs` |
| `Workflows__Namespace` | `default` | Owner decision | | `WorkerOptions.cs` |
| `Workflows__TaskQueue` | `ompos-session-runs` | No | | `WorkerOptions.cs` |
| `Workflows__ApiKey` | none | Secret, when needed | Same secret as the API when both use Temporal Cloud. | `WorkerOptions.cs` |
| `Workflows__Tls` | `false` | Owner decision | | `WorkerOptions.cs` |
| `Sandbox__Provider` | none | Yes when enabled | Only `podman` is accepted. Selects `LocalPodmanComposeProvider`, which runs podman. See blocker 2. | `WorkerHostConfiguration.cs` |
| `Harness__Adapter` | none | Yes when enabled | Only `rpc` is accepted. | `WorkerHostConfiguration.cs` |
| `Harness__OmpPath` | none | Yes when `rpc` | Path to the `omp` binary. The image does not contain one. See blocker 3. | `WorkerHostConfiguration.cs` |
| `Harness__Cwd` | none | Yes when `rpc` | Working directory for `omp`. | `WorkerHostConfiguration.cs` |

Railway injects `PORT` (https://docs.railway.com/deployments/healthchecks). The worker has no HTTP listener, so it needs no port and no health check path.

**Not present in code:** no sign-in or API-key variable exists for the API. `AuthBoundaryMiddleware` attaches a single `local-dev` principal to every request (`SingleDevPrincipal`). Hosting the API publicly therefore exposes it without authentication. See owner decision 1.

## Deploy order

Do not deploy until the owner decisions below are answered.

1. Provision Temporal (owner decision 3) and create the namespace. Store the API key as a secret.
2. Create the worker service with `RAILWAY_DOCKERFILE_PATH=deploy/worker.Containerfile`. Set `Workflows__Enabled=false` first. The image should start and exit 0. This confirms the image runs, and nothing more.
3. Create the API service with `RAILWAY_DOCKERFILE_PATH=deploy/api.Containerfile`, health check path `/health/ready`, and health check timeout 120 s (both in service settings). Keep `Workflows__Enabled=false` and `Harness__Adapter=InMemory`. Confirm `/health/ready` returns 200 and `/health/live` returns 200.
4. Set `Api__Cors__Origins__0` to the owner-confirmed public web origin. Redeploy the API.
5. Enable workflows only after blockers 2 and 3 are resolved. Set `Workflows__Enabled=true` and `Workflows__Address` on both services. The API `/health/ready` then includes a TCP check of the Temporal address.

## Owner decisions

1. **Sign-in method.** None exists. The API serves every request as `local-dev`. Choose an authentication scheme before exposing the API publicly. Until then the API should not be exposed beyond a trusted network.
2. **Public origin.** The API origin and the web origin for `Api__Cors__Origins__0`. Nothing is set.
3. **Temporal.** Temporal Cloud or self-hosted; the address, namespace, whether TLS and an API key are required.
4. **Worker sandbox.** The worker accepts only `Sandbox:Provider=podman`, which runs podman from inside the worker process. Decide whether podman is supported in the Railway container at all, and if so how it is installed and what privileges it needs.
5. **Worker harness.** `Harness:Adapter=rpc` needs an `omp` binary at `Harness__OmpPath`. Decide how that binary gets into the image, and its version.
6. **Config mechanism.** Dashboard and variables, or IaC `.railway/railway.ts`. Config-as-code files stop being read for legacy services on 2026-12-01, and new services cannot use them.
7. **OTLP endpoint and Temporal namespace.** Where traces and metrics go, if anywhere.
8. **Region and replicas.** Not specified anywhere in the repo.

## Known blockers and gaps

1. The API has no authentication (owner decision 1).
2. The worker image has no podman. `Sandbox:Provider=podman` will fail when a run needs it.
3. The worker image has no `omp` binary. `Harness:Adapter=rpc` will fail at `Harness__OmpPath`.
4. With `Workflows__Enabled=false`, the API runs prompts in-process with `FakeSandboxProvider` (`services/OmpOS.Api/Program.cs`, comments at lines 66-70). It does not run a real sandbox.
5. Railway's build context for a subdirectory Dockerfile is the service Root Directory (verified in the Railway docs above). Keep the Root Directory at the repository root.
6. Nothing was built with podman, and nothing was run or deployed.
