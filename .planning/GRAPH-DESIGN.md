#GRAPH-DESIGN — Standing Quality & Verification Design (SPE-35247, Wave 3 · n5)

Date: 2026-11-20. Binding on all later phases of v0.16.0 (units 68, 71, 73, 74,
75, 76, 77 per `.planning/GRAPH-UNITS.md`) unless a phase plan explicitly
deviates with a recorded reason. This is the sole quality/verification design
for the graph; phases reference it instead of restating it.

Grounding (verified 2026-11-20 against the worktree tree, not assumed):

- `Directory.Build.props`: `net10.0`, `Nullable enable`, `ImplicitUsings enable`,
  `TreatWarningsAsErrors true`, `EnableNETAnalyzers true`, `AnalysisLevel
  latest-all`, `EnforceCodeStyleInBuild true`, `Deterministic true`,
  `JsonSerializerIsReflectionEnabledByDefault false`; test projects (`*Tests`)
  get `IsTestProject`, `OutputType Exe`, `UseMicrosoftTestingPlatformRunner`.
- `Directory.Packages.props`: xunit.v3 4.0.2, Shouldly 4.3.0,
  Microsoft.Testing.Platform 2.5.1, coverlet.MTP 10.1.0; GlobalPackageReference
  `Meziantou.Analyzer` 3.0.315.
- `.github/workflows/ci.yml` (`build-test` job) and `bootstrap.sh`: the exact
  gate commands in §10 — copied verbatim, not paraphrased.
- `services/Autonogrammer.Api/Metrics/ApiMetrics.cs`: the metrics pattern —
  `internal sealed class ApiMetrics : IDisposable`, `IMeterFactory`-injected,
  `Meter.CreateCounter<long>` / `CreateHistogram<double>` instruments only.
- `src/Autonogrammer.Accounts/AccountSessionManager.cs:419-421`: the CA1031
  filtered form — `#pragma warning disable CA1031 // <boundary reason>` around
  `catch (Exception ...)`, `#pragma warning restore CA1031` immediately after
  the catch clause, result-returning (never rethrowing) body.
- `tests/Autonogrammer.Web.Tests/DemoPageSetup.cs` + `DemoTestHost.cs`: the
  bUnit pattern — `DemoPageSetup` registers the workbench into the bUnit
  service collection; tests use `TestContext.Current.CancellationToken`
  (xunit v3 / MTP) throughout, e.g. `ApiSessionClientTests.cs:21`.

## 1. Modular structure

1. One project per capability seam already in the tree (`src/`, `services/`,
   `apps/`); new capability = new project added to `Autonogrammer.Linux.slnf`
   (or the owning slnf) plus a central package-version entry in
   `Directory.Packages.props`. No new project is introduced only to hold a
   single helper — helpers live in the owning project.
2. Wire formats are System.Text.Json only (STJ, source-generated contexts);
   `JsonSerializerIsReflectionEnabledByDefault=false` makes reflection-based
   options fail closed. Document/DTO types follow the existing wire pattern:
   every member nullable, unknown members skipped, missing member reported by
   the reader (cf. `PluginConnectionDocument`, `SubstrateWire`), so a vendor
   or server upgrade never breaks a read.
3. Telemetry lives behind an injected factory (`IMeterFactory` →
   `ApiMetrics`); endpoints/activities depend on the metrics class, never on
   `new Meter(...)` inline. New metric = new instrument on the existing
   metrics class + a named method, following `ApiMetrics.cs` exactly.
4. Podman is the only container runtime in tests and CI (`ci.yml` header,
   `scripts/test-services.sh`); never Docker. Test services are throwaway and
   per-gate (see §7).

## 2. Typing: nullable, counters, banned symbols

1. Nullable reference types are on repo-wide (`<Nullable>enable</Nullable>`).
   New code: annotate every reference type; `ArgumentNullException.ThrowIfNull`
   at public boundaries (as `ApiMetrics(IMeterFactory)` does); no nullable
   suppressions without a comment stating why.
2. Metrics counters: `Counter<long>` for countable events, `Histogram<double>`
   for durations/values (verified in `ApiMetrics.cs:13-16,24-27`). Do not
   introduce ad-hoc counters or `double` counters for counts.
3. Meziantou.Analyzer is a GlobalPackageReference (`Directory.Packages.props:74`,
   3.0.315) — MA rules (including MA0002) apply repo-wide under
   `AnalysisLevel latest-all` + `TreatWarningsAsErrors`. Treat any MA finding
   as a build error; fix the code, do not suppress.
4. `BannedSymbols.txt` / `BannedSymbols.NoProcess.txt` are wired as
   `AdditionalFiles` in `Directory.Build.props:50-53` — banned APIs fail the
   build. Do not add new banned-symbol file conventions; extend the existing
   two files if a phase bans a new API.

## 3. Explicit error handling (CA1031 filtered form)

