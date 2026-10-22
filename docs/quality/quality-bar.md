# Quality bar (audit 2026-10-22)

Repository HEAD `d965ba6`, working tree as found. Other work was uncommitted in the tree during this audit (`git status` listed tracked edits to `Directory.Packages.props`, `apps/OmpOS.Native/*`, `src/OmpOS.Harness.Terminal/*` and others, plus untracked peer work). This audit changed only: `docs/open-questions.md` (132 rows appended, 0 deleted; `git diff --numstat` gives `132 0`), `docs/quality/`, `scripts/web-smoke/` and `docs/runbooks/evidence/quality-audit-2026-10-22/`.

No build, test run or `dotnet format` was run as a gate by this audit. The only `dotnet` commands were the `dotnet publish` of `apps/OmpOS.Web` and `services/OmpOS.Api` for WEB-01, each under `timeout 300` with `--no-restore`.

## 1. Gate checks

| # | Check | Yes or no | Proof (file:line) | Re-check command |
|---|---|---|---|---|
| 1 | Analyzers on; warnings as errors | **yes** | `Directory.Build.props:7` `TreatWarningsAsErrors` true; `:8` `EnableNETAnalyzers` true; `:9` `AnalysisLevel` latest-all. Gate build with `-warnaserror`: `bootstrap.sh:39`, `.github/workflows/ci.yml:42`. Caveat: 132 suppressions and 4 `.editorconfig` severity overrides lower what the analyzers report (section 2). | `grep -n "TreatWarningsAsErrors\|AnalysisLevel" Directory.Build.props` (static). Full check: `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` (not run here). |
| 2 | Formatter verified in bootstrap | **yes** | `bootstrap.sh:52` `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`; also `.github/workflows/ci.yml:64`. | `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` (not run here). |
| 3 | Architecture tests run in the gate | **yes** | `OmpOS.Linux.slnf:36` includes `tests/OmpOS.ArchitectureTests`. `bootstrap.sh:51` and `ci.yml:53` run `dotnet test OmpOS.Linux.slnf` (bootstrap filters only `Category=Live`). Repo-hygiene checks: `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:75-112` (open-questions shape, ID uniqueness) and `:114-123` (TODO markers link to an owned open question). | `dotnet test tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj -c Release --no-restore` (not run here). |
| 4 | Secret scan in CI | **yes (CI only)** | `ci.yml:22-23` pins gitleaks 8.30.1 and its SHA-256; `ci.yml:83-88` downloads it, checks the digest (`:86`) and runs `gitleaks git --redact --exit-code 1`. Not in `bootstrap.sh`. Organisation-repo licence is a separate open item (OQ-18). | Gitleaks 8.30.1 as CI installs it: `gitleaks git --redact --exit-code 1 --log-level warn .` (not run here). |
| 5 | Banned symbols | **yes** | `Directory.Build.props:38` adds `BannedSymbols.txt` (Newtonsoft; `Task.Result`; `Task.Wait()`; `Process.Start(string)`), `:39` adds `BannedSymbols.NoProcess.txt` (`System.Diagnostics.Process`) to every project except `OmpOS.Harness.*`, `OmpOS.Sandbox` and `*Tests`. Analyzer wiring: `Directory.Packages.props:69` `GlobalPackageReference` `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0, present in every `packages.lock.json` (for example `src/OmpOS.Parser/packages.lock.json:11`). | `grep -n "BannedApiAnalyzers" Directory.Packages.props` (static). Full check: a build as in row 1; a banned use fails with the analyzer's banned-API diagnostic (RS0030). |
| 6 | Locked restore | **yes (gate scope)** | `bootstrap.sh:38` and `ci.yml:41` run `dotnet restore OmpOS.Linux.slnf --locked-mode`. `Directory.Build.props:16` `RestorePackagesWithLockFile` true; `:25` `RestoreLockedMode` true when `CI=true`. All 54 projects in `OmpOS.Linux.slnf` have a `packages.lock.json`. **Exceptions:** `apps/OmpOS.App/OmpOS.App.csproj` and `tools/gweb/named/OmpOS.Gweb.Named.csproj` have none. Neither is in the filter. `apps/OmpOS.App` is tracked (13 files). `ci.yml:145` runs the `shells` job whenever that csproj exists, and `ci.yml:148` restores it with `--locked-mode`. [INFERENCE, not run: that restore should fail for want of a lock file.] | `dotnet restore OmpOS.Linux.slnf --locked-mode` (not run here). Static: `find . -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' -exec sh -c 'test -f "$(dirname "$1")/packages.lock.json" \|\| echo "$1"' _ {} \;` lists the two exceptions. |

