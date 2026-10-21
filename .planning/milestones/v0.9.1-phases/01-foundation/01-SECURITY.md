---
phase: 01-foundation
asvs_level: 1
block_on: high
threats_total: 13
threats_open: 0
verdict: SECURED
audited: 2026-10-20
head: 86b2610
scope: threat_model blocks in 01-01-PLAN.md, 01-02-PLAN.md, 01-03-PLAN.md; Package Legitimacy Audit in 01-RESEARCH.md (lines 237-313); verified against the working tree at HEAD 86b2610
supersedes: prior 01-SECURITY.md (OPEN_THREATS, threats_open 1). Its sole blocker, the T-1-SC lock-coverage gap, is closed.
---

# Phase 1 Foundation: Security Verification

**Verdict: SECURED. `threats_open: 0`** at `asvs_level: 1`, `block_on: high`. All 13 threats are CLOSED (11 mitigated, 2 accepted). No open threat meets or exceeds `high`.

Two caveats carry through the whole report:

- **Rule P2 is metadata-based.** The 15 P2 verdicts rest on nuspec `<authors>`, `<owners>`, and repository fields. They involve no signature check, no publisher-account check on nuget.org, and no package-content check. Thirteen of the fifteen do not satisfy P2's literal wording (see Legitimacy). All 15 packages are test-only.
- **Rule P1 is also nuspec-field based.** Its 39 rows check the `Microsoft` author string and the `dotnet/` or `microsoft/` repository namespace. The signature and SHA-512 checks in the prior report covered only the three Blazor packages and were not repeated here.

## What changed since the prior report

- **Coverage is complete.** The audit table covers all 65 distinct package IDs in the 17 tracked `packages.lock.json` files. It has 66 rows: those 65 plus `podman-compose`, a PyPI tool used by CI (`.github/workflows/ci.yml:71`) that is not in any NuGet lock. The prior report's count (13 rows, 11 of 65) is superseded.
- **Line citations moved.** The table is now `01-RESEARCH.md:237-313`, and the Blazor rows are at `:306-308` (the prior report cited `:252-254`).
- **HEAD moved** from `634a9cc` to `86b2610`. Commit `86b2610` adds the 54 new legitimacy rows; `5c3238e` in between adds phase reports. I did not diff product code across that range. Every cited line was re-read at `86b2610`. The untracked `.planning/phases/02-core/` and `03-platform/` directories are outside this phase.

## Method

- **(a) Coverage recount.** Parsed all 17 tracked `packages.lock.json` files with `jq`, keeping entries with `type != "Project"`. That gives 65 distinct IDs (75 ID-version pairs; 10 IDs resolve to two versions). The table has 66 data rows: 65 exact matches to the lock IDs, plus `podman-compose`. There are no duplicates and no lock IDs without a row.
- **(b) Metadata spot-check.** `/tmp/legit-meta.json` holds 54 entries: 39 `first_party: true` and 15 `false`, all `ok: true`. The P1/P2 tags in the table match that split exactly. I compared all 54 against the lock versions, author strings, repository namespaces, and the P2 owner match. I also checked the 11 remaining rows against nuspecs in the local NuGet cache (`~/.nuget/packages`). Details are in the Legitimacy section.
- **(c) Threat re-check.** Re-read the three `<threat_model>` registers and grepped or read every cited location at HEAD `86b2610`.
- **Not run.** No builds, tests, `bootstrap.sh`, formatters, or network fetches. Converter and test assertions are verified by reading code and test names, not by execution. Items that depend on execution are marked `[INFERENCE]`.

## Legitimacy (T-1-SC evidence)

### Spot-check of table rows against nuspec data

`RESEARCH` = `01-RESEARCH.md`. "Meta" = `/tmp/legit-meta.json`. "Cache" = nuspec in `~/.nuget/packages`.

