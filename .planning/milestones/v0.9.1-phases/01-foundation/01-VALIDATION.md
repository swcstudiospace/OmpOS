---
phase: "1"
slug: "foundation"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: false
wave_0_complete: true
created: "2026-10-20"
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

**Audit basis (2026-10-20):** checked against REQUIREMENTS.md FOUND-01..FOUND-09, the `## Validation Architecture` section of 01-RESEARCH.md, 01-VERIFICATION.md and 01-SECURITY.md.
Evidence labels used below:
- **orchestrator** — observed by the orchestrator: `./bootstrap.sh` exit 0, 35/35 tests, zero warnings, format verify exit 0; `podman-compose --profile workflows -f deploy/compose.yaml config` exit 0 and `config --services` lists `temporal`.
- **this audit** — read-only check run during this audit (file read, grep, `wc`, YAML parse, `dotnet list … --vulnerable`, `podman-compose … config`).

Nothing in this audit built, tested, restored or mutated the tree.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.2 on Microsoft.Testing.Platform (`global.json` `test.runner`), Shouldly 4.3.0, TngTech.ArchUnitNET.xUnitV3 0.13.4, coverlet.MTP 10.1.0, Microsoft.Testing.Extensions.TrxReport 2.5.1 |
| **Config file** | `global.json` (SDK 10.0.402, `rollForward: latestPatch`, MTP runner); `Directory.Build.props` (test PropertyGroup, `UseMicrosoftTestingPlatformRunner`); `Directory.Packages.props` (versions) |
| **Quick run command** | `dotnet test --project tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj -c Release` (or the same form for `tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj`) |
| **Full suite command** | `./bootstrap.sh` (locked restore → TWAE build → `dotnet test OmpOS.Linux.slnf -c Release --no-build` → `dotnet format --verify-no-changes`) |
| **Estimated runtime** | Quick run: 5–15 s (research probe estimate, not re-measured). Full `./bootstrap.sh`: not timed in this audit. |

---

## Sampling Rate

- **After every task commit:** `dotnet build OmpOS.Linux.slnf -c Release -warnaserror && dotnet test --project tests/<touched>/<touched>.csproj -c Release --no-build`
- **After every plan wave:** `./bootstrap.sh`
- **Before `/gsd:verify-work`:** `./bootstrap.sh` from a fresh `git clone` in `/tmp` (not yet run; see W1) and CI green on both jobs (manual, see Manual-Only Verifications)
- **Max feedback latency:** 15 s for the quick run. The template's 1 s target is not achievable for `dotnet` builds and is replaced by the research estimate.

---

## Per-Task Verification Map

