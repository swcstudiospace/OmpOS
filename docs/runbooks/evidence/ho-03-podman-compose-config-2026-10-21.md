# HO-03: podman-compose config smoke run (2026-10-21)

Scope: `RealPodmanComposeConfig_SmokeTest` renders `deploy/compose.yaml` with
`podman-compose -f deploy/compose.yaml --profile backend config`. It is config-only and starts no
containers (`tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs`, lines 133 to 165).

| Field | Value |
|---|---|
| Date (UTC) | 2026-10-21T21:02:09Z |
| Host | Ubuntu 24.04.4 LTS, x86_64 |
| Repository state | Working tree at `dcd0599` (`feat/v0.9.2-hardening`) |
| Container engine | podman 4.9.3 |
| Compose tool | podman-compose 1.6.0 |
| Runtime | .NET SDK 10.0.402 (`$HOME/.dotnet/dotnet`) |
| Command | `dotnet tests/OmpOS.Sandbox.Tests/bin/Release/net10.0/OmpOS.Sandbox.Tests.dll --filter-method "*RealPodmanComposeConfig_SmokeTest*"` |
| Result | `Test run summary: Passed!`; total 1, succeeded 1, failed 0, skipped 0; duration 742 ms |

Limits:

- Config-only. The record shows the compose file renders for the backend profile. It does not show
  that the PostgreSQL, Dragonfly or GreptimeDB containers start. The container boot path runs in the
  `Boot backend profile` step of `.github/workflows/ci.yml`.
- Podman 4.9.3 is the host engine. The OpenShell podman driver needs Podman 5.x (OD-11, Q-SB-OPENSHELL).
  This record does not cover that driver.