1. Catch only the exception types you handle (`OperationCanceledException`,
   `HttpRequestException`, domain exceptions). A general `catch (Exception)`
   is allowed ONLY at a boundary that must return a result instead of throwing
   (shared refresh, connection check, health probe), and only in the exact
   filtered form verified at `AccountSessionManager.cs:419-421` /
   `McpConnectionChecker.cs:112-118`:
   `#pragma warning disable CA1031 // <one-line boundary reason>`,
   `catch (Exception exception)`, `#pragma warning restore CA1031`,
   body maps to a failure result and logs `exception.GetType().Name` (never the
   message or stack into a wire result).
2. `OperationCanceledException` is always caught first (or filtered) and maps
   to cancellation, never to a generic failure.
3. No empty catch blocks; no `catch { }`; no swallowing without a logged,
   typed outcome. Boundary results use the existing result types
   (`AuthResult<T>` pattern), not exceptions across the seam.

## 4. Tests: xunit v3 / MTP + bUnit

1. Stack (verified): xunit.v3 4.0.2 on Microsoft.Testing.Platform 2.5.1,
   Shouldly 4.3.0, coverlet.MTP 10.1.0 (`Directory.Packages.props:6-12`).
   Test projects are `Exe` + `UseMicrosoftTestingPlatformRunner`
   (`Directory.Build.props:42-48`) — never `dotnet test` with a VSTest adapter.
2. Every async test threads `TestContext.Current.CancellationToken` through
   the call under test (repo-wide pattern, e.g.
   `tests/Autonogrammer.Accounts.Tests/AccountSessionManagerTests.cs:18`).
   No `CancellationToken.None`; no `Task.Delay` without the token. Bounded
   waits use the linked-CTS helper pattern (`AgentWorker.Tests/TestTimeouts.cs:12`).
3. bUnit UI tests (web head): follow `tests/Autonogrammer.Web.Tests/` —
   `DemoPageSetup` registers services into the bUnit context,
   `DemoTestHost` provides the Demo world + manual clock. Route/client tests
   assert against the exercised surface (`ClientRouteSurface.ExerciseAsync`),
   not against mocks of the client under test.
4. Traits: `Category=Live` marks live/external tests; the phase gate excludes
   them with `--filter-not-trait "Category=Live"` (ci.yml test step,
   bootstrap.sh:86). Code that only runs Live is recorded as
   human-verification in the phase VERIFICATION, never silently skipped.
5. Negative controls: where a check has a bypass flag, a test proves the
   bypass fails without the flag (cf. ci.yml `check-no-demo.sh` negative
   control). New gates copy this pattern.

## 5. Accessibility (where UI changes)

1. Web UI changes are gated by the repo's own suites, not by manual review
   alone: `node --test scripts/web-smoke/a11y/*.test.mjs`,
   `node scripts/web-smoke/a11y/fixture-selftest.mjs`, and the axe-core +
   accessibility assertions inside `scripts/web-smoke/run-smoke.sh`
   (ci.yml lines 115-130). Any new web surface needs an entry in the a11y
   suites before its phase gate passes.
2. Native (Avalonia) UI changes: bUnit-equivalent named tests where the head
   has them; otherwise the phase VERIFICATION records the manual keyboard /
   screen-reader walk as human-verification (per Phase 77 unit item 7/9).
   Accessibility matrix cells in `docs/quality/` stay Pending until the named
   test or walk exists — no MET-by-inspection.

## 6. Secrets and least privilege

1. No secret in code, fixture, or doc. Gate: `bash scripts/ci/secret-scan.sh`
   (pinned gitleaks, fails closed offline — bootstrap.sh:49, ci.yml:81-82).
   `AUTONOGRAMMER_SKIP_SECRET_SCAN=1` is an explicit, recorded operator choice,
   never a default.
2. CI permissions: `contents: read` at workflow and job level (ci.yml:13-14,
   36-37, 227-229); forks never receive secrets (ci.yml:38). New jobs declare
   the minimum permissions they need; `contents: write` only where the step
   publishes (e.g. release job).
3. Runtime secrets come from environment / `ISecretStore` (OS-backed),
   never from files in the repo; test services use run-scoped throwaway
   passwords (`AUTONOGRAMMER_*_PASSWORD: ci-${run_id}-...`, ci.yml:30-31;
   `test-services.sh` env file removed on exit, bootstrap.sh:69-80).
4. New `.env.example` variables must be documented in runbooks/reference
   (Phase 77 unit item 3) — an undocumented variable is a gate failure.

## 7. Rollback and compatibility

1. Every change ships behind the existing seams it touches: feature off by
   default where the phase says so (e.g. Computer Use, Dispatch outside
   Development), config/env-gated rollout where a gate exists (71-07 rollout
   gate). A phase that cannot be rolled back by config is flagged in its
   VERIFICATION before merge.
2. Schema/data changes: additive migrations only (M0012/M0014 precedent);
   destructive or truncating migrations need an explicit owner decision
   (cf. 74-11 legacy-row policy, 68-11 erasure). Rollback = the previous
   migration + the previous image; migrations must be re-runnable against a
   restored database (locked restore in §10 covers the restore path).