Task IDs are `1-{plan}-{seq}`; `seq` numbers the checks within a plan. Checks span plan tasks, so they are not mapped one-to-one to the plans' `<task>` indices.
Plans: 01 (wave 1) → FOUND-01..04; 02 (wave 2) → FOUND-06; 03 (wave 3) → FOUND-05, 07, 08, 09.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | FOUND-01 | T-1-03 | One command installs/uses the pinned SDK and exits 0 on a green tree | smoke | `./bootstrap.sh; echo $?` | ✅ `bootstrap.sh` | ✅ green (orchestrator: exit 0) |
| 1-01-02 | 01 | 1 | FOUND-01 | T-1-03 | SDK is pinned by `global.json`, not floating | smoke | `dotnet --version` (repo root) → `10.0.402` | ✅ `global.json` | ✅ green (this audit: pin read; 01-VERIFICATION spot-check) |
| 1-01-03 | 01 | 1 | FOUND-01 | T-1-03 | Failing step exits non-zero: a misformatted file in a clean clone makes format verify fail | smoke (negative, mutating) | In a `/tmp` clone of committed HEAD, add a new `.cs` file with a two-space member inside a class, then `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore; echo $?` → `2` with `error WHITESPACE`; delete the file and the same command → `0`. A stray trailing blank line was tried first and does not fail the verifier (exit 0), so it is not a negative proof. Evidence: `docs/runbooks/evidence/phase1-negative-proofs-2026-10-21/proof-format-indent.log` (exit 2), `proof-format-clean2.log` (exit 0), `proof-format-broken.log` (stray blank line, exit 0) | ✅ | ✅ proven 2026-10-21: exit 2 when broken, exit 0 when clean (the stray-blank-line mutation was withdrawn) |
| 1-01-04 | 01 | 1 | FOUND-02 | — | Warnings are errors; zero warnings on the committed tree | build | `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` → `Build succeeded.`, 0 warnings | ✅ | ✅ green (orchestrator) |
| 1-01-05 | 01 | 1 | FOUND-02 | — | Strictness settings are owned by `Directory.Build.props` (Nullable, TWAE, `latest-all`, Deterministic, reflection off) | static | `grep -cE "<(Nullable\|TreatWarningsAsErrors\|AnalysisLevel\|Deterministic\|JsonSerializerIsReflectionEnabledByDefault)>" Directory.Build.props` → `5` (lines 5, 7, 9, 11, 13) | ✅ | ✅ green (this audit) |
| 1-01-06 | 01 | 1 | FOUND-02 | — | `ContinuousIntegrationBuild` and `RestoreLockedMode` apply only when `CI=true`; the CI workflow sets `CI: true` | static | Read `Directory.Build.props:22-25` (`Condition="'$(CI)' == 'true'"`) and `ci.yml:19` (`CI: true`) | ✅ | ✅ green (this audit) |
| 1-01-07 | 01 | 1 | FOUND-03 | T-1-01 | Every project has a committed lock file; count equals the solution-filter project count | static | `find src services apps tests -name packages.lock.json \| wc -l` → `17` (`OmpOS.Linux.slnf` lists 17 projects) | ✅ | ✅ green (this audit: 17 = 17) |
| 1-01-08 | 01 | 1 | FOUND-03 | T-1-01 | Locked restore succeeds; central management with transitive pinning | restore | `dotnet restore OmpOS.Linux.slnf --locked-mode` → exit 0 | ✅ `Directory.Packages.props` | ✅ green (orchestrator: bootstrap step 2); CPM settings read this audit (`Directory.Packages.props:3-4`) |
| 1-01-09 | 01 | 1 | FOUND-03 | T-1-01 | Version drift without a lock update fails the restore | restore (negative, mutating) | In a `/tmp` clone of committed HEAD, change `Include="Markdig" Version="1.4.2"` to `1.4.1` in `Directory.Packages.props`, then `dotnet restore OmpOS.Linux.slnf --locked-mode; echo $?` → `1` with `NU1004`; restore the file and the same command → `0`. Evidence: `docs/runbooks/evidence/phase1-negative-proofs-2026-10-21/proof-restore-drift.log` (exit 1, NU1004), `proof-restore-clean.log` (exit 0) | ✅ | ✅ proven 2026-10-21: exit 1 with NU1004 when drifted, exit 0 when clean |
| 1-01-10 | 01 | 1 | FOUND-04 | — | `.editorconfig` is the single rule source and format verify passes on the committed tree | format | `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` → exit 0 | ✅ `.editorconfig` | ✅ green (orchestrator) |
| 1-02-01 | 02 | 2 | FOUND-06 | T-2-02 | Every `UiBlock` and `StreamEvent` payload variant round-trips with `kind` first | unit | `dotnet test --project tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj -c Release` → `Test run summary: Passed!`; `EveryBlockKindRoundTripsWithKindFirst`, `EveryPayloadKindRoundTrips`, `MarkdownBlockDeserializesScalarFields` | ✅ `UiBlockJsonTests.cs`, `StreamEventJsonTests.cs` | ✅ green (orchestrator: 35/35 across the slnf) |
| 1-02-02 | 02 | 2 | FOUND-06 | T-2-03 | Unknown or missing `kind` deserializes to `UnknownBlock` / `UnknownPayload` and never throws; unknown block re-serializes byte-identical | unit | Same project: `UnknownKindDeserializesToUnknownBlockAndReserializesByteIdentical`, `MissingKindDeserializesToUnknownBlockWithNullOriginalKind`, `UnknownPayloadKindDeserializesToUnknownPayload`, `MissingPayloadKindDeserializesToUnknownPayloadWithNullOriginalKind` | ✅ | ✅ green (orchestrator) |
| 1-02-03 | 02 | 2 | FOUND-06 | — | A known kind missing a required member throws (no silent default) | unit | Same project: `KnownKindMissingRequiredMemberThrows` | ✅ | ✅ green (orchestrator) |
| 1-02-04 | 02 | 2 | FOUND-06 | T-2-02 | Reflection-based serialization is disabled and throws `InvalidOperationException` | unit | Same project: `PlainObjectSerializationThrowsWhenReflectionDisabled` | ✅ `ReflectionDisabledTests.cs` | ✅ green (orchestrator) |
| 1-02-05 | 02 | 2 | FOUND-06 | — | `StreamEventSeqComparer` orders by `Seq`; `ContractVersion` is three-part semver and the header name is stable | unit | Same project: `OrdersBySeqAscending`, `EqualSeqComparesAsEqual`, `EmptyListSortsToEmptyList`, `CurrentIsThreePartSemver`, `HeaderNameIsStable` | ✅ | ✅ green (orchestrator) |
| 1-02-06 | 02 | 2 | FOUND-06 | — | Each contract shape is declared only in `OmpOS.Contracts` | static | `grep -rnE --include=*.cs "(class\|record\|struct\|enum\|interface) (Session\|Message\|SessionStatus\|IntentEnvelope\|IntentKind\|Diagnostic\|UiBlock\|StreamEvent\|SandboxDescriptor\|SandboxStatus\|WorkflowRun\|WorkflowRunStatus\|OmpOSError\|ErrorCode\|ContractVersion)\b" . \| grep -v /bin/ \| grep -v /obj/ \| grep -v ^./src/OmpOS.Contracts/` → no output (exit 1) | ✅ | ✅ green (this audit); not in any test (W5) |
| 1-03-01 | 03 | 3 | FOUND-07 | T-1-04 | No lower layer references a higher layer; Web never references Harness.Acp/Rpc | arch | `dotnet test --project tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj -c Release` → Passed; `ArchUnit_LowerLayersDoNotDependOnHigher`, `ProjectGraph_RejectsUpwardReference`, `ProjectGraph_WebNeverReferencesProcessSpawningAssemblies` | ✅ `ArchUnitLayerTests.cs`, `ProjectGraphTests.cs`, `LayerMap.cs` | ✅ green (orchestrator) |
| 1-03-02 | 03 | 3 | FOUND-07 | — | Only tests may reference `OmpOS.Api` | arch | Same run: `ProjectGraph_RejectsApiReferenceFromNonTest` | ✅ | ✅ green (orchestrator) |
| 1-03-03 | 03 | 3 | FOUND-07 | — | Live-gate proof: the graph checker and the ArchUnit rule both fire on a known violation | arch (negative) | Same run: `ProjectGraph_RejectsSyntheticUpwardEdge`, `ArchUnit_NegativeRuleFires` | ✅ | ✅ green (orchestrator) |
| 1-03-04 | 03 | 3 | FOUND-08 | — | No Docker tooling in `bootstrap.sh`, `ci.yml`, `compose.yaml`; no tracked Dockerfile or docker-compose file | arch | Same run: `RepoHygiene_NoDockerTooling`, `NoDockerfileOrComposeFileNamed` | ✅ `RepoHygieneTests.cs` | ✅ green (orchestrator); repo-wide Docker-word scan this audit: prose and `docker.io/` only (W6) |
| 1-03-05 | 03 | 3 | FOUND-08 | — | `compose.yaml` validates under podman-compose with the `workflows` profile and exposes `temporal` | smoke | `podman-compose --profile workflows -f deploy/compose.yaml config --services \| grep -qx temporal` → exit 0; `podman-compose --profile workflows -f deploy/compose.yaml config -q` → exit 0 | ✅ `deploy/compose.yaml` | ✅ green (orchestrator; re-run this audit: both exit 0) |
| 1-03-06 | 03 | 3 | FOUND-09 | — | Eight documentation files exist, each over 400 bytes; `open-questions.md` has the exact header row and 27 rows | arch | Same run: `RepoHygiene_DocsSkeletonExists` | ✅ 8 files | ✅ green (orchestrator; this audit: 1713–6948 bytes each, header at line 5, 27 `ompos-maintainers` rows) |
| 1-03-07 | 03 | 3 | FOUND-05 | T-1-02 | `ci.yml` is valid YAML with the `build-test` and `shells` jobs | static | `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"` → exit 0 | ✅ `.github/workflows/ci.yml` | ✅ green (this audit) |
| 1-03-08 | 03 | 3 | FOUND-05 | T-1-01 | Vulnerable-package gate passes on the committed tree | gate | `dotnet list OmpOS.Linux.slnf package --vulnerable --include-transitive --format json > vuln.json`, then CI's JSON gate (`sys.exit(1 if hits else 0)`) → exit 0 | ✅ | ✅ green (this audit: list exit 0, 17 projects, 0 hits) |
| 1-03-09 | 03 | 3 | FOUND-05 | — | CI runs on every push and every PR | static | Read the `on:` block of `ci.yml` (`ci.yml:6-8`): `push:` with no branch filter, `pull_request:` | ✅ `.github/workflows/ci.yml` | ✅ green: `ci.yml:6-8` has `push:` with no branch filter and `pull_request:` (read at HEAD a26fe2f); G1 closed |
| 1-03-10 | 03 | 3 | FOUND-05 | T-1-01 | Vulnerability gate fails when a vulnerable package is present | gate (negative) | Feed a fixture JSON containing a non-empty `vulnerabilities` array through the CI gate script → exit 1 | ✅ (fixture not committed) | ⚠️ not exercised (W4) |
| 1-03-11 | 03 | 3 | FOUND-05 | T-1-02 | Secret scan step executes with a valid licence; remote CI and macOS job green | CI (remote) | See Manual-Only Verifications | — | ⬜ manual (blocked: no remote CI run observed; audit HO-1) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ not exercised · ⬜ manual*

