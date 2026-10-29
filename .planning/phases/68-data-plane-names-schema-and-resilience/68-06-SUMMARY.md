---
phase: 68-data-plane-names-schema-and-resilience
plan: 06
requirements: [AG-68-3, AG-68-13]
---

# 68-06 summary: throwaway services, Greptime bootstrap, gate parity

## Result

Infra only; no product code, no package, no Railway mutation, no hosted step. `.github/workflows/ci.yml` and `bootstrap.sh` are unchanged (read only).

## Task 1: `--maxmemory` verified, then pinned (DEVIATION from the plan's value)

The plan's probe value `--maxmemory 256m` does NOT work on the pinned image (`dragonfly:v1.27.1@sha256:385cb730...`) on this 8-core host. Evidence from the throwaway container `autonogrammer-maxmemory-probe` (removed afterwards):

- `--bind=0.0.0.0 --maxmemory 256m`: `Max memory limit is: 256.00MiB`, `Running 8 io threads`, then `There are 8 threads, so 2.00GiB are required. Exiting...` (Dragonfly needs 256 MiB per proactor thread, so the minimum depends on the host's core count).
- `--bind=0.0.0.0 --proactor_threads=2 --maxmemory 512m`: `Max memory limit is: 512.00MiB`, `Running 2 io threads`; with `AUTH`, `SET k v` / `GET k` answered `+OK` / `v`; `INFO memory` showed `maxmemory:536870912`, `maxmemory_human:512.00MiB`, `maxmemory_policy:noeviction`.

Pinned in BOTH `scripts/test-services.sh` (dragonfly run line) and `deploy/compose.yaml` (dragonfly command): `--proactor_threads=2 --maxmemory=512m`. Fixing the thread count makes the 512m floor (2 x 256 MiB) independent of host size. No `--cache_mode` (eviction off, policy `noeviction`), no new volume (the local `dragonfly-data` volume and `--dir=/data` are unchanged). 68-07 should record the evidence lines above in the runbook; a hosted `autonogrammer-dragonfly` service needs the same two flags (a larger value needs `proactor_threads` x 256 MiB at least) [INFERENCE: Railway container core count not observed].

Plain `up` then `down` round trip: exit 0, env file has exactly two export lines, `podman ps -a` shows no `autonogrammer-*` container afterwards; `INFO memory` on the test container shows `maxmemory:536870912` / `noeviction`.

## Task 2: `up --with-greptime`, bootstrap script, env, entrypoint

- `scripts/test-services.sh`: `GREPTIME_IMAGE=` is the compose image reference verbatim (`...@sha256:9726587e...a44c`); `up --with-greptime` starts `autonogrammer-test-greptimedb` (loopback random host port, no auth), waits on `GET /health` 200 through the published port, exports `AUTONOGRAMMER_TEST_GREPTIME_URL='http://127.0.0.1:<port>'`; plain `up` is unchanged (two lines); an unknown second argument exits 2; `down` removes all three and is idempotent (run twice).
- `scripts/data-plane/bootstrap-greptime.sh` (executable): `CREATE DATABASE IF NOT EXISTS autonogrammer WITH (ttl='30d')` over `POST /v1/sql`; base URL from argument, `$GREPTIME_URL`, else `http://127.0.0.1:4000`; optional `GREPTIME_BASIC_AUTH` is passed to curl on stdin only; treats a JSON `code` other than 0 as failure. Exercised against the throwaway container: ran twice (idempotent), `SHOW DATABASES` listed `autonogrammer` next to the built-in databases, `SHOW CREATE DATABASE autonogrammer` showed `ttl = '30days'`; against a closed port it exits 1 with a message.
- `.env.example`: OTLP sample header now `X-Greptime-DB-Name=autonogrammer`; documents `AUTONOGRAMMER_TEST_GREPTIME_URL`, `Sessions__Postgres__MigratorConnectionString` and `Sessions__Postgres__AutoMigrate` (hosting-only), `DataPlane__Postgres__SchemaWaitSeconds`, `DataPlane__Ledger__Enabled/Endpoint/Token`, `DataPlane__TenantHashPepper` and the retention defaults. GAP: the exact sub-keys under `DataPlane__Retention__*` are not fixed in any plan or decision (D-30 says `DataPlane:Retention:*`), so the file names the section and the defaults in prose only; 68-02/68-07 should align it with the options class.
- `deploy/api.Containerfile`: ENTRYPOINT now `... exec dotnet /app/Autonogrammer.Api.dll "$@"` with `autonogrammer-api` as `$0`, so `migrate` and `migrate --down 3` reach the program; `unset ASPNETCORE_HTTP_PORTS`, the PORT binding and `exec` are kept. NOT BUILT here (no image build; CI builds it at `ci.yml:193`).

Acceptance greps: `with-greptime`, `AUTONOGRAMMER_TEST_GREPTIME_URL`, the `GREPTIME_IMAGE=...@sha256:9726587e...` line and the forwarding entrypoint text are present; no `public` database in the bootstrap script or `.env.example`; `git diff --stat -- .github` empty.

## Task 3: GateParityTests

`BootstrapAndCiStartAndStopTheSameTestServices` added to `tests/Autonogrammer.ArchitectureTests/GateParityTests.cs` (plus one `GeneratedRegex`, `TestServicesUp`): each of `bootstrap.sh` and `ci.yml` runs `bash scripts/test-services.sh up` exactly once with equal (and empty) arguments; `bootstrap.sh` runs `down` inside `cleanup()` with `trap cleanup EXIT` after the definition and before `up`; `ci.yml` runs `down` once in its own later step marked `if: always()`; neither file contains `autonogrammer-test-`. Text-only, no new option.

## Verification: honest status

- `bash scripts/test-services.sh up` + `down`, `up --with-greptime` + `down` (both plans' verify commands): run, pass, no container left.
- `ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*GateParityTests*"`: NOT PASSED, NOT EXECUTED through the test runner. Six attempts at restore (queued behind the `ag-dotnet` lock for up to 70 minutes each; one timed out in the queue) failed: three at restore with `NU1004` (locked mode), because other leads' uncommitted `packages.lock.json` files were stale (Harness.Workspace, Resilience, Accounts, Secrets.Os, Native, Web, Api). The lock files are being regenerated by their owners; I did not touch them. `--no-restore` fails with the Microsoft.Testing.Platform runner error, and disabling lock files (`-p:RestorePackagesWithLockFile=false`) fails with `NU1005`.
- Two later attempts passed restore but failed to build on another lead's untracked `src/Autonogrammer.Harness.Abstractions/UserDataDirectory.cs(94,20)`: `CA2000` (lead-66 was told). Substitute evidence (not the real runner): a scratch console project in /tmp (removed) compiled the unchanged `GateParityTests.cs` against Shouldly 4.3.0 with a stub `[Fact]` and a stub `FindRepoRoot` pointing at this worktree and invoked every test method: all five, including the new one, PASS.
- `ImagePinTests.TestServicesImageReferences_CarryDigest` was not run (same restore blocker); `GREPTIME_IMAGE` carries `@sha256:` by construction.

## Files changed

scripts/test-services.sh, scripts/data-plane/bootstrap-greptime.sh (new), deploy/compose.yaml, deploy/api.Containerfile, .env.example, tests/Autonogrammer.ArchitectureTests/GateParityTests.cs, this SUMMARY. Suppression/skip ledgers: none added or removed.

## Deviations

- Rule 1: `--maxmemory 256m` replaced by `--proactor_threads=2 --maxmemory=512m` (256m does not start on an 8-thread host). Acceptance "same value in both files" holds for the pinned pair.
