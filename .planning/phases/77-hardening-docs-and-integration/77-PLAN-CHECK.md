# 77-PLAN-CHECK — Re-verification after planner Revision 1

**Phase:** 77 (hardening-docs-and-integration), milestone v0.16.0. **Mode:** standard. **Result: ISSUES FOUND.**
**Plans checked:** 8 (77-01 … 77-08). **Issues:** 0 blocker(s), 2 warning(s), 2 info.
**Prior report:** B1..B5, W1, W2, W3, W5, W6, W7, I1, I3 all re-checked below; W4 accepted as-is.

## Fix verification (prior findings)

| Prior | Status | Evidence |
|---|---|---|
| B1 77-01 T1 pipe masks exit | FIXED | `... dry-run --report-dir ... > /tmp/ag-recon/dry-run-77.log 2>&1 && tail -n 5 ...log`: redirect + `&&` propagates dry-run exit; tail is display-only |
| B2 `<dry-run-json>` placeholder | FIXED | Concrete path `/tmp/ag-recon/rebrand-report-77/rebrand-report.json` in both tasks; Task 1 `<files>` declares the report dir; action states the JSON landing path |
| B3 77-01 T2 pipe masks exit | FIXED | `... > /tmp/ag-recon/verify-baseline-77.log 2>&1 && tail -n 5 ...log`: same redirect+`&&` pattern |
| B4 77-07 T1 gate verdict | FIXED | `bash -c 'PATH=/tmp/ag-recon/dotnet-shim:$PATH ./bootstrap.sh > /tmp/ag-recon/gate-77.log 2>&1' && tail -n 5 ...log && git diff --exit-code -- bootstrap.sh`: gate exit propagates through the `&&` chain |
| B5 bare dotnet via bootstrap | FIXED | Action creates the shim file `/tmp/ag-recon/dotnet-shim/dotnet` (execs `/tmp/ag-bin/ag-dotnet`, `chmod +x`, outside the repo, never committed); verify uses `PATH=/tmp/ag-recon/dotnet-shim:$PATH` form. Quoting sound: single-quoted `bash -c` string, `$PATH` expands at runtime inside the subshell |
| W1 wave 2 with empty depends_on | FIXED | 77-02, 77-03, 77-04 now all `depends_on: [77-01]`, `wave: 2` (max-deps + 1 holds) |
| W2 77-04 vague file ids | FIXED | Concrete names in frontmatter and tasks: `tests/Autonogrammer.UI.Tests/CoworkSurfaceAccessibilityTests.cs`, `tests/Autonogrammer.Native.Tests/CoworkHeadlessAccessibilityTests.cs`, `ThemeContrastTests.cs`, `scripts/web-smoke/smoke.mjs` + `run-smoke.sh`. Residual "view-code presentation files only" is defect-dependent and acceptable |
| W3 77-06 lockfiles omitted | FIXED | Frontmatter now carries `'**/packages.lock.json'` (quoted glob) |
| W5 77-08 verify can't see PR/CI | FIXED (observation) | T1/T2 now query `gh pr view --json ... --jq ...` (state, body length, statusCheckRollup failures, unresolved-thread count, tag). jq single-quoted, no `$` interpolation hazards. Residual enforcement gap → N4 (info) |
| W6 matrix grep missing | FIXED | T2 automated appends `grep -c "\| *\| *\|" docs/quality/v0.16.0-accessibility-pass.md`; pattern double-quoted; separator rows (`\|---\|`) correctly do not match; zero-match `grep -c` exits 1 so "count is 0" propagates via `&&` |
| W7 bootstrap.sh editable | FIXED in effect | Action: "bootstrap.sh itself is NEVER edited by this plan ... not patched"; verify enforces `git diff --exit-code -- bootstrap.sh`. Residual stale frontmatter entry → N3 (info) |
| I1 unnamed other-line ref | FIXED | Action: "git fetch the ref named `final` (confirm with git rev-parse final ...)" |
| I3 manual lock-count eyeball | FIXED | T1 records `git ls-files '*packages.lock.json' \| wc -l > /tmp/ag-recon/lock-count-77.txt` (quoted glob); T2 `test "$POST_77_LOCKS" = "$(cat ...)"` encodes equality with exit status |
| W4 77-05 ~20-file task | ACCEPTED | Unchanged (now 20 files incl. guides); prose-only completion under one docs gate; no revision demanded |