Requirement FOUND-06 "no other project redefines these shapes" is checked by the static row `1-02-06` only; the 15 names are the FOUND-06 contract types.

---

## Requirement Coverage Summary

| Requirement | Passing automated check(s) | Manual / remote | Verdict |
|-------------|----------------------------|-----------------|---------|
| FOUND-01 | `./bootstrap.sh` exit 0; SDK pin 10.0.402 | Fresh-clone run (W1) | Covered; negative proof not exercised |
| FOUND-02 | `dotnet build … -warnaserror` 0 warnings; `Directory.Build.props` settings and CI condition | — | Covered; negative proofs not exercised (W2) |
| FOUND-03 | `dotnet restore --locked-mode` exit 0; 17 lock files = 17 projects; CPM transitive pinning | — | Covered; drift negative not exercised (W3) |
| FOUND-04 | `dotnet format --verify-no-changes` exit 0 | — | Covered |
| FOUND-05 | YAML parse; vulnerability gate 0 hits; compose `--services` and `config -q` exit 0; trigger scope `ci.yml:6-8` (1-03-09 green) | Remote CI run, gitleaks licence, macOS job (blocked: no remote run; audit HO-1) | **Partial**: automated checks green; fixture negative not exercised (W4) |
| FOUND-06 | Contracts test suite green (26 tests per 01-VERIFICATION, including theory cases); duplicate-declaration grep | — | Covered; no-redefinition is a grep, not a test (W5) |
| FOUND-07 | 9 `[Fact]` methods in `OmpOS.ArchitectureTests` (ArchUnitLayerTests 2, ProjectGraphTests 4, RepoHygieneTests 3), including synthetic and live-gate proofs | — | Covered |
| FOUND-08 | `RepoHygiene_NoDockerTooling`, `NoDockerfileOrComposeFileNamed`; compose `config` and `--services` exit 0 | Temporal runtime start (manual) | Covered for validation; runtime manual |
| FOUND-09 | `RepoHygiene_DocsSkeletonExists`; sizes 1713–6948 bytes | — | Covered |