## 2. Suppressions and skip markers

Full list, one line per occurrence: `docs/runbooks/evidence/quality-audit-2026-10-22/suppressions-full.tsv` (file, line, kind, rule, justification in source, source line). Method: exhaustive line scan of `.cs`, `.razor`, `.axaml`, project and `.editorconfig` files, excluding `bin`, `obj`, `TestResults`, `.planning/` and the root `docs/`. Counts are in `suppressions-summary.txt`.

| Kind | Count | Notes |
|---|---|---|
| `#pragma warning disable` | 18 | `CA1031` (catch-all) in production code; `VSTHRD003`, `CA1515` in test helpers and seams |
| `[SuppressMessage]` | 81 | `CA1031`, `CA2000`, `CA2007`/`MA0004`, `CA1515`, `CA5394`, `CA2100`, `CA1711`, `CA1034` |
| `.editorconfig` severity `none` | 4 | `CA1303` for all `*.{cs,vb}` (`.editorconfig:395`); `CA1707`, `CS1591`, `CA1062` for `tests/**.cs` (`:398-400`) |
| Skip markers | 28 | `Assert.Skip` / `Assert.SkipUnless` on missing POSIX tools (`sh`, `head`, `tr`) or Linux, and `[Fact(Skip=...)]` on live `omp` tests |
| `Category=Live` trait | 1 | `tests/OmpOS.Harness.Rpc.Tests/RpcLiveCaptureTests.cs:18`; excluded by `bootstrap.sh:51` only |
| `NoWarn`, `#nullable disable`, downgraded warnings | 0 | none found |
| **Total** | **132** | |

Findings:

- All 132 conflict with the repo rule "no pragma, NoWarn, SuppressMessage, Skip or disabled tests". None was removed, per the assignment.
- 128 carry a justification in source. The 4 `.editorconfig` overrides have none in the repo. Their only rationale is in `.planning/phases/01-foundation/01-RESEARCH.md`, outside `docs/`. These are the undocumented, unjustified-looking entries: `CA1303=none` removes that rule for every C# file, not for one site.
- Skip markers hide tests on hosts without the tool. CI has `sh`, `head` and `tr`, so they run there; a host without them reports skipped, not failed.
- Every occurrence has an open-question row, `SUP-001` to `SUP-132`, appended after `OQ-61` in `docs/open-questions.md` (lines 99 to 230). Owner `ompos-maintainers`, status `documented suppression`. `suppressions-id-map.tsv` maps each ID to its file and line. No existing row covered any of these occurrences.

## 3. WEB-01: browser smoke

**Result: PASS**, on commit `d965ba6`, 2026-10-22, against the working tree as found. Driver: `scripts/web-smoke/smoke.mjs`. Wrapper: `scripts/web-smoke/run-smoke.sh`. Result JSON: `web-smoke-d965ba6-2026-10-22.json`. Driver log: `web-smoke-d965ba6-2026-10-22.log`.

Chromium: `chromium`, `chromium-browser` and `chrome` are not on PATH. `google-chrome` is `/usr/bin/google-chrome`, version `Google Chrome 153.0.8010.36`, and was used. A Playwright build also exists at `~/.cache/ms-playwright/chromium-1248/chrome-linux64/chrome`, not used. Set `CHROME_BIN` to choose.

| Assertion (passing run) | Observed |
|---|---|
| First render clean | rendered; error bar hidden; 0 exceptions; 0 console errors; 0 reflection-disabled hits |
| Response completed | "Response complete." announced after the probe prompt |
| Probe markup | `strong` 1; `img` 0; `script` 0; no "Unsupported content"; user turn shown as escaped text |
| No error bar or console errors after prompt | error bar hidden; `consoleErrorsAfterPrompt` empty |
| Reload renders the same history | conversation items 2; opened index 1; `strong` 1; `img` 0; `script` 0; "Response complete." |
| axe-core 4.14.0 (WCAG 2.0/2.1 A and AA), empty state | 0 violations; 0 incomplete |
| axe-core 4.14.0, after streamed response | 0 violations; 0 incomplete |
| Send-button contrast | 18.26:1 (`rgb(0,0,0)` on `rgb(239,239,239)`), both states |
| Skip link | one Tab from body focuses `a.ompos-skip-link`; target `#ompos-conversation` exists |

Screenshots: `web-first-render`, `web-axe-empty`, `web-axe-after-response`, `web-skip-link-focused` (`.png`), and `web-vertical-slice-d965ba6-2026-10-22.webp`. axe-core SHA-256: `axe-core-4.14.0-d965ba6-2026-10-22.sha256`.