| Row | Rule tag | Nuspec evidence | Result |
|---|---|---|---|
| Microsoft.ApplicationInsights (RESEARCH:256) | P1 | Meta: authors `Microsoft`; repo `Microsoft/ApplicationInsights-dotnet` | Match |
| Microsoft.Bcl.AsyncInterfaces (:263) | P1 | Meta: authors `Microsoft`; repo `dotnet/runtime` | Match |
| System.ValueTuple (:296) | P1 | Meta: authors `Microsoft`; repo `dotnet/maintenance-packages` | Match |
| Microsoft.Testing.Extensions.TrxReport.Abstractions (:287) | P1 | Meta: authors `Microsoft`; repo `microsoft/testfx` | Match |
| CycleDetection (:252) | P2 | Meta: authors `DanielBradley,RobertGiesecke`; repo `RobertGiesecke/CycleDetection` | Token match (`RobertGiesecke`) |
| JetBrains.Annotations (:255) | P2 | Meta: authors `JetBrains`; repo `JetBrains/JetBrains.Annotations` | Exact match |
| Mono.Cecil (:291) | P2 | Meta: authors `Jb Evain`; repo `jbevain/cecil` | Name vs handle only. Not literal. |
| Newtonsoft.Json (:292) | P2 | Meta: authors `James Newton-King`; repo `JamesNK/Newtonsoft.Json` | First name vs handle only. Not literal. |
| DiffEngine (:253) | P2 | Meta: authors is the URL `github.com/VerifyTests/DiffEngine/graphs/contributors` | URL only; no person named |
| EmptyFiles (:254) | P2 | Meta: authors is the URL `github.com/SimonCropp/EmptyFiles/graphs/contributors` | URL only; no person named |
| TngTech.ArchUnitNET (:297) | P2 | Meta: authors `ArchUnitNET` (the project name); repo `TNG/ArchUnitNET` | **Mismatch.** Author is not the owner `TNG`. |
| xunit.analyzers (:298) and seven `xunit.v3.*` P2 rows (:299-305) | P2 | Meta: authors are individuals `jnewkirk`, `bradwilson`, and `marcind`; repo `xunit/xunit` | **No literal match.** The authors are maintainers of the owning org. |
| TngTech.ArchUnitNET.xUnitV3 (:245) | none | Cache: authors `ArchUnitNET.xUnitV3`; repo `TNG/ArchUnitNET` | **Mismatch.** Row names publisher "TNG Technology Consulting", which is absent from the nuspec. Repo owner is consistent. |
| coverlet.MTP (:246) | none | Cache: authors `tonerdo`; repo `coverlet-coverage/coverlet` | Repo matches. Row names no publisher. |
| Microsoft.Extensions.Configuration (:264), Microsoft.Extensions.Primitives (:280), and 8 other `Microsoft.Extensions.*` IDs | P1 | Meta and lock: 10.0.13. `tests/OmpOS.Contracts.Tests/packages.lock.json` resolves them to **10.0.12**. | **Version mismatch** for 10 IDs in one test project. Row says "10.0.13 (lock-resolved)." |
| Microsoft.AspNetCore.Components.Web (:306) | none | Cache: authors `Microsoft`; repo `dotnet/dotnet` | Match |

### Full-population results

- **P1 (39 rows).** All 39 have `first_party: true`. Every author is `Microsoft`. Every repository is under `dotnet/` or `microsoft/`: 34 `dotnet`, 4 `microsoft`, 1 `Microsoft`.
- **P2 (15 rows).** Sorted by strength of the owner match:
  - Exact token match: 2 (CycleDetection, JetBrains.Annotations)
  - Name normalized to handle: 1 (Mono.Cecil)
  - First name only: 1 (Newtonsoft.Json)
  - Owner appears only inside a URL in the authors field: 2 (DiffEngine, EmptyFiles)
  - Authors are maintainers of the owning org, not the owner string: 8 (`xunit.analyzers` and the seven `xunit.v3.*` packages)
  - No match: 1 (TngTech.ArchUnitNET)
- **Versions.** Meta versions equal the lock-resolved versions for all 54 IDs, except the 10 `Microsoft.Extensions.*` IDs above.
- **Shipping scope.** All 15 P2 packages appear only in `tests/OmpOS.ArchitectureTests/packages.lock.json` and/or `tests/OmpOS.Contracts.Tests/packages.lock.json`. None appear in a product lock file.
- **Product lock, non-P1 IDs.** Six shipped or build-time NuGet IDs have no P1 tag: `Meziantou.Analyzer`, `Microsoft.CodeAnalysis.BannedApiAnalyzers`, `Microsoft.VisualStudio.Threading.Analyzers`, and the three Blazor packages. Each has a row with a nuspec basis.

### Rule P2, stated plainly

