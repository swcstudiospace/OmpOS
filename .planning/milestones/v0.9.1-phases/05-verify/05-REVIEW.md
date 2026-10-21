---
phase: 05-verify
reviewed: 2026-10-21T00:00:00Z
depth: standard
files_reviewed: 3
files_reviewed_list:
  - tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs
  - docs/handoff.md
  - docs/runbooks/local-dev.md
findings:
  blocker: 3
  major: 6
  minor: 6
  total: 15
status: findings_present
---

# Phase 05: Code Review Report

**Reviewed:** 2026-10-21
**Depth:** standard (full read of all three files + line-level verification of every
numeric / commit / line-reference claim against the repo; no build, no test run)
**Files Reviewed:** 3
**Status:** findings_present (3 blocker, 6 major, 6 minor)

## Summary

The hygiene gate has three gate-defeating false negatives (block-comment
continuation lines, boundary-less ID matching, scan-scope gaps). The handoff's
log line citations (98/99/103, 615/0) verify clean, but its AC_1 row repeats a
stale 614/0, and its "remote unconfigured" claim is factually false. The
runbook's clean-clone command does not match its own evidence log (SDK path
suffix `2`), and the "fresh clone" is a local-path clone. All solution-file,
vercel.json, Program.cs-block, open-question-ID, and SDK-version references
verified accurate except as noted below.

## Blockers

### B1: Bare block-comment continuation lines evade the marker scan (false negative)

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs` (`CommentStartIndex`, ~lines 185-220)
**Code:** a line such as the middle of
`/*` / `   TODO: fix this, no ID` / `*/` — trimmed form starts with neither
`*`, `//`, `<!--`, `@*`, and contains none of `/*`, `<!--`, `@*`, `//` on that
line, so `CommentStartIndex` returns -1 and `HasUnlinkedMarker` returns false.
**Wrong:** an unlinked `TODO` inside a multi-line block comment passes the gate
whenever the continuation line carries no leading `*`.
**Fix:** track block-comment state across lines in `TodoMarkersLinkToOpenQuestion`
(or require the `*` continuation style and flag bare-continuation markers).

### B2: An ID embedded in a longer token satisfies the linkage check (false negative)

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:116`
**Code:** `OpenQuestionIdPattern = new(@"(?:OQ-[0-9]+|Q-[A-Z][A-Z0-9-]*)", ...)` —
neither alternative has a leading or trailing boundary, and
`HasUnlinkedMarker` accepts any `Matches(line)` value present in `knownIds`.
**Wrong:** `// TODO refactor, see AQ-AC-LIST for details` matches substring
`Q-AC-LIST`, which is in `knownIds`, so the gate passes with no real link.
Same for `XOQ-01` matching `OQ-01`. No legitimate `Q-`/`OQ-` token in the tree
requires substring matching (verified: only `knownIds` in `open-questions.md`).
**Fix:** anchor the pattern, e.g.
`@"(?<![A-Z0-9-])(?:OQ-[0-9]+|Q-[A-Z][A-Z0-9-]*)(?![A-Z0-9-])"`.

### B3: Scan scope covers only 4 roots and 2 extensions, but the failure message claims "every marker"

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs` (`TodoMarkersLinkToOpenQuestion`, ~lines 121-152)
**Code:** roots `src/services/apps/tests`, extensions `*.cs`/`*.razor` only.
**Wrong:** a `TODO` in `deploy/compose.yaml`, `.github/workflows/ci.yml`,
`bootstrap.sh`, any `*.sh`/`*.py`/`*.yaml`/`*.md`/`*.ts`, or a future root is
never scanned, yet the assertion message says "every marker comment must link
its open-question ID". Scope gap = silent gate bypass.
**Fix:** either extend the scan (or make roots/extensions explicit policy in
CONTRIBUTING.md) and reword the assertion to state the exact covered scope.

## Majors

### M1: AC_1 row cites 614/0; the cited log says 615/0

**File:** `docs/handoff.md:38`
**Claim:** "`docs/runbooks/evidence/bootstrap-gate-2026-10-21.log` (exit path OK, 614/0)".
**Wrong:** log lines 98-99 read `total: 615` / `failed: 0` (line 81 `Passed!`,
line 103 `bootstrap: OK (SDK 10.0.402, ...)` — all verified). 614 is the stale
total from the earlier `8479c7c` clean-clone log
(`clean-clone-bootstrap-2026-10-21-8479c7c.log:139`), not the gate log.
**Fix:** change `614/0` to `615/0` in the AC_1 evidence cell.

### M2: "local `git remote -v` is unconfigured" is false

**File:** `docs/handoff.md:59` (Owner decisions, row 6)
**Claim:** "local `git remote -v` is unconfigured".
**Wrong:** `git remote -v` in `/root/src/repos/ompos` returns
`origin https://github.com/swcstudiospace/OmpOS.git` for fetch and push
(verified). The follow-up ("First push + remote CI run are human steps") is
still true, but the parenthetical is factually wrong.
**Fix:** replace with "local remote `origin` points at
`https://github.com/swcstudiospace/OmpOS`; first push + remote CI run are human
steps (remote repo currently empty)".

