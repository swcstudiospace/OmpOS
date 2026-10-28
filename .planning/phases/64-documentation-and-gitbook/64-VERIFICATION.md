# Phase 64 Verification: Documentation and GitBook

How to re-run each proof and what it proves. Run from the repository root. No .NET project is involved; for completeness:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `64-SUMMARY.md`.

Recorded totals: this phase has no .NET test. The checks below were run by the author of this record on the final tree on 2026-10-28. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 layout and links | `bash scripts/ci/check-docs.sh` | Every page is in `SUMMARY.md` or an ignored tree; no duplicate entry, broken link or anchor, second H1, unquoted description, missing status line, unbalanced `{% %}` block; `docs/api/openapi.json` is valid JSON | Run on 2026-10-28: FAILED with 4 orphans (`docs/decisions/addendum-ADR-0015-containers.md`, `docs/reference/automations.md`, `docs/reference/builtin-skills.md`, `docs/runbooks/remote-containers.md`); `138 markdown files, 69 listed in SUMMARY.md, 4 errors, 0 warnings` |
| 1 screenshots | `OMPOS_RELEASE=1 bash scripts/ci/check-docs.sh` | As above, and every pinned screenshot exists | Run on 2026-10-28: the same 3 listed orphans plus the addendum remain; no screenshot error (the eight PNGs exist in `docs/assets/screens/`) |
| 1 gate | `grep -n "check-docs\|check_gitbook" bootstrap.sh .github/workflows/ci.yml` | The checker runs in the gate | No match: not wired |
| 2 runbook | Read `docs/runbooks/gitbook-publish.md` | The exact owner steps exist and the page states the site is not published | Status line `Blocked`; no space URL exists |
| 3 audit counts | A script over `docs/quality/cursor-parity-audit-2026-10-28.md` counting the status cell of each table row in sections 20 (settings) and 21 (slash) | 50 settings rows and 92 slash rows, each with one status; counts equal the section headings | Run on 2026-10-28: settings 50 (MET 5, PARTIAL 16, DEFERRED 6, NOT-STARTED 9, OMITTED-BY-DECISION 14); slash 92 (MET 16, PARTIAL 53, DEFERRED 5, NOT-STARTED 3, OMITTED-BY-DECISION 15); shortcuts table 55 rows. The summary table of 18 family totals adds to 494 |
| 3 freshness | `grep -n "fbd5266" docs/quality/cursor-parity-audit-2026-10-28.md` | The audit's audited tree | Matches: the audit covers `fbd5266`, before the wiring; not refreshed |
| Changelog | `bash scripts/ci/check-changelog.sh` | The changelog is valid | `CHANGELOG.md is valid (8 versions)` (2026-10-28) |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/SUMMARY.md`, `.gitbook.yaml`, `docs/README.md` | The GitBook layout |
| `scripts/docs/check_gitbook.py`, `scripts/ci/check-docs.sh` | The checker |
| `docs/runbooks/gitbook-publish.md` | Owner steps and the Blocked status |
| `docs/quality/cursor-parity-audit-2026-10-28.md` | The audit |
| `docs/assets/screens/` | The eight pinned screenshots |

## Known limits of this verification

- No GitBook import or publish was done; no space exists.
- The audit rows were not re-traced to the final tree; only the settings, slash and shortcuts tables were counted again.
- `check-docs.sh` fails on the final tree until the four orphan pages are listed in `docs/SUMMARY.md`.
- No model turn (OD-07); nothing ran on Windows, macOS or Android.
