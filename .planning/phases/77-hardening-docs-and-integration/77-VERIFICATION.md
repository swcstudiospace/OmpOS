---
phase: 77-hardening-docs-and-integration
verified: 2026-11-22T01:53:17Z
status: gaps_found
score: "machine gates pass; security/runbook/a11y remain"
covered_files:
  - ".planning/phases/77-hardening-docs-and-integration/77-01-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-01-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-02-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-02-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-03-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-03-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-04-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-04-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-05-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-05-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-06-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-06-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-07-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-07-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-08-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-08-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-09-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-09-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-10-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-10-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-11-PLAN.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-11-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-CONTEXT.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-PLAN-CHECK.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-RESEARCH.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-SOTA-HARDENING-DOCS-INTEGRATION.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-SOTA-SUMMARY.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-VALIDATION.md"
  - ".planning/phases/77-hardening-docs-and-integration/77-VERIFICATION.md"
covered_digest: "v1:sha256:92d47bc1d0fd1fa24b796c70fb36fdebada5dce4f1163d2d0d94abe77375ecaa"
behavior_unverified: 4
overrides_applied: 0
re_verification: true
previous_status: unknown
previous_score: "unknown"
commits: ["b46fd81f"]
gaps:
  - truth: "Security review open SEC-77-01/02/04 (AG-77-10/11)."
    status: unverified
    closing_step: "Close with owners; never downgrade while High open."
  - truth: "Missing runbooks cowork/computer-use/v0.16.0-owner-actions; stubs dispatch/browser-use."
    status: unverified
    closing_step: "Author pages; complete stubs; run CoworkAuditTests.Security."
  - truth: "Missing parity-audit-elevation.md + SOTA elevation section."
    status: unverified
    closing_step: "Write elevation file + SOTA section."
  - truth: "A11y matrix Pending + no a11y-report bundle on HEAD (AG-77-13, OQ-483)."
    status: unverified
    closing_step: "Record bundle on HEAD; fill matrix; keep screen-reader human."
human_verification:
  - "Screen-reader (OQ-483); device a11y; keyring/pairing; Accounts:Enabled transition; owner runbook review."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - machine gates pass; security/runbook/a11y remain*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: Parity/changelog/openapi/sbom/CoworkAudit 16/16/Arch/build/format pass. PLAN/SUMMARY 11/11, no new SUMMARY.*

*Gaps:*
- Security review open SEC-77-01/02/04 (AG-77-10/11). -> Close with owners; never downgrade while High open.
- Missing runbooks cowork/computer-use/v0.16.0-owner-actions; stubs dispatch/browser-use. -> Author pages; complete stubs; run CoworkAuditTests.Security.
- Missing parity-audit-elevation.md + SOTA elevation section. -> Write elevation file + SOTA section.
- A11y matrix Pending + no a11y-report bundle on HEAD (AG-77-13, OQ-483). -> Record bundle on HEAD; fill matrix; keep screen-reader human.

*Human (never automated):*
- Screen-reader (OQ-483); device a11y; keyring/pairing; Accounts:Enabled transition; owner runbook review.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*
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