### M3: Test enforces less than CONTRIBUTING.md requires, and more than it states

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs` (`HasUnlinkedMarker`, ~lines 164-180) vs `CONTRIBUTING.md:39-43`
**Policy:** form ``// TODO(owner): description — see docs/open-questions.md#OQ-NN``.
**Wrong:** (a) the test never checks for `(owner)` — `// TODO blah — see OQ-01`
passes the gate but violates policy; (b) any ID anywhere on the line satisfies
any marker, so a spurious/unrelated ID silences the gate; (c) an ID on the
following line (multi-line link) is flagged, forcing same-line style the policy
example happens to use but never mandates.
**Fix:** decide: either enforce `(owner)` + same-line-ID in the test and document
both, or relax CONTRIBUTING to "same line must contain a known ID". Minimum:
document that any-ID-on-line counts as linked.

### M4: Exclusion list can hide first-party code; matching is case-sensitive

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:118`
**Code:** `ExcludedScanSegments = ["bin", "obj", "node_modules", "wwwroot", "lib", "TestData"]`,
matched with `StringComparer.Ordinal` against every path segment.
**Wrong:** (a) bare `"lib"` excludes any first-party directory named `lib`
(e.g. under `src/`); (b) `"TestData"` excludes the replay-fixture tree
(`TestData/frames`, load-bearing for Q-HARN-UPDATE) from the marker gate;
(c) `"wwwroot"` excludes the whole web-head static tree; (d) Ordinal matching
means `Lib`/`WWWROOT`/`testdata` variants are inconsistently handled. No current
offenders (only one tree-wide marker hit: the test's own regex-definition line),
so this is latent, not active hiding.
**Fix:** scope exclusions to generated/output roots (e.g. prefix-match on known
build dirs) or assert the excluded paths contain no `*.cs`/`*.razor` first-party
sources; document why `TestData`/`wwwroot`/`lib` are excluded.

### M5: `Program.cs:54-63` citation implies `finally` teardown lives at those lines

**File:** `docs/handoff.md:46` (AC_9 evidence)
**Claim:** "...with teardown in `finally` (`services/OmpOS.Api/Program.cs:54-63`, ...)".
**Wrong:** lines 54-63 are the composition-root comment + `if (!...Enabled)` DI
registration block (verified `sed -n '50,70p'`); there is no `finally` there.
The `finally` teardown lives in `src/OmpOS.Workflows/DirectSessionRunner.cs:65`
(and is referenced from `PromptRunner.cs:161`, `ApiSessionRunActivities.cs:26`).
**Fix:** cite `src/OmpOS.Workflows/DirectSessionRunner.cs:65` for the `finally`
teardown; keep `Program.cs:54-63` for the `FakeSandboxProvider` composition only.

### M6: Runbook clone command disagrees with its own evidence log

**File:** `docs/runbooks/local-dev.md:111-113`
**Claim:** `env -u DOTNET_ROOT DOTNET_ROOT=/tmp/ompos-clean-dotnet ./bootstrap.sh`.
**Wrong:** `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21.log:1` reads
"Installing .NET SDK 10.0.402 into /tmp/ompos-clean-dotnet2" (trailing `2`).
Additionally `git clone /root/src/repos/ompos` (line 111) is a local-path clone,
so "from a fresh clone into an empty temp directory" (and handoff item 5's
"Fresh `git clone`") does not exercise remote reachability; the runbook's own
Limits paragraph admits the local-source caveat honestly, but the command and
the log path still disagree.
**Fix:** make the command's `DOTNET_ROOT` match the log (`...-dotnet2`) or
re-run and update the log; qualify "fresh clone" as "local-path clone (remote
untested)" wherever VERIFY-01 is cited.

## Minors

### m1: Lowercase markers (`todo`, `hack`, `fixme`) are invisible to the gate

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:114`
**Code:** `MarkerPattern` has no `RegexOptions.IgnoreCase`.
**Wrong:** `// todo: link missing` never matches, so it is never flagged.
CONTRIBUTING shows uppercase form, so this may be intended — but then lowercase
markers are an undocumented bypass.
**Fix:** add `IgnoreCase` or document "only uppercase TODO/FIXME/HACK/XXX are markers".

