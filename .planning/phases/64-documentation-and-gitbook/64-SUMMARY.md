# Phase 64 Summary: Documentation and GitBook

Status (recorded 2026-10-28): criteria 1 to 3 are Partial. The GitBook layout, the checker, the landing pages, the guides and the refreshed audit exist. On the final tree `bash scripts/ci/check-docs.sh` fails with 4 orphan pages (written by later phases and not added to `docs/SUMMARY.md`), the audit describes the Wave 2 tree `fbd5266` and was not refreshed after the head wiring, and the GitBook space does not exist (owner step). Nothing ran on Windows, macOS or Android; no model turn was made (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded results (recorded 2026-10-28): the full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The checks quoted below were run by the author of this record on the final tree (`7535859`) on 2026-10-28; they are shell and Python scripts, not .NET tests.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `.gitbook.yaml`, `SUMMARY.md` and about 30 pages; every page states built, partial or blocked; README is the landing page; `check_gitbook.py` in the gate with no orphan, duplicate, broken link or unbalanced block | Partial | `.gitbook.yaml` (`root: ./docs/`), `docs/SUMMARY.md` (69 entries in nine groups), `docs/README.md` as the GitBook landing page, and the root `README.md` as the repository landing page. Run on 2026-10-28: `bash scripts/ci/check-docs.sh` reports `138 markdown files, 69 listed in SUMMARY.md, 4 errors, 0 warnings`; the 4 errors are orphans: `docs/decisions/addendum-ADR-0015-containers.md`, `docs/reference/automations.md`, `docs/reference/builtin-skills.md`, `docs/runbooks/remote-containers.md` (no duplicate, broken link, anchor, status or block error). The 8 pinned screenshots now exist in `docs/assets/screens/`. Missing: the four SUMMARY entries, and the call of `check-docs.sh` from `bootstrap.sh` or `ci.yml` (neither calls it). Pages that say heads are not wired are stale: `docs/reference/hooks.md` still says the heads do not construct the hooks service, while `HeadServices.cs` and `MainView.Shell.cs` now reference it (grep 2026-10-28); `docs/runbooks/multi-repo.md` also carries wave wording |
| 2 | Owner steps to publish the space are in `docs/runbooks/gitbook-publish.md`; if a signed-in session is available the space is created and connected, otherwise the exact steps remain | Partial | The runbook exists with the exact steps (sign in, create the site, install the GitBook GitHub app, land `docs/` on `main`, sync, brand colours and logo, OpenAPI, verify) and its Status line reads `Blocked (the documentation is not published: it needs the owner's GitBook account; no GitBook credential exists in this repository or on the build host)`. No signed-in session was available, so the space was not created and no space URL exists. The runbook tells owners not to link a GitBook URL until one is confirmed. The runbook is met; creating the space is Blocked on the owner. Whether GitBook publishes files outside `SUMMARY.md` (`plans/`, `runbooks/evidence/`) is UNVERIFIED (no import was done) |
| 3 | The Cursor parity audit is refreshed against the 2026-10-27 inventory with a status for every feature; verify the audit file counts and the settings and slash tables | Partial | `docs/quality/cursor-parity-audit-2026-10-28.md` states 494 rows in 18 families (the family totals in its summary table add to 494). I counted the tables with a script on 2026-10-28: the settings table has 50 rows (MET 5, PARTIAL 16, DEFERRED 6, NOT-STARTED 9, OMITTED-BY-DECISION 14), the slash table has 92 rows (MET 16, PARTIAL 53, DEFERRED 5, NOT-STARTED 3, OMITTED-BY-DECISION 15), the shortcuts table has 55 rows; the counts agree with its own section 20 and 21 headings and its summary. Every row carries one status of the vocabulary in its section 2. Missing: the audit's own Status line says it covers the tree at `fbd5266` (Wave 2, not compiled) and that rows depending on head wiring must be re-verified after the Wave 3 merge; section 4 lists the wiring as not done (25 slash tokens PARTIAL only because their handlers were not composed into the head; the Settings window with 11 pages). Those statements were not re-verified after the wiring commits (`2e7d1f8`, `5275a23`, `942d813`, `e7c451e`), so the statuses of the wired rows are stale in the pessimistic direction. `docs/settings-parity.md` is exempt from the status rule and was refreshed in `2e7d1f8` |

## What the phase delivered

- The GitBook layout and a checker that catches orphans, duplicates, broken links and anchors, missing statuses and unbalanced blocks.
- A landing page, nine navigation groups with 69 entries, ten guides, an API reference built from source, an architecture overview with a diagram, and a README with a status per feature.
- The 494-row audit against the Cursor 3.27 inventory with its drift list and owner list.
- The publish runbook and the GitHub setup pages for the owner.

## Deviations

- The space is not created: no GitBook credential on the build host (criterion 2 allows this and asks for the steps).
- Reference pages (hooks, MCP, run modes, checkpoints) keep their own "Status of this release" block, written when the heads did not construct the services.
- `docs/api/` is an ignored tree: `openapi.json` lists 26 operations (written before the Phase 53 routes); regenerate it with the API build (UNVERIFIED: `docs/api/openapi.json` regeneration was not run).

## Partial

- Criterion 1: add the four orphans to `docs/SUMMARY.md`, update the stale hooks and multi-repo statements, wire `scripts/ci/check-docs.sh` into the gate.
- Criterion 2: the owner creates the space (steps in the runbook).
- Criterion 3: re-verify the wired rows against the final tree.

## Blocked (OQ/OD)

- OQ-101: the GitBook space is an account action (default: the repository carries the layout; connecting is the owner's).

## Known limits

- The web guide's screens were not viewed in a browser by the author of the guide; the Demo captures of `docs/runbooks/evidence/web-app-2026-10-28/` were taken from the live Demo (recorded 2026-10-28).
- No GitBook import was done; GitBook's treatment of files outside `SUMMARY.md` is UNVERIFIED.
- The audit counts come from a script that reads the tables; I checked the settings, slash and shortcuts tables only, not the other 15 families.