3. API compatibility: `scripts/ci/check-openapi-breaking.py` runs on PRs
   (ci.yml:103-108); breaking changes need `ALLOW_BREAKING_API` or a
   `BREAKING-API` commit line, plus a CHANGELOG entry (enforced by
   `scripts/ci/check-version-bump.sh`, ci.yml:109-111).
4. Two-way sync (per GRAPH-CRITIQUE gap 3): planning state → Linear is gated
   on `VERIFICATION passed` at phase HEAD; a phase never moves to Done on a
   checkbox or reconciliation record alone.

## 8. Rule for counting a phase done (from GRAPH-CRITIQUE gaps 1-2)

1. Repo scope is the current repo (Autonogrammer/Autonogrammer monorepo); archive
   phases under `.planning/milestones/` are closed history, never gate
   evidence.
2. A phase is done only when its success criteria are observable AND the
   §10 gate passes at the phase HEAD AND a fresh `<phase>-VERIFICATION.md`
   records `passed` at that HEAD. Reconciliation verdicts ("substantially
   complete", "partial") and ROADMAP checkboxes are not gate results.
3. Archived rules: verify only existing evidence — an older VERIFICATION
   counts only if it names the current HEAD/commit and the current tree;
   otherwise run the gate fresh (68 unit item 3, 74 unit item 6).

## 9. What later phases must never do

1. Never run `dotnet test` without `--filter-not-trait "Category=Live"` in a
   gate run; never share `test-services.sh` containers between concurrent
   gates (run-owned name per gate, bootstrap.sh:67).
2. Never add a general catch outside the §3 form; never suppress an MA/CA
   finding with a bare pragma lacking the boundary reason.
3. Never introduce Docker, Newtonsoft, or VSTest-based test hosting; never
   commit a secret or a Live-only test as the sole evidence for a criterion.

## 10. Per-phase-practice checklist (exact commands, from ci.yml / bootstrap.sh)

Run in this order inside the phase's worktree. "Gate" = all of 10.1-10.8 exit 0.

10.1 Locked restore + warnings-as-errors build (ci.yml:70-71, bootstrap.sh:60-62):

    dotnet restore Autonogrammer.Linux.slnf --locked-mode -p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13
    dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13

10.2 Format (ci.yml:76, bootstrap.sh:87):

    dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore

10.3 Repo checks + licences + SBOM + secrets + plugin tests (ci.yml:81-102,
bootstrap.sh:49-61):

    bash scripts/ci/secret-scan.sh
    bash scripts/ci/plugin-node-tests.sh
    bash scripts/ci/repo-checks.sh
    bash scripts/ci/check-licences.sh
    bash scripts/ci/check-sbom.sh
    dotnet list Autonogrammer.Linux.slnf package --vulnerable --include-transitive --format json > vuln.json
    python3 scripts/ci/check-vulnerable-packages.py vuln.json

10.4 Tests (non-Live) with throwaway services (ci.yml:144-159, bootstrap.sh:79-86):

    env_file="$(bash scripts/test-services.sh up)"; source "$env_file"
    dotnet test Autonogrammer.Linux.slnf -c Release --no-build --max-parallel-test-modules 2 --report-trx --coverlet --coverlet-output-format cobertura --results-directory TestResults -- --filter-not-trait "Category=Live"
    bash scripts/test-services.sh down

    Per-project equivalent (single test project during development):
    dotnet test tests/<Project>.Tests/<Project>.Tests.csproj -c Release -- --filter-not-trait "Category=Live"

10.5 CI-script + web-script unit tests (ci.yml:113-119):

    python3 -m unittest scripts/ci/test_check_skip_markers.py scripts/legal/test_generate_sbom.py
    node --test scripts/vercel/refresh-web-manifest.test.mjs
    node --test scripts/web-smoke/a11y/*.test.mjs
    node scripts/web-smoke/a11y/fixture-selftest.mjs

10.6 Browser smoke with axe gate (ci.yml:120-130 + 212-218, bootstrap.sh:92-93):

    OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh

10.7 PR-only gates (ci.yml:103-111):

    python3 scripts/ci/test_check_openapi_breaking.py
    python3 scripts/ci/check-openapi-breaking.py --base "origin/<base-branch>"
    bash scripts/ci/check-version-bump.sh --base "origin/<base-branch>"

10.8 Full gate in one command (Unit 7 final gate; bootstrap.sh:97):

    ./bootstrap.sh

    Under the flock wrapper per Phase 77 unit item 8: `flock` + `./bootstrap.sh`,
    evidence page dated and committed.

10.9 Record: write/refresh `<phase>-VERIFICATION.md` at the gate HEAD with the
    exact commands above, their exit codes, and the Live/human-verification
    remainder. Only then: ROADMAP boxes, STATE, CHANGELOG (serialised per
    GRAPH-UNITS "Serialization notes": ROADMAP/STATE one writer at a time,
    parity doc 75→76→77, CHANGELOG one writer per wave).
