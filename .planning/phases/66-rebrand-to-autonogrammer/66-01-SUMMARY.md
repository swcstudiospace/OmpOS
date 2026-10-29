---
phase: 66-rebrand-to-autonogrammer
plan: "01"
status: complete (executed before planning; evidence only)
---
# 66-01 Summary: mechanical rename (tool, runbook, apply)

## Built
- Commit 5cd0ada1 (docs(66-00)): v0.16.0 research reports, eight proposed ADRs, phase briefs 66 to 77, scripts/rebrand tool (rebrand.py, config, unit tests) and docs/runbooks/rebrand-autonogrammer.md. 38 files, 7563 insertions. Tool was dry-run proven, not applied, in this commit.
- Commit 4af7d834 (rename(66-01)): `rebrand.py apply`: 208 path moves (83 directories, 125 files), 5,202 files rewritten, 27,789 brand tokens plus 161 product-abbreviation edits. 5,495 files changed, 27,327 insertions and deletions each (git stat).
- Kept by design (per commit body): external omp engine names, engine-integration identifiers, protected URLs and hostnames, recorded evidence, frames, milestone archive, released CHANGELOG sections, generated files.

## Verification evidence in git
- Commit 4af7d834 body states: `rebrand.py verify --baseline` reports nothing left to rename and identical keep-pattern and rejected near-match counts. The tree is stated not to build until the regenerate commits.
- Not re-run here: the unit tests (plan says 49; a later commit reports 61 after 66-03 tool changes), the verify run, the three runbook probes.

## Known gaps
- Plan's baseline JSON lives outside the repo (/tmp), so it is not evidenced in git.
- Probes 1 to 3 refined forms are not re-run here.