Run history. The smoke ran six times. Only attempt 6 is the result of record. Attempt logs are kept with `-attemptN` names.

| Attempt | Outcome | Cause | Change made |
|---|---|---|---|
| 1 | API publish failed; no browser | MSBuild internal error `Invalid node id specified: 3` | `-nodeReuse:false` on both publishes |
| 2 | API exited at start | `Api:Auth:Authority` and `Audience` required outside Development (OD-03); the published API defaults to Production | `ASPNETCORE_ENVIRONMENT=Development` |
| 3 | API exited at start | `Sessions:Store is required` | `Sessions__Store=InMemory`, `Coordination__Store=InMemory` |
| 4 | Browser ran; first render, axe and skip link passed; response never completed | every endpoint requires a bearer token by default; `/sessions` returned 401 ("You are offline.") | `Api__Auth__AllowDevelopmentPrincipal=true` (validator refuses it outside Development) |
| 5 | Response completed; reload assertion failed | driver defect: `Index.razor` selects the first listed conversation, which can be an empty session created on first load | driver opens each listed conversation until one renders its history |
| 6 | All 9 assertions true; exit 0 | — | result of record |

Gaps between `docs/runbooks/local-dev.md` and the running tree, for the owner to fix in the runbook (not edited here, outside this audit's paths):

- Its serve command omits `ASPNETCORE_ENVIRONMENT=Development`, `Sessions__Store`, `Coordination__Store` and `Api__Auth__AllowDevelopmentPrincipal`. Without them the API does not start, or the chat returns 401.
- It says the button is "Start new conversation". The tree has `aria-label="New conversation"` (visible text "+ New", `apps/OmpOS.Web/Pages/Index.razor:20`). The driver uses the tree's label.
- Its screenshot path is `docs/runbooks/evidence/web-vertical-slice-smoke.webp`. This run wrote `docs/runbooks/evidence/quality-audit-2026-10-22/web-vertical-slice-d965ba6-2026-10-22.webp`.

Limits: a local Development run with the explicit dev principal and in-memory stores. It does not exercise the production sign-in path (OD-03), and it does not cover the MAUI or native heads, or screen readers.

## 4. HO-03: podman-compose config

**Result: record still matches; config renders for the backend profile.** Full output: `ho-03-compose-config-d965ba6-2026-10-22.md`.

| Command (read-only) | Exit | Output |
|---|---|---|
| `podman-compose -f deploy/compose.yaml config` | **1** | `ValueError: required variable OMPOS_POSTGRES_PASSWORD is missing a value` |
| same, with the two password variables set to placeholders | **0** | `services: {}` and `WARNING: No services defined` |
| `podman-compose -f deploy/compose.yaml --profile backend config` (the test's argument order, placeholders set) | **0** | services `postgres`, `dragonfly`, `greptimedb`; no `temporal` |

Finding: without `--profile`, the compose file exposes no services, so the task's bare command exits 0 and validates nothing. The test and `ci.yml:92-102` pass `--profile backend`.

Comparison with `ho-03-podman-compose-config-2026-10-21.md`: the test name is unchanged; the file is `tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs`; the `[Fact]` is at line 133 and the closing brace at line 165, matching the record's "lines 133 to 165"; podman 4.9.3 and podman-compose 1.6.0 match. The 2026-10-21 pass itself was not re-run (no test run in this audit).

## 5. Findings for owners

- Repo rule conflict: 132 suppressions and skip markers (section 2).
- `ci.yml` `shells` job will run for `apps/OmpOS.App`, which has no lock file (row 6). [INFERENCE, not run]
- `apps/OmpOS.App` still exists although `local-dev.md` says it retires at v0.10.0 Phase 25.
- `tools/gweb/stock` and `tools/gweb/named` are outside the solution filter and CI. `named` has no lock file.
- Runbook drift for WEB-01 (section 3).

## Evidence folder

`docs/runbooks/evidence/quality-audit-2026-10-22/`: `suppressions-full.tsv`, `suppressions-summary.txt`, `suppressions-id-map.tsv`; `ho-03-compose-config-d965ba6-2026-10-22.md`; `web-smoke-d965ba6-2026-10-22.{json,log}`, the screenshots and `.webp`, `axe-core-4.14.0-d965ba6-2026-10-22.sha256`, `chrome-*`, `api-*`, `web-*` and `api-publish-*` logs; and `*-attemptN.*` files for attempts 1 to 5.
