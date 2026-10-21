---
phase: 01-foundation
reviewed: 2026-10-20T00:00:00Z
depth: standard
files_reviewed: 62
status: findings
findings:
  critical: 0
  high: 0
  medium: 4
  low: 4
  info: 2
  total: 10
---

# Phase 01 (Foundation): Code Review Report

**Reviewed:** 2026-10-20
**Depth:** standard (per-file read of all product, test, and infra sources; cross-file checks on layer map vs. project graph vs. solution files)
**Files Reviewed:** 62 (every key-file from `01-01/01-02/01-03-SUMMARY.md` plus `src/`, `services/`, `apps/`, `tests/`, `deploy/`, `bootstrap.sh`, `.github/workflows/ci.yml`)
**Status:** findings (4 medium, 4 low, 2 info; no critical/high)

## Summary

The Phase 1 tree is in good shape for a foundation: the STJ source-generation converters are correctly implemented (verbatim `Raw` preservation, `JsonDocument` disposed with `Clone()` retained, kind-first write via `JsonObject.Insert`), the csproj-graph + ArchUnitNET gates are complementary as designed, and build governance (TWAE, CPM, lock files, audit) is coherent. No bugs were found in the serialization round-trips, the layer checker logic, or the solution wiring. The findings below are all in the surrounding scaffolding: an unauthenticated dev stack bound to all interfaces, unverifiable CI action pins, an unaudited bootstrap download, a compose gate that never boots, and several small test/doc weaknesses. All `TODO` grep hits are vendored Bootstrap assets under `apps/OmpOS.Web/wwwroot/lib/`, already acknowledged as upstream code in the plan summary — not findings.

## Medium

### M-01: Temporal dev ports published on all host interfaces with no auth

**File:** `deploy/compose.yaml:9-11`
**Issue:** `"7233:7233"` and `"8233:8233"` bind the Temporal gRPC frontend and Web UI to every host interface. The dev server has no authentication, so on any shared or untrusted network any peer can reach the gRPC API and the Web UI and drive workflows.
**Fix:**
```yaml
    ports:
      - "127.0.0.1:7233:7233" # gRPC frontend
      - "127.0.0.1:8233:8233" # Web UI
```

### M-02: CI pins action majors that cannot be verified to exist

**File:** `.github/workflows/ci.yml:28,31,39,78,79`
**Issue:** `actions/checkout@v7`, `actions/setup-dotnet@v6` (twice), and `actions/upload-artifact@v7` pin major versions far beyond the last widely-known majors (checkout v4, setup-dotnet v4, upload-artifact v4). If any of those tags does not exist, every CI run fails at the checkout step before any gate executes — a single-point total CI outage with no fallback.
**Fix:** Confirm each major tag resolves (e.g. `git ls-remote https://github.com/actions/checkout v7`), or drop to the newest verified majors and note the verification date in a comment. Consider pinning full SHAs with a major-version comment for supply-chain stability.

### M-03: bootstrap.sh executes a network-fetched installer without integrity check

**File:** `bootstrap.sh:16-17`
**Issue:** `curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh` followed by `bash /tmp/dotnet-install.sh` runs whatever the network returned with the user's privileges. TLS protects transport but there is no checksum or signature verification, and the fixed `/tmp` path is predictable (symlink-race surface on multi-user machines).
**Fix:**
```bash
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_ROOT/dotnet-install.sh"
  # verify against a pinned sha256 published alongside the SDK pin before executing
  echo "$DOTNET_INSTALL_SHA256  $DOTNET_ROOT/dotnet-install.sh" | sha256sum -c -
  bash "$DOTNET_ROOT/dotnet-install.sh" --jsonfile global.json --install-dir "$DOTNET_ROOT"
```

### M-04: CI compose gate validates YAML parsing only, never boots the stack

**File:** `.github/workflows/ci.yml:70-71`
**Issue:** The gate runs `podman-compose ... config --services | grep -qx temporal` and `config -q`. That proves the YAML parses and the service name exists. It never pulls the image, never starts the container, and never exercises the healthcheck (`temporal operator cluster health`), the port bindings, or the volume mount — so a bad image tag, a failing healthcheck, or a bad mount path all pass CI.
**Fix:** Extend the step to boot and probe, then tear down:
```bash
podman-compose --profile workflows -f deploy/compose.yaml up -d
trap 'podman-compose --profile workflows -f deploy/compose.yaml down' EXIT
# poll temporal operator cluster health on 127.0.0.1:7233 until healthy or timeout
```

## Low

