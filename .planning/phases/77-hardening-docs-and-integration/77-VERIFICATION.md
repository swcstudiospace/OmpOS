# Phase 77 Verification — Hardening, docs and integration

**Verified:** 2026-11-21 (auto-78 worktree at `c04095ba`, v0.20.11)
**Verdict: gaps_found** — the shipped gates below are green on runnable checks, but AG-77-10/11 stay red (open High), three runbook pages are missing, the 77-09 elevation page is missing, and a11y/live surfaces need a recorded run. This file claims no release readiness.

## Runnable gates (re-run this batch, HEAD `c04095ba`)

| Check | Result | Evidence |
|-------|--------|----------|
| `python3 scripts/release/test_verify_desktop_assets.py` (AG-78-7 gate, also repo) | PASS 58/58 | `Ran 58 tests in 1.512s OK` |
| `bash scripts/ci/check-docs.sh` | PASS 224 files, 121 listed, 0 errors | observed, exit 0 |
| `python3 scripts/docs/parity_counts.py --check` | PASS `OK: parity counts match` | AG-77-8/9 gate intact (77-02, redone in 77-09) |
| `bash scripts/ci/check-changelog.sh` | **not re-run this batch** | gap noted; `[0.16.0]` + `[0.20.0]` sections present on HEAD |
| `dotnet test` suites (`CoworkAudit*`, `DefaultConfigTests`, `LaunchDirectory*`) | **not re-run this batch** | full integration gate (`bootstrap.sh` + run-owned Postgres) out of batch scope |

## Gaps found (never claim passed)

1. **Security review open (AG-77-10/11, D-16).** `docs/quality/v0.16.0-security-review.md` status: **Open findings; not release-ready.** SEC-77-01 (High, phase 72 browser/tool policy) and SEC-77-02 (75-12) remain open with independent owners; SEC-77-04 (memory idempotency) also open per 77-03-SUMMARY. The architecture security audit fails while any High is open — by design, not a CI flake.
2. **Missing runbook pages (AG-77-5).** `docs/runbooks/cowork.md`, `docs/runbooks/computer-use.md`, `docs/runbooks/v0.16.0-owner-actions.md` are **absent** on HEAD. `dispatch.md` (1.4K) and `browser-use.md` (2.1K) are stubs, not D-09 completes. Docs part of `CoworkAuditTests` would fail on the missing pages if it were run.
3. **Missing 77-09 elevation (SOTA).** `docs/quality/v0.16.0-parity-audit-elevation.md` MISSING; `77-SOTA-HARDENING-DOCS-INTEGRATION.md` carries no Gap Closure Elevation section.
4. **A11y matrix pending (AG-77-13).** `docs/quality/v0.16.0-accessibility-pass.md` has no empty cells, but Devices/artifact cells read Pending / Not established. No recorded `run-smoke.sh --a11y-report <dir>` for this milestone's surfaces. Screen readers: not checked (OQ-483) — human-verification item.
5. **77-06 history count.** Current tree: 3 commits touch `<VersionPrefix>0.16.0`/`0.20.x` history (`b234bb98`, `1f27b917`, `a0bb806c`); the plan's "exactly 1" assertion retired by superseding cuts (see 77-06-SUMMARY). Current VersionPrefix 0.20.11.
6. **77-11 live items.** OS keyring + native pairing UI never exercised; `Accounts:Enabled=true` transition is an explicit operator action, not automatic.

## What this phase does claim (with evidence)

- Parity audit machine gate works: `parity_counts.py --check` PASS, `check-docs.sh` PASS (above).
- Security review + `CoworkAuditTests.Security` + `DefaultConfigTests` shipped (`ef479d72`, `34ade052`, `374e2514`); dispatch pairing rejects file-backed stores before writes; four account routes default-disabled.
- 77-01 integration evidence + OQ-484 amend + rebrand tool restore (77-01-SUMMARY) stand.

## Human-verification items (open)

- Screen-reader pass (OQ-483), populated-device a11y run, OS keyring/native pairing on real desktop, `Accounts:Enabled=true` operator transition on an existing install, three missing runbook pages (owner must review them before any release claim).

## Next

Close gaps 1-4 (runbooks, elevation, recorded a11y run, security fixes by owners 72/75-12). Do not re-open AG-77-10/11 as passed while a High is open.
