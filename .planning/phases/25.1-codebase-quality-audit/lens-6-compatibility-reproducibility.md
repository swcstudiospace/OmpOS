# Lens 6: Compatibility and reproducibility

QUALITY_BAR items: Compatibility and reproducibility; Lint, type-check, test, build (tool pins).
Repository: /root/src/repos/autonogrammer at commit 59e9dd1. Read-only audit. No build, restore, test, or package manager was run.

Severity rule applied. The lens brief classifies every unpinned item by whether CI executes it: executed by CI is High, documentation of a developer step is Low. Two items fall outside that split and are rated on the common definition, as noted in their evidence: `vercel.ts:28` (rated High, external script executed on every hosted build) and `deploy/api.Containerfile` (rated Low, documented developer build that CI does not run). See Method.

## 1. Findings table

Total findings: 20 (High 10, Medium 1, Low 9). The table holds all 20; the 40-row cap was not reached.

| file:line | QUALITY_BAR item | severity | disposition | evidence (120 chars or fewer) |
|---|---|---|---|---|
| .github/workflows/ci.yml:33,36,59,132,133 | Compatibility and reproducibility (action pins) | High | fix | "uses: actions/checkout@v7, setup-dotnet@v6, upload-artifact@v7: 5 of 5 pinned by tag; 0 by 40-char SHA" |
| .github/workflows/ci.yml:29,129 | Compatibility and reproducibility (runner) | High | fix | "runs-on: ubuntu-latest in build-test and android jobs; runner image changes without a repo commit" |
| .github/workflows/ci.yml:45 | Compatibility and reproducibility (tool pins) | High | defer | "Unversioned `sudo apt-get install -y podman` runs in CI when podman is absent" |
| .github/workflows/ci.yml:73,140 | Lint, type-check, test, build (tool pins) | High | accept | "python3 comes from the runner image, unpinned; check-vulnerable-packages.py imports only json and sys" |
| .github/workflows/ci.yml:141 | Compatibility and reproducibility (tool pins) | High | accept | "`dotnet workload install android` has no version; [INFERENCE] set follows SDK 10.0.402 in global.json" |
| global.json:2 | Compatibility and reproducibility (SDK pin) | High | defer | "rollForward latestPatch lets the SDK resolve above 10.0.402; bootstrap.sh:22 SHA-512 covers 10.0.402 only" |
| deploy/compose.yaml:9,27,38 | Compatibility and reproducibility (images) | High | fix | "Backend images pinned by tag, not digest (timescaledb 2.13.1-pg16, dragonfly v1.27.1, greptimedb v1.1.4); CI runs up" |
| scripts/test-services.sh:11,12 | Compatibility and reproducibility (images) | High | fix | "Test DB and Dragonfly images pinned by tag, not digest; CI (ci.yml:48) and bootstrap.sh:54 run this script" |
| scripts/web-smoke/run-smoke.sh:128,129 | Compatibility and reproducibility (downloads) | High | fix | "npm pack axe-core@4.14.0 (pinned :35) with no hash; line 129 records sha256 after download, compares nothing" |
| vercel.ts:28 | Compatibility and reproducibility (downloads) | High | fix | "Vercel installCommand curls dot.net dotnet-install.sh and runs it unverified each build; not GitHub CI, see Method" |
| src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs:730 | Compatibility and reproducibility (images) | Medium | defer | "Spec.Image copied to the compose service unchecked; no tag or digest rule for caller-supplied images" |
| src/Autonogrammer.Sandbox/PodmanDriverSection.cs:20,24 | Compatibility and reproducibility (images) | Low | fix | "Defaults end in :latest; rendered only by GenerateGatewayToml, called from GoldenPolicyTests.cs:26, no prod caller" |
| deploy/api.Containerfile:10,11 | Compatibility and reproducibility (images) | Low | defer | "SDK and runtime base images pinned by tag (10.0.402-noble, 10.0.13-noble), not digest; CI never builds this file" |
| deploy/api.Containerfile:47,48 | Compatibility and reproducibility (tool pins) | Low | accept | "apt-get install ca-certificates curl unzip with no versions; build stage only; CI never builds this file" |
| deploy/api.Containerfile:63 | Compatibility and reproducibility (downloads) | Low | accept | "bun install --production resolves transitive ranges at build; documented as NOT FROZEN at :39-41" |
| .github/workflows/ci.yml:82 | Lint, type-check, test, build (tool pins) | Low | accept | "pipx install podman-compose==1.6.0: exact version, no hash check; bootstrap.sh:65 repeats the pin" |
| .github/workflows/ci.yml:53 | Lint, type-check, test, build (test gate) | Low | fix | "CI runs dotnet test without the Category=Live filter bootstrap.sh:56 applies; ci.yml:2 says it mirrors bootstrap" |
| .github/workflows/ci.yml:156 | Compatibility and reproducibility (tool pins) | Low | accept | "sdkmanager installs platforms;android-36 and build-tools;36.0.0 by identifier; no repo-side checksum" |
| Directory.Build.props:25 | Compatibility and reproducibility (lock files) | Low | accept | "RestoreLockedMode only when CI=true; local restores rewrite packages.lock.json; CI and bootstrap pass --locked-mode" |
| tests/Autonogrammer.Sandbox.Tests/SandboxLifecycleTests.cs:209 | Compatibility and reproducibility (images) | Low | accept | "alpine:latest in 13 test fixtures; grep found no podman pull command in tests/Autonogrammer.Sandbox.Tests" |

