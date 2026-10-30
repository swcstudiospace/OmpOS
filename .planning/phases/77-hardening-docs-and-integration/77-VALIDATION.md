# 77-VALIDATION — Requirement-to-verification map

## Quick (minutes)

- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkAudit*"` — AG-77-6, 7, 8, 9, 10, 11
- `python3 scripts/docs/parity_counts.py --check` — AG-77-8
- `bash scripts/ci/check-docs.sh` — AG-77-5
- `bash scripts/ci/check-changelog.sh` — AG-77-3

## Full (gate + CI)

- `./bootstrap.sh` with Live skipped by trait and reason — AG-77-14
- CI green on the 77-08 PR — AG-77-16
- `/tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode` — AG-77-2
- `bash scripts/ci/check-licences.sh` — AG-77-2 relock hygiene

## Requirement map

| Requirement | Verifier | Pass signal |
|-------------|----------|-------------|
| AG-77-1 | `git log -S'<VersionPrefix>0.16.0' -- Directory.Build.props` finds exactly one commit; its `git diff --stat` lists only version files and locks; `deploy/api.Containerfile` ARG equals 0.16.0 | One commit; clean stat |
| AG-77-2 | `--locked-mode` restore exits 0; lock count matches baseline plus scaffold; no post-bump lock touches; check-licences passes | Exit 0, counts match |
| AG-77-3 | `bash scripts/ci/check-changelog.sh` | Exit 0, 0.16.0 section with compare link, Unreleased empty |
| AG-77-4 | `git log` on `src/Autonogrammer.Contracts/ContractVersion.cs` shows no post-scaffold touch; Contracts.Tests value 1.6.0 | No later commit |
| AG-77-5 | `python3 scripts/docs/check_gitbook.py .` and `bash scripts/ci/check-docs.sh` | Exit 0, no orphan |
| AG-77-6 | `CoworkAuditTests.Docs` variable-coverage test | Every Dispatch__/Accounts__/DataPlane__/Cowork__ variable in a runbook table |
| AG-77-7 | `CoworkAuditTests.Docs` project-row test; manual parity-citation spot check | One architecture row per src/services project; no fake-working claim |
| AG-77-8 | `CoworkAuditTests` parity part; `parity_counts.py --check` | 51 rows graded, vocabulary enforced, counts match, zero MET-LIVE |
| AG-77-9 | Bad-fixture suite (one per D-12 failure) RED then GREEN on real file | Each fixture fails for its named reason |
| AG-77-10 | Review page exists with scope table; `CoworkAuditTests.Security` green | All areas covered with evidence |
| AG-77-11 | Findings-table parser test | No open/accepted Critical/High; Medium accepted carries OQ id |
| AG-77-12 | `DefaultConfigTests` per module in Api.IntegrationTests | Boot plus disabled types plus three validator refusals |
| AG-77-13 | Matrix has no empty cell; UI/Native/WebShell/HeadMotion/ThemeContrast tests plus axe output | Every cell cites test/file or reason |
| AG-77-14 | `./bootstrap.sh` exit 0; evidence page committed | Command, date, commit, counts recorded |
| AG-77-15 | `rebrand.py verify --baseline` plus three probes; integration evidence page | No duplicate ids; regenerations in order |
| AG-77-16 | PR from template; CI green; threads answered; no merge, no tag | Merge-ready, owner holds merge |
| AG-77-17 | Owner-actions page in ADR-0030 form with D-24 groups; PR performs none | Listed, not performed |

## Wave 0 gaps (do not exist yet — built inside this phase)

- `scripts/docs/parity_counts.py` (77-02)
- `tests/Autonogrammer.ArchitectureTests/CoworkAuditTests.cs` plus fixtures (77-02), `.Security.cs` (77-03), `.Docs.cs` (77-05)
- `docs/parity/cowork-parity.md` grading input from 75-06 (STOP-gated in 77-02)
- Six runbooks, `v0.16.0-owner-actions.md`, security review, accessibility pass, gate and integration evidence pages (77-01/03/04/05/07)
- `tests/Autonogrammer.Api.IntegrationTests/DefaultConfigTests.cs` (77-03) and U5 automated checks (77-04)
- `.env.example` Dispatch__/Accounts__/DataPlane__/Cowork__ rows from phases 67-76 (STOP-gated in 77-03/05)

## Revision 1

- B1/B3/B4: every gating verify now redirects the real command to a file under /tmp/ag-recon/ and tails the file only on success (no pipe masking exit status).
- B2: dry-run JSON path fixed to /tmp/ag-recon/rebrand-report-77/rebrand-report.json via rebrand.py dry-run --report-dir /tmp/ag-recon/rebrand-report-77.
- B5: gate verdict runs under PATH=/tmp/ag-recon/dotnet-shim:$PATH (shim execs /tmp/ag-bin/ag-dotnet); bootstrap.sh is never edited.
- W1: 77-02/03/04 now depend on 77-01. W2: 77-04 names concrete test files with NEW marks. W3/I3: 77-06 frontmatter lists '**/packages.lock.json' and asserts the recorded pre-bump count. W5: 77-08 verifies gh PR state, checks and thread count. W6: 77-04 Task 2 fails_when names command-visible signals. W7: bootstrap.sh removed from 77-07 files. W4 accepted (see 77-05 scope note). I1: 77-01 names ref final.
