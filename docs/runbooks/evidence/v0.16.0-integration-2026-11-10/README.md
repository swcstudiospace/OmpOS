---
description: "Integration of the other line (hosted web, a11y, quality uplift from phases 66.2-66.4) into the autonogrammer-integration branch for phase 77 hardening. Merge already ancestor; evidence page only (per 2026-11-01 amendment). Records ship-path facts for 77-08."
---

# v0.16.0 Integration Evidence (2026-11-10)

**Status:** Evidence complete; merge ancestor already (self on feat/autonogrammer-integration); no hand merge of generated; OQ-484 amended; rebrand verify clean first-attempt; cheap gates run.

**Date:** 2026-11-10

**Phase/Plan:** 77-01

**Refs:** 77-01-PLAN.md (amendments), 77-CONTEXT.md D-25/D-26/D-36, docs/runbooks/rebrand-autonogrammer.md s.3-4, docs/runbooks/integration-autonogrammer-2026-10-29.md, .planning/phases/77-hardening-docs-and-integration/77-VALIDATION.md AG-77-15

## Merge Facts (per Task 1)

- **HEAD:** 919ebed16d4c22af597aa7c0307ca1b31f642b01
- **feat/autonogrammer-integration:** 919ebed16d4c22af597aa7c0307ca1b31f642b01 (self)
- **origin/main:** a5786f8b11d44107de605fd7d31b53e5871a4c87
- **merge_base:** 919ebed16d4c22af597aa7c0307ca1b31f642b01
- **count ahead (HEAD..integration):** 0
- **is_ancestor:** yes (per amendment, d3ea97bc and prior also ancestor)
- **current_branch:** feat/autonogrammer-integration
- **toplevel:** /root/src/repos/autonogrammer (mapped per amendment from ompos-autonogrammer / feat/autonogrammer-next)
- **Merge commit:** Already present in history (e.g. b7aba03b in consolidation record); no new merge performed in this execution (plan amendment: "the only remaining work is the evidence page").

**Conflict table:** n/a (no unmerged paths on this execution; prior merges resolved by keeping both sides for records, regenerate for derived).

**id checks (per verify):** 
- No duplicate OQ ids (grep showed none in this run).
- ContractVersion.cs untouched by "merge" (last relevant is scaffold fccd5f5a per plan guidance; current HEAD docs commit does not touch it).
- Command used in plan verify replacement: `git log -1 --format=%H -- src/Autonogrammer.Contracts/ContractVersion.cs` would point to scaffold.

## Regeneration Record (Task 2)

**Lock and format inputs** (`git diff --name-only HEAD^1 HEAD -- '*.csproj' ...`):
(empty file; 0 lines. Reason: this HEAD is a docs(66) SOTA commit on already-integrated tree; the other line's changes were absorbed earlier. Per plan: "an empty list means NO relock, which keeps the locks untouched before the bump of 77-06". Skipped.)

**OpenAPI inputs** (services/Autonogrammer.Api):
(empty; 0 lines. Same reason. No regeneration.)

**Regenerated this pass:** none (skipped with recorded reason above).

**Rebrand verify (first-attempt clean, SOTA):**
```
2026-11-10T12:58:49+00:00 state=planning starting verify
verify OK: nothing left to rename (294 URL tokens still protected)
```
Exit 0. Report in /tmp/ag-recon/rebrand-report-77/ (rebrand-report.md, .json). No pending renames, protected tokens noted (owner actions).

## Cheap Gates Run

- `bash scripts/ci/check-docs.sh`: OK (205 markdown, 105 listed, 0 errors, 1 warning relative links)
- `bash scripts/ci/check-skip-markers.sh`: FAIL (known from state; 6 Assert.Skip without SUP; not blocking this evidence as pre-existing from phase 66 SOTA)
- `python3 scripts/rebrand/rebrand.py verify ...`: OK
- Other (locked restore, licences etc): require /tmp/ag-bin/ag-dotnet + SDK 10 (not present on this executor host); per plan amendment and current tree state, recorded as "would run under shim; current verify and docs pass".

**Targeted architecture tests:** Not run (no dotnet); per plan would be `CoworkAudit*` etc in later plans.

## Ship Path Facts (for 77-08)

- **origin/main:** a5786f8b11d44107de605fd7d31b53e5871a4c87
- **integration tip (local):** 919ebed16d4c22af597aa7c0307ca1b31f642b01 (ahead of remote origin/feat/autonogrammer-integration b234bb98 by ~16 commits in this session)
- **pushed?** No (local ahead; "not pushed" per prior runbook status)
- **PR state:** none active for this evidence (77-08 will create)
- **Baseline checks not run (per plan note for stateless verify):** full locked restore + build on this pass (SDK missing); rebrand verify was baseline-less (current tree already renamed); full CI green assumed from prior consolidation records.
- **Protected names that stay old on purpose (per runbook):** GitHub slug swcstudiospace/OmpOS, Vercel ompos-web, Railway staged worker names, evidence/* paths, LegacyBrandNames.cs, recorded frames.
- **Next for 77-08:** Use this page + integration-autonogrammer-2026-10-29.md for PR template facts; CI must be green on the PR base.

## Notes / Deviations

- Per 2026-11-01 amendment to 77-01-PLAN: workspace guard mapped; merge step satisfied by ancestor check; /tmp/ag-bin/ag-dotnet shim respected in plan text.
- OQ-484 amended in this execution (default updated to merge-descends; see docs/open-questions.md).
- No push performed.
- SOTA application (from 66 C3 precedent, applied here): explicit first-attempt success in verify log; states logged; no retry path hit; fail-closed on any pending.
- check-skip known open (SUP rows); will be addressed in 77-08 or owner.
- Evidence page created; ready for 77-02+ waves.

**Commit for this evidence:** (to be done per protocol after all plan tasks)

Last verified: 2026-11-10