---

## Wave 0 Requirements

- [x] `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `BannedSymbols.txt`, `BannedSymbols.NoProcess.txt`, `OmpOS.slnx`, `OmpOS.Linux.slnf` — present (this audit)
- [x] `tests/OmpOS.Contracts.Tests/` — `UiBlockJsonTests.cs`, `StreamEventJsonTests.cs`, `ReflectionDisabledTests.cs`, `StreamEventSeqComparerTests.cs`, `ContractVersionTests.cs`, csproj present (this audit)
- [x] `tests/OmpOS.ArchitectureTests/` — `ProjectGraphTests.cs`, `ArchUnitLayerTests.cs`, `RepoHygieneTests.cs`, `LayerMap.cs`, `ProjectGraphChecker.cs`, csproj present (this audit)
- [x] `bootstrap.sh`, `.github/workflows/ci.yml`, `deploy/compose.yaml` — present (this audit)
- [x] Framework install — none separate; restored from `Directory.Packages.props`
- [x] Documentation skeleton — all 8 D-07 files present (this audit)

Naming note: the research text used `CsprojGraph_RejectsSyntheticUpwardEdge` and `DocsSkeletonExists`. The implemented names are `ProjectGraph_RejectsSyntheticUpwardEdge` and `RepoHygiene_DocsSkeletonExists`; the commands above use the implemented names.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Remote CI `build-test` job is green on a push or PR | FOUND-05 | GitHub Actions cannot run from this environment; no remote run has been observed (01-VERIFICATION human item 1) | Open the Actions run for the commit. Confirm success on: `dotnet restore … --locked-mode`; `dotnet build … -warnaserror`; `dotnet test … --report-trx --coverlet …` with `Test run summary: Passed!`; `upload-artifact` `test-results` present; `dotnet format` step; "Vulnerable packages" step (exit 0, no JSON hits); gitleaks step (`No leaks found`); compose step (`temporal` printed, `config -q` exit 0). |
| gitleaks licence is available to the step | FOUND-05, T-1-02 | Requirement depends on whether the `swcstudio` GitHub repository is organisation-owned (Open Question 1); not determinable from the repo | Confirm the owner type in GitHub settings. If organisation-owned, confirm secret `GITLEAKS_LICENSE` exists (`gh secret list` shows names only) and that the gitleaks step passes. If it is missing, the step fails with the licence message. Record the outcome in `docs/open-questions.md`. |
| macOS `shells` job | FOUND-05 | `macos-15` runner has never executed; job is guarded by `hashFiles('apps/OmpOS.App/OmpOS.App.csproj')` | On a push or PR, confirm the job concludes `success`. While `apps/OmpOS.App/OmpOS.App.csproj` is absent, the checkout and setup-dotnet steps pass and the MAUI step is `skipped`. Confirm setup-dotnet installs SDK 10.0.402 from `global.json`. |
| Temporal dev server starts and reports healthy | FOUND-08 | Starts containers, so it is not a read-only check; compose validation does not prove runtime behaviour | `podman-compose --profile workflows -f deploy/compose.yaml up -d`; `podman ps` shows `ompos-temporal` as `healthy`; `podman exec ompos-temporal temporal operator cluster health --address 127.0.0.1:7233` → exit 0; `podman port ompos-temporal` shows only `127.0.0.1` bindings; confirms the `/home/temporal` volume is writable (Assumption A2). Then `podman-compose --profile workflows -f deploy/compose.yaml down`. |

---

## Gaps

- **G1 — FOUND-05 trigger scope (closed).** `.github/workflows/ci.yml:6-8` now has `push:` with no branch filter and `pull_request:`, so every push and every PR runs CI, as FOUND-05 requires. Re-checked as row `1-03-09` (green). No gap remains open. The blocker for `nyquist_compliant` is manual row `1-03-11` (see Manual-Only Verifications), not a gap.

## Warnings (non-blocking)

- **W1 — FOUND-01 negative proof and clean-clone run not exercised.** The orchestrator ran `./bootstrap.sh` in the working tree, not from a fresh clone. Evidence for non-zero exit is `set -euo pipefail` (`bootstrap.sh:6`) and the gated steps. Run check `1-01-03` and a fresh-clone bootstrap before `/gsd:verify-work`. Both are mutating and were out of scope for this read-only audit.
- **W2 — FOUND-02 negative proofs not exercised.** The research probe showed CS8600 (nullable) and RS0030 (banned symbol) failing as expected. Not re-run on this tree.
- **W3 — FOUND-03 drift negative not exercised.** NU1004 on a version bump without a lock update was not re-run on this tree.
- **W4 — FOUND-05 vulnerability-gate negative not exercised.** The gate logic was run on the clean tree only (0 hits). The fixture-with-vulnerability path (exit 1) is untested.
- **W5 — FOUND-06 no-redefinition is not a test.** Check `1-02-06` is an ad-hoc grep and is not part of `bootstrap.sh`. Candidate for a `RepoHygiene` test.
- **W6 — FOUND-08 prose hits.** A repo-wide Docker-word scan (excluding `.planning/`) finds prose and comments in `README.md`, `AGENTS.md`, `docs/decisions/ADR-0002-surface-technology.md`, `docs/runbooks/sandbox.md`, and the `docker.io/temporalio/temporal` image prefix. None is Docker tooling. The enforced scope is `bootstrap.sh`, `ci.yml` and `compose.yaml` (`RepoHygieneTests`), and those files contain only allowed phrases.
- **W7 — Compose volume path deviates from D-05.** `/home/temporal/temporal.db` instead of `/data/temporal.db` (Assumption A2, recorded in ADR-0001). Runtime ownership is covered by the manual Temporal item.

## Out-of-Scope Observation

`01-SECURITY.md` reports `OPEN_THREATS` with one open threat, T-1-SC (Blazor and locked-package supply-chain coverage: 54 of 65 locked IDs lack a verdict row). This is not a FOUND requirement and does not change this file's verdict. It is tracked in 01-SECURITY.md.

---

## Validation Sign-Off

- [x] All checks have an `<automated>` command or a Manual-Only entry
- [x] Sampling continuity: no run of consecutive checks without an automated command (manual items are listed explicitly)
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [ ] Feedback latency < 1 s — not achievable for `dotnet`; replaced by the 15 s quick-run target above
- [ ] Every FOUND requirement has an automated command that passes — G1 closed; still open: `1-03-10` not exercised (W4); remote items are manual
- [ ] `nyquist_compliant: true` set in frontmatter — **not set; blocked by manual row 1-03-11 (remote CI run, gitleaks licence, macOS job; audit HO-1)**

**Approval:** pending (verdict partial; row 1-03-11 is open and blocked by the remote CI run, audit HO-1; resolve it and re-run the Manual-Only checks before approval)
