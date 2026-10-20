---
phase: 01-foundation
asvs_level: 1
block_on: high
threats_total: 13
threats_open: 1
verdict: OPEN_THREATS
audited: 2026-10-20
scope: threat_model blocks in 01-01-PLAN.md, 01-02-PLAN.md, 01-03-PLAN.md, verified against the working tree at HEAD 634a9cc
supersedes: prior 01-SECURITY.md of the same date (T-1-SC was its sole open item)
---

# Phase 1 Foundation: Security Verification (re-run)

**Verdict: OPEN_THREATS. `threats_open: 1` (T-1-SC, severity high, blocks at `block_on: high`).**

## What changed since the prior report

- **The named gap is closed.** `Microsoft.AspNetCore.Components.Web`, `...WebAssembly`, and `...WebAssembly.DevServer` now have verdict rows (`.planning/phases/01-foundation/01-RESEARCH.md:252-254`). I checked each one against nuget.org (details under T-1-SC). The metadata and signatures support the `Approved` verdict.
- **T-1-SC stays open for a scope reason the prior report did not enumerate.** The Package Legitimacy Audit table has 13 rows and covers 11 of the 65 distinct package IDs resolved in the 17 tracked `packages.lock.json` files. The T-1-SC mitigation is "verdicts for packages that enter the build". 54 locked IDs have no row.
- **The other 12 threats are still mitigated or accepted.** Four cited line numbers had drifted; the current lines are below. Review finding M-01 (Temporal ports on all interfaces) is already fixed: `deploy/compose.yaml:10-11` bind `127.0.0.1`.

## Method

- Read the `<threat_model>` blocks of all three plans and the prior report. Re-grepped or re-read every cited location against the current tree.
- Blazor packages: fetched each `.nuspec` and `.nupkg` from `api.nuget.org/v3-flatcontainer` (version 10.0.13). Compared each `.nupkg` SHA-512 with NuGet's own `.nupkg.sha512` sidecar in `~/.nuget/packages`. Checked the `.signature.p7s` strings.
- Lock coverage: parsed all 17 tracked `packages.lock.json` files and compared the resolved IDs with the audit table.
- Also fetched nuspecs for the non-Microsoft locked IDs that lack rows (listed under T-1-SC).
- No builds, tests, or `bootstrap.sh` runs. No product or planning files edited besides this report. No commits.

## Threat Verification