P2 is metadata-based. It compares the nuspec `<authors>` and repository fields with the repository owner. A package author controls those fields, so the check establishes only that the metadata is internally consistent. It does not establish the publisher's identity, nor that the package bytes were signed by the publisher. Thirteen of the fifteen P2 rows do not satisfy the literal wording "nuspec publisher matches repository owner". Both the wording and the row tags should be corrected (R9).

## Threat Verification

Status key: CLOSED means present and verified at L1. Residual items are in the next section.

| Threat ID | Category | Severity | Disposition | Evidence (path:line) | Status |
|---|---|---|---|---|---|
| T-1-01 | Tampering | high | mitigate | `Directory.Packages.props:3-4` (`ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`). `Directory.Build.props:7` (`TreatWarningsAsErrors`), `:15` (`RestorePackagesWithLockFile`), `:16-17` (`NuGetAuditMode` all, `NuGetAuditLevel` low), `:22-25` (CI block; `RestoreLockedMode` at `:24`). `.github/workflows/ci.yml:38` and `bootstrap.sh:25` run `dotnet restore … --locked-mode`. 17 `packages.lock.json` files tracked. | CLOSED. Residual R3. |
| T-1-SC | Tampering | high | mitigate | Legitimacy audit `01-RESEARCH.md:237-313`: 66 rows, all `OK` and `Approved`, covering 65 of 65 lock IDs. Central pins: `Directory.Packages.props:15-17` (Blazor, 10.0.13), `:31` (BannedApiAnalyzers). Coverage check in §Method. | CLOSED. Residual R1, R2, R9. Not blocking under `block_on: high`. |
| T-1-02 | Information disclosure | medium | mitigate | `.gitignore:482` (`.env`), `:483` (`*.user`), `:484` (`TestResults/`). `.env.example:4` (`OMPOS_ENV=Development` only). `git ls-files` shows no tracked `.env`. `ci.yml:65-68` (gitleaks). Regex scan of tracked files for AWS, GitHub PAT, PEM private-key, Slack, and `sk-` key shapes: no hits. | CLOSED. gitleaks not run locally (R11). |
| T-1-03 | Tampering | medium | accept | `bootstrap.sh:14-21`: installer fetched only when the SDK directory is absent, from `https://dot.net/v1`, pinned by `--jsonfile global.json`. `mktemp` at `:18`. No checksum. Tracked as `docs/open-questions.md:33` (OQ-27). | ACCEPTED. Logged below. Residual R8. |
| T-1-04 | Elevation of privilege | low | mitigate | `BannedSymbols.NoProcess.txt:1` (`T:System.Diagnostics.Process`). `Directory.Packages.props:31` (BannedApiAnalyzers as GlobalPackageReference). `Directory.Build.props:38` (applied except to `OmpOS.Harness.*`, `OmpOS.Sandbox`, and `*Tests`). `tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs:12` (`ProcessSpawningAssemblies` = Harness.Acp, Harness.Rpc) and `:35-41` (Web never references them). `apps/OmpOS.Web/OmpOS.Web.csproj:14-15` (Web references Contracts and UI only). `src/OmpOS.UI/OmpOS.UI.csproj`: no `ProjectReference`. Grep: no `System.Diagnostics`, `Process.Start`, or `ProcessStartInfo` in `src/`, `services/`, `apps/`. | CLOSED. Residual R7. |
| T-2-01 | Tampering / DoS | medium | mitigate | `src/OmpOS.Contracts/Ui/UiBlockConverter.cs:8-11` (doc), `:38-40` (kind lookup; non-string kind → null), `:41-49` (unknown or missing kind → `UnknownBlock`). `src/OmpOS.Contracts/Streaming/StreamEventPayloadConverter.cs:10-11`, `:34-42` (same pattern → `UnknownPayload`). Tests: `tests/OmpOS.Contracts.Tests/Ui/UiBlockJsonTests.cs:53-75`, `tests/OmpOS.Contracts.Tests/Streaming/StreamEventJsonTests.cs:52-64`. | CLOSED for object payloads. Residual R6. |
| T-2-02 | Tampering | high | mitigate | `Directory.Build.props:13` (`JsonSerializerIsReflectionEnabledByDefault` = false). `tests/OmpOS.Contracts.Tests/Serialization/ReflectionDisabledTests.cs:14-18` (asserts `InvalidOperationException` on plain-object serialization). | CLOSED. Test not executed (R11). |
| T-2-03 | Information disclosure | low | accept | `src/OmpOS.Contracts/Ui/UnknownBlock.cs:15` and `src/OmpOS.Contracts/Streaming/UnknownPayload.cs:15` (`Raw` JsonElement). Retained at `UiBlockConverter.cs:49` and `StreamEventPayloadConverter.cs:42`. | ACCEPTED. Logged below. |
| T-3-01 | Information disclosure | high | mitigate | `.github/workflows/ci.yml:30-32` (checkout with `fetch-depth: 0`). `ci.yml:65-68` (`gitleaks/gitleaks-action@v3`, `GITLEAKS_LICENSE` from secrets). `.gitignore:482`. | CLOSED. Residual R5. |
| T-3-02 | Elevation of privilege | medium | mitigate | `ci.yml:11-12` (top-level `permissions: contents: read`; no write scope anywhere in the file). `ci.yml:14-16` (`concurrency` per ref, `cancel-in-progress`). | CLOSED. |
| T-3-03 | Tampering | high | mitigate | `ci.yml:47-64`: `dotnet list OmpOS.Linux.slnf package --vulnerable --include-transitive --format json`, parsed by Python; `sys.exit(1 if hits else 0)` at `:63`. NU1903 backstop: `Directory.Build.props:16-17` and `:7`. `bootstrap.sh:25-28` does not run this gate. | CLOSED (CI gate). Residual R4. |
| T-3-04 | Tampering | medium | mitigate | `tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs:14-25` (upward reference) and `:43-50` (synthetic edge). `tests/OmpOS.ArchitectureTests/ArchUnitLayerTests.cs:21-22` (lower layers do not depend on higher). `ci.yml:40` (`dotnet test`). `OmpOS.Linux.slnf:20-21` (both test projects included). | CLOSED. |
| T-3-05 | Denial of service | low | mitigate | Same converters as T-2-01 (`UiBlockConverter.cs:8-11`, `StreamEventPayloadConverter.cs:10-11`). Tests: `UiBlockJsonTests.cs:53`, `StreamEventJsonTests.cs:52`. `docs/architecture.md:48` records the invariant, but names `UnknownEvent`, which does not exist. The type is `UnknownPayload`. | CLOSED. Documentation defect (R9). |

