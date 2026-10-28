# Phase 64 Verification: Documentation and GitBook

How to re-run each proof and what it proves. Run from the repository root. No .NET project is involved; for completeness:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `64-SUMMARY.md`.

Recorded totals: this phase has no .NET test. The checks below were re-run by the author of this record on commit e120714b on 2026-10-28 (read-only). The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`): its repository-checks step printed `141 markdown files, 73 listed in SUMMARY.md, 0 errors, 0 warnings` and `repo-checks: ok`, and `dotnet test Autonogrammer.Linux.slnf` reported 12215 total, 12213 succeeded, 0 failed, 2 skipped. Commits after it are documentation only. The GitHub `ci` runs of main (40758215402) and of the tag (40758275399) failed at 'Start test services' (rootless podman user namespace, OQ-168); the local gate is the gate of record.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 layout and links | `bash scripts/ci/check-docs.sh` | Every page is in `SUMMARY.md` or an ignored tree; no duplicate entry, broken link or anchor, second H1, unquoted description, missing status line, unbalanced `{% %}` block; `docs/api/openapi.json` is valid JSON | Run on 2026-10-28: exit 0, `141 markdown files, 73 listed in SUMMARY.md, 0 errors, 0 warnings` (the earlier run on the first record, with 4 orphans, is superseded) |
| 1 screenshots | `AUTONOGRAMMER_RELEASE=1 bash scripts/ci/check-docs.sh` | As above, and every pinned screenshot exists | Run on 2026-10-28: exit 0, the same line; the eight PNGs exist in `docs/assets/screens/` |
| 1 gate | `grep -n "repo-checks" bootstrap.sh .github/workflows/ci.yml` and `grep -n "check-docs" scripts/ci/repo-checks.sh` and `GateParityTests.BootstrapAndCiRunTheSameRepositoryChecks` | The checker runs in the gate, in both `bootstrap.sh` and `ci.yml` | Run on 2026-10-28: `bootstrap.sh:57` and `ci.yml:106` call `scripts/ci/repo-checks.sh`, which calls `scripts/ci/check-docs.sh` (line 12), which calls `scripts/docs/check_gitbook.py`; the architecture test is inside the passing gate of e24e6851, whose log carries the 0-error line and `repo-checks: ok` |
| 1 stale statements | `grep -n -i "do not construct\|does not construct\|no head constructs" docs/reference/hooks.md` and `grep -n -i wave docs/runbooks/multi-repo.md` | The pages no longer say the heads do not construct the hooks service, and carry no wave wording | Run on 2026-10-28: no match in either; the hooks Status says wired into the desktop head, its banner keeps ssh, container and WSL sessions as not wired |
| 1 SUMMARY entries | `grep -c` of each of `decisions/addendum-ADR-0015-containers.md`, `reference/automations.md`, `reference/builtin-skills.md`, `runbooks/remote-containers.md` in `docs/SUMMARY.md` | The four former orphans are listed | Run on 2026-10-28: 1 each |
| 2 runbook | Read `docs/runbooks/gitbook-publish.md` | The exact owner steps exist and the page states the site is not published | Run on 2026-10-28: 11 numbered steps (sign in, create the site, GitBook GitHub app, land docs on `main`, Git Sync, commit what GitBook writes, customize, OpenAPI, publish, custom domain, record the URL) and a verification checklist; Status line `Blocked`; no space URL exists (OQ-101) |
| 3 audit counts | A script over `docs/quality/cursor-parity-audit-2026-10-28.md` counting the status word of each table row whose first cell is an inventory id, and over the tables of `docs/settings-parity.md` and `docs/reference/slash-commands.md` | 494 rows, one status each, equal to the summary table; settings and slash counts equal the source tables | Run on 2026-10-28: 494 rows, 494 distinct ids: MET 80, MET-LIVE 1, PARTIAL 170, DEFERRED 27, NOT-STARTED 111, OMITTED-BY-DECISION 105; settings 50 (audit MET 8, PARTIAL 20, OMITTED-BY-DECISION 22; `docs/settings-parity.md` Built 6, Wired 2, Partial 20, Omitted 22); slash 92 (audit MET 44, PARTIAL 24, DEFERRED 4, NOT-STARTED 6, OMITTED-BY-DECISION 14; `docs/reference/slash-commands.md` 93 rows: Built 36, Delegated 9, Partial 24, Unavailable 24, the 93rd being `/update-settings`); shortcuts 55 (MET 7, PARTIAL 11, NOT-STARTED 31, OMITTED-BY-DECISION 6). Per-family counts of the other 15 families were not recomputed one by one; their sum equals the summary table |
| 3 freshness | Read the audit sections 1, 'What changed since the first draft', 23 and 24; `grep -n "fbd5266"` | The audited tree, whether wired rows were re-verified, the drift list | Run on 2026-10-28: audited tree `7535859`; every row re-graded by reading code and test names (82 rows moved, 99 re-evidenced; no test run by the audit); section 24 lists 7 drift items and says which were fixed afterwards (ddae10be); `fbd5266` appears in two lines, both the history of the first draft. Stale in it: sentences that the gate on `7535859` had not finished (lines 7, 21, 126, 854) |
| 3 tree after the audit | `git diff --stat 7535859 HEAD -- src apps services` | How much product code changed after the audited tree | Run on 2026-10-28: 9 files, 36 insertions and 13 deletions (web interop, composer Stop, popup `aria-controls`, worker live check script outside this path); not re-graded |
| Changelog | `bash scripts/ci/check-changelog.sh` | The changelog is valid | Run in the passing gate of e24e6851 (inside `repo-checks.sh`); the first record quoted `CHANGELOG.md is valid (8 versions)` |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/SUMMARY.md`, `.gitbook.yaml`, `docs/README.md` | The GitBook layout |
| `scripts/docs/check_gitbook.py`, `scripts/ci/check-docs.sh`, `scripts/ci/repo-checks.sh` | The checker and its place in the gate |
| `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log` | The passing gate, with the docs check line |
| `docs/runbooks/gitbook-publish.md` | Owner steps and the Blocked status |
| `docs/quality/cursor-parity-audit-2026-10-28.md` | The audit |
| `docs/assets/screens/` | The eight pinned screenshots |

## Known limits of this verification

- No GitBook import or publish was done; no space exists (owner, OQ-101). Whether GitBook publishes files outside `SUMMARY.md` is UNVERIFIED.
- The audit rows were re-graded by reading code and test names; this record recomputed the total, settings, slash and shortcuts counts, not the other 15 families one by one, and did not re-trace rows to code.
- `docs/api/openapi.json` holds 69 operations (counted 2026-10-28); UNVERIFIED: that it equals the output of the API build (regeneration not run).
- The gate sentences of the audit and its audited tree `7535859` predate the passing gate on e24e6851 and 9 later product files.
- No model turn (OD-07); nothing ran on Windows, macOS or Android.