| Threat ID | Category | Severity | Disposition | Evidence (path:line) | Status |
|-----------|----------|----------|-------------|----------------------|--------|
| T-1-01 | Tampering | high | mitigate | `Directory.Packages.props:3-4` (central management, transitive pinning). `Directory.Build.props:15` (`RestorePackagesWithLockFile`), `:16-17` (`NuGetAuditMode` all, `NuGetAuditLevel` low), `:7` (`TreatWarningsAsErrors`), `:22-25` (CI block sets `RestoreLockedMode` at `:24`). `.github/workflows/ci.yml:38` and `bootstrap.sh:25` run `dotnet restore --locked-mode`. 17 `packages.lock.json` files tracked (`git ls-files`). *Line numbers corrected: prior cited `ci.yml:36` and `bootstrap.sh:21`.* | mitigated |
| T-1-SC | Tampering | high | mitigate | **Blazor gap, now closed:** verdict rows `01-RESEARCH.md:252-254`. Versions pinned to 10.0.13 at `Directory.Packages.props:15-17` and consumed at `apps/OmpOS.Web/OmpOS.Web.csproj:9-10`, `src/OmpOS.UI/OmpOS.UI.csproj:9`. Independent checks in "T-1-SC detail" below. **Coverage gap, still open:** 54 of 65 locked IDs have no row (`01-RESEARCH.md:237-256`). | **open (blocking)** |
| T-1-02 | Information disclosure | medium | mitigate | `.gitignore:482` (`.env`), `:483` (`*.user`), `:484` (`TestResults/`). `git ls-files --error-unmatch .env` reports untracked, and `git check-ignore -v .env` resolves to `.gitignore:482`. `.env.example:4` holds only `OMPOS_ENV=Development`. Gitleaks gate `ci.yml:65-68`; CI secrets limited to `GITHUB_TOKEN` and `GITLEAKS_LICENSE` (`ci.yml:67-68`). Rescan of tracked files for AWS access-key, PEM private-key, GitHub PAT, and Slack token patterns found no matches (generic `password=` assignments were not rescanned). *Corrected: prior cited `.gitignore:7`, which is `*.rsuser`.* | mitigated |
| T-1-03 | Tampering | medium | accept | `bootstrap.sh:14-21`: installer fetched over HTTPS from `https://dot.net/v1/dotnet-install.sh` (`:20`) into a `mktemp -d` directory (`:18`, trap at `:19`), run with `--jsonfile global.json` (`:21`), skipped when the SDK directory exists (`:14`). No checksum pin. The gap is tracked as OQ-27 (`docs/open-questions.md:33`, status `open`). Accepted-risk entry below. *The prior observation about the fixed `/tmp` path is resolved: the code now uses `mktemp`.* | accepted |
| T-1-04 | Elevation of privilege | low | mitigate | `BannedSymbols.NoProcess.txt:1` bans `T:System.Diagnostics.Process`. `Directory.Build.props:37-38` applies it to every project except `OmpOS.Harness.*`, `OmpOS.Sandbox`, and `*Tests` (`:38` condition). The only `Process` use found is `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:54` (`Process.Start("git")`), which is in an exempt `*Tests` project. Web-reference guard: `tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs:12` (list), `:36-41` (asserts `OmpOS.Web` does not reference `OmpOS.Harness.Acp`/`Rpc`). | mitigated |
| T-2-01 | Tampering / Denial of service | medium | mitigate | `src/OmpOS.Contracts/Ui/UiBlockConverter.cs:41-49`: unknown kind or missing kind returns `UnknownBlock` (`:49`); no throw on that path. `src/OmpOS.Contracts/Streaming/StreamEventPayloadConverter.cs:37-42`: same for `UnknownPayload` (`:42`). Tests: `tests/OmpOS.Contracts.Tests/Ui/UiBlockJsonTests.cs:53` (unknown kind), `:64` (missing kind), `:75` (known kind missing a required member throws, by design); `tests/OmpOS.Contracts.Tests/Streaming/StreamEventJsonTests.cs:52`, `:64`. No `MaxDepth` override in tracked code. | mitigated |
| T-2-02 | Tampering | high | mitigate | `Directory.Build.props:13` sets `JsonSerializerIsReflectionEnabledByDefault` to `false`. No tracked csproj or props overrides it. Proof test: `tests/OmpOS.Contracts.Tests/Serialization/ReflectionDisabledTests.cs:15-17` asserts `InvalidOperationException`. Corroboration (build output, not authoritative): `tests/OmpOS.ArchitectureTests/bin/Release/net10.0/OmpOS.Web.runtimeconfig.json:47` is `false`. | mitigated |
| T-2-03 | Information disclosure | low | accept | `src/OmpOS.Contracts/Ui/UnknownBlock.cs:15` and `src/OmpOS.Contracts/Streaming/UnknownPayload.cs:15` retain `Raw` verbatim (needed for byte-identical re-serialization). Accepted-risk entry below. | accepted |
| T-3-01 | Information disclosure | high | mitigate | Gitleaks action `.github/workflows/ci.yml:65-68` with full history checkout (`fetch-depth: 0`, `ci.yml:32`). `.gitignore:482` covers `.env`. Secrets limited to `GITHUB_TOKEN` and `GITLEAKS_LICENSE`. *Not re-verified:* the runbook clause "runbooks never print secret values" is a documentation claim; I did not audit the runbooks. | mitigated |
| T-3-02 | Elevation of privilege | medium | mitigate | `.github/workflows/ci.yml:11-12`: top-level `permissions: contents: read`, no write scopes anywhere (the only workflow file is `ci.yml`). `ci.yml:14-16`: concurrency group per ref with `cancel-in-progress`. The `shells` job (`ci.yml:75`) has no permissions override and inherits read-only. | mitigated |
| T-3-03 | Tampering | high | mitigate | Vulnerability gate `.github/workflows/ci.yml:49-63`: `dotnet list ... --vulnerable --include-transitive` writes JSON; a Python step parses it and `sys.exit(1 if hits else 0)` at `:63`. If `dotnet list` fails, `json.load` raises on the bad file and the step fails closed. NU1903 backstop: `Directory.Build.props:16-17`, `:7`. *Note:* this gate runs only in CI, not in `bootstrap.sh` (see observations). | mitigated |
| T-3-04 | Tampering | medium | mitigate | Csproj-graph check `tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs:14-25`. ArchUnitNET layer rule `tests/OmpOS.ArchitectureTests/ArchUnitLayerTests.cs:21-22`. Both run in CI via `ci.yml:40` (`dotnet test`) and are in `OmpOS.Linux.slnf:20-21`. | mitigated |
| T-3-05 | Denial of service | low | mitigate | Same converters as T-2-01: `UiBlockConverter.cs:10-11` and `StreamEventPayloadConverter.cs:10-11` document "never throw" for unknown kinds. Invariant recorded in `docs/architecture.md:48`. *Documentation defect, not a control failure:* that line names `UnknownEvent`, which does not exist in `src/` or `tests/`; the type is `UnknownPayload`. | mitigated |