**Counts.** 13 threats: 11 mitigated and CLOSED, 2 accepted (T-1-03, T-2-03). Open: 0. The five threats at severity `high` or above are T-1-01, T-1-SC, T-2-02, T-3-01, and T-3-03. All five are CLOSED.

**Unregistered flags.** `01-03-SUMMARY.md:145-147` `## Threat Flags`: "None". `01-01-SUMMARY.md` and `01-02-SUMMARY.md` have no `## Threat Flags` section. No unregistered flags.

## Accepted Risks

| Threat | Severity | Rationale | Owner | Review trigger |
|---|---|---|---|---|
| T-1-03 | medium | No verified digest for `dotnet-install.sh` is published. Checksum pinning would need its own verified update channel. The installer runs only when the SDK is absent. Tracked as OQ-27. | ompos-maintainers | Any published digest or signed installer channel for dot.net |
| T-2-03 | low | `Raw` must hold the received bytes for byte-identical re-serialization. Secret redaction inside payloads is a Phase 2 logging concern. | ompos-maintainers | Phase 2 API logging design |

## Residual Risk (non-blocking)

Each residual is below `block_on: high` or is a documentary issue. None changes the verdict.

- **R1. P2 is metadata-only; 13 of 15 rows fail the literal rule.** The 15 test-only packages are not shipped. The weakest case is TngTech.ArchUnitNET: its nuspec author is `ArchUnitNET` and its owner is `TNG`. A sibling row (`TngTech.ArchUnitNET.xUnitV3`) names TNG as publisher, and the same repository backs both, which supports the verdict. A dependency-confusion or account-takeover compromise of any of these is not detectable from this evidence. Exposure is limited to the test projects.
- **R2. P1 is metadata-only.** It rests on the nuspec author string and repository URL. NuGet's repository signing is the only cryptographic control on these bytes, and this audit did not verify it per package.
- **R3. Microsoft.Extensions.\* at 10.0.12 in test lock.** Ten transitive IDs resolve to 10.0.12 in `tests/OmpOS.Contracts.Tests/packages.lock.json`, and to 10.0.13 elsewhere. No central pin exists for them, so per-project resolution is expected under `CentralPackageTransitivePinningEnabled`. The lock is consistent with itself. The 10.0.12 artifacts have no nuspec record in `/tmp/legit-meta.json`, and the table's "10.0.13 (lock-resolved)" claim is inaccurate for that project.
- **R4. Vulnerability gate is CI-only and reads one JSON field.** `bootstrap.sh:25-28` does not run `dotnet list --vulnerable`. The gate inspects only `vulnerabilities` arrays. `[INFERENCE]` If the audit data fetch fails, the SDK may report a problem rather than hits, and the gate would pass. The NU1903 restore backstop (`Directory.Build.props:16-17`) still covers the restore path.
- **R5. Actions are pinned by tag, not commit SHA.** `ci.yml:30,33,41,65`. The tag verification comment at `ci.yml:28-29` is not re-verified here. Review finding M-02 is therefore unverified.
- **R6. Converters throw on a non-object root.** `[INFERENCE]` `JsonElement.TryGetProperty` throws `InvalidOperationException` when the value is not an object. Both read paths call it first (`UiBlockConverter.cs:38`, `StreamEventPayloadConverter.cs:34`). A top-level array, string, or null therefore throws rather than degrading to `Unknown*`, which contradicts the "never throw" wording at `UiBlockConverter.cs:8-11`. Medium severity, below `high`, so non-blocking. Fix: check `root.ValueKind == JsonValueKind.Object` before the lookup.
- **R7. Process-ban guard is narrower than the ban's exemption list.** `ProcessSpawningAssemblies` covers only `OmpOS.Harness.Acp` and `OmpOS.Harness.Rpc`. `OmpOS.Sandbox` is exempt from the symbol ban but not from the architecture test. Web does not reference it today. The symbol ban does not stop P/Invoke or other native process spawning. No such code exists in the tree.
- **R8. Installer has no checksum.** T-1-03, OQ-27.
- **R9. Documentation defects.** `docs/architecture.md:48` says `UnknownEvent`; the type is `UnknownPayload`. In `01-RESEARCH.md`, the Blazor rows (`:306-308`) put a version in the "Age" column. The TngTech.ArchUnitNET row (`:297`) claims a P2 owner match that does not exist. The TngTech.ArchUnitNET.xUnitV3 row (`:245`) names a publisher absent from its nuspec. The `Microsoft.Extensions.*` rows claim 10.0.13 for all projects. P2's wording should be restated, for example as "publisher is a named maintainer of the repository owner, metadata only", and each row should carry its own rule tag.
- **R10. Lock hash not reproduced.** Carried from the prior report. The lock `contentHash` does not equal the SHA-512 of the nuget.org bytes for the signed packages checked. NuGet's own sidecar and `.nupkg.metadata` agree with the lock. `[INFERENCE]` The cause is NuGet's `contentHash` excluding the repository signature. Not verdict-relevant.
- **R11. Nothing executed.** No build, test, gitleaks, or `bootstrap.sh` run. Assertions in `ReflectionDisabledTests`, the converter tests, and the architecture tests were verified by reading their code, not by running them. The secret scan is a regex subset, not gitleaks.

## Recommended documentary follow-ups

1. Correct the TngTech.ArchUnitNET row and the TngTech.ArchUnitNET.xUnitV3 publisher claim in `01-RESEARCH.md` (R9).
2. Restate rule P2 as metadata-based, and give the 11 untagged rows an explicit rule or basis (R1, R9).
3. Correct the `Microsoft.Extensions.*` version claim, or add 10.0.12 nuspec evidence (R3).
4. Replace `UnknownEvent` with `UnknownPayload` at `docs/architecture.md:48` (R9).
5. Add an explicit `OmpOS.Sandbox` entry to `ProcessSpawningAssemblies` or document why it is excluded (R7).
6. Guard non-object JSON in both converters before `TryGetProperty` (R6).

## Summary

- Threats: 13 total. Severity `high` or above: 5 (T-1-01, T-1-SC, T-2-02, T-3-01, T-3-03), all CLOSED.
- Closed: 11 mitigated. Accepted: 2 (T-1-03, T-2-03). Open: 0.
- `threats_open: 0`. Verdict: **SECURED**, with residuals R1-R11 as documented above. R1 and R2 are the principal residual risks: P2 is metadata-based, and P1 is nuspec-field based. Neither is signature-verified in this audit.