### L-01: NoDockerTooling hygiene test misses `Dockerfile` and scans only 3 files

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:75-96`
**Issue:** `ContainsDockerWord` requires a word boundary after `docker` (`IsWordBoundary(line, index + 6)` is false for the `f` in `Dockerfile`), so the literal string `Dockerfile` — the most likely Docker reintroduction — returns false and passes. Additionally the scan covers only `bootstrap.sh`, `ci.yml`, and `compose.yaml`; a `Dockerfile` or `docker` reference anywhere else in the repo is never examined.
**Fix:** Add an explicit filename/content check, e.g. fail when any file named `Dockerfile*`/`docker-compose*` exists outside an allow-list, and add a `case "dockerfile": return true` arm (or check `line.Contains("dockerfile", OrdinalIgnoreCase)` first) in `ContainsDockerWord`.

### L-02: Docs-skeleton row assertion is weaker than the 26-row requirement

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:72`
**Issue:** The plan and summary specify 26 owned rows in `docs/open-questions.md`, but the test asserts `rows.Count.ShouldBeGreaterThanOrEqualTo(20)`. Up to 6 rows can be silently deleted without failing the gate.
**Fix:** `rows.Count.ShouldBeGreaterThanOrEqualTo(26);` (or an exact `ShouldBe(26)` if the count is contractual).

### L-03: L1 same-layer allow-list has no entry for Parser-to-Abstractions references

**File:** `tests/OmpOS.ArchitectureTests/LayerMap.cs:37-41`
**Issue:** L1 contains three assemblies (`OmpOS.Parser`, `OmpOS.Harness.Abstractions`, `OmpOS.Sandbox.Abstractions`) but `SameLayerAllowed` only covers L2 edges. `FindViolations` rejects every non-allow-listed same-layer edge, so if Phase 2's parser needs harness/sandbox abstraction types (the natural direction for a catch-all prompt parser feeding the harness), the gate forces either a layer renumber or duplicated types. Green today only because no edges exist yet.
**Fix:** Decide before Phase 2: either pre-add the intended L1 edge(s) to `SameLayerAllowed` with a comment, or move `OmpOS.Parser` to its own layer. Do not discover this mid-Phase-2 via a red gate.

### L-04: Stale kind string in A2uiSurfaceBlock doc comment

**File:** `src/OmpOS.Contracts/Ui/A2uiSurfaceBlock.cs:6`
**Issue:** The comment says `(kind "a2ui")`, but the authoritative mapping in `src/OmpOS.Contracts/Ui/UiBlockConverter.cs:22` is `"a2ui-surface"`, and bare `"a2ui"` is deliberately treated as an unknown kind per the test at `tests/OmpOS.Contracts.Tests/Ui/UiBlockJsonTests.cs` (`UnknownKindDeserializesToUnknownBlockAndReserializesByteIdentical`). A reader trusting the doc comment will emit payloads that deserialize to `UnknownBlock`.
**Fix:** Change the comment to `(kind "a2ui-surface")`.

## Info

### IN-01: Semver test assertion is vacuous

**File:** `tests/OmpOS.Contracts.Tests/ContractVersionTests.cs:16`
**Issue:** `version.Build.ShouldBeGreaterThanOrEqualTo(0)` is always true whenever the next line's `version.Revision.ShouldBe(-1)` holds (a `Version` with `Revision == -1` necessarily has `Build >= 0`). The three-part check therefore reduces to the `Revision` assertion alone.
**Fix:** Assert the exact shape instead: `version.Build.ShouldBeGreaterThanOrEqualTo(0)` → `version.Minor.ShouldBeGreaterThanOrEqualTo(0)` is no better; prefer `ContractVersion.Current.Split('.').Length.ShouldBe(3)` alongside the `Revision` check, or drop the `Build` line.

### IN-02: Duplicate `.env` entry in .gitignore

**File:** `.gitignore:7` and `.gitignore:485`
**Issue:** `.env` is ignored twice (once in the template section, once in the OmpOS additions). Harmless, but the duplication invites future edits to update one copy and miss the other.
**Fix:** Remove the line-7 entry (or the line-485 entry) so exactly one rule owns dotenv files.

---

## Explicitly checked, no issue found

- `UiBlockConverter` / `StreamEventPayloadConverter` (Read/Write): unknown/missing kinds degrade without throwing; `JsonDocument` disposed with `Raw = root.Clone()`; kind-first write; `"unknown"` kind round-trips verbatim. `TypeToKind[type]` indexing is unreachable for unmapped types today (all leaves sealed and mapped).
- `OmpOsJsonContext`: every contract shape registered; `WhenWritingNull` + camelCase + string-enum consistent with test literals.
- `StreamEventSeqComparer`: null handling correct; instability documented at both the comparer and the plan summary.
- `ProjectGraphChecker.FindViolations` / `ProjectGraphTests` / `ArchUnitLayerTests`: upward, same-layer, and unmapped-assembly edges all reported; synthetic + real live-gate proofs present; `OmpOS.App` absence handled consistently across `LayerMap`, `slnx`/`slnf`, and tests.
- `Directory.Build.props`, `Directory.Packages.props`, `OmpOS.slnx`, `OmpOS.Linux.slnf`, `global.json`, `bootstrap.sh` (except M-03), `.env.example` + `.gitignore` dotenv coverage, `apps/OmpOS.Web/Program.cs`, `BannedSymbols*.txt`, contract DTOs and enums.

_Reviewed: 2026-10-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