Notes on the rows:
- `ci.yml:29,129` and `ci.yml:33,...` each cover two or more sites on one row.
- `ci.yml:141` is classified High by the brief's CI rule. Its disposition is accept because the Android workload set is tied to the pinned SDK; the repo does not pin it directly.

## 2. Scans

Every pattern run, with hit count. "Fine" entries give the reason a hit is not a finding.

| Pattern (tool and scope) | Hits | Classification |
|---|---|---|
| `Version="\*"\|Version="[^"]*\*"` in `*.csproj`, `*.props`, `*.targets` (git grep) | 0 | No floating versions. |
| `\[[0-9.]+,\s*\)` in same scope | 0 | No open-ended ranges. |
| `git ls-files -- '*packages.lock.json'` | 75 lock files | 75 `.csproj` files, one lock per project directory. |
| Lock coverage check: each `.csproj` directory against a tracked `packages.lock.json` | 0 projects without a lock; 0 locks without a project | Every restoring project has a committed lock. |
| `PackageReference[^>]*Version=` in `*.csproj` | 0 | Central package management is respected at project level. |
| `<PackageReference` count, `.csproj` files with no PackageReference | 22 `.csproj` with none | These still carry locks (empty graphs). Not a gap. |
| `PackageVersion Include` without `Version="` in `*.props` | 0 | All 68 `PackageVersion` items carry an explicit version. |
| `GlobalPackageReference` (Directory.Packages.props:69-71) | 3 | Analyzers pinned exactly: Meziantou.Analyzer 3.0.315, VS Threading 18.12.1, BannedApiAnalyzers 5.6.0. |
| Central package management | Present | `ManagePackageVersionsCentrally` true at Directory.Packages.props:3 and Directory.Build.props:14; transitive pinning on at Directory.Build.props:15. |
| `RestorePackagesWithLockFile` / `RestoreLockedMode` | 1 / 1 | Lock generation at Directory.Build.props:16; locked mode only under `CI=true` at :25. |
| `global.json` (read in full) | sdk 10.0.402, rollForward latestPatch | Finding R6. |
| `TargetFramework` not net10.0, `LangVersion` in `*.csproj`, `*.props`, `*.targets` | 0 TFM overrides; 1 `LangVersion` line (Directory.Build.props:4) | One TFM, set centrally; `LangVersion latest` is a setting, not a version pin. |
| `curl\s+.*-o` in `.github/workflows`, `scripts`, `bootstrap.sh` | 3 | ci.yml:76 (gitleaks; sha256 check at :77, pass). bootstrap.sh:31 (SDK; SHA-512 at :32, pass). run-smoke.sh:77 is a readiness probe (`curl -s -o /dev/null`), not a download: fine. |
| `uses:\s*[^@]+@` in `.github/workflows` | 5 | All five tag-pinned (ci.yml:33,36,59,132,133). 0 SHA-pinned. Finding R1. |
| `:latest\b` in tracked `Dockerfile*` / `Containerfile*` | 0 | The only container build file is deploy/api.Containerfile, which has no `:latest`. |
| `:latest\b` across tracked files, excluding docs/ and .planning/ | 17 | 13 `alpine:latest` in test fixtures (Low row R20); 2 defaults in PodmanDriverSection.cs (R12); 2 in test golden toml (rendered by the golden test). Fine: test data. |
| `npm (install\|i)\b` and `npm ci` in `scripts`, `.github`, `bootstrap.sh` | 0 | No npm install or ci path in CI. |
| `\bnpm\b\|\bnpx\b` outside docs/.planning/*.md | 8 | 1 `npm pack` (run-smoke.sh:128, Finding R9); 1 comment (deploy/api.Containerfile:36, a version string); 6 in tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames, recorded RPC text (not invocations). |
| `dotnet tool (install\|update)` in all code, scripts, and CI paths | 0 | No tool install. No `.config/dotnet-tools.json` tracked (`git ls-files` grep: 0). |
| `Category=Live` / `Trait("Category"` in tests, src, services, apps | 1 (tests/Autonogrammer.Harness.Rpc.Tests/RpcLiveCaptureTests.cs:18) | Skipped unless AUTONOGRAMMER_RPC_LIVE_CAPTURE=1 and omp on PATH (:46). CI does not set either. Finding R17 concerns the filter, not the skip. |
| `--locked-mode` in ci.yml, bootstrap.sh, deploy/api.Containerfile (read directly) | 4 | ci.yml:41 and :136; bootstrap.sh:38; api.Containerfile:27. Locked restore enforced on every CI and build path. |
| `sha256sum\|sha512sum` in scripts, .github, bootstrap.sh, deploy | 5 | ci.yml:77 (gitleaks, verified); bootstrap.sh:32 (SDK, verified); api.Containerfile:52 (bun, verified); :58 (omp tgz, verified); run-smoke.sh:129 (axe; records a stamp, does not compare: Finding R9). |
| `wget\|Invoke-WebRequest\|urllib\|requests\.get\|git clone\|pip install\|apt-get` in scripts, .github, bootstrap.sh, deploy, tools, services, apps | 3 | ci.yml:45 (Finding R3); api.Containerfile:47 (apt-get update) and :48 (Finding R14). |
| `podman-compose\|pipx` in .github and bootstrap.sh | 7 | ci.yml:80, :82 (pinned podman-compose==1.6.0, Finding R16), :83, :93, :95, :124; bootstrap.sh:65 (hint). |
| `runs-on:` in .github | 2 | Both `ubuntu-latest` (Finding R2). |
| Image references in deploy/compose.yaml and scripts/test-services.sh (`image:` and `*_IMAGE=`) | 5 | compose.yaml:9,27,38; test-services.sh:11,12 (Findings R7, R8). All tag pins, none digest-pinned. |
| `Spec.Image` / `Image =` in src/Autonogrammer.Sandbox | 2 | LocalPodmanComposeProvider.cs:730 (Finding R11); AgentOsProvider.cs:335 rejects image selection (fine, explicit). |
| `GenerateGatewayToml` callers | 1 caller outside its definition | GoldenPolicyTests.cs:26 (test only). Supports R12 severity. |
| `Containerfile\|podman build` in .github, scripts, bootstrap.sh | 0 | CI never builds deploy/api.Containerfile (basis for R13 to R15 Low). |
| Lines 76-125 of ci.yml, bootstrap.sh, deploy/api.Containerfile, vercel.ts | read in full | Used for every item above. |

## 3. Gaps

- Lock freshness (whether each lock matches current props and project graph). Needs `dotnet restore`, which the brief forbids. Only presence of each lock was checked.
- Whether `rollForward: latestPatch` resolves to a patch newer than 10.0.402 on the runner. Needs a resolver run. Finding R6 is stated as possibility, not observed behaviour.
- Whether the five action tags resolve to commits that are themselves immutable. Needs GitHub API calls, outside this read-only scope. The ci.yml comment (lines 31-32) says tags were verified 2026-10-20; that was not re-checked.
- Upstream mutability of image tags (timescaledb, dragonfly, greptimedb, dotnet base images). Not checkable offline. Digest pins are the only immutable form.
- Runner image contents (ubuntu-latest: python3, npm, podman, Chrome, Android SDK). Not checkable without running CI.
- Hosted Vercel project settings that may override the `vercel.ts` install command. Outside the repository.
- Whether the Containerfile builds reproducibly, and whether `bun install` in the image yields the same tree twice. Needs `podman build`, forbidden here.
- Whether the test sets from `ci.yml:53` and `bootstrap.sh:56` differ in practice. Only the filter text was compared (R17); the result was not run.
- Vulnerability status of pinned versions. Owned by Lens 2 and by the CI vulnerable-packages gate; not assessed here.

## 4. Method

I started from the tracked build inputs: `git ls-files` for every `.csproj`, `.props`, `.targets`, lock file, `global.json`, workflow, script, Containerfile, compose file, and `vercel.ts`. I read the four files that define how the repository builds and gets its tools in full: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `.github/workflows/ci.yml`. I also read `bootstrap.sh`, `deploy/api.Containerfile`, `scripts/web-smoke/run-smoke.sh` (download section), and `vercel.ts` (install command). From those files I took every download, image reference, and runner tool, then ran the grep patterns in section 2 to find any I had missed. I judged each unpinned item by whether CI executes it, per the brief. Two items fall outside that split and I rated them explicitly: `vercel.ts:28` runs an unverified remote script on every hosted build, and I rated it High on the common external-input definition. `deploy/api.Containerfile` is a documented developer build that CI never runs, so I rated it Low. The package-version scans came back clean, so the findings concentrate on runner, action, image, and download pins, which is where this repository is not frozen.