**threats_open = 1** (T-1-SC, severity high, status open, blocking).

Counts: mitigated 10 (T-1-01, T-1-02, T-1-04, T-2-01, T-2-02, T-3-01 to T-3-05), accepted 2 (T-1-03, T-2-03), open 1 (T-1-SC). Total 13.

## T-1-SC detail

### Blazor packages (closed)

| Package (10.0.13) | Lock evidence | Nuspec (nuget.org) | Package bytes | Signature |
|---|---|---|---|---|
| `Microsoft.AspNetCore.Components.Web` | `src/OmpOS.UI/packages.lock.json:11` (Direct); `apps/OmpOS.Web/packages.lock.json:278` (CentralTransitive) | authors `Microsoft`; projectUrl `https://asp.net/`; repository `https://github.com/dotnet/dotnet`, commit `4099f83977f4d29604a9aeb971fb0a7bc1d5f699`; MIT | SHA-512 equals NuGet's local `.nupkg.sha512` sidecar | `.signature.p7s` contains "Microsoft Corporation" and "NuGet.org Repository by Microsoft" |
| `Microsoft.AspNetCore.Components.WebAssembly` | `apps/OmpOS.Web/packages.lock.json:17` (Direct) | same as above | same | same |
| `Microsoft.AspNetCore.Components.WebAssembly.DevServer` | `apps/OmpOS.Web/packages.lock.json:30` (Direct) | same as above | same | same |

The table's `Approved` verdict for these three holds. Their publisher, repository, and version match the claim.

### Lock hash observation (non-blocking, not a verdict input)

- For these three packages, the lock `contentHash` does not equal the SHA-512 of the nuget.org bytes. Their `.nupkg.metadata` (NuGet's record) does equal the lock hash.
- The same divergence holds for every signed package in the cache: 75 of 75. The cache contains no unsigned counterexample, so I could not isolate a cause.
- Mechanism not confirmed. [INFERENCE] NuGet's `contentHash` excludes the repository signature.
- Effect: I could not reproduce the lock hash from the bytes independently. NuGet's own verification chain remains consistent (sidecar, metadata, and lock all agree within NuGet's own definitions).

### Coverage gap (open, blocking)

- 65 distinct locked IDs across 17 lock files (15 product, 2 test). 11 IDs have a verdict row. 54 do not.
- **39 `Microsoft.*` / `System.*` IDs have no row.** These are first-party. Most are in the shipped closure (e.g. `Microsoft.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Forms`, `Microsoft.Extensions.*`, `Microsoft.JSInterop*`); the rest are test-only (e.g. `Microsoft.Testing.Platform`, `Microsoft.Win32.Registry`, `System.ValueTuple`). The prior report's remediation text offered "an explicit verified-publisher exception recorded for Microsoft first-party packages" as an option. That exception is not recorded.
- **15 non-first-party IDs have no row.** All 15 are test-only: they appear only in `tests/OmpOS.ArchitectureTests/packages.lock.json` or `tests/OmpOS.Contracts.Tests/packages.lock.json`:
  - `CycleDetection` 2.0.0 (authors DanielBradley, RobertGiesecke; a fork, repo `github.com/RobertGiesecke/CycleDetection`; provenance needs a human call)
  - `DiffEngine` 11.3.0 (authors field is a contributors URL; repo `github.com/VerifyTests/DiffEngine`)
  - `EmptyFiles` 4.4.0 (authors field is a contributors URL; repo `github.com/SimonCropp/EmptyFiles`)
  - `JetBrains.Annotations` 2026.2.0 (JetBrains; repo `github.com/JetBrains/JetBrains.Annotations`)
  - `Mono.Cecil` 0.11.6 (Jb Evain; `github.com/jbevain/cecil`)
  - `Newtonsoft.Json` 13.0.4 (James Newton-King; `github.com/JamesNK/Newtonsoft.Json`)
  - `TngTech.ArchUnitNET` 0.13.4 (not fetched; the `TngTech.ArchUnitNET.xUnitV3` row names the same publisher)
  - `xunit.analyzers` 2.2.0 (xunit maintainers; `github.com/xunit/xunit.analyzers`)
  - 7 `xunit.v3.*` sub-packages (the `xunit.v3` row names the same publisher; not fetched individually)
