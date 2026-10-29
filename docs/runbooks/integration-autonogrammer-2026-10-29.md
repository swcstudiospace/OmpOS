---
description: "How the OmpOS line and the Autonogrammer rename line were integrated on 2026-10-29: method, conflicts and their resolutions, what was regenerated, what was fixed by hand, and how the other session replays onto the result."
---

# Integration of the rename line (2026-10-29)

Status: Done on branch `feat/autonogrammer-integration`; not pushed

Last verified: 2026-10-29

## What was integrated

Two lines of the same repository had diverged from `82129b9d`:

| Line | Branch | Content |
|------|--------|---------|
| Ours (OmpOS) | `feat/v0.9.2-hardening` = `origin/main` at `2e21f8df` | v0.15.0, the staged Railway worker, the live Demo and GitHub setup; 16 commits after the merge base |
| Theirs (Autonogrammer) | `feat/autonogrammer` at `50b7757d`, in the worktree `/root/src/repos/ompos-autonogrammer` | the mechanical rename (5495 files), the rebrand tool, the v0.16.0 research, ADR-0030 to ADR-0037 and the plans of phases 66 to 77; 8 commits after the merge base |

The integration followed the other line's own rule ([Rebrand to Autonogrammer](rebrand-autonogrammer.md), section 3): replay, not merge. Only committed content of the other line was used (read through git); nothing in its worktree was written, built, formatted or changed, and its 47 uncommitted paths (the Phase 66.1 scaffold) were left alone and are not part of the result.

## Method

1. A new branch from `origin/main` (`2e21f8df`).
2. Cherry-picks of the un-renamed commits of the other line, in this order: `5cd0ada1` (research, ADRs, briefs, the rebrand tool), `50b7757d` (tool: keep markers, frozen post-rename planning directories), `9aa8aa8a` (GSD records for v0.16.0), `246448a0` and `3c41f10f` (plans). The two rename commits (`4af7d834`, `325cd727`) were dropped; the tool re-did them.
3. One change to the tool configuration: a protection rule for the Railway names of the staged worker (`ompos-staging`, `ompos-worker`, `ompos-worker-data`) on lines that mention Railway, because those resources still exist under their old names and the other line's rules only protected `ompos-web` and `ompos-api`.
4. `python3 scripts/rebrand/rebrand.py apply`: 208 path moves, 5204 files rewritten, 0 collisions; `verify --baseline` clean; 309 protected URL tokens left (the GitHub slug, `ompos-web.vercel.app` and the Railway names).
5. `f7a93884` (AGENTS.md, CLAUDE.md) cherry-picked on top of the rename.
6. Regeneration in the order of the runbook: relock (`dotnet restore --force-evaluate`, 69 lock files), `dotnet format` (499 files: `using` order), third-party notices, OpenAPI by building the API (brand strings only).

Conflicts, all in records, all resolved by keeping both sides:

| File | Resolution |
|------|-----------|
| `docs/decisions/README.md` | our ADR-0020 row (staged worker) and their ADR-0030 to ADR-0037 rows |
| `docs/open-questions.md` | our OQ-168 to OQ-171 rows (newer) and their OQ-300 to OQ-485 rows; their older OQ-168 text dropped |
| `CHANGELOG.md` (twice) | their `[Unreleased]` rename entry with its keep markers |
| `.planning/REQUIREMENTS.md`, `ROADMAP.md` | our newer PX-21 to PX-23 and phase 59 to 65 statuses, then their v0.16.0 sections |
| `.planning/STATE.md` | their frontmatter and direction update, our v0.15.0 position kept under "Previous milestone" |

## Fixed by hand after the tool

The tool cannot split a wordmark, so these were edited: the six brand SVG sources and `docs/brand.md` (`Auto` in the text colour and `nogrammer` in the accent colour, re-fitted, assets rebuilt), the web top bar (`Omp<span>OS</span>`), the evidence file names that the rewritten links already used (`railway-autonogrammer-worker-2026-10-28.md`, `railway-autonogrammer-api-2026-10-21.md`; their content is unchanged). Names that stay old on purpose: the GitHub slug `swcstudiospace/OmpOS`, the Vercel project `ompos-web` and its hostnames, the Railway project, service and volume of the staged worker, and everything under `docs/runbooks/evidence/`, `LegacyBrandNames.cs` and the recorded frames.

## What the other session does next

Replay again, in its own worktree, on top of this branch:

1. `git fetch`, then `git worktree add -b feat/autonogrammer-next <path> feat/autonogrammer-integration`.
2. Cherry-pick only your commits that came after `50b7757d` and the WIP you commit later; drop `4af7d834`, `325cd727`, `f7a93884`, `50b7757d`, `5cd0ada1`, `9aa8aa8a`, `246448a0` and `3c41f10f` (their content is already here).
3. Content you wrote already uses the new names; the tool leaves it alone. If you add old-name literals, run `python3 scripts/rebrand/rebrand.py dry-run` first.
4. Expect conflicts in `.planning/STATE.md`, `ROADMAP.md`, `REQUIREMENTS.md`, `CHANGELOG.md` and `docs/open-questions.md` (both sides append). Open-question numbers: this line uses OQ-172 to OQ-183 and the `Q-TM`, `Q-BR`, `Q-DR`, `Q-DEP` rows; yours start at OQ-300.
5. Overlaps to look at: `GET /auth/config` (this line returns `{ mode, authority, audience }`; ADR-0030 D7 adds the project URL and the publishable key to the same response: keep `mode`), `IApiTokenProvider` (this line registers `OwnerTokenSession` for real builds; ADR-0030's session manager replaces it and should keep the Demo's `NoApiTokenProvider`), the web routes and `AuthGate`, `Api:Auth:*` options (a new `Mode`), `HarnessOptions` (`Harness:Acp:Environment`, `WorkspaceRoot`, `Approval`), `AcpHarnessSessionFactory`, and the API pipeline order (`UseHostingHardening` first).
6. Phase 66.1's scaffold and `ContractVersion` bump are untouched here; this line changed no contract.

## What this line added after the integration

Phases 66.2 (hosted real web app), 66.3 (accessibility) and 66.4 (quality uplift): see ADR-0021, `.planning/phases/66.2-hosted-real-web-app/` and the CHANGELOG `[Unreleased]` section.
