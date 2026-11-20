# GRAPH-APPROACH — v0.16.0 build-out approach (SPE-35248, Wave 3 · n3)

Date: 2026-11-20. Inputs: `.planning/GRAPH-UNITS.md` (SPE-35246),
`.planning/GRAPH-STATE-discovery.md` (SPE-35244),
`.planning/GRAPH-CRITIQUE.md` (gaps 1–7), `.github/workflows/ci.yml`,
`bootstrap.sh`, `scripts/ci/*`. Repo-truth context: `.NET 10, podman only`;
main at `f5d453d1` (66.4 merged); 67 closing via PR #24; 68 docs committed
locally (`94d64280`).

## Verdict table (approach × 4 criteria)

| Criterion | A. Stacked independently-verifiable increments (7 units, Waves A→D) | B. Single big-bang (all phases in one PR stack) |
|---|---|---|
| 1. Reviewable phase-scoped diffs | **Pass.** Each unit = one phase (68/71/73/74/75/76/77), one branch/PR, one VERIFICATION.md + SUMMARY. Reviewer judges one phase's criteria; ROADMAP checkboxes move per phase. Matches `per-phase` completion rule (GRAPH-CRITIQUE gap 2: gate pass at HEAD). | **Fail.** One diff spans 54 outstanding items across 7 phases + 29 blocked rows; unreviewable, violates phase-scoped review. Single failure blocks all phases; CODEOWNERS review (`@SomeRandmGuyy`, gap 5) cannot sign off piecemeal. |
| 2. Rollback feasibility | **Pass.** Per-PR revert; each unit is append-only to shared files in declared serialization order (ROADMAP/STATE one writer at a time; parity doc 75→76→77; CHANGELOG one writer per wave; 73 before 76). Reverted unit leaves other units green. | **Fail.** Rollback = revert 7 phases atomically; no bisect point. Shared-file edits (AgentHead, CoworkEngineRequest, parity doc) interleave with no clean inverse. |
| 3. Green-CI gating (repo's own definition) | **Pass.** Every unit's gate is independently runnable at its HEAD via `bootstrap.sh` (local) and `ci.yml` `build-test` (remote). A failing unit blocks only itself. PR-only gates (OpenAPI breaking, version-bump) evaluated per-PR against `origin/main`. | **Fail.** One red step (e.g. browser-smoke gate, SBOM, cautonogrammere boot) blocks all 7 phases for days; `concurrency.cancel-in-progress` cancels superseded PR runs, so big-bang gets no partial signal. `image-scan`/`android`/`desktop` need `build-test` first — one broken link silences the rest. |
| 4. Failure blast radius / bisectability | **Pass.** Failures localize to one phase/project/service (e.g. `Dispatch.Client` new project in Unit 4; isolation `bwrap` in Unit 6). Per-gate run-owned podman services (`AUTONOGRAMMER_TEST_SERVICES_NAME=gate-$$`) keep concurrent gates isolated. | **Fail.** Single failure domain: post-mortem cannot attribute (68 docs vs 74 dispatch vs 76 isolation share CI, shared files, shared services). |

## Chosen default

**A. Stacked independently-verifiable increments.** Execute units in
GRAPH-UNITS suggested waves (A={68, 71, 74-code}; B={74-verify → 73, 75};
C={76}; D={77 last}), each as its own mergeable PR with its own gate run and
VERIFICATION.md. Blocked rows stay OPEN per unit (owner/external/human
verification) — never silently dropped (GRAPH-UNITS totals: 54 outstanding +
29 blocked in-scope).

## The ONLY exception (atomic unit with HITL approval)

Single atomic unit requiring HITL approval before merge: **Unit 1 item 5 —
68-11 production account-erasure entry point, credential/session revocation,
resumable coordinator (`PostgresAccountErasure`)**. It is owner-decision
gated (Candidate A/B + IdP revocation/denial design + independent security
review via 74.1; SEC-77-09). It lands as one atomic, non-splittable unit
(schema/fence + cautonogrammerition change — partial landing is unsafe) and merges
only after explicit owner HITL approval recorded in the PR (AskUserQuestion
channel per GRAPH-CRITIQUE gap 6; `human_verify_mode: end-of-phase`). All
other units are stacked increments; no other atomic/HITL exception is
claimed — everything else is PR-review + green-CI gated (gap 5: review is
non-blocking today; do not treat merge as approved without owner sign-off
for this one unit).

## Verification commands (exact, from repo)

Per-unit gate (`.NET 10` host, podman only; `ci.yml` `build-test` mirrors
`bootstrap.sh` — run the full script locally, read per-step output on CI):

```bash
# 1. SDK + submodule + secret + plugin + repo checks (bootstrap.sh top)
dotnet --version   # pinned SDK from global.json (10.0.402)
bash scripts/ci/secret-scan.sh
bash scripts/ci/plugin-node-tests.sh
bash scripts/ci/repo-checks.sh

# 2. Restore/build/format (locked, warn-as-errors)
dotnet restore Autonogrammer.Linux.slnf --locked-mode
dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror
dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore

# 3. Licence / vuln / SBOM / OpenAPI / version (CI steps; OpenAPI+version PR-only)
bash scripts/ci/check-licences.sh
bash scripts/ci/check-sbom.sh
dotnet list Autonogrammer.Linux.slnf package --vulnerable --include-transitive --format json > vuln.json && python3 scripts/ci/check-vulnerable-packages.py vuln.json
python3 scripts/ci/check-openapi-breaking.py --base origin/main
bash scripts/ci/check-version-bump.sh --base origin/main

# 4. Tests (non-Live; throwaway podman services, run-owned name)
bash scripts/test-services.sh up   # returns TEST_SERVICES_ENV; name: gate-$$
dotnet test Autonogrammer.Linux.slnf -c Release --no-build --max-parallel-test-modules 2 -- --filter-not-trait "Category=Live"
bash scripts/test-services.sh down

# 5. Full local gate + browser smoke (axe) — Unit 7 evidence command
./bootstrap.sh   # flock-wrapped at final HEAD, no Live tests

# 6. Cautonogrammere backend validation (CI only)
podman-cautonogrammere --profile backend -f deploy/cautonogrammere.yaml config -q
```

Per-unit proof: its `*-VERIFICATION.md` at HEAD states `passed` (GRAPH-CRITIQUE
gap 2 rule) and the PR shows green `build-test` (+ `android`/`desktop` where
the unit touches those heads; `image-scan` on push). Repo all-green =
`build-test` + `android` + `desktop` matrix (+ push-only `image-scan`)
(Graph-Critique gap 4; confirm branch-protection statuses from GitHub
settings before relying on them).