### m2: `//` or `/*` inside a string literal is treated as a comment start (false positive)

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs` (`CommentStartIndex`)
**Code:** `IndexOf("//")` / `IndexOf("/*")` with no string-literal awareness
(the `:`-predecessor skip handles only `https://`-style schemes).
**Wrong:** `var s = "// TODO";` yields commentStart at the in-string `//`,
marker index equal (not less), ID absent → flagged, though no comment exists.
No such line exists in the tree today (verified: sole tree-wide marker hit is
the test's own line 114), so this is a latent FP.
**Fix:** strip string/char literals before scanning, or document the limitation.

### m3: Gate relies on accidental self-exclusion for its own definition line

**File:** `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:114`
**Code:** the `MarkerPattern` definition line itself contains `TODO|FIXME|HACK|XXX`
but no `//`/`/*`, so `CommentStartIndex` returns -1 and the line is skipped.
**Wrong:** nothing intentional excludes it — adding a `//` comment with an
example marker anywhere on that line would make the gate flag itself.
**Fix:** add an inline comment noting the self-exclusion, or anchor the scan to
skip the pattern-definition line deliberately.

### m4: AC_11 evidence path `05-01-SUMMARY.md` is not resolvable as written

**File:** `docs/handoff.md:48`
**Claim:** "VERIFY-02 sweep recorded in `05-01-SUMMARY.md`".
**Wrong:** no such file exists at repo root or under the (currently empty)
`.planning/phases/`; the actual file is
`.planning/milestones/v0.9.1-phases/05-verify/05-01-SUMMARY.md` (verified).
**Fix:** use the full archived path.

### m5: "Pre-registration total of 552" has no cited evidence in the repo

**File:** `docs/handoff.md:28`
**Claim:** "a pre-registration total of 552 excluded it [the API suite]".
**Wrong:** no log in `docs/runbooks/evidence/` contains `552` (verified by grep);
the arithmetic is plausible (615 − ~60 ≈ 552) but uncorroborated by any checked-in artifact.
**Fix:** cite the pre-registration log or soften to "approximately 552 (pre-registration run, log not retained)".

### m6: Browser-measured claims are recorded but not independently verifiable read-only

**File:** `docs/runbooks/local-dev.md` (Vertical slice section) and `docs/handoff.md` axe paragraphs
**Claims:** `strong 1, img 0, no script, no error bar`; contrast **18.26:1**;
"one unexplained screenshot" episode.
**Status:** no defect found — `vercel.json:31` CSP text, `appsettings.json`
`Api.BaseUrl`, and `rewrites`-without-`routes` (verified: `rewrites` at
`vercel.json:10`, no `routes` key) all corroborate the surrounding prose. The
live numbers themselves cannot be confirmed without re-running the browser
smoke; they are honestly hedged ("cause was not isolated", "not verified here:
MAUI/screen reader"). No fix; re-verify on the next live run.

## Verified accurate (no finding)

- Bootstrap gate log: line 81 `Test run summary: Passed!`, lines 98-99
  `total: 615` / `failed: 0`, line 103 `bootstrap: OK (SDK 10.0.402, locked
  restore, Release build, tests, format)` — all match handoff §"Bootstrap gate evidence".
- Clean-clone log: `total: 615`, `failed: 0` at lines 139-140; earlier `8479c7c`
  log `total: 614` at line 139 — matches local-dev's "614 total" for that run.
- `OmpOS.slnx:39` and `OmpOS.Linux.slnf:35` both register
  `tests/OmpOS.Api.IntegrationTests` — exact.
- `vercel.json:31` is the CSP line (`connect-src 'self' http://127.0.0.1:5187`) — exact.
- `services/OmpOS.Api/Program.cs:54-63` block content as described (minus the
  `finally` placement, see M5).
- All open-question IDs cited in handoff (`Q-HARN-WS`, `Q-HARN-PERMISSION`,
  `Q-HARN-UPDATE`, `Q-SB-AGENTOS`, `Q-SB-RAILWAY`, `Q-SB-ENCLAVE`,
  `Q-SB-CREDENTIALS`, `Q-SB-WORKER-COMPOSE`, `Q-WF-RETRY`, `Q-WEB-REFLECTION`,
  `Q-AC-LIST`) exist in `docs/open-questions.md` with matching semantics.
- AC evidence test-class references spot-checked (`AcpFrameReplayTests`,
  `WebSocketOmpTransportStubTests`, `RpcLiveSmokeTests`, `VercelConfigTests`,
  `PublishSmokeTests`, `EngineHostTests`) — all files exist.
- Commits `a6fcadd` and `8479c7c` exist; `a6fcadd` is on branch
  `feat/ompos-scaffold` as claimed. (HEAD has since moved to `213faa3`; the
  handoff describes the gate-time tree, which it states.)
- Date `2026-10-21`, SDK `10.0.402`, evidence filenames — consistent.

---

_Reviewed: 2026-10-21_
_Reviewer: Phase5Review (read-only review; no source files modified)_
_Depth: standard_
