# HO-03 re-check: podman-compose config (2026-10-22)

Read-only. Each command renders `deploy/compose.yaml` and starts nothing. Repository HEAD `d965ba6`, working tree as found (5 uncommitted paths, not touched by this audit).

Host tools: podman 4.9.3 (`/usr/bin/podman`); podman-compose 1.6.0 (`/usr/local/bin/podman-compose`). Same versions as the 2026-10-21 record.

## Run 1: task command, no variables

```
podman-compose -f deploy/compose.yaml config
```

Exit code: **1**. The tool raised before rendering:

```
ValueError: required variable OMPOS_POSTGRES_PASSWORD is missing a value: set OMPOS_POSTGRES_PASSWORD
```

The traceback (about 20 frames inside `/opt/podman-compose/lib/python3.12/site-packages/podman_compose.py`) is omitted; the final line is verbatim.

## Run 2: task command, placeholder variables

The test (`RealPodmanComposeConfig_SmokeTest`) supplies both required variables for the config-only run. Placeholder values, not secrets:

```
OMPOS_POSTGRES_PASSWORD=config-only-placeholder OMPOS_DRAGONFLY_PASSWORD=config-only-placeholder \
  podman-compose -f deploy/compose.yaml config
```

Exit code: **0**. Output:

```
WARNING:podman_compose:WARNING: No services defined
services: {}
volumes:
  dragonfly-data: null
  greptimedb-data: null
  postgres-data: null
```

Finding: without `--profile`, every service is profile-gated, so `config` validates zero services and exits 0. The task command alone therefore proves nothing about the stack. `RealPodmanComposeConfig_SmokeTest` and `.github/workflows/ci.yml:92-102` both pass `--profile backend`.

## Run 3: the test's own argument order, with `--profile backend`

```
OMPOS_POSTGRES_PASSWORD=config-only-placeholder OMPOS_DRAGONFLY_PASSWORD=config-only-placeholder \
  podman-compose -f /root/src/repos/ompos/deploy/compose.yaml --profile backend config
```

Exit code: **0**. Services rendered: `dragonfly` (image `docker.io/dragonflydb/dragonfly:v1.27.1`), `greptimedb` (image `docker.io/greptime/greptimedb:v1.1.4`), `postgres` (image `docker.io/timescale/timescaledb:2.13.1-pg16`). All three carry `profiles: [backend]`. Volumes: `dragonfly-data`, `greptimedb-data`, `postgres-data`. The string `temporal` does not occur in the output, so the test's `ShouldNotContain("temporal")` holds.

## Comparison with `ho-03-podman-compose-config-2026-10-21.md`

| Item | 2026-10-21 record | Now | Result |
|---|---|---|---|
| Test name | `RealPodmanComposeConfig_SmokeTest` | `public async Task RealPodmanComposeConfig_SmokeTest()`, `tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs:134` | matches |
| Test lines | 133 to 165 | `[Fact]` at 133, closing brace at 165 | matches |
| Compose file | `deploy/compose.yaml` | same path, `--profile backend` argument order | matches |
| Engine and compose tool | podman 4.9.3, podman-compose 1.6.0 | podman 4.9.3, podman-compose 1.6.0 | matches |
| Services asserted | postgres, dragonfly, greptimedb; no temporal | same three services; no temporal in output | matches |
| Test result | Passed, 1 test, 742 ms | not re-run in this audit (no test run assigned, build-and-test rule) | not re-checked |

Conclusion: the 2026-10-21 record still matches the test name, file and line range, and the config it asserts still renders. It records a config-only pass, not a container boot. The test run itself is not re-verified here.

Limits: config only; the containers are not started. The OpenShell podman driver (OD-11, Q-SB-OPENSHELL) still needs Podman 5.x and is not covered.
