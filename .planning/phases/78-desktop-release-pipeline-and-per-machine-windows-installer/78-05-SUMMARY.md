---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "05"
subsystem: docs, runbook, changelog
tags: [install-guide, desktop-release-runbook, changelog]
dependency_graph:
  requires: [78-01, 78-02, 78-03, 78-04]
  provides: [install-guide, release-runbook, 0.20.0-changelog]
  affects: [78-06, docs/SUMMARY.md]
tech_stack:
  added: []
  patterns: [unsigned-installer honesty, human checklist handover, runbook recovery steps]
key_files:
  modified:
    - docs/guides/install.md
    - docs/runbooks/desktop-release.md
    - CHANGELOG.md
    - docs/SUMMARY.md
decisions:
  - "Guide states plainly the packages are unsigned and why warnings appear; checksum-first is the only trust path (release page is the only source)"
  - "Human checklist is the only verification of Windows/macOS installs beyond CI build + artifact inspection — no person-tested claim made"
metrics:
  tasks: 2
  status: complete
---

# Phase 78 Plan 05: Install Guide + Runbook + Changelog Summary

**One-liner:** User install guide (per-OS install, checksum verification, SmartScreen/Gatekeeper meaning, manual checklist), release runbook (rehearsal/tag/recovery/deploy-key/rc→release), and the `## [0.20.0]` changelog — all gated green.

## Tasks Executed

**Task 1 (auto):** `docs/guides/install.md` confirmed: Windows (per-machine, UAC, Program Files, upgrade in place, uninstall keeps data), macOS (ad-hoc signed, not notarized), Linux; `SHA256SUMS-desktop` verification; SmartScreen + Gatekeeper warnings explained honestly (unsigned) with proceed + checksum steps. Two numbered checklists (Windows: download→checksum→SmartScreen→UAC→wizard→shortcuts→launch→upgrade→uninstall; macOS: download→checksum→Gatekeeper→launch→quit/remove). Frontmatter + Status line pass `check_gitbook.py`.

**Task 2 (auto):** `docs/runbooks/desktop-release.md`: rehearsal command + what it checks/never does, tag flow order, seven attached files, hidden-draft recovery (re-run failed jobs → release job), deploy-key creation (read-only, repo secret `SUBSTRATE_DEPLOY_KEY`), rc-then-release sequence. `CHANGELOG.md` `## [0.20.0]` lists Added/Changed for this phase; `VersionPrefix` 0.20.0 at the time (HEAD now 0.20.11 after later patch cuts). Both pages listed in `docs/SUMMARY.md`.

## Verification Performed

- `bash scripts/ci/check-docs.sh` — ran this batch: **224 markdown files, 0 errors** (guide + runbook pass gitbook rules)
- `bash scripts/ci/check-changelog.sh` — cited from 78-VERIFICATION criterion 3 (`valid, 28 versions` at the time); **not re-run this batch**
- Files exist on HEAD: `docs/guides/install.md` EXISTS, `docs/runbooks/desktop-release.md` EXISTS (27.9K)

## Deviations

None. Signing/notarization not described as done (they are not). No Windows/macOS person-tested claim.

**Status:** 78-05 complete.