Requirement coverage unchanged: all AG-77-1..17 claimed (05: 3,5,6,7,17; 06: 1,2,3,4; union complete). Dependency/wave chain consistent: 01/w1 → {02,03,04}/w2 on 77-01 → 05/w3 → 06/w4 → 07/w5 → 08/w6. No fenced code in actions. No `git add -A`, force-push, merge, or tag. No deferred/live content.

## New issues (introduced by Revision 1)

### N1 — 77-06 Task 2 lost its `<files>` element (regression)
- Plan: 77-06, Task 2. Dimension: task_completeness. Severity: warning.
- Required property: every `auto` task carries a `<files>` element (assert-only tasks name the asserted path).
- Evidence: round-0 Task 2 had `<files>Directory.Build.props (assert only, no edit)</files>`; the revision has no `<files>` element at all (`<files>` absent between `<read_first>` and `<action>`), while the task asserts on `ContractVersion.cs`, changelog/licence gates, and the lock inventory.

### N2 — 77-05 frontmatter dropped the 7 guides Task 2 still edits
- Plan: 77-05. Dimension: task_completeness. Severity: warning.
- Required property: `files_modified` covers every file the plan's tasks write.
- Evidence: frontmatter `files_modified` now has 14 items with zero guides, but Task 2 `<files>` still lists all seven (`docs/guides/status-and-roadmap.md`, `desktop-app-guide.md`, `web-app-guide.md`, `settings-guide.md`, `troubleshooting-and-faq.md`, `glossary.md`, `concepts.md`) and the action updates them. AG-77-7 coverage still holds via the task, but ownership/wave tooling consumes frontmatter.

### N3 — 77-07 frontmatter still lists bootstrap.sh (stale ownership entry)
- Plan: 77-07. Dimension: task_completeness. Severity: info.
- Required property: `files_modified` matches what the plan may write.
- Evidence: frontmatter ends `... | bootstrap.sh`, while the action forbids editing it and the verify fails on any diff. Harmless (verify enforces unmodified) but the entry now over-claims ownership.

### N4 — gh verdict signals are output-observable but not exit-encoded
- Plan: 77-08, Tasks 1–2. Dimension: verification_derivation. Severity: info.
- Required property: every `fails_when` signal drives a non-zero exit (mechanical verdict, not output parsing).
- Evidence: both automates print JSON (`{state, sections}`, `{state, decision, failed[]}`, thread count) and exit 0 whenever `gh` succeeds — `state != OPEN`, empty body, non-empty `failed[]`, non-zero thread count, or a present tag do not change the exit code. All signals are present in full in the output (nothing truncated), so an executor comparing output to `fails_when` can judge correctly; exit-encoding (e.g. `test "$(... --jq '.state')" = OPEN`) would make it mechanical. Robustness note: prefer `(.statusCheckRollup // [])` since `null[]` errors when no checks exist yet.

## Structured issues

```yaml
issues:
  - { plan: "77-06", task: 2, dimension: task_completeness, severity: warning,
      required_property: "Every auto task carries a files element, including assert-only tasks",
      description: "Revision dropped Task 2's files element (round-0 had 'Directory.Build.props (assert only, no edit)'); task asserts on ContractVersion.cs, gates, and lock inventory with no declared scope" }
  - { plan: "77-05", dimension: task_completeness, severity: warning,
      required_property: "files_modified covers every file the plan's tasks write",
      description: "Frontmatter has 14 items with zero guides while Task 2 files still lists all seven guides.md files and the action updates them" }
  - { plan: "77-07", dimension: task_completeness, severity: info,
      required_property: "files_modified matches what the plan may write",
      description: "Frontmatter still lists bootstrap.sh although action forbids editing it and verify enforces git diff --exit-code" }
  - { plan: "77-08", dimension: verification_derivation, severity: info,
      required_property: "Every fails_when signal drives a non-zero exit",
      description: "gh --jq verdicts print state/failed-list/thread-count but exit 0 whenever gh succeeds; signals fully present in output yet not exit-encoded; also suggest (.statusCheckRollup // []) for the no-checks null case" }
```

## Recommendation

0 blockers; 2 warnings (N1, N2) require a small revision — restore Task 2's assert-only `<files>` in 77-06 and re-add the seven guides to 77-05 frontmatter. N3/N4 are advisory. No re-verification of unrelated dimensions needed; a targeted re-check of the two warning fixes suffices before `/gsd:execute-phase 77`.