- The only non-first-party ID in the shipped product lock files is `Meziantou.Analyzer`, which has a row.

### Why the verdict is OPEN

The Blazor gap the prior report named is fixed. T-1-SC's declared mitigation covers the packages that enter the build, not just the three named ones. The audit table covers 11 of 65. That leaves a high-severity threat open at `block_on: high`. I did not downgrade it.

### Remediation (documentary; no code change required)

1. Add verdict rows for the 15 non-first-party IDs. Publisher data for most is in the list above. `CycleDetection` needs a provenance decision.
2. Decide whether a documented first-party publisher rule may cover the 39 `Microsoft.*` / `System.*` IDs. That is a policy call; I did not make it. If accepted, record the rule explicitly in `01-RESEARCH.md`.
3. Correct the three new rows: their rationale ("same publisher and repo as the approved ASP.NET Core 10 packages") cites rows that do not exist in the table, and the "Age" column holds a version string.
4. Re-run `/gsd:secure-phase`. T-1-SC closes when 1 and 2 are complete. `threats_open` then becomes 0.

## Accepted Risks Log

| Threat ID | Severity | Rationale | Owner |
|-----------|----------|-----------|-------|
| T-1-03 | medium | `dotnet-install.sh` is fetched over HTTPS from the official `dot.net/v1` URL into a `mktemp` directory and pinned to the SDK version via `--jsonfile global.json` (`bootstrap.sh:14-21`). No checksum pin: no verified digest is published. Residual risk: a compromised upstream installer. Tracked as OQ-27 (`docs/open-questions.md:33`). | ompos-maintainers |
| T-2-03 | low | `UnknownBlock.Raw` and `UnknownPayload.Raw` retain received bytes verbatim (`UnknownBlock.cs:15`, `UnknownPayload.cs:15`), required for byte-identical re-serialization. Secret redaction inside payloads is deferred to the Phase 2 API logging design. | ompos-maintainers |

## Unregistered Flags

- `01-03-SUMMARY.md:145-147` `## Threat Flags`: "None". No new attack surface beyond the plan's threat model.
- `01-01-SUMMARY.md` and `01-02-SUMMARY.md` have no `## Threat Flags` section. Absence is recorded as absence, not as "none".
- No unregistered flags.

## Observations (non-blocking, not registered threats)

1. **Review M-01 is fixed.** `deploy/compose.yaml:10-11` bind `127.0.0.1`.
2. **Review M-03 is partly fixed.** `bootstrap.sh` now uses `mktemp` (`:18`). No checksum pin yet (OQ-27).
3. **Review M-02 (action tags) is unverified here.** `ci.yml:29` states the tags were checked on 2026-10-20 against the GitHub releases API. I did not re-check them. Actions are pinned by tag, not commit SHA, so T-3-01 and T-3-03 rely on the tag pins.
4. **The vulnerability gate is CI-only.** `bootstrap.sh:25-28` runs restore, build, test, and format but not `dotnet list --vulnerable`. Local bootstrap does not catch vulnerable packages.
5. **Review M-04 is open.** The compose gate (`ci.yml:71-73`) validates YAML only and never boots Temporal. Not a registered threat.
6. **Documentation defect.** `docs/architecture.md:48` names `UnknownEvent`; the type is `UnknownPayload`.
7. **Legitimacy table defects.** The three Blazor rows cite nonexistent ASP.NET Core 10 rows and put a version in the "Age" column (see T-1-SC remediation item 3).
8. **Lock hash not independently reproduced.** See "Lock hash observation" above.

## Summary

- Threats: 13 total. Severity `high` or above: 5 (T-1-01, T-1-SC, T-2-02, T-3-01, T-3-03).
- Closed: 12. Mitigated: 10. Accepted: 2 (T-1-03, T-2-03). Open and blocking: 1 (T-1-SC).
- Verdict: **OPEN_THREATS** (`threats_open: 1`). Remediation is documentary and listed under T-1-SC.
